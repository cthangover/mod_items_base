namespace Cthangover.Core.Items
{
    using System;

    /// <summary>
    /// Optional occurrence owner supplies world placement, counterpart checks and atomic transfer. The bag
    /// never guesses a drop destination or falls back to ordinary count-only discard when its owner is absent.
    /// </summary>
    public interface IInventoryInstanceDropHandler
    {
        /// <summary>Exact domain ID stored in InventoryInstanceData.Owner.</summary>
        string Owner { get; }

        /// <summary>Unsaved domain generation nonce; loads/reset invalidate an already open confirmation prompt.</summary>
        Guid Generation { get; }

        /// <summary>Read-only admission check; unavailable or mismatched counterparts remain saved and inert.</summary>
        bool CanDrop(Inventory inventory, InventoryInstanceData value, out string reason);

        /// <summary>Transfers the selected occurrence; stale inventory objects or revisions refuse before effects.</summary>
        bool TryDrop(Inventory inventory, InventoryInstanceData value, out string reason);
    }
}
