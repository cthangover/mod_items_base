namespace Cthangover.Mods.Items.Tests
{
    using System.Collections.Generic;
    using Cthangover.Core.Items;

    /// <summary>A fixture consumption domain whose validation replaces the active bag without editing the previous owner.</summary>
    public sealed class InventoryReentryConsumptionOwner : IInventoryInstanceConsumptionOwner
    {
        /// <summary>Dedicated fixture domain, separate from actual scene or gameplay owners.</summary>
        public string Owner
        {
            get
            {
                return "storage_test/reentry";
            }
        }

        /// <summary>Preparation itself is read-only; the participant models a later reentrant validation callback.</summary>
        public bool TryPrepare(Inventory inventory, string operation, string request, List<InventoryInstanceData> instances,
            out IInventoryStorageParticipant participant, out string reason)
        {
            participant = new InventoryReentryConsumptionChange();
            reason = null;
            return true;
        }
    }
}
