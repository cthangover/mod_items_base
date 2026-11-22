namespace Cthangover.Mods.Items.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text.Json;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Games;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;

    /// <summary>Checks capacity semantics, safe policy transitions and per-game factory selection through public inventory data.</summary>
    public sealed class InventoryProfileTests
    {
        /// <summary>Two large unstackable objects occupy two slots; their physical dimensions and grouping rules survive.</summary>
        [ModTest("inventory/profiles_slots_capacity_ignores_physical_footprints")]
        public static void Slots(ModTestContext context)
        {
            var store = new InventoryStorage(new InventoryStorageState());
            ModAssert.True(store.TryConfigure("slots", new InventoryGridData
            {
                Layout = "slots",
                Width = 2,
                Height = 1
            }, out _, out _));
            var size = new InventoryItemSize
            {
                Width = 9,
                Height = 7,
                GroupWidth = 12,
                StackLimit = 1
            };
            ModAssert.True(store.TryAdd("large", "test/large", 2, size, null, out _, out _));
            var saved = store.Capture();
            ModAssert.Equal(2, saved.Stacks.Count);
            ModAssert.Equal(9, saved.Stacks[0].OccupiedWidth);
            ModAssert.Equal(1, saved.Grids["player"].ItemWidth(saved.Stacks[0]));
            ModAssert.Equal(0, saved.Stacks[0].X);
            ModAssert.Equal(1, saved.Stacks[1].X);
            var before = JsonSerializer.Serialize(saved);
            ModAssert.False(store.TryAdd("full", "test/large", 1, size, null, out _, out _));
            ModAssert.False(store.TryMove("overlap", saved.Stacks[0].Id, "player", 1, 0, false, out _, out _));
            ModAssert.Equal(before, JsonSerializer.Serialize(store.Capture()));
            var restored = new InventoryStorage(JsonSerializer.Deserialize<InventoryStorageState>(before));
            ModAssert.Equal(before, JsonSerializer.Serialize(restored.Capture()));
        }

        /// <summary>Changing presentation/capacity retains exact identities and origins; a too-small grid cannot destroy stock.</summary>
        [ModTest("inventory/profiles_transition_roundtrip_and_atomic_refusal")]
        public static void Transition(ModTestContext context)
        {
            var store = new InventoryStorage(new InventoryStorageState());
            var size = new InventoryItemSize
            {
                Width = 3,
                Height = 2,
                StackLimit = 2,
                Rotatable = true
            };
            ModAssert.True(store.TryAdd("items", "test/large", 4, size, null, out _, out _));
            var original = store.Capture().Stacks;
            ModAssert.True(store.TryConfigure("slots", new InventoryGridData
            {
                Layout = "slots",
                Width = 2,
                Height = 1
            }, out _, out _, true));
            ModAssert.True(store.TryConfigure("list", new InventoryGridData { Layout = "list" }, out _, out _, true));
            var list = JsonSerializer.Serialize(store.Capture());
            store = new InventoryStorage(JsonSerializer.Deserialize<InventoryStorageState>(list));
            ModAssert.Equal("list", store.Capture().Grids["player"].Layout);
            ModAssert.False(store.TryConfigure("tiny", new InventoryGridData
            {
                Width = 2,
                Height = 2
            }, out _, out _, true));
            ModAssert.Equal(list, JsonSerializer.Serialize(store.Capture()));
            ModAssert.True(store.TryConfigure("grid", new InventoryGridData
            {
                Width = 6,
                Height = 2
            }, out _, out _, true));
            var grid = store.Capture();
            for (var index = 0; index < original.Count; index++)
            {
                ModAssert.Equal(original[index].Id, grid.Stacks[index].Id);
                ModAssert.Equal(original[index].Count, grid.Stacks[index].Count);
                ModAssert.Equal(JsonSerializer.Serialize(original[index].Origins), JsonSerializer.Serialize(grid.Stacks[index].Origins));
                ModAssert.Equal(JsonSerializer.Serialize(original[index].Size), JsonSerializer.Serialize(grid.Stacks[index].Size));
            }

            var json = JsonSerializer.Serialize(grid);
            ModAssert.Equal(json, JsonSerializer.Serialize(new InventoryStorage(JsonSerializer.Deserialize<InventoryStorageState>(json))
                .Capture()));
        }

        /// <summary>Fresh inventories follow each active game's selection; a saved bag retains its own policy when loaded.</summary>
        [ModTest("inventory/profiles_game_factory_and_save_authority")]
        public static void GameSelection(ModTestContext context)
        {
            // Static singleton setup only; assertions below observe the public factory and normal save-owner API.
            var field = typeof(ActiveGame).GetField("_set", BindingFlags.Static | BindingFlags.NonPublic);
            var previousSet = (GameModSet)field.GetValue(null);
            var wasFrontend = ActiveGame.IsFrontendOnly;
            var runtime = GameData.Instance.Runtime;
            var previousInventory = runtime.Inventory;
            try
            {
                SelectGame("inventory/grid");
                var grid = (Inventory)new RuntimeData().Inventory;
                ModAssert.Equal("grid", grid.CaptureStorage().Grids["player"].Layout);
                ModAssert.Equal(10, grid.CaptureStorage().Grids["player"].Width);
                ModAssert.True(grid.TryAdd(FirstItem(), 1, out _));
                runtime.Inventory = grid;
                var saveable = new ItemsSaveable();
                var slot = new ModSlot();
                saveable.OnSave(slot);
                var saved = JsonSerializer.Deserialize<ModSlot>(JsonSerializer.Serialize(slot));
                SelectGame("inventory/list");
                var list = (Inventory)new RuntimeData().Inventory;
                ModAssert.Equal("list", list.CaptureStorage().Grids["player"].Layout);
                ModAssert.Equal(0, list.Items.Count);
                runtime.Inventory = list;
                ModAssert.True(saveable.OnLoad(saved, out var error), error);
                ModAssert.Equal("grid", list.CaptureStorage().Grids["player"].Layout);
                ModAssert.Equal(1, list.CheckCount(grid.Items[0].Item.ID));
                ModAssert.Equal("list", InventoryProfiles.Create().CaptureStorage().Grids["player"].Layout);
                SelectGame("inventory/slots");
                ModAssert.Equal("slots", InventoryProfiles.Create().CaptureStorage().Grids["player"].Layout);
                SelectGame(null);
                ModAssert.Equal(0, InventoryProfiles.Create().CaptureStorage().Grids["player"].Width);
                var unchanged = JsonSerializer.Serialize(list.CaptureStorage());
                SelectGame("test/missing-profile-provider");
                var refused = false;
                try
                {
                    InventoryProfiles.Create();
                }
                catch (InvalidOperationException)
                {
                    refused = true;
                }

                ModAssert.True(refused, "A broken game profile must not silently grant an unlimited bag");
                ModAssert.Equal(unchanged, JsonSerializer.Serialize(list.CaptureStorage()));
            }
            finally
            {
                runtime.Inventory = previousInventory;
                if (wasFrontend)
                {
                    ActiveGame.ActivateFrontend(previousSet);
                }
                else if (previousSet != null)
                {
                    ActiveGame.Activate(previousSet);
                }
                else
                {
                    ActiveGame.Clear();
                }
            }
        }

        /// <summary>Scripted switches use entity references; missing profiles and malformed modes cannot bypass capacity.</summary>
        [ModTest("inventory/profiles_actions_missing_and_invalid_are_atomic")]
        public static void Actions(ModTestContext context)
        {
            var inventory = new Inventory();
            ModAssert.True(inventory.TryAdd(FirstItem(), 1, out _));
            var action = new InventoryProfileAction();
            var actionContext = new FakeActionContext { Inventory = inventory };
            actionContext.Params["id"] = "inventory/list";
            action.Run(actionContext);
            ModAssert.Equal(0, actionContext.Warnings.Count);
            ModAssert.Equal("list", inventory.CaptureStorage().Grids["player"].Layout);
            var before = JsonSerializer.Serialize(inventory.CaptureStorage());
            actionContext.Params["id"] = "test/missing-profile";
            action.Run(actionContext);
            ModAssert.Equal(1, actionContext.Warnings.Count);
            ModAssert.False(inventory.TryConfigureGrid("invalid", new InventoryGridData { Layout = "unknown" }, out _, true));
            ModAssert.False(inventory.TryConfigureGrid("list-size", new InventoryGridData
            {
                Layout = "list",
                Width = 1,
                Height = 1
            }, out _, true));
            ModAssert.Equal(before, JsonSerializer.Serialize(inventory.CaptureStorage()));
            var legacy = JsonSerializer.Deserialize<InventoryGridData>("{\"Width\":2,\"Height\":1}");
            ModAssert.Equal("grid", legacy.Layout);
        }

        private static IItem FirstItem()
        {
            foreach (var id in Entities.Collect<ItemInfo>().Keys)
            {
                return ItemFactory.Instance.Get(id);
            }

            throw new InvalidOperationException("The profile fixture requires an item provider");
        }

        private static void SelectGame(string profile)
        {
            var collection = new ModCollection
            {
                Id = "inventory-profile-fixture",
                Settings = new Dictionary<string, string>()
            };
            if (profile != null)
            {
                collection.Settings["inventory.profile"] = profile;
            }

            var collections = new Dictionary<string, ModCollection> { [collection.Id] = collection };
            ActiveGame.Activate(GameModSet.Compute(collection.Id, collections, new Dictionary<string, IModInfo>(), Array.Empty<string>()));
        }
    }
}
