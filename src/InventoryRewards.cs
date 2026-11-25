namespace Cthangover.Core.Items
{
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Utils;
    using Cthangover.Mods.Interface.Messages;
    using Godot;

    /// <summary>Grants scripted and battle rewards, placing overflow at the player's location.</summary>
    public static class InventoryRewards
    {
        /// <summary>Resolves the item and grants a scenario reward at the current location.</summary>
        public static void Grant(IInventory inventory, string itemId, int count)
        {
            if (inventory is not Inventory)
            {
                inventory?.Add(itemId, count);
                return;
            }

            Grant(inventory, ItemFactory.Instance.Get(itemId), count);
        }

        /// <summary>Grants the item and reports the quantity that stayed on the ground.</summary>
        public static void Grant(IInventory inventory, IItem item, int count, string scene = null)
        {
            if (item == null || count <= 0) return;
            if (inventory is not Inventory bag)
            {
                inventory?.Add(item, count);
                return;
            }

            var position = bag.HasBoundedRewardStorage
                ? InventoryGroundWorlds.Capture(scene ?? InventoryGroundRewardsView.CurrentLocation)
                : null;
            if (!bag.TryGrantReward(item, count, position, out var dropped, out var reason))
            {
                GameLogger.Log("INVENTORY", $"Reward '{item.ID}' x{count} rejected: {reason}", LogLevel.Warning);
                return;
            }

            if (dropped > 0 && GameEnvironment.IsRuntime)
                MessagesHelper.AddMessage(string.Format(TranslationServer.Translate("inventory/reward/ground").ToString(),
                    DisplayName(item), dropped));
        }

        internal static string DisplayName(IItem item) =>
            TranslationServer.Translate(string.IsNullOrEmpty(item.Name) ? item.ID : item.Name).ToString();
    }
}
