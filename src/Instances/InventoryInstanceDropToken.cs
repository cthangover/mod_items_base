namespace Cthangover.Core.Items
{
    using System;

    /// <summary>
    /// Confirmation identity captured before the bag opens its drop prompt. Only weak references and values
    /// are retained: unloading a domain removes its registration without the UI pinning its assembly or native state.
    /// </summary>
    public sealed class InventoryInstanceDropToken
    {
        internal WeakReference<Inventory> Inventory { get; }
        internal WeakReference<InventoryInstanceContainer> Row { get; }
        internal string Instance { get; }
        internal long InventoryRevision { get; }
        internal long InstanceRevision { get; }
        internal Guid Provider { get; }
        internal Guid Generation { get; }

        internal InventoryInstanceDropToken(Inventory inventory, InventoryInstanceContainer row, long instanceRevision,
            Guid provider, Guid generation)
        {
            Inventory = new WeakReference<Inventory>(inventory);
            Row = new WeakReference<InventoryInstanceContainer>(row);
            Instance = row.InstanceId;
            InventoryRevision = inventory.Revision;
            InstanceRevision = instanceRevision;
            Provider = provider;
            Generation = generation;
        }
    }
}
