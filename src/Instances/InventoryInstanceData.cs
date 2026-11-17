namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Serialization;

    /// <summary>
    /// An occurrence in the ordinary bag, separate from fungible item-ID stock. Its domain owner retains the
    /// world recipe and payload; this descriptor stores only the stable identity and shared item reference.
    /// </summary>
    public sealed class InventoryInstanceData
    {
        /// <summary>Immutable occurrence identity supplied by its domain, never a generated stack index.</summary>
        public string Id { get; set; }

        /// <summary>Shared item blueprint used for presentation; missing blueprints leave the descriptor unresolved.</summary>
        public string ItemId { get; set; }

        /// <summary>Domain handler responsible for identity-aware actions and counterpart reconciliation.</summary>
        public string Owner { get; set; }

        /// <summary>Committed occurrence revision shared with its domain counterpart.</summary>
        public long Revision { get; set; }

        /// <summary>Compatible future metadata survives unresolved-provider save/load without becoming stock.</summary>
        [JsonExtensionData]
        public Dictionary<string, JsonElement> Extra { get; set; } = new();

        /// <summary>Checks portable data only; validation never resolves a provider or constructs a native view.</summary>
        public void Validate()
        {
            CheckIdentity(Id);
            CheckIdentity(ItemId);
            CheckIdentity(Owner);
            if (Revision <= 0 || Extra == null || JsonSerializer.Serialize(this).Length > 16384)
            {
                throw new ArgumentException("Invalid inventory occurrence descriptor");
            }
        }

        internal static void CheckIdentity(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 1024)
            {
                throw new ArgumentException("Invalid inventory occurrence identity");
            }

            foreach (var character in value)
            {
                if (char.IsControl(character))
                {
                    throw new ArgumentException("Invalid inventory occurrence identity");
                }
            }
        }
    }
}
