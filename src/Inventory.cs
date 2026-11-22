namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Events;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Utils;

    /// <summary>
    /// The bag owns persistent groups and world occurrences in the same storage. Count operations sum all
    /// accessible groups; mutations validate geometry and domain ownership before publication.
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

        /// <summary>True when the accessible bag contains this blueprint, including world occurrences.</summary>
        public bool HasItem(IItem item)
        {
            return item != null && HasItem(item.ID);
        }

        /// <summary>True when any accessible bag group contains this blueprint.</summary>
        public bool HasItem(string id)
        {
            return TryGet(id) != null;
        }

        /// <summary>Returns the first accessible group; identity-sensitive callers retain StackId.</summary>
        public IItemContainer TryGet(IItem item)
        {
            return item == null ? null : TryGet(item.ID);
        }

        /// <summary>Returns the first accessible group without modifying quantity or placement.</summary>
        public IItemContainer TryGet(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            foreach (var container in Items)
            {
                if (container.Item != null && container.Count > 0 && container.Item.ID == id)
                {
                    return container;
                }
            }

            return null;
        }

        /// <summary>Accessible quantity summed over ordinary groups and world occurrences.</summary>
        public int CheckCount(IItem item)
        {
            return item == null ? 0 : CheckCount(item.ID);
        }

        /// <summary>Sums every accessible group; equipment and other compartments remain separate.</summary>
        public int CheckCount(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return 0;
            }

            long total = 0;
            foreach (var row in Items)
            {
                if (row.Item?.ID == id)
                {
                    total += row.Count;
                }
            }

            return checked((int)total);
        }

        /// <summary>Adds stock through the same capacity and group rules used by crafting and pickup.</summary>
        public void Add(IItem item, int count = 1)
        {
            if (item != null && !TryAdd(item, count, out var reason))
            {
                GameLogger.Log("INVENTORY", $"Add('{item.ID}', {count}) refused: {reason}", LogLevel.Warning);
            }
        }

        /// <summary>Resolves a blueprint and adds the entire quantity, or leaves all stock unchanged.</summary>
        public void Add(string id, int count = 1)
        {
            if (count <= 0)
            {
                return;
            }

            Add(ItemFactory.Instance.Get(id), count);
        }

        /// <summary>Consumes the requested quantity through the unified transaction.</summary>
        public bool Remove(IItem item, int count = 1)
        {
            return item != null && Remove(item.ID, count);
        }

        /// <summary>Consumes across groups in bag order. Insufficient stock or a failed owner admission changes nothing.</summary>
        public bool Remove(string id, int count = 1)
        {
            return !string.IsNullOrWhiteSpace(id) && count > 0
                && TryExchange(new Dictionary<string, int> { [id] = count }, new Dictionary<string, int>(), out _);
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
