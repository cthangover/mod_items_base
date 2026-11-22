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

        /// <summary>Legacy quantities are split to current group limits; the full old quantity survives its first modern save.</summary>
        [ModTest("inventory/storage_binding_legacy_count_migrates_to_group_limits")]
        public static void LegacyGroups(ModTestContext context)
        {
            var id = FirstItem();
            var definition = Entities.Collect<ItemInfo>()[id];
            var properties = definition.ExtraProperties;
            var runtime = GameData.Instance.Runtime;
            var previous = runtime.Inventory;
            try
            {
                definition.ExtraProperties = properties == null ? new Dictionary<string, object>()
                    : new Dictionary<string, object>(properties);
                definition.ExtraProperties["Inventory"] = JsonSerializer.Deserialize<JsonElement>("{\"StackLimit\":2}");
                runtime.Inventory = new Inventory();
                var slot = new ModSlot();
                slot.Data["Inventory"] = new DataBlob(1, new List<CItem>
                {
                    new()
                    {
                        ID = id,
                        Count = 5
                    }
                });
                var owner = new ItemsSaveable();
                ModAssert.True(owner.OnLoad(slot, out var error), error);
                ModAssert.Equal(5, runtime.Inventory.CheckCount(id));
                ModAssert.Equal(3, runtime.Inventory.Items.Count);
                var modern = new ModSlot();
                owner.OnSave(modern);
                var saved = modern.Data["Storage"].ReadJson<InventoryStorageState>();
                saved.Validate();
                ModAssert.Equal(2, saved.Stacks[0].Count);
                ModAssert.Equal(2, saved.Stacks[1].Count);
                ModAssert.Equal(1, saved.Stacks[2].Count);
            }
            finally
            {
                definition.ExtraProperties = properties;
                runtime.Inventory = previous;
            }
        }

        /// <summary>A legacy unresolved item receives stable storage metadata and survives a second, modern JSON load.</summary>
        [ModTest("inventory/storage_binding_unresolved_legacy_stock_survives_modern_reload")]
        public static void Unresolved(ModTestContext context)
        {
            var runtime = GameData.Instance.Runtime;
            var previous = runtime.Inventory;
            try
            {
                runtime.Inventory = new Inventory();
                var slot = new ModSlot();
                slot.Data["Inventory"] = new DataBlob(1, new List<CItem>
                {
                    new()
                    {
                        ID = "unavailable_storage_test/item",
                        Count = 3
                    }
                });
                var owner = new ItemsSaveable();
                ModAssert.True(owner.OnLoad(slot, out var error), error);
                var modern = new ModSlot();
                owner.OnSave(modern);
                var json = JsonSerializer.Serialize(modern);
                modern = JsonSerializer.Deserialize<ModSlot>(json);
                runtime.Inventory = new Inventory();
                ModAssert.True(owner.OnLoad(modern, out error), error);
                var state = ((Inventory)runtime.Inventory).CaptureStorage();
                ModAssert.Equal(1, state.Stacks.Count);
                ModAssert.Equal(3, state.Stacks[0].Count);
                ModAssert.Equal(0, runtime.Inventory.Items.Count);
            }
            finally
            {
                runtime.Inventory = previous;
            }
        }
    }
}
