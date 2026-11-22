namespace Cthangover.Mods.Items.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;

    /// <summary>Public inventory and normal save-owner checks complement the detached storage arithmetic contracts.</summary>
    public sealed class InventoryStorageBindingTests
    {
        /// <summary>The public bag and normal items slot preserve selected group IDs, origins and positions across JSON.</summary>
        [ModTest("inventory/storage_binding_groups_sum_and_selected_split_roundtrip")]
        public static void Save(ModTestContext context)
        {
            var id = FirstItem();
            var item = ItemFactory.Instance.Get(id);
            item.Properties.SetStr("Inventory", "{\"Width\":1,\"Height\":1,\"StackLimit\":3}");
            var inventory = new Inventory();
            ModAssert.True(inventory.TryConfigureGrid("grid", new InventoryGridData
            {
                Width = 4,
                Height = 2
            }, out _));
            ModAssert.True(inventory.TryAdd(item, 5, out _));
            ModAssert.Equal(2, inventory.Items.Count);
            ModAssert.Equal(5, inventory.CheckCount(id));
            var first = Inventory.StackId(inventory.Items[0]);
            ModAssert.True(inventory.TrySplitStack("split", first, 1, "player", -1, -1, false, out var child, out _));
            var runtime = GameData.Instance.Runtime;
            var before = runtime.Inventory;
            try
            {
                runtime.Inventory = inventory;
                var slot = new ModSlot();
                var saveable = new ItemsSaveable();
                saveable.OnSave(slot);
                var json = JsonSerializer.Serialize(slot);
                var restored = JsonSerializer.Deserialize<ModSlot>(json);
                runtime.Inventory = new Inventory();
                ModAssert.True(saveable.OnLoad(restored, out var error), error);
                var loaded = (Inventory)runtime.Inventory;
                ModAssert.Equal(5, loaded.CheckCount(id));
                ModAssert.Equal(first, loaded.Stack(child).Parent);
                ModAssert.Equal(JsonSerializer.Serialize(inventory.CaptureStorage()), JsonSerializer.Serialize(loaded.CaptureStorage()));
                ModAssert.True(loaded.Remove(id, 4));
                ModAssert.Equal(1, loaded.CheckCount(id));
                var unchanged = JsonSerializer.Serialize(loaded.CaptureStorage());
                var corrupt = restored.Data["Storage"].ReadJson<InventoryStorageState>();
                corrupt.Stacks[0].X = 100;
                restored.Data["Storage"] = new DataBlob(1, corrupt);
                ModAssert.False(saveable.OnLoad(restored, out _));
                ModAssert.Equal(unchanged, JsonSerializer.Serialize(loaded.CaptureStorage()));
            }
            finally
            {
                runtime.Inventory = before;
            }
        }
        /// <summary>Unified counts include occurrences, while an absent owner prevents their permanent consumption.</summary>

        [ModTest("inventory/storage_binding_unique_counts_and_missing_owner_atomic_refusal")]
        public static void Unique(ModTestContext context)
        {
            var id = FirstItem();
            var inventory = new Inventory();
            var descriptor = new InventoryInstanceData
            {
                Id = "test/unique",
                ItemId = id,
                Owner = "missing_test_owner",
                Revision = 1
            };
            ModAssert.True(inventory.TryPrepareInstanceInsert("pickup", new string ('a', 64), descriptor, inventory.Revision,
                out var change, out _));
            using (change)
            {
                change.ApplySilently();
                change.Publish();
            }

            inventory.Add(id, 2);
            ModAssert.Equal(3, inventory.CheckCount(id));
            ModAssert.True(inventory.HasItem(id));
            var before = JsonSerializer.Serialize(inventory.CaptureStorage());
            ModAssert.False(inventory.TryExchange(new Dictionary<string, int> { [id] = 3 }, new Dictionary<string, int>(), out _));
            ModAssert.Equal(before, JsonSerializer.Serialize(inventory.CaptureStorage()));
            ModAssert.NotNull(inventory.FindInstanceRow(descriptor.Id));
        }

        private static string FirstItem()
        {
            foreach (var id in Entities.Collect<ItemInfo>().Keys)
            {
                return id;
            }

            throw new System.InvalidOperationException("The fixture requires one loaded item definition");
        }
    }
}
