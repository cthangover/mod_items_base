namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;
    using Cthangover.Core.Utils;

    public partial class Inventory
    {
        private bool PrepareConsumption(InventoryStorageReceipt receipt, out InventoryInstanceState next, out Dictionary<string,
            InventoryInstanceContainer> rows, List<IInventoryStorageParticipant> participants, out string reason)
        {
            next = InventoryStorage.Copy(instanceState);
            rows = new Dictionary<string, InventoryInstanceContainer>(instanceRows, StringComparer.Ordinal);
            var groups = new Dictionary<string, List<InventoryInstanceData>>(StringComparer.Ordinal);
            reason = null;
            foreach (var removed in receipt.Removed)
            {
                if (removed.ExternalOwner == null)
                {
                    continue;
                }

                if (!InstancesAvailable || !next.Records.TryGetValue(removed.Id, out var descriptor)
                    || descriptor.Owner != removed.ExternalOwner || descriptor.ItemId != removed.ItemId
                    || descriptor.Revision == long.MaxValue)
                {
                    reason = "inventory/instances/unavailable";
                    return false;
                }

                if (!groups.TryGetValue(descriptor.Owner, out var group))
                {
                    group = new List<InventoryInstanceData>();
                    groups.Add(descriptor.Owner, group);
                }

                group.Add(InventoryStorage.Copy(descriptor));
                next.Records.Remove(removed.Id);
                rows.Remove(removed.Id);
                var operation = receipt.Operation + "/" + descriptor.Id;
                next.Receipts.Add(operation, new InventoryInstanceReceipt
                {
                    Operation = operation,
                    Request = receipt.Request,
                    Instance = descriptor.Id,
                    ItemId = descriptor.ItemId,
                    Owner = descriptor.Owner,
                    Revision = descriptor.Revision + 1,
                    Inserted = false
                });
            }

            if (groups.Count > 0)
            {
                next.Revision = checked(next.Revision + 1);
                next.Validate();
            }

            foreach (var group in groups.Values)
            {
                if (!InventoryConsumptionOwners.TryPrepare(this, receipt.Operation, receipt.Request, group, out var participant,
                    out reason))
                {
                    return false;
                }

                participants.Add(participant);
            }

            return true;
        }

        private bool CommitExchange(InventoryStorageState nextStorage, InventoryStorageReceipt receipt, bool preview, out string reason)
        {
            reason = null;
            if (!StorageAvailable)
            {
                reason = "inventory/instances/unavailable";
                return false;
            }

            var revision = mutationRevision;
            var beforeStorage = CaptureStorage();
            var observed = JsonSerializer.Serialize(beforeStorage);
            var participants = new List<IInventoryStorageParticipant>();
            try
            {
                if (!PrepareConsumption(receipt, out var nextInstances, out var nextRows, participants, out reason))
                {
                    return false;
                }

                var nextItems = BuildStorageRows(nextStorage);
                foreach (var participant in participants)
                {
                    if (!participant.IsCurrent)
                    {
                        reason = "inventory/instances/stale_revision";
                        return false;
                    }
                }

                if (revision != mutationRevision || observed != JsonSerializer.Serialize(CaptureStorage()))
                {
                    reason = "inventory/instances/stale_revision";
                    return false;
                }

                foreach (var participant in participants)
                {
                    if (!participant.CanApply)
                    {
                        reason = "inventory/instances/stale_revision";
                        return false;
                    }
                }

                if (preview)
                {
                    return true;
                }

                var applied = 0;
                try
                {
                    foreach (var participant in participants)
                    {
                        participant.ApplySilently();
                        applied++;
                    }
                }
                catch
                {
                    for (var index = applied - 1; index >= 0; index--)
                    {
                        participants[index].RollbackSilently();
                    }

                    throw;
                }

                storage = nextStorage;
                instanceState = nextInstances;
                instanceRows = nextRows;
                items = nextItems;
                ApplyStoragePlacements(nextStorage);
                mutationRevision++;
                foreach (var participant in participants)
                {
                    try
                    {
                        participant.Publish();
                    }
                    catch (Exception error)
                    {
                        GameLogger.Log("INVENTORY", "Committed consumption observer failed: " + error.Message, LogLevel.Warning);
                    }
                }

                NotifyStorageChanges(beforeStorage, nextStorage, receipt.Removed.Count > 0 ? receipt.Removed[0].ItemId : null);
                return true;
            }
            finally
            {
                foreach (var participant in participants)
                {
                    participant.Dispose();
                }
            }
        }
    }
}
