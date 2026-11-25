namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Mods.Resolvers;

    public partial class Inventory
    {
        internal bool HasBoundedRewardStorage => storage.Grids["player"].Width > 0;

        internal bool TryGrantReward(IItem item, int count, InventoryGroundPosition position,
            out int dropped, out string reason)
        {
            dropped = 0;
            if (!TryRewardItemSize(item, out var size, out reason)) return false;
            var store = new InventoryStorage(CaptureStorage());
            return store.TryGrantReward("reward/" + Guid.NewGuid().ToString("N"), item.ID, count,
                size, position, out dropped, out reason) && ApplyStorage(store.Capture(), item.ID, out reason);
        }

        internal IEnumerable<InventoryGroundReward> GroundRewards()
        {
            foreach (var pile in storage.GroundRewards.Values)
                yield return InventoryStorage.Copy(pile);
        }

        internal bool TryPickUpGroundReward(InventoryGroundPosition position, string id, out string reason)
        {
            if (!storage.GroundRewards.TryGetValue(id, out var pile)
                || !Entities.Collect<ItemInfo>(required: false).ContainsKey(pile.ItemId))
            {
                reason = "inventory/instances/missing_provider";
                return false;
            }

            var item = ItemFactory.Instance.Get(pile.ItemId);
            if (!TryRewardItemSize(item, out var size, out reason)) return false;
            var store = new InventoryStorage(CaptureStorage());
            return store.TryTakeGroundReward("take/reward/" + Guid.NewGuid().ToString("N"), id,
                size, position.Domain, position.Location, out reason)
                && ApplyStorage(store.Capture(), pile.ItemId, out reason);
        }

        private static bool TryRewardItemSize(IItem item, out InventoryItemSize size, out string reason)
        {
            try
            {
                size = InventoryItemSize.From(item);
                reason = null;
                return true;
            }
            catch (Exception ex) when (ex is ArgumentException or JsonException)
            {
                size = null;
                reason = "inventory/storage/invalid";
                return false;
            }
        }
    }
}
