namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Events;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Utils;

    /// <summary>
    /// The existing ordered bag owns ordinary stacks and optional protected occurrences. Legacy item-ID
    /// operations address fungible stock only; an occurrence cannot be consumed, merged or discarded by count.
    /// Identity-aware adapters use prepared changes and publish observers after their counterpart commits.
    /// </summary>
    public partial class Inventory : IInventory
    {
        private List<IItemContainer> items = new();

        private long mutationRevision;

        /// <summary>
        /// Ordered UI rows. Replacing or editing this legacy list cannot erase authoritative occurrence data;
        /// protected rows are reconstructed from the same Inventory's identity records when next observed.
        /// </summary>
        public List<IItemContainer> Items
        {
            get
            {
                EnsureInstanceRows();
                return items;
            }
            set
            {
                ReplaceOrdinaryRows(value);
            }
        }

        /// <summary>Unknown ordinary item stacks remain saveable until their blueprint provider returns.</summary>
        public List<Settings.CItem> UnresolvedItems { get; } = new();

        /// <summary>Successful changes notify the bag; unique changes defer this until both owners commit.</summary>
        public event Action<string, int> Change;

        /// <summary>True when ordinary stock contains this item; protected occurrences use TryGetInstance.</summary>
        public bool HasItem(IItem item)
        {
            return item != null && HasItem(item.ID);
        }

        /// <summary>True when ordinary stock contains this blueprint, preserving legacy count semantics.</summary>
        public bool HasItem(string id)
        {
            return TryGet(id) != null;
        }

        /// <summary>Returns the ordinary stack for this blueprint; unique rows are never selected by type.</summary>
        public IItemContainer TryGet(IItem item)
        {
            return item == null ? null : TryGet(item.ID);
        }

        /// <summary>Returns the first ordinary stack, or null without consuming an occurrence of the same type.</summary>
        public IItemContainer TryGet(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            foreach (var container in Items)
            {
                if (container is not InventoryInstanceContainer && container.Item != null && container.Item.ID == id)
                {
                    return container;
                }
            }

            return null;
        }

        /// <summary>Ordinary fungible count; protected occurrences do not satisfy count-only crafting or spending.</summary>
        public int CheckCount(IItem item)
        {
            return item == null ? 0 : CheckCount(item.ID);
        }

        /// <summary>Preserves the existing first-stack count contract for ordinary item-ID clients.</summary>
        public int CheckCount(string id)
        {
            return TryGet(id)?.Count ?? 0;
        }

        /// <summary>Adds ordinary stock; the shared blueprint overload never turns an occurrence into a stack.</summary>
        public void Add(IItem item, int count = 1)
        {
            if (item != null)
            {
                Add(item.ID, count);
            }
        }

        /// <summary>
        /// Stacks only with ordinary stock or resolves a new ordinary row. Nonpositive counts and unknown IDs
        /// retain their previous behavior; successful changes invalidate outstanding prepared identity tokens.
        /// </summary>
        public void Add(string id, int count = 1)
        {
            if (count <= 0)
            {
                GameLogger.Log("INVENTORY", $"Add('{id}', {count}): count must be positive, ignored", LogLevel.Warning);
                return;
            }

            var container = TryGet(id);
            if (container != null)
            {
                container.Count += count;
                mutationRevision++;
                Change?.Invoke(id, count);
                PublishInventoryChanged(id, count, container.Count);
                return;
            }

            var item = ItemFactory.Instance.Get(id);
            if (item != null)
            {
                container = new ItemContainer
                {
                    Item = item,
                    Count = count
                };
                Items.Add(container);
                mutationRevision++;
                Change?.Invoke(id, count);
                PublishInventoryChanged(id, count, count);
            }
        }

        /// <summary>Removes ordinary stock; unique occurrence removal requires its prepared identity API.</summary>
        public bool Remove(IItem item, int count = 1)
        {
            return item != null && Remove(item.ID, count);
        }

        /// <summary>Short or nonpositive requests preserve stock; a depleted ordinary row is removed as before.</summary>
        public bool Remove(string id, int count = 1)
        {
            if (count <= 0)
            {
                return false;
            }

            var container = TryGet(id);
            if (container == null || container.Count < count)
            {
                return false;
            }

            var remaining = container.Count - count;
            if (remaining == 0)
            {
                Items.Remove(container);
            }
            else
            {
                container.Count = remaining;
            }

            mutationRevision++;
            Change?.Invoke(id, -count);
            PublishInventoryChanged(id, -count, remaining);
            return true;
        }

        private static void PublishInventoryChanged(string id, int delta, int count)
        {
            ModEventBus.Publish(new InventoryChangedEvent
            {
                ItemId = id,
                Delta = delta,
                NewCount = count
            });
        }
    }
}
