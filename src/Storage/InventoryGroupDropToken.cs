namespace Cthangover.Core.Items
{
    using System;

    /// <summary>Unsaved confirmation fence; weak owner/row references never pin an unloaded domain or native scene.</summary>
    public sealed class InventoryGroupDropToken
    {
        internal WeakReference<IInventory> Inventory { get; }
        internal WeakReference<IItemContainer> Row { get; }
        internal string Snapshot { get; }
        internal long Revision { get; }
        internal Guid Provider { get; }
        internal Guid Generation { get; }

        internal InventoryGroupDropToken(IInventory inventory, IItemContainer row, string snapshot, long revision,
            Guid provider, Guid generation)
        {
            Inventory = new WeakReference<IInventory>(inventory);
            Row = new WeakReference<IItemContainer>(row);
            Snapshot = snapshot;
            Revision = revision;
            Provider = provider;
            Generation = generation;
        }
    }
}
