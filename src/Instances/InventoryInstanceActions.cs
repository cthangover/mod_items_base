namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Consumers;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;

    /// <summary>
    /// Symmetric mod-owned drop routing. Confirmation tokens contain no domain objects; provider registration,
    /// bag revision, exact row and domain generation are rechecked before the handler can admit side effects.
    /// </summary>
    public sealed class InventoryInstanceActions : ModTypeConsumer<IInventoryInstanceDropHandler>
    {
        private static readonly Dictionary<IInventoryInstanceDropHandler, Guid> handlers = new();

        /// <summary>Stable lifecycle key for replay, hot reload and provider removal.</summary>
        public override string Id
        {
            get
            {
                return "inventory_instance_actions";
            }
        }

        protected override void Add(IInventoryInstanceDropHandler value, string mod)
        {
            handlers.Add(value, Guid.NewGuid());
        }

        protected override void Remove(IInventoryInstanceDropHandler value, string mod)
        {
            handlers.Remove(value);
        }

        /// <summary>Checks the exact selected occurrence without publishing effects or changing ordinary stock.</summary>
        public static bool CanDrop(Inventory inventory, string id, out string reason)
        {
            if (!Find(inventory, id, out var data, out var handler, out _, out reason))
            {
                return false;
            }

            return handler.CanDrop(inventory, data, out reason);
        }

        /// <summary>
        /// Captures the selected row and owner generations before confirmation. A stale row cannot silently
        /// address a replacement occurrence after load, even if both retain the same durable instance ID.
        /// </summary>
        public static bool TryPrepareDrop(Inventory inventory, InventoryInstanceContainer row,
            out InventoryInstanceDropToken token, out string reason)
        {
            token = null;
            reason = "inventory/instances/stale_revision";
            if (row == null || inventory == null || !ReferenceEquals(inventory.FindInstanceRow(row.InstanceId), row)
                || !Find(inventory, row.InstanceId, out var data, out var handler, out var provider, out reason))
            {
                return false;
            }

            var revision = inventory.Revision;
            if (!handler.CanDrop(inventory, data, out reason))
            {
                return false;
            }

            var generation = handler.Generation;
            if (generation == Guid.Empty || inventory.Revision != revision
                || !ReferenceEquals(inventory.FindInstanceRow(row.InstanceId), row)
                || !Find(inventory, row.InstanceId, out var current, out var registered, out var registration, out reason)
                || !ReferenceEquals(registered, handler) || registration != provider || current.Revision != data.Revision)
            {
                reason = "inventory/instances/stale_revision";
                return false;
            }

            token = new InventoryInstanceDropToken(inventory, row, data.Revision, provider, generation);
            reason = null;
            return true;
        }

        /// <summary>Commits only the confirmation's exact current owners; unload/load/mutation refuses without fallback.</summary>
        public static bool TryDrop(InventoryInstanceDropToken token, out string reason)
        {
            reason = "inventory/instances/stale_revision";
            if (token == null || !token.Inventory.TryGetTarget(out var inventory) || !token.Row.TryGetTarget(out var row)
                || inventory.Revision != token.InventoryRevision
                || !ReferenceEquals(inventory.FindInstanceRow(token.Instance), row)
                || !Find(inventory, token.Instance, out var data, out var handler, out var provider, out reason)
                || provider != token.Provider || data.Revision != token.InstanceRevision || handler.Generation != token.Generation
                || !handlers.TryGetValue(handler, out var active) || active != token.Provider)
            {
                reason = "inventory/instances/stale_revision";
                return false;
            }

            if (!handler.CanDrop(inventory, data, out reason))
            {
                return false;
            }

            if (inventory.Revision != token.InventoryRevision || handler.Generation != token.Generation
                || !ReferenceEquals(inventory.FindInstanceRow(token.Instance), row)
                || !handlers.TryGetValue(handler, out provider) || provider != token.Provider
                || GameEnvironment.IsRuntime && !ReferenceEquals(GameData.Instance.Runtime.Inventory, inventory))
            {
                reason = "inventory/instances/stale_revision";
                return false;
            }

            return handler.TryDrop(inventory, data, out reason);
        }

        /// <summary>Immediate callers use the same guarded route; UI confirmations retain the prepared token instead.</summary>
        public static bool TryDrop(Inventory inventory, string id, out string reason)
        {
            if (!TryPrepareDrop(inventory, inventory?.FindInstanceRow(id), out var token, out reason))
            {
                return false;
            }

            return TryDrop(token, out reason);
        }

        private static bool Find(Inventory inventory, string id, out InventoryInstanceData data,
            out IInventoryInstanceDropHandler handler, out Guid registration, out string reason)
        {
            data = null;
            handler = null;
            registration = Guid.Empty;
            reason = "inventory/instances/unavailable";
            if (inventory == null || !inventory.InstancesAvailable
                || GameEnvironment.IsRuntime && !ReferenceEquals(GameData.Instance.Runtime.Inventory, inventory)
                || !inventory.TryGetInstance(id, out data))
            {
                return false;
            }

            // A provider property may unload its own mod. Snapshot iteration does not retain a registration afterward.
            foreach (var candidate in new List<IInventoryInstanceDropHandler>(handlers.Keys))
            {
                if (candidate.Owner != data.Owner || !handlers.TryGetValue(candidate, out var current))
                {
                    continue;
                }

                if (handler != null)
                {
                    reason = "inventory/instances/ambiguous_owner";
                    return false;
                }

                handler = candidate;
                registration = current;
            }

            return handler != null;
        }
    }
}
