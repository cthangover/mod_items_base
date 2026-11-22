namespace Cthangover.Mods.Items.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;

    /// <summary>Quantity custody has one stock owner, exact lineage and no ordinary path around its paired transfer boundary.</summary>
    public sealed class InventoryCustodyTests
    {
        private const string Domain = "storage_test/world";
        private const string World = "born/0123456789abcdef0123456789abcdef";
        private const string Contract = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

        /// <summary>Drop splits once, replay conserves stock, and pickup merges the exact contributing origins back into the bag.</summary>
        [ModTest("inventory/custody_quantity_split_merge_receipts_and_roundtrip")]
        public static void Roundtrip(ModTestContext context)
        {
            var store = new InventoryStorage(new InventoryStorageState());
            ModAssert.True(store.TryAdd("genesis", "test/token", 10, new InventoryItemSize { StackLimit = 20 }, null, out _, out _));
            var original = store.Capture().Stacks[0];
            ModAssert.True(store.TryTransferCustody("drop", original.Id, 3, Domain, World, Contract, false,
                out var drop, out var reason), reason);
            ModAssert.Equal(7, store.Count(original.ItemId));
            ModAssert.False(drop.Result == original.Id);
            var moved = Find(store.Capture(), drop.Result);
            ModAssert.Equal(3, moved.Count);
            ModAssert.Equal(original.Id, moved.Parent);
            ModAssert.Equal(World, moved.CustodyReference);
            ModAssert.Equal(3, drop.Custody.Count);
            ModAssert.Equal(original.Id, drop.Custody.SourceGroup);
            ModAssert.False(store.TryMerge("illegal-merge", moved.Id, original.Id, out _, out _));
            ModAssert.False(store.TryMoveAuto("illegal-return", moved.Id, "player", out _, out _));
            ModAssert.False(store.TrySplit("illegal-split", moved.Id, 1, "player", -1, -1, false, out _, out _));
            var serialized = JsonSerializer.Serialize(store.Capture());
            store = new InventoryStorage(JsonSerializer.Deserialize<InventoryStorageState>(serialized));
            ModAssert.Equal(serialized, JsonSerializer.Serialize(store.Capture()));
            ModAssert.True(store.TryTransferCustody("drop", original.Id, 3, Domain, World, Contract, false,
                out var retry, out reason), reason);
            ModAssert.Equal(JsonSerializer.Serialize(drop), JsonSerializer.Serialize(retry));
            ModAssert.False(store.TryTransferCustody("drop", original.Id, 2, Domain, World, Contract, false, out _, out _));
            ModAssert.Equal(serialized, JsonSerializer.Serialize(store.Capture()));
            ModAssert.True(store.TryTransferCustody("pickup", moved.Id, 3, Domain, World, Contract, true,
                out var pickup, out reason), reason);
            ModAssert.Equal(10, store.Count(original.ItemId));
            ModAssert.Equal(original.Id, pickup.Result);
            var returned = Find(store.Capture(), original.Id);
            ModAssert.Equal(JsonSerializer.Serialize(original.Origins), JsonSerializer.Serialize(returned.Origins));
            ModAssert.Equal(1, store.Capture().Stacks.Count);
            ModAssert.Equal(moved.Id, pickup.Custody.SourceGroup);
            ModAssert.Equal(3, pickup.Custody.Destinations[original.Id]);
            ModAssert.True(store.TryExchange("consume", new Dictionary<string, int> { [original.ItemId] = 10 },
                new List<InventoryStackData>(), null, out _, out reason), reason);
            ModAssert.Equal(0, store.Count(original.ItemId));
            ModAssert.Equal(3, store.Capture().Receipts["pickup"].Custody.Count);
        }

        /// <summary>A full bag, incorrect counterpart and ordinary direct operations cannot discard or reclaim external stock.</summary>
        [ModTest("inventory/custody_capacity_and_unpaired_mutations_refuse_atomically")]
        public static void Refusals(ModTestContext context)
        {
            var state = new InventoryStorageState();
            state.Grids["player"].Width = 2;
            state.Grids["player"].Height = 1;
            var store = new InventoryStorage(state);
            var size = new InventoryItemSize { StackLimit = 1 };
            ModAssert.True(store.TryAdd("two", "test/token", 2, size, null, out _, out _));
            var id = store.Capture().Stacks[0].Id;
            ModAssert.True(store.TryTransferCustody("drop", id, 1, Domain, World, Contract, false, out var drop, out _));
            ModAssert.True(store.TryAdd("fill", "test/blocker", 1, size, null, out _, out _));
            var before = JsonSerializer.Serialize(store.Capture());
            ModAssert.False(store.TryTransferCustody("full", id, 1, Domain, World, Contract, true, out _, out var reason));
            ModAssert.Equal("inventory/storage/no_space", reason);
            ModAssert.False(store.TryTransferCustody("wrong", id, 1, Domain, "other/world", Contract, true, out _, out _));
            ModAssert.False(store.TryMove("move", id, "player", 0, 0, false, out _, out _));
            ModAssert.False(store.TryConfigure("resize", new InventoryGridData
            {
                Id = drop.Custody.To,
                Layout = "list",
                Custodian = Domain
            }, out _, out _, true));
            ModAssert.False(store.TryExchange("spend-world", new Dictionary<string, int> { ["test/token"] = 1 },
                new List<InventoryStackData>(), id, out _, out _));
            ModAssert.Equal(before, JsonSerializer.Serialize(store.Capture()));
            ModAssert.Equal(1, Find(store.Capture(), id).Count);
        }

        /// <summary>
        /// Preparation changes no live stock; rollback restores exact row references and stale row edits invalidate validation.
        /// </summary>
        [ModTest("inventory/custody_prepared_swap_rollback_and_stale_rows")]
        public static void Prepared(ModTestContext context)
        {
            var inventory = Bag(5);
            var id = Inventory.StackId(inventory.Items[0]);
            var beforeRows = inventory.Items;
            var before = JsonSerializer.Serialize(inventory.CaptureStorage());
            ModAssert.True(inventory.TryPrepareCustodyTransfer("drop", id, 3, Domain, World, Contract, false,
                inventory.Revision, out var change, out var reason), reason);
            using (change)
            {
                ModAssert.Equal(before, JsonSerializer.Serialize(inventory.CaptureStorage()));
                ModAssert.True(change.IsCurrent);
                change.ApplySilently();
                ModAssert.Equal(2, inventory.CheckCount(inventory.Items[0].Item.ID));
                ModAssert.Equal(3, inventory.Stack(change.Receipt.Result).Count);
                change.RollbackSilently();
                ModAssert.True(ReferenceEquals(beforeRows, inventory.Items));
                ModAssert.Equal(before, JsonSerializer.Serialize(inventory.CaptureStorage()));
            }

            ModAssert.True(inventory.TryPrepareCustodyTransfer("stale", id, 3, Domain, World, Contract, false,
                inventory.Revision, out change, out reason), reason);
            using (change)
            {
                inventory.Items[0].Count = 4;
                ModAssert.False(change.IsCurrent);
                ModAssert.Equal(4, inventory.Items[0].Count);
                ModAssert.False(inventory.TryGetStorageReceipt("stale", out _));
            }
        }

        /// <summary>
        /// The normal items slot saves both bag stock and hidden custody once, without mirroring it as accessible quantity.
        /// </summary>
        [ModTest("inventory/custody_normal_items_slot_preserves_single_owner")]
        public static void Save(ModTestContext context)
        {
            var runtime = GameData.Instance.Runtime;
            var previous = runtime.Inventory;
            try
            {
                var inventory = Bag(5);
                runtime.Inventory = inventory;
                var item = inventory.Items[0].Item.ID;
                var id = Inventory.StackId(inventory.Items[0]);
                ModAssert.True(inventory.TryPrepareCustodyTransfer("drop", id, 3, Domain, World, Contract, false,
                    inventory.Revision, out var change, out var reason), reason);
                using (change)
                {
                    ModAssert.True(change.IsCurrent);
                    change.ApplySilently();
                    change.Publish();
                }

                var before = JsonSerializer.Serialize(inventory.CaptureStorage());
                var slot = new ModSlot();
                new ItemsSaveable().OnSave(slot);
                slot = JsonSerializer.Deserialize<ModSlot>(JsonSerializer.Serialize(slot));
                var loaded = new Inventory();
                runtime.Inventory = loaded;
                ModAssert.True(new ItemsSaveable().OnLoad(slot, out var error), error);
                ModAssert.Equal(2, loaded.CheckCount(item));
                ModAssert.Equal(before, JsonSerializer.Serialize(loaded.CaptureStorage()));
                ModAssert.True(loaded.TryGetStorageReceipt("drop", out var proof));
                ModAssert.Equal(3, loaded.Stack(proof.Result).Count);
                ModAssert.False(loaded.TryMoveStack("unpaired", proof.Result, "player", out _));
            }
            finally
            {
                runtime.Inventory = previous;
            }
        }

        private static Inventory Bag(int count)
        {
            foreach (var id in Entities.Collect<ItemInfo>().Keys)
            {
                var item = ItemFactory.Instance.Get(id);
                item.Properties.SetStr("Inventory", "{\"StackLimit\":20}");
                var inventory = new Inventory();
                ModAssert.True(inventory.TryAdd(item, count, out var reason), reason);
                return inventory;
            }

            throw new InvalidOperationException("The custody fixture requires an item definition");
        }

        private static InventoryStackData Find(InventoryStorageState state, string id)
        {
            foreach (var row in state.Stacks)
            {
                if (row.Id == id)
                {
                    return row;
                }
            }

            throw new InvalidOperationException("Missing conserved group " + id);
        }
    }
}
