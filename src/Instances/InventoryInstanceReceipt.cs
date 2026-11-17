namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Durable evidence for one identity-aware inventory change. Receipts survive removal of the bag row,
    /// preventing operation replay or rebinding an existing occurrence to another blueprint.
    /// </summary>
    public sealed class InventoryInstanceReceipt
    {
        /// <summary>Caller-owned operation key retained without time-based eviction.</summary>
        public string Operation { get; set; }

        /// <summary>SHA256 of the complete cross-owner request, including expected revisions and destination.</summary>
        public string Request { get; set; }

        /// <summary>Stable occurrence affected by this operation.</summary>
        public string Instance { get; set; }

        /// <summary>Immutable shared blueprint associated with this occurrence.</summary>
        public string ItemId { get; set; }

        /// <summary>Domain owner that admitted the operation.</summary>
        public string Owner { get; set; }

        /// <summary>Resulting occurrence revision, including a removal from this bag.</summary>
        public long Revision { get; set; }

        /// <summary>True when the operation inserted the occurrence; false when it transferred it out.</summary>
        public bool Inserted { get; set; }

        /// <summary>Compatible unknown receipt fields remain part of retained transfer evidence.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement> Extra { get; set; } = new();

        /// <summary>Rejects malformed receipts without resolving current inventory or domain content.</summary>
        public void Validate()
        {
            InventoryInstanceData.CheckIdentity(Operation);
            InventoryInstanceData.CheckIdentity(Instance);
            InventoryInstanceData.CheckIdentity(ItemId);
            InventoryInstanceData.CheckIdentity(Owner);
            if (Revision <= 0 || Request == null || Request.Length != 64 || Extra == null)
            {
                throw new ArgumentException("Invalid inventory occurrence receipt");
            }

            foreach (var character in Request)
            {
                if (!Uri.IsHexDigit(character))
                {
                    throw new ArgumentException("Invalid inventory occurrence receipt digest");
                }
            }
        }
    }
}
