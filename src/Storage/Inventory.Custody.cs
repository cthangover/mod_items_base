namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;

    public partial class Inventory
    {
        /// <summary>Reads durable movement proof without requiring a formerly transferred group to remain in the bag.</summary>
        public bool TryGetStorageReceipt(string operation, out InventoryStorageReceipt receipt)
        {
            receipt = null;
            if (operation == null || !storage.Receipts.TryGetValue(operation, out var value))
            {
                return false;
            }

            receipt = InventoryStorage.Copy(value);
            return true;
        }

        /// <summary>
        /// Prepares a domain-coordinated quantity transfer. This does not mutate live rows or notify observers;
        /// the counterpart must validate, swap both owners, then publish. Replays are read through the receipt API.
        /// </summary>
        public bool TryPrepareCustodyTransfer(string operation, string id, int count, string domain, string reference,
            string contract, bool pickup, long expectedRevision, out InventoryStorageChange change, out string reason)
        {
            change = null;
            reason = "inventory/instances/stale_revision";
            if (!StorageAvailable || expectedRevision != mutationRevision || mutationRevision == long.MaxValue)
            {
                return false;
            }

            var observed = CaptureStorage();
            if (observed.Receipts.ContainsKey(operation ?? ""))
            {
                reason = "inventory/storage/operation_conflict";
                return false;
            }

            var store = new InventoryStorage(observed);
            if (!store.TryTransferCustody(operation, id, count, domain, reference, contract, pickup, out var receipt, out reason))
            {
                return false;
            }

            var next = store.Capture();
            var nextItems = BuildStorageRows(next);
            if (!StorageAvailable || expectedRevision != mutationRevision)
            {
                reason = "inventory/instances/stale_revision";
                return false;
            }

            change = new InventoryStorageChange(this, storage, observed, next, items, nextItems, mutationRevision, receipt);
            return true;
        }

        internal bool OwnsStorageChange(InventoryStorageState expected, List<IItemContainer> expectedItems, long revision)
        {
            return ReferenceEquals(storage, expected) && ReferenceEquals(items, expectedItems) && mutationRevision == revision;
        }

        internal void SwapStorageChange(InventoryStorageState next, List<IItemContainer> nextItems, long revision)
        {
            storage = next;
            items = nextItems;
            mutationRevision = revision;
        }

        internal void PublishStorageChange(InventoryStorageState before, InventoryStorageState after)
        {
            NotifyStorageChanges(before, after, null);
        }
    }
}
