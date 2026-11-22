namespace Cthangover.Mods.Items.Tests
{
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;

    /// <summary>
    /// Common consumption preserves the old ledger when validation replaces its context or exhausts identity history.
    /// </summary>
    public sealed class InventoryConsumptionContextTests
    {
        /// <summary>A reentrant context replacement cannot consume the previous bag or write into the new one.</summary>
        [ModTest("inventory/consumption_context_replacement_refuses_old_owner")]
        public static void Context(ModTestContext context)
        {
            var runtime = GameData.Instance.Runtime;
            var previous = runtime.Inventory;
            var consumer = new InventoryConsumptionOwners();
            var assembly = typeof(InventoryReentryConsumptionOwner).Assembly;
            try
            {
                consumer.OnAssemblyLoaded(assembly, "items_base__tests");
                var inventory = Bag(0, out var item);
                runtime.Inventory = inventory;
                var before = JsonSerializer.Serialize(inventory.CaptureStorage());
                ModAssert.False(inventory.TryExchange(new Dictionary<string, int> { [item] = 1 },
                    new Dictionary<string, int>(), out var reason));
                ModAssert.Equal("inventory/instances/stale_revision", reason);
                ModAssert.False(ReferenceEquals(runtime.Inventory, inventory));
                ModAssert.Equal(0, runtime.Inventory.Items.Count);
                ModAssert.Equal(before, JsonSerializer.Serialize(inventory.CaptureStorage()));
                ModAssert.Equal(1, inventory.CheckCount(item));
            }
            finally
            {
                consumer.OnAssemblyUnloading(assembly, "items_base__tests");
                runtime.Inventory = previous;
            }
        }

        /// <summary>Exhausted identity history refuses before arithmetic overflow or provider callbacks.</summary>
        [ModTest("inventory/consumption_identity_budget_refuses_without_overflow")]
        public static void Budget(ModTestContext context)
        {
            var inventory = Bag(long.MaxValue, out var item);
            var before = JsonSerializer.Serialize(inventory.CaptureStorage());
            ModAssert.False(inventory.TryExchange(new Dictionary<string, int> { [item] = 1 },
                new Dictionary<string, int>(), out var reason));
            ModAssert.Equal("inventory/instances/budget", reason);
            ModAssert.Equal(before, JsonSerializer.Serialize(inventory.CaptureStorage()));
        }

        private static Inventory Bag(long revision, out string item)
        {
            item = null;
            foreach (var id in Entities.Collect<ItemInfo>().Keys)
            {
                item = id;
                break;
            }

            ModAssert.NotNull(item);
            var state = new InventoryInstanceState
            {
                Revision = revision
            };
            state.Records["storage_test/occurrence"] = new InventoryInstanceData
            {
                Id = "storage_test/occurrence",
                ItemId = item,
                Owner = "storage_test/reentry",
                Revision = 1
            };
            var inventory = new Inventory();
            inventory.RestoreInstances(state);
            return inventory;
        }
    }
}
