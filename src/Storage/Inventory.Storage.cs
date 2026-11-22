namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using System.Runtime.CompilerServices;
    using System.Text.Json.Nodes;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Utils;

    public partial class Inventory
    {
        private InventoryStorageState storage = new();
        private static readonly ConditionalWeakTable<IItemContainer, InventoryStackData> legacyStorage = new();
        /// <summary>Transient owner identity fences drag data across new games and loaded inventory objects.</summary>
        public Guid StorageGeneration { get; } = Guid.NewGuid();

        internal void ImportUnresolvedStorage(List<Settings.CItem> unresolved)
        {
            foreach (var value in unresolved)
            {
                if (value.Count < 1)
                {
                    continue;
                }

                var row = new InventoryStackData
                {
                    Id = "unresolved/" + Guid.NewGuid().ToString("N"),
                    ItemId = value.ID,
                    Count = value.Count
                };
                row.Origins[row.Id] = value.Count;
                storage.Stacks.Add(row);
            }
        }

        /// <summary>Captures all compartments, exact group placements, unresolved blueprints and operation history.</summary>
        public InventoryStorageState CaptureStorage()
        {
            EnsureInstanceRows();
            var next = InventoryStorage.Copy(storage);
            var definitions = Entities.Collect<ItemInfo>(required: false);
            var rows = new List<InventoryStackData>();
            foreach (var saved in next.Stacks)
            {
                if (saved.Container != "player" || !definitions.ContainsKey(saved.ItemId))
                {
                    if (saved.ExternalOwner == null || instanceState.Records.ContainsKey(saved.Id))
                    {
                        rows.Add(saved);
                    }
                }
            }

            foreach (var row in new List<IItemContainer>(items))
            {
                InventoryStackData data;
                if (row is InventoryInstanceContainer occurrence)
                {
                    if (!instanceState.Records.TryGetValue(occurrence.InstanceId, out var descriptor))
                    {
                        continue;
                    }

                    data = InventoryStorage.Copy(occurrence.Storage);
                    data.ExternalOwner = descriptor.Owner;
                }
                else if (row is ItemContainer ordinary)
                {
                    if (ordinary.Count < 1)
                    {
                        continue;
                    }

                    data = InventoryStorage.Copy(ordinary.Storage);
                }
                else
                {
                    data = DescribeLegacyRow(row);
                    if (data == null)
                    {
                        continue;
                    }
                }

                if (InventoryStorage.Find(rows, data.Id) == null)
                {
                    rows.Add(data);
                }
            }

            foreach (var descriptor in instanceState.Records.Values)
            {
                if (InventoryStorage.Find(rows, descriptor.Id) != null)
                {
                    continue;
                }

                var data = new InventoryStackData
                {
                    Id = descriptor.Id,
                    ItemId = descriptor.ItemId,
                    Count = 1,
                    ExternalOwner = descriptor.Owner
                };
                data.Origins[descriptor.Id] = 1;
                next.Stacks = rows;
                if (!InventoryStorage.Place(next, data))
                {
                    throw new ArgumentException("Unresolved inventory occurrence has no valid storage placement");
                }

                rows.Add(data);
            }

            next.Stacks = rows;
            next.Validate();
            return next;
        }

        /// <summary>Restores one validated storage candidate without dropping items whose definition provider is absent.</summary>
        public void RestoreStorage(InventoryStorageState value)
        {
            var next = InventoryStorage.Copy(value ?? throw new ArgumentNullException(nameof(value)));
            next.Validate();
            var nextItems = BuildStorageRows(next);
            ApplyStoragePlacements(next);
            storage = next;
            items = nextItems;
            unavailableStorage = false;
            mutationRevision++;
        }

        /// <summary>Validates both storage and domain descriptors in isolation before replacing this inventory generation.</summary>
        public void RestoreStorage(InventoryStorageState value, InventoryInstanceState identities)
        {
            var candidate = new Inventory(InstanceCapacity);
            candidate.RestoreInstances(identities);
            candidate.RestoreStorage(value);
            foreach (var pair in candidate.instanceState.Records)
            {
                var placement = InventoryStorage.Find(candidate.storage, pair.Key);
                if (placement == null || placement.ExternalOwner != pair.Value.Owner || placement.ItemId != pair.Value.ItemId)
                {
                    throw new ArgumentException("Inventory occurrence is missing its saved placement");
                }
            }

            storage = candidate.storage;
            instanceState = candidate.instanceState;
            instanceRows = candidate.instanceRows;
            items = candidate.items;
            retainedInstances = null;
            unavailableStorage = false;
            mutationRevision++;
        }

        /// <summary>Finds the exact selected group, independently of its blueprint and any other equal groups.</summary>
        public IItemContainer FindStack(string id)
        {
            foreach (var row in Items)
            {
                if (StackId(row) == id)
                {
                    return row;
                }
            }

            return null;
        }

        /// <summary>Portable selection key for both ordinary groups and world occurrences.</summary>
        public static string StackId(IItemContainer row)
        {
            return row is ItemContainer ordinary ? ordinary.Storage.Id : row is InventoryInstanceContainer occurrence
                ? occurrence.InstanceId : row == null ? null : legacyStorage.GetValue(row, NewLegacyStorage).Id;
        }

        private static InventoryStackData NewLegacyStorage(IItemContainer row)
        {
            return new InventoryStackData
            {
                Id = "legacy/" + Guid.NewGuid().ToString("N")
            };
        }

        private static InventoryStackData DescribeLegacyRow(IItemContainer row)
        {
            var data = legacyStorage.GetValue(row, NewLegacyStorage);
            var item = row.Item;
            var count = row.Count;
            if (item == null || count < 1)
            {
                return null;
            }

            if (data.ItemId != item.ID || data.Count != count)
            {
                data.ItemId = item.ID;
                data.Count = count;
                data.Size = InventoryItemSize.From(item);
                data.Origins.Clear();
                data.Origins["legacy/" + Guid.NewGuid().ToString("N")] = count;
            }

            return InventoryStorage.Copy(data);
        }

        /// <summary>Reads detached position and footprint for a bag, nested compartment or equipment group.</summary>
        public InventoryStackData Stack(string id)
        {
            return InventoryStorage.Find(CaptureStorage(), id);
        }

        /// <summary>Creates or resizes a compartment only when all saved placements remain valid.</summary>
        public bool TryConfigureGrid(string operation, InventoryGridData grid, out string reason, bool repack = false)
        {
            if (grid == null || grid.EquipmentCharacter != null || grid.Id == null || grid.Id.StartsWith("equipment/",
                StringComparison.Ordinal))
            {
                reason = "inventory/storage/invalid";
                return false;
            }

            var store = new InventoryStorage(CaptureStorage());
            if (!store.TryConfigure(operation, grid, out _, out reason, repack))
            {
                return false;
            }

            return ApplyStorage(store.Capture(), null, out reason);
        }

        /// <summary>Moves a whole selected group without changing its identity or provenance.</summary>
        public bool TryMoveStack(string operation, string id, string container, int x, int y, bool rotated, out string reason)
        {
            var store = new InventoryStorage(CaptureStorage());
            if (!store.TryMove(operation, id, container, x, y, rotated, out _, out reason))
            {
                return false;
            }

            return ApplyStorage(store.Capture(), Stack(id)?.ItemId, out reason);
        }

        /// <summary>Automatically places a whole selected group in a compartment, retaining identity and exact properties.</summary>
        public bool TryMoveStack(string operation, string id, string container, out string reason)
        {
            var store = new InventoryStorage(CaptureStorage());
            return store.TryMoveAuto(operation, id, container, out _, out reason) && ApplyStorage(store.Capture(), Stack(id)?.ItemId,
                out reason);
        }

        /// <summary>Equips one unit with its lineage; replacing equipment refuses if the old item cannot return to the bag.</summary>
        public bool TryEquipStack(string operation, string id, string character, string slot, string legacyReturn, out string reason,
            string generatedItem = null)
        {
            var compartment = new InventoryGridData
            {
                Id = "equipment/" + Uri.EscapeDataString(character) + "/" + Uri.EscapeDataString(slot),
                EquipmentCharacter = character,
                EquipmentSlot = slot
            };
            InventoryStackData returned = null;
            if (legacyReturn != null)
            {
                var item = ItemFactory.Instance.Get(legacyReturn);
                if (item == null)
                {
                    reason = "inventory/instances/missing_provider";
                    return false;
                }

                returned = new InventoryStackData
                {
                    ItemId = item.ID,
                    Count = 1,
                    Size = InventoryItemSize.From(item)
                };
            }

            var store = new InventoryStorage(CaptureStorage());
            InventoryStackData generated = null;
            if (generatedItem != null)
            {
                var item = ItemFactory.Instance.Get(generatedItem);
                if (item == null)
                {
                    reason = "inventory/instances/missing_provider";
                    return false;
                }

                generated = new InventoryStackData
                {
                    ItemId = item.ID,
                    Count = 1,
                    Size = InventoryItemSize.From(item)
                };
            }

            return store.TryEquip(operation, id, compartment, returned, out _, out reason, generated) && ApplyStorage(store.Capture(),
                Stack(id)?.ItemId, out reason);
        }

        /// <summary>Creates one split child with retry-safe identity; source quantity and child footprint must both fit.</summary>
        public bool TrySplitStack(string operation, string id, int count, string container, int x, int y, bool rotated, out string child,
            out string reason)
        {
            child = null;
            var store = new InventoryStorage(CaptureStorage());
            if (!store.TrySplit(operation, id, count, container, x, y, rotated, out var receipt, out reason)
                || !ApplyStorage(store.Capture(), Stack(id)?.ItemId, out reason))
            {
                return false;
            }

            child = receipt.Result;
            return true;
        }

        /// <summary>Merges compatible selected groups, preserving their origin contributions and target identity.</summary>
        public bool TryMergeStacks(string operation, string source, string target, out string reason)
        {
            var store = new InventoryStorage(CaptureStorage());
            if (!store.TryMerge(operation, source, target, out _, out reason))
            {
                return false;
            }

            return ApplyStorage(store.Capture(), Stack(target)?.ItemId, out reason);
        }

        /// <summary>Reports add refusal to pickup/deal callers instead of silently losing an item when a container is full.</summary>
        public bool TryAdd(IItem item, int count, out string reason)
        {
            reason = "inventory/storage/invalid";
            if (item == null)
            {
                return false;
            }

            var store = new InventoryStorage(CaptureStorage());
            if (!store.TryAdd("add/" + Guid.NewGuid().ToString("N"), item.ID, count, InventoryItemSize.From(item), new JsonObject(),
                out _, out reason))
            {
                return false;
            }

            return ApplyStorage(store.Capture(), item.ID, out reason);
        }

        /// <summary>Checks a complete removal/product batch on detached state, including product footprints and stack limits.</summary>
        public bool CanExchange(Dictionary<string, int> inputs, Dictionary<string, int> outputs, string selected = null)
        {
            return PrepareExchange(inputs, outputs, selected, "preview", out var next, out var receipt, out _) && CommitExchange(next,
                receipt, true, out _);
        }

        /// <summary>Consumes ingredients and places products in one commit; observers never see an intermediate empty bag.</summary>
        public bool TryExchange(Dictionary<string, int> inputs, Dictionary<string, int> outputs, out string reason,
            string selected = null, string purpose = "exchange")
        {
            if (!PrepareExchange(inputs, outputs, selected, purpose, out var next, out var receipt, out reason))
            {
                return false;
            }

            return CommitExchange(next, receipt, false, out reason);
        }

        private bool PrepareExchange(Dictionary<string, int> inputs, Dictionary<string, int> outputs, string selected, string purpose,
            out InventoryStorageState next, out InventoryStorageReceipt receipt, out string reason)
        {
            next = null;
            receipt = null;
            reason = "inventory/storage/invalid";
            if (inputs == null || outputs == null)
            {
                return false;
            }

            var products = new List<InventoryStackData>();
            foreach (var pair in outputs)
            {
                var item = ItemFactory.Instance.Get(pair.Key);
                if (item == null || pair.Value < 1)
                {
                    return false;
                }

                products.Add(new InventoryStackData
                {
                    ItemId = item.ID,
                    Count = pair.Value,
                    Size = InventoryItemSize.From(item)
                });
            }

            var store = new InventoryStorage(CaptureStorage());
            if (!store.TryExchange(purpose + "/" + Guid.NewGuid().ToString("N"), inputs, products, selected, out receipt, out reason))
            {
                return false;
            }

            next = store.Capture();
            return true;
        }

        private List<IItemContainer> BuildStorageRows(InventoryStorageState next)
        {
            var result = new List<IItemContainer>();
            var definitions = Entities.Collect<ItemInfo>(required: false);
            foreach (var data in next.Stacks)
            {
                if (data.ExternalOwner != null)
                {
                    if (!instanceState.Records.TryGetValue(data.Id, out var descriptor) || data.ItemId != descriptor.ItemId
                        || data.ExternalOwner != descriptor.Owner)
                    {
                        throw new ArgumentException("Inventory storage occurrence has no matching owner descriptor");
                    }

                    if (!instanceRows.TryGetValue(data.Id, out var occurrence))
                    {
                        continue;
                    }

                    if (data.Container == "player")
                    {
                        result.Add(occurrence);
                    }
                }
                else if (data.Container == "player" && definitions.ContainsKey(data.ItemId))
                {
                    var item = ItemFactory.Instance.Get(data.ItemId);
                    var row = new ItemContainer
                    {
                        Item = item,
                        Storage = InventoryStorage.Copy(data)
                    };
                    result.Add(row);
                }
            }

            return result;
        }

        private void ApplyStoragePlacements(InventoryStorageState next)
        {
            foreach (var data in next.Stacks)
            {
                if (data.ExternalOwner != null && instanceRows.TryGetValue(data.Id, out var occurrence))
                {
                    occurrence.Storage = data;
                }
            }
        }

        private bool ApplyStorage(InventoryStorageState next, string itemId, out string reason)
        {
            if (!StorageAvailable)
            {
                reason = "inventory/instances/unavailable";
                return false;
            }

            var before = CaptureStorage();
            if (JsonSerializer.Serialize(before) == JsonSerializer.Serialize(next))
            {
                reason = null;
                return true;
            }

            RestoreStorage(next);
            reason = null;
            NotifyStorageChanges(before, next, itemId);
            return true;
        }

        private void NotifyStorageChanges(InventoryStorageState before, InventoryStorageState after, string itemId)
        {
            var deltas = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var row in before.Stacks)
            {
                if (row.Container == "player")
                {
                    deltas.TryGetValue(row.ItemId, out var delta);
                    deltas[row.ItemId] = delta - row.Count;
                }
            }

            foreach (var row in after.Stacks)
            {
                if (row.Container == "player")
                {
                    deltas.TryGetValue(row.ItemId, out var delta);
                    deltas[row.ItemId] = delta + row.Count;
                }
            }

            var published = false;
            foreach (var pair in deltas)
            {
                if (pair.Value == 0)
                {
                    continue;
                }

                published = true;
                try
                {
                    Change?.Invoke(pair.Key, pair.Value);
                    PublishInventoryChanged(pair.Key, pair.Value, CheckCount(pair.Key));
                }
                catch (Exception error)
                {
                    GameLogger.Log("INVENTORY", "Committed storage observer failed: " + error.Message, LogLevel.Warning);
                }
            }

            if (!published)
            {
                NotifyStorage(itemId);
            }
        }

        private void NotifyStorage(string itemId)
        {
            try
            {
                Change?.Invoke(itemId, 0);
                PublishInventoryChanged(itemId, 0, itemId == null ? 0 : CheckCount(itemId));
            }
            catch (Exception error)
            {
                GameLogger.Log("INVENTORY", "Committed storage observer failed: " + error.Message, LogLevel.Warning);
            }
        }
    }
}
