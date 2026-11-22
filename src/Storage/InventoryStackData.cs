namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Nodes;

    /// <summary>
    /// A persistent group, its placement and conserved origin quantities. Split children get new IDs; merges retain every contributing
    /// origin.
    /// </summary>
    public sealed class InventoryStackData
    {
        /// <summary>Stable group or world-occurrence identity, retained when moved.</summary>
        public string Id { get; set; }
        /// <summary>Shared blueprint reference; absence of its provider never erases this record.</summary>
        public string ItemId { get; set; }
        /// <summary>Positive quantity exactly equal to the sum of Origins.</summary>
        public int Count { get; set; }
        /// <summary>Original group identity for a split child, without renaming the retained source.</summary>
        public string Parent { get; set; }
        /// <summary>Conserved quantity contributed by each genesis, retained through split and merge.</summary>
        public Dictionary<string, int> Origins { get; set; } = new(StringComparer.Ordinal);
        /// <summary>Current compartment address, independent of the displayed row order.</summary>
        public string Container { get; set; } = "player";
        /// <summary>Leftmost occupied cell in the current compartment.</summary>
        public int X { get; set; }
        /// <summary>Topmost occupied cell in the current compartment.</summary>
        public int Y { get; set; }
        /// <summary>Saved quarter-turn orientation; only rotatable items may set it.</summary>
        public bool Rotated { get; set; }
        /// <summary>Saved footprint and grouping rules, retained even without the blueprint provider.</summary>
        public InventoryItemSize Size { get; set; } = new();
        /// <summary>Individual properties used to prevent incompatible groups from merging.</summary>
        public JsonObject Properties { get; set; } = new();
        /// <summary>Optional domain whose prepared participant must admit permanent consumption.</summary>
        public string ExternalOwner { get; set; }
        /// <summary>External realization identity while a quantity group is held in a custodian compartment.</summary>
        public string CustodyReference { get; set; }

        /// <summary>Actual occupied width, including group-specific dimensions and the saved rotation.</summary>
        public int OccupiedWidth
        {
            get
            {
                return Rotated ? BaseHeight : BaseWidth;
            }
        }

        /// <summary>Actual occupied height, including group-specific dimensions and the saved rotation.</summary>
        public int OccupiedHeight
        {
            get
            {
                return Rotated ? BaseWidth : BaseHeight;
            }
        }

        private int BaseWidth
        {
            get
            {
                return Count > 1 && Size.GroupWidth > 0 ? Size.GroupWidth : Size.Width;
            }
        }

        private int BaseHeight
        {
            get
            {
                return Count > 1 && Size.GroupHeight > 0 ? Size.GroupHeight : Size.Height;
            }
        }

        /// <summary>Validates portable data and exact quantity conservation without looking up definitions.</summary>
        public void Validate()
        {
            InventoryInstanceData.CheckIdentity(Id);
            InventoryInstanceData.CheckIdentity(ItemId);
            InventoryInstanceData.CheckIdentity(Container);
            if (CustodyReference != null)
            {
                InventoryInstanceData.CheckIdentity(CustodyReference);
            }

            Size?.Validate();
            if (Size == null || Count < 1 || Count > Size.StackLimit || X < 0 || Y < 0 || Rotated && !Size.Rotatable
                || Properties == null || Origins == null || Origins.Count == 0)
            {
                throw new ArgumentException("Invalid inventory group");
            }

            long total = 0;
            foreach (var pair in Origins)
            {
                InventoryInstanceData.CheckIdentity(pair.Key);
                if (pair.Value < 1)
                {
                    throw new ArgumentException("Invalid inventory origin quantity");
                }

                total += pair.Value;
            }

            if (total != Count || ExternalOwner != null && (Count != 1 || Origins.Count != 1 || !Origins.ContainsKey(Id)))
            {
                throw new ArgumentException("Inventory origin quantities do not match the group");
            }
        }
    }
}
