namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Preallocated inventory participant in a synchronous cross-owner commit. Apply and rollback contain no
    /// observers or provider work; the coordinator publishes only after every participant has committed.
    /// Disposing an unused token cancels it without touching stock, descriptors or receipts.
    /// </summary>
    public sealed class InventoryInstanceChange : IDisposable
    {
        private readonly Inventory owner;
        private readonly InventoryInstanceState before;
        private readonly InventoryInstanceState after;
        private readonly List<IItemContainer> beforeItems;
        private readonly List<IItemContainer> afterItems;
        private readonly List<IItemContainer> observedRows;
        private readonly Dictionary<string, InventoryInstanceContainer> beforeRows;
        private readonly Dictionary<string, InventoryInstanceContainer> afterRows;
        private readonly IItem[] observedItems;
        private readonly int[] observedCounts;
        private readonly long revision;
        private readonly string observedStorage;
        private readonly InventoryInstanceReceipt receipt;
        private bool applied;
        private bool published;
        private bool disposed;

        internal InventoryInstanceChange(Inventory owner, InventoryInstanceState before, InventoryInstanceState after,
            List<IItemContainer> beforeItems, List<IItemContainer> afterItems,
            Dictionary<string, InventoryInstanceContainer> beforeRows,
            Dictionary<string, InventoryInstanceContainer> afterRows, InventoryInstanceReceipt receipt)
        {
            this.owner = owner;
            this.before = before;
            this.after = after;
            this.beforeItems = beforeItems;
            observedRows = new List<IItemContainer>(beforeItems);
            this.afterItems = afterItems;
            this.beforeRows = beforeRows;
            this.afterRows = afterRows;
            this.receipt = receipt;
            revision = owner.Revision;
            observedStorage = JsonSerializer.Serialize(owner.CaptureStorage());
            observedItems = new IItem[beforeItems.Count];
            observedCounts = new int[beforeItems.Count];
            for (var i = 0; i < beforeItems.Count; i++)
            {
                observedItems[i] = beforeItems[i].Item;
                observedCounts[i] = beforeItems[i].Count;
            }
        }

        /// <summary>Detached committed result; reading the token never gives write access to its frozen next state.</summary>
        public InventoryInstanceReceipt Receipt
        {
            get
            {
                return JsonSerializer.Deserialize<InventoryInstanceReceipt>(JsonSerializer.Serialize(receipt));
            }
        }

        /// <summary>
        /// Full validation may inspect consumer-owned row getters. Complete it before either participant mutates,
        /// then check CanApply on both participants without calling external code again.
        /// </summary>
        public bool IsCurrent
        {
            get
            {
                return !disposed && !applied && owner.MatchesInstanceChange(before, revision,
                    observedRows, observedItems, observedCounts)
                    && JsonSerializer.Serialize(owner.CaptureStorage()) == observedStorage && CanApply;
            }
        }

        /// <summary>Owned reference/revision check only; safe between participant swaps, with no row or provider access.</summary>
        public bool CanApply
        {
            get
            {
                return !disposed && !applied && owner.OwnsInstanceState(before, revision);
            }
        }

        /// <summary>
        /// Publishes only prepared managed references, without callbacks. The coordinator must apply its peer
        /// immediately on the same thread, then Publish; it must roll this participant back if the peer fails.
        /// </summary>
        public void ApplySilently()
        {
            if (!CanApply)
            {
                throw new InvalidOperationException("Inventory occurrence preparation is stale");
            }

            owner.SwapInstanceState(after, afterItems, afterRows, revision + 1);
            applied = true;
        }

        /// <summary>Restores the exact old references before any observer; a diverged state refuses rather than overwriting it.</summary>
        public void RollbackSilently()
        {
            if (!applied || published || !owner.OwnsInstanceState(after, revision + 1))
            {
                throw new InvalidOperationException("Inventory occurrence rollback cannot prove ownership");
            }

            owner.SwapInstanceState(before, beforeItems, beforeRows, revision);
            applied = false;
        }

        /// <summary>Delivers the prepared delta once, after the coordinator has committed every owner.</summary>
        public void Publish()
        {
            if (disposed || !applied || published)
            {
                throw new InvalidOperationException("Inventory occurrence change is not publishable");
            }

            published = true;
            owner.PublishInstanceChange(receipt);
        }

        /// <summary>Cancels future token use; it never guesses whether an already applied peer should be rolled back.</summary>
        public void Dispose()
        {
            disposed = true;
        }
    }
}
