namespace Cthangover.Core.Items
{
    using System.Collections.Generic;

    /// <summary>
    /// Optional identity owner prepares permanent consumption for a recipe, sale or use; unavailable owners refuse without erasing
    /// evidence.
    /// </summary>
    public interface IInventoryInstanceConsumptionOwner
    {
        /// <summary>Domain key stored by the occurrence descriptors this provider handles.</summary>
        string Owner { get; }

        /// <summary>Preallocates a consumption participant without changing either owner.</summary>
        bool TryPrepare(Inventory inventory, string operation, string request, List<InventoryInstanceData> instances,
            out IInventoryStorageParticipant participant, out string reason);
    }
}
