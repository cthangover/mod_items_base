namespace Cthangover.Mods.Items
{
    using System;
    using Cthangover.Core.Actions;
    using Cthangover.Core.Items;

    /// <summary>Scenario bridge for an explicit, capacity-checked bag policy change.</summary>
    public sealed class InventoryProfileAction : IScenarioAction
    {
        /// <summary>Uses an inventory_profile entity ID rather than a hardcoded game or mod name.</summary>
        public string Name
        {
            get
            {
                return "inventory.profile";
            }
        }

        /// <summary>A failed profile change leaves every item, placement and receipt untouched.</summary>
        public void Run(IActionContext context)
        {
            if (!InventoryProfiles.TryApply(context.Inventory as Inventory, "profile/" + Guid.NewGuid().ToString("N"),
                context.GetParam("id"), out var reason, context.GetParam("container") ?? "player"))
            {
                context.LogWarning("INVENTORY", "inventory.profile refused: " + reason);
            }
        }
    }
}
