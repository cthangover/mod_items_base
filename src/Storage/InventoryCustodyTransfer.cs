namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Durable proof of an explicit external ownership transfer. Quantity/origin evidence survives later bag merges
    /// and consumption, so a retired world realization never depends on a still-live row with its former ID.
    /// </summary>
    public sealed class InventoryCustodyTransfer
    {
        /// <summary>Domain whose realization is bound to this operation.</summary>
        public string Owner { get; set; }
        /// <summary>External realization identity shared with its owner's receipt.</summary>
        public string Reference { get; set; }
        /// <summary>Group selected before this transfer; a partial drop creates a distinct result group.</summary>
        public string SourceGroup { get; set; }
        /// <summary>Source compartment address.</summary>
        public string From { get; set; }
        /// <summary>Destination compartment address.</summary>
        public string To { get; set; }
        /// <summary>Exactly conserved positive transferred quantity.</summary>
        public int Count { get; set; }
        /// <summary>Digest of the coordinator's complete request, independent of generated result identities.</summary>
        public string Contract { get; set; }
        /// <summary>Exact origin contributions carried across the ownership boundary.</summary>
        public Dictionary<string, int> Origins { get; set; } = new(StringComparer.Ordinal);
        /// <summary>Destination groups and their received portions, including compatible bag merges.</summary>
        public Dictionary<string, int> Destinations { get; set; } = new(StringComparer.Ordinal);

        /// <summary>Rejects malformed or nonconserving history before restoring either participant.</summary>
        public void Validate()
        {
            InventoryInstanceData.CheckIdentity(Owner);
            InventoryInstanceData.CheckIdentity(Reference);
            InventoryInstanceData.CheckIdentity(SourceGroup);
            InventoryInstanceData.CheckIdentity(From);
            InventoryInstanceData.CheckIdentity(To);
            var compartment = "custody/" + Uri.EscapeDataString(Owner);
            if (Count < 1 || Contract?.Length != 64 || Origins == null || Destinations == null
                || !(From == "player" && To == compartment || From == compartment && To == "player")
                || Total(Origins) != Count || Total(Destinations) != Count)
            {
                throw new ArgumentException("Invalid inventory custody transfer");
            }
        }

        private static long Total(Dictionary<string, int> quantities)
        {
            long total = 0;
            foreach (var pair in quantities)
            {
                InventoryInstanceData.CheckIdentity(pair.Key);
                if (pair.Value < 1)
                {
                    throw new ArgumentException("Invalid inventory transfer quantity");
                }

                total += pair.Value;
            }

            return total;
        }
    }
}
