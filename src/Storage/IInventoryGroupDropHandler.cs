namespace Cthangover.Core.Items
{
    using System;

    /// <summary>
    /// Optional drop domain for ordinary groups. An active domain owns the entire operation, including refusal;
    /// the bag never falls back to destroying stock when that domain cannot place it.
    /// </summary>
    public interface IInventoryGroupDropHandler
    {
        /// <summary>True while this domain governs ordinary drops in the current scene.</summary>
        bool Active { get; }
        /// <summary>Changes after load or domain-provider replacement, invalidating open confirmations.</summary>
        Guid Generation { get; }
        /// <summary>Checks the current bag and exact selected row without changing quantities or action points.</summary>
        bool CanDrop(IInventory inventory, IItemContainer row, out string reason);
        /// <summary>Commits the requested positive portion through the domain's coordinated owner transfer.</summary>
        bool TryDrop(IInventory inventory, IItemContainer row, int count, out string reason);
    }
}
