namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Optional identity data owned by the existing Inventory. Ordinary stacks keep their old ID/count blob;
    /// bag rows are derived views of these descriptors, so a missing provider cannot erase occurrence evidence.
    /// </summary>
    public sealed class InventoryInstanceState
    {
        /// <summary>Unsupported versions are retained by the normal mod-slot dormancy mechanism.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Monotonic identity-ledger revision, independent of native controls and ordinary stack counts.</summary>
        public long Revision { get; set; }

        /// <summary>Active unique occurrences, including unresolved descriptors without usable bag rows.</summary>
        public Dictionary<string, InventoryInstanceData> Records { get; set; } = new(StringComparer.Ordinal);

        /// <summary>Committed requests remain after a row leaves; exhausted budgets refuse new work.</summary>
        public Dictionary<string, InventoryInstanceReceipt> Receipts { get; set; } = new(StringComparer.Ordinal);

        /// <summary>Unrecognized compatible state fields round-trip through the existing inventory save owner.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement> Extra { get; set; } = new();

        /// <summary>Validates the complete detached candidate before an inventory generation publishes it.</summary>
        public void Validate()
        {
            if (Version != 1 || Revision < 0 || Records == null || Receipts == null || Extra == null
                || Records.Count > 4096 || Receipts.Count > 16384)
            {
                throw new ArgumentException("Invalid inventory occurrence state");
            }

            foreach (var pair in Records)
            {
                var value = pair.Value ?? throw new ArgumentException("Missing inventory occurrence");
                value.Validate();
                if (pair.Key != value.Id)
                {
                    throw new ArgumentException("Mismatched inventory occurrence identity");
                }
            }

            foreach (var pair in Receipts)
            {
                var value = pair.Value ?? throw new ArgumentException("Missing inventory occurrence receipt");
                value.Validate();
                if (pair.Key != value.Operation)
                {
                    throw new ArgumentException("Mismatched inventory operation identity");
                }

                if (Records.TryGetValue(value.Instance, out var current)
                    && (current.ItemId != value.ItemId || current.Owner != value.Owner || current.Revision < value.Revision))
                {
                    throw new ArgumentException("Conflicting inventory occurrence evidence");
                }
            }
        }
    }
}
