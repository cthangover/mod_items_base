namespace Cthangover.Mods.Items.Tests
{
    using System.Text.Json;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;

    /// <summary>Legacy no-domain permission never consumes stock itself and becomes invalid when its selected data changes.</summary>
    public sealed class InventoryGroupDropTests
    {
        /// <summary>Legacy discard permission captures the exact group and current primary inventory.</summary>
        [ModTest("inventory/group_drop_legacy_and_stale_confirmation")]
        public static void Legacy(ModTestContext context)
        {
            var runtime = GameData.Instance.Runtime;
            var before = runtime.Inventory;
            try
            {
                var definitions = Entities.Collect<ItemInfo>(required: false);
                ItemInfo blueprint = null;
                foreach (var value in definitions.Values)
                {
                    if (!value.ItemType.HasFlag(ItemType.CantDrop))
                    {
                        blueprint = value;
                        break;
                    }
                }

                ModAssert.NotNull(blueprint);
                var inventory = new Inventory();
                runtime.Inventory = inventory;
                ModAssert.True(inventory.TryAdd(ItemFactory.Instance.Get(blueprint.ID), 4, out var reason), reason);
                var row = inventory.Items[0];
                var snapshot = JsonSerializer.Serialize(inventory.CaptureStorage());
                ModAssert.True(InventoryGroupDropActions.TryPrepare(inventory, row, out var token, out reason), reason);
                ModAssert.True(InventoryGroupDropActions.TryDrop(token, 1, out var handled, out reason), reason);
                ModAssert.False(handled);
                ModAssert.Equal(snapshot, JsonSerializer.Serialize(inventory.CaptureStorage()));
                ((ItemContainer)row).Storage.Properties["quality"] = 5;
                snapshot = JsonSerializer.Serialize(inventory.CaptureStorage());
                ModAssert.False(InventoryGroupDropActions.TryDrop(token, 1, out handled, out _));
                ModAssert.True(handled);
                ModAssert.Equal(snapshot, JsonSerializer.Serialize(inventory.CaptureStorage()));
                ModAssert.True(InventoryGroupDropActions.TryPrepare(inventory, row, out token, out reason), reason);
                runtime.Inventory = new Inventory();
                ModAssert.False(InventoryGroupDropActions.TryDrop(token, 1, out handled, out _));
                ModAssert.True(handled);
                ModAssert.Equal(snapshot, JsonSerializer.Serialize(inventory.CaptureStorage()));
            }
            finally
            {
                runtime.Inventory = before;
            }
        }
    }
}
