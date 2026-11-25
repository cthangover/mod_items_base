namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One items-slot storage journal for all compartments. Unknown item definitions keep both their footprint and provenance.
    /// </summary>
    public sealed class InventoryStorageState
    {
        /// <summary>Storage schema version checked before replacing live data.</summary>
        public int Version { get; set; } = 1;
        /// <summary>Compartment definitions addressed by every group placement.</summary>
        public Dictionary<string, InventoryGridData> Grids { get; set; } = new(StringComparer.Ordinal)
        {
            ["player"] = new InventoryGridData()
        };
        /// <summary>Ordered groups, including unresolved blueprints and equipment.</summary>
        public List<InventoryStackData> Stacks { get; set; } = new();
        /// <summary>Rewards waiting in their own locations until picked up by the player.</summary>
        public Dictionary<string, InventoryGroundReward> GroundRewards { get; set; } = new(StringComparer.Ordinal);
        /// <summary>Committed requests and consumed portions retained for retries and provenance.</summary>
        public Dictionary<string, InventoryStorageReceipt> Receipts { get; set; } = new(StringComparer.Ordinal);

        /// <summary>Unknown compatible state fields round-trip through the items save slot.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement> Extra { get; set; } = new();

        /// <summary>Rejects a corrupt save, overlap or off-grid placement before replacing any live owner data.</summary>
        public void Validate()
        {
            if (Version != 1 || Grids == null || !Grids.ContainsKey("player") || Grids.Count > 256 || Stacks == null
                || Stacks.Count > 16384 || Receipts == null || Receipts.Count > 16384 || Extra == null
                || GroundRewards == null || GroundRewards.Count > 16384)
            {
                throw new ArgumentException("Invalid inventory storage state");
            }

            foreach (var pair in Grids)
            {
                pair.Value?.Validate();
                if (pair.Value == null || pair.Key != pair.Value.Id)
                {
                    throw new ArgumentException("Invalid inventory compartment identity");
                }
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            var occupiedEquipment = new HashSet<string>(StringComparer.Ordinal);
            var externalReferences = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stack in Stacks)
            {
                stack.Validate();
                if (!ids.Add(stack.Id) || !Grids.TryGetValue(stack.Container, out var grid) || !InventoryStorage.Fits(this, stack, grid,
                    stack.Id))
                {
                    throw new ArgumentException("Inventory groups overlap or lie outside their container");
                }

                if (grid.EquipmentCharacter != null && (stack.Count != 1 || !occupiedEquipment.Add(grid.Id)))
                {
                    throw new ArgumentException("An equipment compartment holds exactly one unit");
                }

                if ((grid.Custodian == null) != (stack.CustodyReference == null)
                    || grid.Custodian != null && (stack.ExternalOwner != null
                        || !externalReferences.Add(grid.Custodian + "\n" + stack.CustodyReference)))
                {
                    throw new ArgumentException("Inventory custody reference does not match its reserved compartment");
                }
            }

            foreach (var pair in Receipts)
            {
                var value = pair.Value;
                InventoryInstanceData.CheckIdentity(pair.Key);
                if (value == null || value.Operation != pair.Key || value.Request?.Length != 64 || value.Removed == null
                    || value.CreatedOrigins == null)
                {
                    throw new ArgumentException("Invalid inventory storage receipt");
                }

                foreach (var removed in value.Removed)
                {
                    removed.Validate();
                }

                value.Custody?.Validate();
            }

            foreach (var pair in GroundRewards)
            {
                if (pair.Value == null || pair.Key != pair.Value.Id) throw new ArgumentException("Invalid ground reward identity");
                pair.Value.Validate();
            }
        }
    }
}
