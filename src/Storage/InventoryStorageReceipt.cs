namespace Cthangover.Core.Items
{
    using System.Collections.Generic;

    /// <summary>
    /// Durable retry evidence and the exact removed quantities. Removal history never turns a spent identity into newly generated
    /// stock.
    /// </summary>
    public sealed class InventoryStorageReceipt
    {
        /// <summary>Stable retry key supplied by the caller.</summary>
        public string Operation { get; set; }
        /// <summary>Digest of the complete request; conflicting retries refuse.</summary>
        public string Request { get; set; }
        /// <summary>Created or selected group identity returned by this operation.</summary>
        public string Result { get; set; }
        /// <summary>Exact consumed portions and their provenance, retained after the live group disappears.</summary>
        public List<InventoryStackData> Removed { get; set; } = new();
        /// <summary>Genesis keys created by this operation; product origins link back to its consumed inputs.</summary>
        public List<string> CreatedOrigins { get; set; } = new();
        /// <summary>Optional paired-domain movement proof; these quantities were transferred rather than consumed.</summary>
        public InventoryCustodyTransfer Custody { get; set; }
    }
}
