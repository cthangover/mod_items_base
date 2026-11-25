namespace Cthangover.Mods.Items.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Testing;

    /// <summary>
    /// Storage contracts exercise full rectangles, group dimensions, origin conservation and failed multi-item transactions.
    /// </summary>
    public sealed class InventoryStorageTests
    {
        [ModTest("inventory/rewards_ground_partial_reload_and_pickup")]
        public static void GroundReward(ModTestContext context)
        {
            const string item = "test/ore";
            var store = Grid(1, 1);
            var size = new InventoryItemSize { StackLimit = 3 };
            var point = new InventoryGroundPosition { Domain = "rpg", Location = "meadow", X = 3, Y = 5 };
            ModAssert.True(store.TryAdd("stock", item, 2, size, null, out _, out _));
            ModAssert.True(store.TryGrantReward("quest", item, 5, size, point, out var dropped, out _));
            ModAssert.Equal(4, dropped);
            ModAssert.Equal(3, store.Count(item));
            var json = JsonSerializer.Serialize(store.Capture());
            var saved = JsonSerializer.Deserialize<InventoryStorageState>(json);
            store = new InventoryStorage(saved);
            var pileId = saved.Receipts["quest"].Result;
            ModAssert.Equal(1, saved.Stacks[0].Origins[pileId]);
            ModAssert.Equal(5f, saved.GroundRewards[pileId].Position.Y);
            ModAssert.True(store.TryGrantReward("quest", item, 5, size, point, out var replay, out _));
            ModAssert.Equal(0, replay, "idempotent calls do not repeat the overflow message");
            ModAssert.False(store.TryGrantReward("quest", item, 6, size, point, out _, out _));
            ModAssert.False(store.TryTakeGroundReward("wrong_scene", pileId, size, point.Domain, "forest", out _));
            ModAssert.False(store.TryTakeGroundReward("still_full", pileId, size, point.Domain, point.Location, out var reason));
            ModAssert.Equal("inventory/storage/no_space", reason);
            ModAssert.Equal(json, JsonSerializer.Serialize(store.Capture()));

            ModAssert.True(store.TryExchange("use", new() { [item] = 2 }, new(), null, out _, out _));
            ModAssert.True(store.TryTakeGroundReward("pick1", pileId, size, point.Domain, point.Location, out _));
            ModAssert.Equal(3, store.Count(item));
            ModAssert.Equal(2, store.Capture().GroundRewards[pileId].Count);
            ModAssert.True(store.TryTakeGroundReward("pick1", pileId, size, point.Domain, point.Location, out _));
            ModAssert.Equal(2, store.Capture().GroundRewards[pileId].Count);
            ModAssert.True(store.TryExchange("use_again", new() { [item] = 2 }, new(), null, out _, out _));
            ModAssert.True(store.TryTakeGroundReward("pick2", pileId, size, point.Domain, point.Location, out _));
            ModAssert.Equal(3, store.Count(item));
            ModAssert.Equal(0, store.Capture().GroundRewards.Count);
        }

        [ModTest("inventory/rewards_unbounded_slots_never_drop")]
        public static void UnboundedReward(ModTestContext context)
        {
            var store = new InventoryStorage(new InventoryStorageState());
            ModAssert.True(store.TryGrantReward("gift", "test/ore", 9, new InventoryItemSize { StackLimit = 2 },
                null, out var dropped, out _));
            ModAssert.Equal(0, dropped);
            ModAssert.Equal(9, store.Count("test/ore"));
            ModAssert.Equal(0, store.Capture().GroundRewards.Count);
        }

        /// <summary>A full rotated rectangle refuses another item and every invalid move without losing its original placement.</summary>
        [ModTest("inventory/storage_rectangles_rotation_and_atomic_full_refusal")]
        public static void Rectangles(ModTestContext context)
        {
            var store = Grid(3, 2);
            var size = new InventoryItemSize
            {
                Width = 2,
                Height = 3,
                StackLimit = 1,
                Rotatable = true
            };
            ModAssert.True(store.TryAdd("first", "test/rifle", 1, size, new JsonObject(), out _, out _));
            var row = store.Capture().Stacks[0];
            ModAssert.True(row.Rotated);
            ModAssert.Equal(3, row.OccupiedWidth);
            ModAssert.Equal(2, row.OccupiedHeight);
            var before = JsonSerializer.Serialize(store.Capture());
            ModAssert.False(store.TryAdd("full", "test/knife", 1, new InventoryItemSize(), null, out _, out _));
            ModAssert.Equal(before, JsonSerializer.Serialize(store.Capture()));
            ModAssert.False(store.TryMove("outside", row.Id, "player", 1, 0, true, out _, out _));
            ModAssert.False(store.TryMove("unrotate", row.Id, "player", 0, 0, false, out _, out _));
            ModAssert.Equal(before, JsonSerializer.Serialize(store.Capture()));
        }
        /// <summary>Group dimensions and limits reserve all occupied cells while total quantity spans multiple groups.</summary>

        [ModTest("inventory/storage_group_limit_custom_footprint_and_overlap")]
        public static void Groups(ModTestContext context)
        {
            var store = Grid(4, 2);
            var size = new InventoryItemSize
            {
                GroupWidth = 2,
                GroupHeight = 1,
                StackLimit = 3
            };
            ModAssert.True(store.TryAdd("ammo", "test/ammo", 7, size, null, out _, out _));
            var saved = store.Capture();
            ModAssert.Equal(3, saved.Stacks.Count);
            ModAssert.Equal(3, saved.Stacks[0].Count);
            ModAssert.Equal(3, saved.Stacks[1].Count);
            ModAssert.Equal(1, saved.Stacks[2].Count);
            ModAssert.Equal(2, saved.Stacks[0].OccupiedWidth);
            ModAssert.Equal(1, saved.Stacks[2].OccupiedWidth);
            var before = JsonSerializer.Serialize(saved);
            ModAssert.False(store.TryMove("overlap", saved.Stacks[2].Id, "player", 1, 0, false, out _, out _));
            ModAssert.Equal(before, JsonSerializer.Serialize(store.Capture()));
            ModAssert.Equal(7, store.Count("test/ammo"));
        }
        /// <summary>Split and merge conserve every origin quantity, and retries survive a full JSON cycle.</summary>

        [ModTest("inventory/storage_split_merge_retry_and_json_conserve_origins")]
        public static void Lineage(ModTestContext context)
        {
            var store = Grid(4, 2);
            ModAssert.True(store.TryAdd("genesis", "test/ore", 10, new InventoryItemSize(), null, out _, out _));
            var source = store.Capture().Stacks[0];
            ModAssert.True(store.TrySplit("split", source.Id, 3, "player", 1, 0, false, out var split, out _));
            var childId = split.Result;
            var json = JsonSerializer.Serialize(store.Capture());
            var restored = JsonSerializer.Deserialize<InventoryStorageState>(json);
            restored.Validate();
            ModAssert.Equal(json, JsonSerializer.Serialize(restored));
            store = new InventoryStorage(restored);
            ModAssert.True(store.TrySplit("split", source.Id, 3, "player", 1, 0, false, out var retry, out _));
            ModAssert.Equal(childId, retry.Result);
            ModAssert.Equal(json, JsonSerializer.Serialize(store.Capture()));
            ModAssert.False(store.TrySplit("split", source.Id, 4, "player", 1, 0, false, out _, out _));
            ModAssert.Equal(json, JsonSerializer.Serialize(store.Capture()));
            var saved = store.Capture();
            ModAssert.Equal(7, saved.Stacks[0].Count);
            ModAssert.Equal(3, saved.Stacks[1].Count);
            ModAssert.Equal(source.Id, saved.Stacks[1].Parent);
            ModAssert.True(store.TryMerge("merge", childId, source.Id, out _, out _));
            saved = store.Capture();
            ModAssert.Equal(1, saved.Stacks.Count);
            ModAssert.Equal(source.Id, saved.Stacks[0].Id);
            ModAssert.Equal(10, saved.Stacks[0].Count);
            ModAssert.Equal(1, saved.Stacks[0].Origins.Count);
            ModAssert.Equal(10, store.Count("test/ore"));
        }
        /// <summary>Different individual properties and external identities cannot be merged.</summary>

        [ModTest("inventory/storage_different_properties_and_unique_occurrences_do_not_merge")]
        public static void Properties(ModTestContext context)
        {
            var store = Grid(4, 1);
            ModAssert.True(store.TryAdd("a", "test/sword", 1, new InventoryItemSize(), new JsonObject { ["durability"] = 5 }, out _,
                out _));
            ModAssert.True(store.TryAdd("b", "test/sword", 1, new InventoryItemSize(), new JsonObject { ["durability"] = 9 }, out _,
                out _));
            var saved = store.Capture();
            var before = JsonSerializer.Serialize(saved);
            ModAssert.False(store.TryMerge("mixed", saved.Stacks[0].Id, saved.Stacks[1].Id, out _, out _));
            ModAssert.Equal(before, JsonSerializer.Serialize(store.Capture()));
            var unique = saved.Stacks[0];
            unique.ExternalOwner = "scene3d_base";
            unique.Origins.Clear();
            unique.Origins[unique.Id] = 1;
            store = new InventoryStorage(saved);
            ModAssert.False(store.TryMerge("unique", unique.Id, saved.Stacks[1].Id, out _, out _));
        }
        /// <summary>Insufficient product space leaves ingredients and history byte-for-byte unchanged.</summary>

        [ModTest("inventory/storage_exchange_full_output_never_consumes_input")]
        public static void Exchange(ModTestContext context)
        {
            var store = Grid(2, 1);
            ModAssert.True(store.TryAdd("ore", "test/ore", 4, new InventoryItemSize(), null, out _, out _));
            ModAssert.True(store.TryAdd("block", "test/block", 1, new InventoryItemSize(), null, out _, out _));
            var before = JsonSerializer.Serialize(store.Capture());
            var inputs = new Dictionary<string, int>
            {
                ["test/ore"] = 2
            };
            var outputs = new List<InventoryStackData>
            {
                new()
                {
                    ItemId = "test/product",
                    Count = 1,
                    Size = new InventoryItemSize
                    {
                        Width = 2
                    }
                }
            };
            ModAssert.False(store.TryExchange("craft_full", inputs, outputs, null, out _, out _));
            ModAssert.Equal(before, JsonSerializer.Serialize(store.Capture()));
            inputs["test/ore"] = 4;
            outputs[0].Size.Width = 1;
            ModAssert.True(store.TryExchange("craft", inputs, outputs, null, out var receipt, out _));
            ModAssert.Equal(0, store.Count("test/ore"));
            ModAssert.Equal(1, store.Count("test/product"));
            ModAssert.Equal(4, receipt.Removed[0].Count);
            ModAssert.Equal(4, Sum(receipt.Removed[0].Origins));
        }
        /// <summary>Overlapping placements and mismatched origin quantities refuse before live state changes.</summary>

        [ModTest("inventory/storage_saved_overlap_and_lost_lineage_are_rejected")]
        public static void CorruptSave(ModTestContext context)
        {
            var store = Grid(3, 1);
            ModAssert.True(store.TryAdd("a", "test/a", 1, new InventoryItemSize(), null, out _, out _));
            ModAssert.True(store.TryAdd("b", "test/b", 1, new InventoryItemSize(), null, out _, out _));
            var saved = store.Capture();
            saved.Stacks[1].X = 0;
            ModAssert.Throws(saved.Validate, anyException: true);
            saved = store.Capture();
            saved.Stacks[0].Count++;
            ModAssert.Throws(saved.Validate, anyException: true);
        }

        private static InventoryStorage Grid(int width, int height)
        {
            var store = new InventoryStorage(new InventoryStorageState());
            ModAssert.True(store.TryConfigure("grid", new InventoryGridData
            {
                Width = width,
                Height = height
            }, out _, out _));
            return store;
        }
        /// <summary>Equipment retains its exact group identity and refuses removal into a full bag.</summary>

        [ModTest("inventory/storage_equipment_preserves_id_and_full_bag_refuses_return")]
        public static void Equipment(ModTestContext context)
        {
            var store = Grid(1, 1);
            ModAssert.True(store.TryAdd("gear", "test/sword", 1, new InventoryItemSize(), null, out _, out _));
            var selected = store.Capture().Stacks[0].Id;
            var grid = new InventoryGridData
            {
                Id = "equipment/hero/hand",
                EquipmentCharacter = "hero",
                EquipmentSlot = "hand"
            };
            ModAssert.True(store.TryEquip("wear", selected, grid, null, out var equipped, out _));
            ModAssert.Equal(selected, equipped.Result);
            ModAssert.Equal(0, store.Count("test/sword"));
            ModAssert.True(store.TryAdd("fill", "test/stone", 1, new InventoryItemSize(), null, out _, out _));
            var before = JsonSerializer.Serialize(store.Capture());
            ModAssert.False(store.TryMoveAuto("take_off_full", selected, "player", out _, out _));
            ModAssert.Equal(before, JsonSerializer.Serialize(store.Capture()));
            ModAssert.True(store.TryExchange("free", new Dictionary<string, int> { ["test/stone"] = 1 }, new List<InventoryStackData>(),
                null, out _, out _));
            ModAssert.True(store.TryMoveAuto("take_off", selected, "player", out _, out _));
            ModAssert.Equal(selected, store.Capture().Stacks[0].Id);
            ModAssert.Equal(1, store.Count("test/sword"));
        }
        /// <summary>Repacking and compartment transfer retain IDs and saved quantities.</summary>

        [ModTest("inventory/storage_repack_and_compartment_transfer_preserve_groups")]
        public static void Compartments(ModTestContext context)
        {
            var store = new InventoryStorage(new InventoryStorageState());
            ModAssert.True(store.TryAdd("a", "test/a", 1, new InventoryItemSize(), null, out _, out _));
            ModAssert.True(store.TryAdd("b", "test/b", 1, new InventoryItemSize(), null, out _, out _));
            ModAssert.True(store.TryConfigure("pack", new InventoryGridData
            {
                Width = 2,
                Height = 1
            }, out _, out _, true));
            var saved = store.Capture();
            ModAssert.Equal(0, saved.Stacks[0].X);
            ModAssert.Equal(1, saved.Stacks[1].X);
            var id = saved.Stacks[0].Id;
            ModAssert.True(store.TryConfigure("chest", new InventoryGridData
            {
                Id = "chest",
                Width = 1,
                Height = 1
            }, out _, out _));
            ModAssert.True(store.TryMoveAuto("put_in_chest", id, "chest", out _, out _));
            ModAssert.Equal(0, store.Count("test/a"));
            ModAssert.Equal(1, store.Count("test/a", "chest"));
            var json = JsonSerializer.Serialize(store.Capture());
            store = new InventoryStorage(JsonSerializer.Deserialize<InventoryStorageState>(json));
            ModAssert.True(store.TryMoveAuto("return", id, "player", out _, out _));
            ModAssert.Equal(id, store.Capture().Stacks[0].Id);
        }

        private static int Sum(Dictionary<string, int> values)
        {
            var result = 0;
            foreach (var pair in values)
            {
                result += pair.Value;
            }

            return result;
        }

        /// <summary>A huge ungroupable grant refuses before allocating an unbounded number of rows.</summary>
        [ModTest("inventory/storage_group_budget_refuses_large_grant_before_allocation")]
        public static void Budget(ModTestContext context)
        {
            var store = new InventoryStorage(new InventoryStorageState());
            var before = JsonSerializer.Serialize(store.Capture());
            var size = new InventoryItemSize
            {
                StackLimit = 1
            };
            ModAssert.False(store.TryAdd("huge", "test/individual", int.MaxValue, size, null, out _, out var reason));
            ModAssert.Equal("inventory/storage/budget", reason);
            ModAssert.Equal(before, JsonSerializer.Serialize(store.Capture()));
        }
    }
}
