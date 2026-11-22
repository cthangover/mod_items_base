namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Utils;

    public partial class Inventory
    {
        private InventoryInstanceState instanceState = new();
        private Dictionary<string, InventoryInstanceContainer> instanceRows = new(StringComparer.Ordinal);

        /// <summary>Optimistic admission revision; ordinary mutations and restore also invalidate prepared tokens.</summary>
        public long Revision
        {
            get
            {
                return mutationRevision;
            }
        }

        /// <summary>Bounded unique capacity; ordinary stock retains its previous unlimited stack policy.</summary>
        public int InstanceCapacity { get; }

        /// <summary>Preserves the ordinary parameterless bag constructor, including reflection-based callers.</summary>
        public Inventory() : this(256)
        {
        }

        /// <summary>Creates an empty bag with a bounded unique-row capacity; zero refuses new occurrences only.</summary>
        public Inventory(int instanceCapacity)
        {
            if (instanceCapacity < 0 || instanceCapacity > 4096)
            {
                throw new ArgumentOutOfRangeException(nameof(instanceCapacity));
            }

            InstanceCapacity = instanceCapacity;
        }

        /// <summary>Returns a detached occurrence descriptor, including unresolved providers, without creating a bag row.</summary>
        public bool TryGetInstance(string id, out InventoryInstanceData value)
        {
            value = null;
            if (id == null || !instanceState.Records.TryGetValue(id, out var stored))
            {
                return false;
            }

            value = CopyInstance(stored);
            return true;
        }

        /// <summary>
        /// Returns the current borrowed row for selection; unresolved blueprints retain descriptors without usable rows.
        /// </summary>
        public InventoryInstanceContainer FindInstanceRow(string id)
        {
            EnsureInstanceRows();
            return id != null && instanceRows.TryGetValue(id, out var value) ? value : null;
        }

        /// <summary>Copies the complete optional state, including unresolved identities and retained transfer receipts.</summary>
        public InventoryInstanceState CaptureInstances()
        {
            return CopyInstance(instanceState);
        }

        /// <summary>Reads detached retry evidence even after the corresponding occurrence left the bag.</summary>
        public bool TryGetInstanceReceipt(string operation, out InventoryInstanceReceipt receipt)
        {
            receipt = null;
            if (operation == null || !instanceState.Receipts.TryGetValue(operation, out var stored))
            {
                return false;
            }

            receipt = CopyInstance(stored);
            return true;
        }

        /// <summary>
        /// Prepares one protected insertion without changing stock or notifying observers. All provider resolution
        /// and allocation finish here; current ordinary or unique changes invalidate the resulting token.
        /// </summary>
        public bool TryPrepareInstanceInsert(string operation, string request, InventoryInstanceData value,
            long expectedInventoryRevision, out InventoryInstanceChange change, out string reason)
        {
            change = null;
            reason = null;
            try
            {
                var frozen = CopyInstance(value) ?? throw new ArgumentException("Missing inventory occurrence");
                frozen.Validate();
                if (!InstanceAdmission(operation, request, expectedInventoryRevision, out reason))
                {
                    return false;
                }

                if (instanceState.Records.ContainsKey(frozen.Id))
                {
                    reason = "inventory/instances/already_present";
                    return false;
                }

                if (instanceState.Records.Count >= InstanceCapacity)
                {
                    reason = "inventory/instances/capacity";
                    return false;
                }

                foreach (var previous in instanceState.Receipts.Values)
                {
                    if (previous.Instance == frozen.Id && (previous.ItemId != frozen.ItemId || previous.Owner != frozen.Owner))
                    {
                        reason = "inventory/instances/identity_conflict";
                        return false;
                    }
                }

                var baseline = instanceState;
                if (!Entities.Collect<ItemInfo>(required: false).ContainsKey(frozen.ItemId))
                {
                    reason = "inventory/instances/missing_provider";
                    return false;
                }

                var item = ItemFactory.Instance.Get(frozen.ItemId);
                if (item == null)
                {
                    reason = "inventory/instances/missing_provider";
                    return false;
                }

                if (!ReferenceEquals(baseline, instanceState)
                    || !InstanceAdmission(operation, request, expectedInventoryRevision, out reason))
                {
                    reason ??= "inventory/instances/stale_revision";
                    return false;
                }

                var before = Items;
                if (!InstanceAdmission(operation, request, expectedInventoryRevision, out reason))
                {
                    return false;
                }

                var after = CopyInstance(baseline);
                after.Records.Add(frozen.Id, frozen);
                var receipt = MakeInstanceReceipt(operation, request, frozen, true, frozen.Revision);
                after.Receipts.Add(operation, receipt);
                after.Revision++;
                after.Validate();
                var rows = new Dictionary<string, InventoryInstanceContainer>(instanceRows, StringComparer.Ordinal);
                var container = new InventoryInstanceContainer(frozen.Id, item);
                var layout = CaptureStorage();
                container.Storage.ExternalOwner = frozen.Owner;
                if (!InventoryStorage.Place(layout, container.Storage))
                {
                    reason = "inventory/storage/no_space";
                    return false;
                }

                rows.Add(frozen.Id, container);
                var nextItems = new List<IItemContainer>(before) { container };
                change = new InventoryInstanceChange(this, baseline, after, before, nextItems, instanceRows, rows, receipt);
                return true;
            }
            catch (ArgumentException)
            {
                reason = "inventory/instances/invalid";
                return false;
            }
        }

        /// <summary>Prepares exact occurrence removal; ordinary count-only stock and all durable evidence remain untouched.</summary>
        public bool TryPrepareInstanceRemove(string operation, string request, string id, long expectedInstanceRevision,
            long expectedInventoryRevision, out InventoryInstanceChange change, out string reason)
        {
            change = null;
            if (!InstanceAdmission(operation, request, expectedInventoryRevision, out reason))
            {
                return false;
            }

            if (id == null || !instanceState.Records.TryGetValue(id, out var source))
            {
                reason = "inventory/instances/absent";
                return false;
            }

            if (source.Revision != expectedInstanceRevision || source.Revision == long.MaxValue)
            {
                reason = "inventory/instances/stale_revision";
                return false;
            }

            if (instanceRows.TryGetValue(id, out var selected) && selected.Storage.Container != "player")
            {
                reason = "inventory/instances/unavailable";
                return false;
            }

            var before = Items;
            if (!InstanceAdmission(operation, request, expectedInventoryRevision, out reason))
            {
                return false;
            }

            var after = CopyInstance(instanceState);
            after.Records.Remove(id);
            var receipt = MakeInstanceReceipt(operation, request, source, false, source.Revision + 1);
            after.Receipts.Add(operation, receipt);
            after.Revision++;
            after.Validate();
            var rows = new Dictionary<string, InventoryInstanceContainer>(instanceRows, StringComparer.Ordinal);
            rows.Remove(id);
            var nextItems = new List<IItemContainer>();
            foreach (var container in before)
            {
                if (container is not InventoryInstanceContainer unique || unique.InstanceId != id)
                {
                    nextItems.Add(container);
                }
            }

            change = new InventoryInstanceChange(this, instanceState, after, before, nextItems, instanceRows, rows, receipt);
            return true;
        }

        /// <summary>
        /// Validates and resolves the complete candidate before publishing either ordinary rows or unique state.
        /// Missing blueprints preserve descriptors without usable rows; restore invalidates every previous token.
        /// </summary>
        public void RestoreInstances(InventoryInstanceState value, List<IItemContainer> ordinaryRows = null)
        {
            var next = CopyInstance(value) ?? throw new ArgumentException("Missing inventory occurrence state");
            next.Validate();
            var nextItems = OrdinaryRows(ordinaryRows ?? Items);
            var nextRows = new Dictionary<string, InventoryInstanceContainer>(StringComparer.Ordinal);
            var definitions = Entities.Collect<ItemInfo>(required: false);
            foreach (var entry in next.Records.Values)
            {
                if (!definitions.ContainsKey(entry.ItemId))
                {
                    continue;
                }

                var item = ItemFactory.Instance.Get(entry.ItemId);
                if (item != null)
                {
                    var row = new InventoryInstanceContainer(entry.Id, item);
                    nextRows.Add(entry.Id, row);
                    nextItems.Add(row);
                }
            }

            if (ordinaryRows != null)
            {
                storage = new InventoryStorageState();
            }

            SwapInstanceState(next, nextItems, nextRows, mutationRevision + 1);
            retainedInstances = null;
        }

        private bool InstanceAdmission(string operation, string request, long revision, out string reason)
        {
            reason = null;
            if (!InstancesAvailable)
            {
                reason = "inventory/instances/unavailable";
                return false;
            }

            if (revision != mutationRevision)
            {
                reason = "inventory/instances/stale_revision";
                return false;
            }

            if (string.IsNullOrWhiteSpace(operation) || operation.Length > 1024 || request == null || request.Length != 64)
            {
                reason = "inventory/instances/invalid";
                return false;
            }

            foreach (var character in operation)
            {
                if (char.IsControl(character))
                {
                    reason = "inventory/instances/invalid";
                    return false;
                }
            }

            foreach (var character in request)
            {
                if (!Uri.IsHexDigit(character))
                {
                    reason = "inventory/instances/invalid";
                    return false;
                }
            }

            if (instanceState.Receipts.ContainsKey(operation))
            {
                reason = "inventory/instances/operation_conflict";
                return false;
            }

            if (instanceState.Receipts.Count >= 16384 || instanceState.Revision == long.MaxValue
                || mutationRevision == long.MaxValue)
            {
                reason = "inventory/instances/budget";
                return false;
            }

            return true;
        }

        private static InventoryInstanceReceipt MakeInstanceReceipt(string operation, string request,
            InventoryInstanceData value, bool inserted, long revision)
        {
            return new InventoryInstanceReceipt
            {
                Operation = operation,
                Request = request,
                Instance = value.Id,
                ItemId = value.ItemId,
                Owner = value.Owner,
                Revision = revision,
                Inserted = inserted
            };
        }

        private static T CopyInstance<T>(T value)
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
        }

        private static List<IItemContainer> OrdinaryRows(IEnumerable<IItemContainer> source)
        {
            var result = new List<IItemContainer>();
            foreach (var container in source)
            {
                if (container is not InventoryInstanceContainer)
                {
                    result.Add(container ?? throw new ArgumentException("Missing inventory row"));
                }
            }

            return result;
        }

        private void ReplaceOrdinaryRows(List<IItemContainer> value)
        {
            items = OrdinaryRows(value ?? throw new ArgumentNullException(nameof(value)));
            mutationRevision++;
            EnsureInstanceRows();
        }

        private void EnsureInstanceRows()
        {
            if (instanceRows.Count == 0)
            {
                return;
            }

            var present = new HashSet<string>(StringComparer.Ordinal);
            var repaired = false;
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i] is InventoryInstanceContainer row
                    && (!instanceRows.TryGetValue(row.InstanceId, out var owned) || !ReferenceEquals(owned, row)
                        || !present.Add(row.InstanceId)))
                {
                    items.RemoveAt(i--);
                    repaired = true;
                }
            }

            foreach (var pair in instanceRows)
            {
                if (!present.Contains(pair.Key) && pair.Value.Storage.Container == "player")
                {
                    items.Add(pair.Value);
                    repaired = true;
                }
            }

            if (repaired)
            {
                mutationRevision++;
            }
        }

        internal bool MatchesInstanceChange(InventoryInstanceState state, long revision, List<IItemContainer> observed,
            IItem[] blueprints, int[] counts)
        {
            if (!ReferenceEquals(instanceState, state) || mutationRevision != revision || Items.Count != observed.Count)
            {
                return false;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var current = items[i];
                if (!ReferenceEquals(current, observed[i]) || !ReferenceEquals(current.Item, blueprints[i])
                    || current.Count != counts[i])
                {
                    return false;
                }
            }

            return ReferenceEquals(instanceState, state) && mutationRevision == revision && items.Count == observed.Count;
        }

        internal bool OwnsInstanceState(InventoryInstanceState state, long revision)
        {
            return ReferenceEquals(instanceState, state) && mutationRevision == revision;
        }

        internal void SwapInstanceState(InventoryInstanceState state, List<IItemContainer> rows,
            Dictionary<string, InventoryInstanceContainer> protectedRows, long revision)
        {
            instanceState = state;
            items = rows;
            instanceRows = protectedRows;
            mutationRevision = revision;
        }

        internal void PublishInstanceChange(InventoryInstanceReceipt receipt)
        {
            if (GameEnvironment.IsRuntime && !ReferenceEquals(GameData.Instance.Runtime.Inventory, this))
            {
                return;
            }

            var delta = receipt.Inserted ? 1 : -1;
            try
            {
                Change?.Invoke(receipt.ItemId, delta);
            }
            catch (Exception error)
            {
                GameLogger.Log("INVENTORY", "Committed occurrence observer failed: " + error.Message, LogLevel.Warning);
            }

            if (GameEnvironment.IsRuntime && !ReferenceEquals(GameData.Instance.Runtime.Inventory, this))
            {
                return;
            }

            try
            {
                PublishInventoryChanged(receipt.ItemId, delta, CheckCount(receipt.ItemId));
            }
            catch (Exception error)
            {
                GameLogger.Log("INVENTORY", "Committed occurrence event failed: " + error.Message, LogLevel.Warning);
            }
        }
    }
}
