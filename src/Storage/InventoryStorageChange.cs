namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    /// <summary>
    /// Preallocated items-owner participant for external quantity transfer. Only owned pointers/revisions change
    /// inside the swap window; getters/providers run during IsCurrent before any owner commits.
    /// </summary>
    public sealed class InventoryStorageChange : IInventoryStorageParticipant
    {
        private readonly Inventory owner;
        private readonly InventoryStorageState before;
        private readonly InventoryStorageState after;
        private readonly InventoryStorageState observed;
        private readonly List<IItemContainer> beforeItems;
        private readonly List<IItemContainer> afterItems;
        private readonly long revision;
        private readonly InventoryStorageReceipt receipt;
        private readonly string snapshot;
        private bool applied;
        private bool published;
        private bool disposed;

        internal InventoryStorageChange(Inventory owner, InventoryStorageState before, InventoryStorageState observed,
            InventoryStorageState after, List<IItemContainer> beforeItems, List<IItemContainer> afterItems,
            long revision, InventoryStorageReceipt receipt)
        {
            this.owner = owner;
            this.before = before;
            this.after = after;
            this.observed = observed;
            this.beforeItems = beforeItems;
            this.afterItems = afterItems;
            this.revision = revision;
            this.receipt = receipt;
            snapshot = JsonSerializer.Serialize(observed);
        }

        /// <summary>Detached result and exact movement evidence for the counterpart owner's prepared binding.</summary>
        public InventoryStorageReceipt Receipt
        {
            get
            {
                return InventoryStorage.Copy(receipt);
            }
        }

        /// <summary>Performs all potentially reentrant row reads before the coordinator's final pure checks.</summary>
        public bool IsCurrent
        {
            get
            {
                return CanApply && owner.StorageAvailable && JsonSerializer.Serialize(owner.CaptureStorage()) == snapshot && CanApply;
            }
        }

        /// <summary>Pure ownership check, safe immediately before the two participant swaps.</summary>
        public bool CanApply
        {
            get
            {
                return !disposed && !applied && owner.OwnsStorageChange(before, beforeItems, revision);
            }
        }

        /// <summary>Publishes prepared managed storage and row references without callbacks.</summary>
        public void ApplySilently()
        {
            if (!CanApply)
            {
                throw new InvalidOperationException("Inventory quantity transfer is stale");
            }

            owner.SwapStorageChange(after, afterItems, revision + 1);
            applied = true;
        }

        /// <summary>Restores the exact prior row/ledger references before any observer receives the transfer.</summary>
        public void RollbackSilently()
        {
            if (!applied || published || !owner.OwnsStorageChange(after, afterItems, revision + 1))
            {
                throw new InvalidOperationException("Inventory quantity rollback cannot prove ownership");
            }

            owner.SwapStorageChange(before, beforeItems, revision);
            applied = false;
        }

        /// <summary>Notifies the accessible bag only after every participant has committed.</summary>
        public void Publish()
        {
            if (disposed || !applied || published)
            {
                throw new InvalidOperationException("Inventory quantity transfer is not publishable");
            }

            published = true;
            owner.PublishStorageChange(observed, after);
        }

        /// <summary>Releases this permission token; applied participants require explicit rollback before publication.</summary>
        public void Dispose()
        {
            disposed = true;
        }
    }
}
