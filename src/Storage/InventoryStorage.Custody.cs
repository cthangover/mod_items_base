namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Nodes;

    public sealed partial class InventoryStorage
    {
        /// <summary>
        /// Explicit domain transfer of an ordinary group. Drop can split a positive portion into reserved custody;
        /// pickup returns the whole external group, preserving origins through compatible bag merges or placement.
        /// Ordinary mutations cannot address this reserved compartment. The coordinator pairs the resulting receipt.
        /// </summary>
        public bool TryTransferCustody(string operation, string id, int count, string owner, string reference,
            string contract, bool pickup, out InventoryStorageReceipt receipt, out string reason)
        {
            var request = new JsonObject
            {
                ["Kind"] = "custody",
                ["Group"] = id,
                ["Count"] = count,
                ["Owner"] = owner,
                ["Reference"] = reference,
                ["Contract"] = contract,
                ["Pickup"] = pickup
            };
            if (!Begin(operation, request, out var next, out receipt, out reason))
            {
                return receipt != null;
            }

            receipt = null;
            try
            {
                InventoryInstanceData.CheckIdentity(owner);
                InventoryInstanceData.CheckIdentity(reference);
                var compartment = "custody/" + Uri.EscapeDataString(owner);
                var source = Find(next, id);
                if (source == null || source.ExternalOwner != null || count < 1 || count > source.Count
                    || contract?.Length != 64 || (!pickup && (source.Container != "player" || source.CustodyReference != null))
                    || pickup && (source.Container != compartment || source.CustodyReference != reference || count != source.Count))
                {
                    reason = "inventory/storage/custody";
                    return false;
                }

                var proof = new InventoryCustodyTransfer
                {
                    Owner = owner,
                    Reference = reference,
                    Contract = contract,
                    SourceGroup = id,
                    ItemId = source.ItemId,
                    From = source.Container,
                    To = pickup ? "player" : compartment,
                    Count = count
                };
                InventoryStackData moved;
                if (pickup || count == source.Count)
                {
                    moved = source;
                    next.Stacks.Remove(source);
                }
                else
                {
                    moved = Portion(source, count);
                    moved.Id = "stack/" + Guid.NewGuid().ToString("N");
                    moved.Parent = source.Id;
                    if (!Fits(next, source, next.Grids["player"], source.Id))
                    {
                        reason = "inventory/storage/no_space";
                        return false;
                    }
                }

                proof.Origins = new Dictionary<string, int>(moved.Origins, StringComparer.Ordinal);
                moved.Container = proof.To;
                moved.CustodyReference = pickup ? null : reference;
                if (pickup)
                {
                    if (!ReturnCustody(next, moved, proof, out reason))
                    {
                        return false;
                    }
                }
                else
                {
                    if (!next.Grids.TryGetValue(compartment, out var grid))
                    {
                        grid = new InventoryGridData
                        {
                            Id = compartment,
                            Layout = "list",
                            Custodian = owner
                        };
                        next.Grids.Add(compartment, grid);
                    }

                    if (grid.Custodian != owner)
                    {
                        reason = "inventory/storage/custody";
                        return false;
                    }

                    moved.X = moved.Y = 0;
                    next.Stacks.Add(moved);
                    proof.Destinations[moved.Id] = count;
                }

                proof.Validate();
                string result = null;
                foreach (var target in proof.Destinations.Keys)
                {
                    result = target;
                    break;
                }

                receipt = Receipt(operation, request, result);
                receipt.Custody = proof;
                return Commit(next, receipt, out reason);
            }
            catch (ArgumentException)
            {
                receipt = null;
                reason = "inventory/storage/invalid";
                return false;
            }
            catch (OverflowException)
            {
                receipt = null;
                reason = "inventory/storage/overflow";
                return false;
            }
        }

        private static bool ReturnCustody(InventoryStorageState next, InventoryStackData moved,
            InventoryCustodyTransfer proof, out string reason)
        {
            reason = null;
            foreach (var target in next.Stacks)
            {
                if (target.Container != "player" || !Compatible(target, moved))
                {
                    continue;
                }

                var count = Math.Min(moved.Count, target.Size.StackLimit - target.Count);
                if (count <= 0)
                {
                    continue;
                }

                var candidate = Copy(target);
                candidate.Count += count;
                if (!Fits(next, candidate, next.Grids["player"], target.Id))
                {
                    continue;
                }

                MergeOrigins(target, Portion(moved, count));
                proof.Destinations[target.Id] = count;
                if (moved.Count == 0)
                {
                    return true;
                }
            }

            if (!Place(next, moved))
            {
                reason = "inventory/storage/no_space";
                return false;
            }

            next.Stacks.Add(moved);
            proof.Destinations[moved.Id] = moved.Count;
            return true;
        }
    }
}
