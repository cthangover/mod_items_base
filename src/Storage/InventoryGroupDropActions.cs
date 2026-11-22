namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Mods.Consumers;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Ordinary drop routing is independent of grid/list presentation. Confirmation captures the selected group's
    /// full data and provider generation, including an explicit no-domain route for legacy 2D discard.
    /// </summary>
    public sealed class InventoryGroupDropActions : ModTypeConsumer<IInventoryGroupDropHandler>
    {
        private static readonly Dictionary<IInventoryGroupDropHandler, Guid> handlers = new();

        /// <summary>Lifecycle registration key; provider removal invalidates every prepared permission.</summary>
        public override string Id
        {
            get
            {
                return "inventory_group_drop_actions";
            }
        }

        protected override void Add(IInventoryGroupDropHandler value, string mod)
        {
            handlers.Add(value, Guid.NewGuid());
        }

        protected override void Remove(IInventoryGroupDropHandler value, string mod)
        {
            handlers.Remove(value);
        }

        /// <summary>Returns a domain refusal for button/tooltip presentation without allocating native previews.</summary>
        public static bool CanDrop(IInventory inventory, IItemContainer row, out string reason)
        {
            return TryPrepare(inventory, row, out _, out reason);
        }

        /// <summary>Captures the exact live group and route before opening the quantity confirmation form.</summary>
        public static bool TryPrepare(IInventory inventory, IItemContainer row, out InventoryGroupDropToken token, out string reason)
        {
            token = null;
            reason = "inventory/instances/stale_revision";
            if (row == null || row is InventoryInstanceContainer || inventory == null || !Present(inventory, row)
                || row.Item == null || row.Count < 1)
            {
                return false;
            }

            var revision = Revision(inventory);
            var snapshot = Snapshot(inventory, row);
            if (snapshot == null || !Find(out var handler, out var registration, out reason))
            {
                return false;
            }

            if (handler != null && !handler.CanDrop(inventory, row, out reason))
            {
                return false;
            }

            var generation = handler?.Generation ?? Guid.Empty;
            if (handler != null && generation == Guid.Empty)
            {
                reason = "inventory/instances/unavailable";
                return false;
            }

            var candidate = new InventoryGroupDropToken(inventory, row, snapshot, revision, registration, generation);
            if (!Ready(candidate, out _, out _, out _, out reason))
            {
                return false;
            }

            token = candidate;
            reason = null;
            return true;
        }

        /// <summary>
        /// Runs a still-current domain transfer. Success with handled=false permits the caller's legacy discard;
        /// every refusal has handled=true so a changed/unloaded 3D domain can never cause a destructive fallback.
        /// </summary>
        public static bool TryDrop(InventoryGroupDropToken token, int count, out bool handled, out string reason)
        {
            handled = true;
            if (!Ready(token, out var inventory, out var row, out var handler, out reason))
            {
                return false;
            }

            if (count < 1 || count > row.Count)
            {
                reason = "inventory/storage/invalid";
                return false;
            }

            if (handler == null)
            {
                handled = false;
                return true;
            }

            if (!handler.CanDrop(inventory, row, out reason)
                || !Ready(token, out inventory, out row, out handler, out reason))
            {
                return false;
            }

            return handler.TryDrop(inventory, row, count, out reason);
        }

        private static bool Ready(InventoryGroupDropToken token, out IInventory inventory, out IItemContainer row,
            out IInventoryGroupDropHandler handler, out string reason)
        {
            inventory = null;
            row = null;
            handler = null;
            reason = "inventory/instances/stale_revision";
            if (token == null || !token.Inventory.TryGetTarget(out inventory) || !token.Row.TryGetTarget(out row)
                || !Present(inventory, row) || Revision(inventory) != token.Revision || Snapshot(inventory, row) != token.Snapshot
                || Revision(inventory) != token.Revision)
            {
                return false;
            }

            if (!Find(out handler, out var registration, out reason) || registration != token.Provider
                || (handler?.Generation ?? Guid.Empty) != token.Generation
                || handler != null && (!handlers.TryGetValue(handler, out var active) || active != registration))
            {
                reason ??= "inventory/instances/stale_revision";
                return false;
            }

            reason = null;
            return true;
        }

        private static bool Find(out IInventoryGroupDropHandler handler, out Guid registration, out string reason)
        {
            handler = null;
            registration = Guid.Empty;
            reason = null;
            foreach (var candidate in new List<IInventoryGroupDropHandler>(handlers.Keys))
            {
                if (!candidate.Active || !handlers.TryGetValue(candidate, out var current))
                {
                    continue;
                }

                if (handler != null)
                {
                    reason = "inventory/quantity/ambiguous_owner";
                    return false;
                }

                handler = candidate;
                registration = current;
            }

            return true;
        }

        private static bool Present(IInventory inventory, IItemContainer row)
        {
            if (GameEnvironment.IsRuntime && !ReferenceEquals(GameData.Instance?.Runtime?.Inventory, inventory))
            {
                return false;
            }

            foreach (var candidate in inventory.Items)
            {
                if (ReferenceEquals(candidate, row))
                {
                    return true;
                }
            }

            return false;
        }

        private static long Revision(IInventory inventory)
        {
            return inventory is Inventory storage ? storage.Revision : 0;
        }

        private static string Snapshot(IInventory inventory, IItemContainer row)
        {
            if (inventory is Inventory storage)
            {
                if (!storage.StorageAvailable)
                {
                    return null;
                }

                var state = storage.CaptureStorage();
                var id = Inventory.StackId(row);
                foreach (var group in state.Stacks)
                {
                    if (group.Id == id && group.Container == "player")
                    {
                        return JsonSerializer.Serialize(group);
                    }
                }

                return null;
            }

            return new JsonObject
            {
                ["Item"] = row.Item?.ID,
                ["Count"] = row.Count
            }.ToJsonString();
        }
    }
}
