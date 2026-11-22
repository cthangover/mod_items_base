namespace Cthangover.Mods.Items
{
    using Cthangover.Core.Actions;
    using Cthangover.Core.Items;

    /// <summary>
    /// Author-defined bag capacity changes use the same atomic repack as the authoring form; overflow never discards stock.
    /// </summary>
    public sealed class InventoryGridAction : IScenarioAction
    {
        /// <summary>Scenario command for an explicit author-defined capacity change.</summary>
        public string Name
        {
            get
            {
                return "inventory.grid";
            }
        }

        /// <summary>Validates dimensions and atomically repacks without discarding overflow.</summary>
        public void Run(IActionContext context)
        {
            var container = context.GetParam("container") ?? "player";
            if (context.Inventory is not Inventory inventory || !int.TryParse(context.GetParam("width"), out var width)
                || !int.TryParse(context.GetParam("height"), out var height))
            {
                context.LogWarning("INVENTORY", "inventory.grid: expected an inventory and integer width/height");
                return;
            }

            if (!inventory.TryConfigureGrid("grid/" + System.Guid.NewGuid().ToString("N"), new InventoryGridData
            {
                Id = container,
                Width = width,
                Height = height
            }, out var reason, true))
            {
                context.LogWarning("INVENTORY", "inventory.grid refused: " + reason);
            }
        }
    }
}
