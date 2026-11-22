namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Text.Json.Nodes;

    /// <summary>
    /// Detached storage arithmetic used by inventory, crafting, trading and equipment. Every operation builds
    /// and validates a candidate before publication, so capacity, overlap or quantity failure changes nothing.
    /// </summary>
    public sealed class InventoryStorage
    {
        private InventoryStorageState state;
        /// <summary>Copies and validates a save candidate; callers retain no mutable references to its ledger.</summary>
        public InventoryStorage(InventoryStorageState value)
        {
            state = Copy(value ?? throw new ArgumentNullException(nameof(value)));
            state.Validate();
        }

        /// <summary>Returns detached current rows, compartment sizes, origin quantities and retry evidence.</summary>
        public InventoryStorageState Capture()
        {
            return Copy(state);
        }

        /// <summary>Total accessible stock of one blueprint, summed over distinct groups in the selected compartment.</summary>
        public int Count(string itemId, string container = "player")
        {
            long total = 0;
            foreach (var row in state.Stacks)
            {
                if (row.ItemId == itemId && row.Container == container)
                {
                    total += row.Count;
                }
            }

            return checked((int)total);
        }

        /// <summary>Adds explicitly generated stock, grouping only compatible properties and retaining each genesis quantity.</summary>
        public bool TryAdd(string operation, string itemId, int count, InventoryItemSize size, JsonObject properties,
            out InventoryStorageReceipt receipt, out string reason)
        {
            var request = new JsonObject
            {
                ["Kind"] = JsonSerializer.SerializeToNode("add"),
                ["Item"] = JsonSerializer.SerializeToNode(itemId),
                ["Count"] = JsonSerializer.SerializeToNode(count),
                ["Size"] = JsonSerializer.SerializeToNode(size),
                ["Properties"] = JsonSerializer.SerializeToNode(properties)
            };
            if (!Begin(operation, request, out var next, out receipt, out reason))
            {
                return receipt != null;
            }

            if (string.IsNullOrWhiteSpace(itemId) || count < 1 || size == null)
            {
                reason = "inventory/storage/invalid";
                return false;
            }

            try
            {
                size.Validate();
                if ((long)Count(itemId) + count > int.MaxValue)
                {
                    reason = "inventory/storage/overflow";
                    return false;
                }

                var origin = "stock/" + Guid.NewGuid().ToString("N");
                if (!Add(next, itemId, count, size, properties ?? new JsonObject(), origin, out reason))
                {
                    return false;
                }

                receipt = Receipt(operation, request, origin);
                receipt.CreatedOrigins.Add(origin);
                return Commit(next, receipt, out reason);
            }
            catch (ArgumentException)
            {
                reason = "inventory/storage/invalid";
                return false;
            }
        }

        /// <summary>
        /// Consumes an entire recipe/deal and places every product as one candidate. Exact-row selection is
        /// optional; removed rows retain provenance for downstream owners and permanent consumption history.
        /// </summary>
        public bool TryExchange(string operation, Dictionary<string, int> inputs, List<InventoryStackData> outputs, string selected,
            out InventoryStorageReceipt receipt, out string reason)
        {
            var request = new JsonObject
            {
                ["Kind"] = JsonSerializer.SerializeToNode("exchange"),
                ["Inputs"] = JsonSerializer.SerializeToNode(inputs),
                ["Outputs"] = JsonSerializer.SerializeToNode(outputs),
                ["Selected"] = JsonSerializer.SerializeToNode(selected)
            };
            if (!Begin(operation, request, out var next, out receipt, out reason))
            {
                return receipt != null;
            }

            receipt = Receipt(operation, request, null);
            if (inputs == null || outputs == null)
            {
                reason = "inventory/storage/invalid";
                receipt = null;
                return false;
            }

            foreach (var pair in inputs)
            {
                if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value < 1 || !Remove(next, pair.Key, pair.Value, selected, receipt.Removed))
                {
                    reason = "inventory/storage/insufficient";
                    receipt = null;
                    return false;
                }
            }

            try
            {
                foreach (var output in outputs)
                {
                    if (output == null || output.Size == null || output.Count < 1 || output.ExternalOwner != null)
                    {
                        reason = "inventory/storage/invalid";
                        receipt = null;
                        return false;
                    }

                    output.Size.Validate();
                    var origin = "stock/" + Guid.NewGuid().ToString("N");
                    if (!Add(next, output.ItemId, output.Count, output.Size, output.Properties ?? new JsonObject(), origin, out reason))
                    {
                        receipt = null;
                        return false;
                    }

                    receipt.CreatedOrigins.Add(origin);
                }

                if (!Commit(next, receipt, out reason))
                {
                    receipt = null;
                    return false;
                }

                return true;
            }
            catch (ArgumentException)
            {
                reason = "inventory/storage/invalid";
                receipt = null;
                return false;
            }
        }

        /// <summary>Moves or rotates one whole group; a refused placement leaves its original cells occupied.</summary>
        public bool TryMove(string operation, string id, string container, int x, int y, bool rotated,
            out InventoryStorageReceipt receipt, out string reason)
        {
            var request = new JsonObject
            {
                ["Kind"] = JsonSerializer.SerializeToNode("move"),
                ["Id"] = JsonSerializer.SerializeToNode(id),
                ["Container"] = JsonSerializer.SerializeToNode(container),
                ["X"] = JsonSerializer.SerializeToNode(x),
                ["Y"] = JsonSerializer.SerializeToNode(y),
                ["Rotated"] = JsonSerializer.SerializeToNode(rotated)
            };
            if (!Begin(operation, request, out var next, out receipt, out reason))
            {
                return receipt != null;
            }

            var row = Find(next, id);
            if (row == null || container == null || !next.Grids.TryGetValue(container, out var grid))
            {
                reason = "inventory/storage/missing";
                return false;
            }

            row.Container = container;
            row.X = x;
            row.Y = y;
            row.Rotated = rotated;
            if (x < 0 || y < 0 || rotated && !row.Size.Rotatable || !Fits(next, row, grid, id))
            {
                reason = "inventory/storage/no_space";
                return false;
            }

            receipt = Receipt(operation, request, id);
            return Commit(next, receipt, out reason);
        }

        /// <summary>Changes compartment dimensions without repacking other items or dropping overflow.</summary>
        public bool TryConfigure(string operation, InventoryGridData grid, out InventoryStorageReceipt receipt, out string reason,
            bool repack = false)
        {
            var request = new JsonObject
            {
                ["Kind"] = JsonSerializer.SerializeToNode("configure"),
                ["Grid"] = JsonSerializer.SerializeToNode(grid),
                ["Repack"] = JsonSerializer.SerializeToNode(repack)
            };
            if (!Begin(operation, request, out var next, out receipt, out reason))
            {
                return receipt != null;
            }

            try
            {
                grid?.Validate();
                if (grid == null)
                {
                    throw new ArgumentException("Missing inventory compartment");
                }

                next.Grids[grid.Id] = Copy(grid);
                if (repack)
                {
                    var moving = new List<InventoryStackData>();
                    foreach (var row in next.Stacks)
                    {
                        if (row.Container == grid.Id)
                        {
                            moving.Add(row);
                        }
                    }

                    foreach (var row in moving)
                    {
                        next.Stacks.Remove(row);
                    }

                    foreach (var row in moving)
                    {
                        if (!Place(next, row))
                        {
                            reason = "inventory/storage/no_space";
                            receipt = null;
                            return false;
                        }

                        next.Stacks.Add(row);
                    }
                }

                receipt = Receipt(operation, request, grid.Id);
                return Commit(next, receipt, out reason);
            }
            catch (ArgumentException)
            {
                receipt = null;
                reason = "inventory/storage/invalid";
                return false;
            }
        }

        /// <summary>Splits a proper positive portion into a fresh child group with exactly conserved origins.</summary>
        public bool TrySplit(string operation, string id, int count, string container, int x, int y, bool rotated,
            out InventoryStorageReceipt receipt, out string reason)
        {
            var request = new JsonObject
            {
                ["Kind"] = JsonSerializer.SerializeToNode("split"),
                ["Id"] = JsonSerializer.SerializeToNode(id),
                ["Count"] = JsonSerializer.SerializeToNode(count),
                ["Container"] = JsonSerializer.SerializeToNode(container),
                ["X"] = JsonSerializer.SerializeToNode(x),
                ["Y"] = JsonSerializer.SerializeToNode(y),
                ["Rotated"] = JsonSerializer.SerializeToNode(rotated)
            };
            if (!Begin(operation, request, out var next, out receipt, out reason))
            {
                return receipt != null;
            }

            var row = Find(next, id);
            if (row == null || count < 1 || count >= row.Count || row.ExternalOwner != null)
            {
                reason = "inventory/storage/invalid";
                return false;
            }

            var child = Portion(row, count);
            child.Id = "stack/" + Guid.NewGuid().ToString("N");
            child.Parent = row.Id;
            child.Container = container;
            child.X = x;
            child.Y = y;
            child.Rotated = rotated;
            if (x == -1 && y == -1 && !Place(next, child))
            {
                reason = "inventory/storage/no_space";
                return false;
            }

            next.Stacks.Add(child);
            receipt = Receipt(operation, request, child.Id);
            return Commit(next, receipt, out reason);
        }

        /// <summary>Merges two compatible groups; a full target, differing per-item properties or overlap refuses atomically.</summary>
        public bool TryMerge(string operation, string source, string target, out InventoryStorageReceipt receipt, out string reason)
        {
            var request = new JsonObject
            {
                ["Kind"] = JsonSerializer.SerializeToNode("merge"),
                ["Source"] = JsonSerializer.SerializeToNode(source),
                ["Target"] = JsonSerializer.SerializeToNode(target)
            };
            if (!Begin(operation, request, out var next, out receipt, out reason))
            {
                return receipt != null;
            }

            var from = Find(next, source);
            var into = Find(next, target);
            if (from == null || into == null || source == target || !Compatible(from, into) || (long)from.Count
                + into.Count > into.Size.StackLimit)
            {
                reason = "inventory/storage/incompatible";
                return false;
            }

            MergeOrigins(into, from);
            next.Stacks.Remove(from);
            receipt = Receipt(operation, request, target);
            return Commit(next, receipt, out reason);
        }

        /// <summary>
        /// Moves one selected unit into an equipment compartment and returns the replaced unit only if it fits in the bag.
        /// </summary>
        public bool TryEquip(string operation, string id, InventoryGridData compartment, InventoryStackData legacyReturn,
            out InventoryStorageReceipt receipt, out string reason, InventoryStackData generated = null)
        {
            var request = new JsonObject
            {
                ["Kind"] = JsonSerializer.SerializeToNode("equip"),
                ["Id"] = JsonSerializer.SerializeToNode(id),
                ["Compartment"] = JsonSerializer.SerializeToNode(compartment),
                ["Legacy"] = JsonSerializer.SerializeToNode(legacyReturn)
            };
            request["Generated"] = JsonSerializer.SerializeToNode(generated);
            if (!Begin(operation, request, out var next, out receipt, out reason))
            {
                return receipt != null;
            }

            var source = Find(next, id);
            if (source == null && id == null && generated != null && generated.ExternalOwner == null && generated.Count == 1)
            {
                source = Copy(generated);
                source.Id = "stack/" + Guid.NewGuid().ToString("N");
                source.Origins.Clear();
                source.Origins["stock/" + Guid.NewGuid().ToString("N")] = 1;
                try
                {
                    source.Validate();
                }
                catch (ArgumentException)
                {
                    reason = "inventory/storage/invalid";
                    return false;
                }

                next.Stacks.Add(source);
            }

            if (source == null || source.Container != "player" || compartment?.EquipmentCharacter == null)
            {
                reason = "inventory/storage/invalid";
                return false;
            }

            try
            {
                compartment.Validate();
            }
            catch (ArgumentException)
            {
                reason = "inventory/storage/invalid";
                return false;
            }

            next.Grids[compartment.Id] = Copy(compartment);
            var returning = new List<InventoryStackData>();
            foreach (var row in next.Stacks)
            {
                if (row.Container == compartment.Id)
                {
                    returning.Add(row);
                }
            }

            var unit = Portion(source, 1);
            if (source.Count == 0)
            {
                next.Stacks.Remove(source);
            }
            else
            {
                unit.Id = "stack/" + Guid.NewGuid().ToString("N");
                unit.Parent = source.Id;
            }

            unit.Container = compartment.Id;
            unit.X = unit.Y = 0;
            unit.Rotated = false;
            next.Stacks.Add(unit);
            foreach (var row in returning)
            {
                row.Container = "player";
                if (!Place(next, row))
                {
                    reason = "inventory/storage/no_space";
                    return false;
                }
            }

            if (legacyReturn != null && !Add(next, legacyReturn.ItemId, 1, legacyReturn.Size, legacyReturn.Properties, "stock/"
                + Guid.NewGuid().ToString("N"), out reason))
            {
                return false;
            }

            receipt = Receipt(operation, request, unit.Id);
            return Commit(next, receipt, out reason);
        }

        /// <summary>Finds space for one whole group in another container without merging or changing its identity.</summary>
        public bool TryMoveAuto(string operation, string id, string container, out InventoryStorageReceipt receipt, out string reason)
        {
            var request = new JsonObject
            {
                ["Kind"] = JsonSerializer.SerializeToNode("move_auto"),
                ["Id"] = JsonSerializer.SerializeToNode(id),
                ["Container"] = JsonSerializer.SerializeToNode(container)
            };
            if (!Begin(operation, request, out var next, out receipt, out reason))
            {
                return receipt != null;
            }

            var row = Find(next, id);
            if (row == null)
            {
                reason = "inventory/storage/missing";
                return false;
            }

            row.Container = container;
            if (!Place(next, row))
            {
                reason = "inventory/storage/no_space";
                return false;
            }

            receipt = Receipt(operation, request, id);
            return Commit(next, receipt, out reason);
        }

        /// <summary>Tests exact rectangular occupancy. Unbounded legacy containers do not impose geometry constraints.</summary>
        public static bool Fits(InventoryStorageState data, InventoryStackData row, InventoryGridData grid, string ignore)
        {
            if (row.X < 0 || row.Y < 0)
            {
                return false;
            }

            if (grid.Width == 0)
            {
                return true;
            }

            if ((long)row.X + row.OccupiedWidth > grid.Width || (long)row.Y + row.OccupiedHeight > grid.Height)
            {
                return false;
            }

            foreach (var other in data.Stacks)
            {
                if (other.Id != ignore && other.Container == row.Container && row.X < (long)other.X + other.OccupiedWidth
                    && other.X < (long)row.X + row.OccupiedWidth && row.Y < (long)other.Y + other.OccupiedHeight
                    && other.Y < (long)row.Y + row.OccupiedHeight)
                {
                    return false;
                }
            }

            return true;
        }

        private bool Begin(string operation, object request, out InventoryStorageState next, out InventoryStorageReceipt receipt,
            out string reason)
        {
            next = null;
            receipt = null;
            reason = "inventory/storage/invalid";
            if (string.IsNullOrWhiteSpace(operation) || operation.Length > 1024)
            {
                return false;
            }

            if (state.Receipts.TryGetValue(operation, out var existing))
            {
                if (existing.Request != Digest(request))
                {
                    reason = "inventory/storage/operation_conflict";
                    return false;
                }

                receipt = Copy(existing);
                reason = null;
                return false;
            }

            if (state.Receipts.Count >= 16384)
            {
                reason = "inventory/storage/budget";
                return false;
            }

            next = Copy(state);
            reason = null;
            return true;
        }

        private bool Commit(InventoryStorageState next, InventoryStorageReceipt receipt, out string reason)
        {
            try
            {
                next.Receipts.Add(receipt.Operation, receipt);
                next.Validate();
                var counts = new Dictionary<string, long>(StringComparer.Ordinal);
                foreach (var row in next.Stacks)
                {
                    var key = row.Container + "\n" + row.ItemId;
                    counts.TryGetValue(key, out var total);
                    total += row.Count;
                    if (total > int.MaxValue)
                    {
                        reason = "inventory/storage/overflow";
                        return false;
                    }

                    counts[key] = total;
                }

                state = next;
                reason = null;
                return true;
            }
            catch (ArgumentException)
            {
                reason = "inventory/storage/no_space";
                return false;
            }
        }

        private static bool Add(InventoryStorageState next, string itemId, int count, InventoryItemSize size, JsonObject properties,
            string origin, out string reason)
        {
            var prototype = new InventoryStackData
            {
                ItemId = itemId,
                Size = Copy(size),
                Properties = properties.DeepClone().AsObject()
            };
            foreach (var row in next.Stacks)
            {
                if (row.Container != "player" || !Compatible(row, prototype))
                {
                    continue;
                }

                var amount = Math.Min(count, size.StackLimit - row.Count);
                if (amount == 0)
                {
                    continue;
                }

                var candidate = Copy(row);
                candidate.Count += amount;
                if (!Fits(next, candidate, next.Grids[row.Container], row.Id))
                {
                    continue;
                }

                row.Count += amount;
                row.Origins.TryGetValue(origin, out var previous);
                row.Origins[origin] = previous + amount;
                count -= amount;
                if (count == 0)
                {
                    reason = null;
                    return true;
                }
            }

            if ((count + (long)size.StackLimit - 1) / size.StackLimit > 16384 - next.Stacks.Count)
            {
                reason = "inventory/storage/budget";
                return false;
            }

            while (count > 0)
            {
                var row = Copy(prototype);
                row.Id = "stack/" + Guid.NewGuid().ToString("N");
                row.Count = Math.Min(count, size.StackLimit);
                row.Origins[origin] = row.Count;
                if (!Place(next, row))
                {
                    reason = "inventory/storage/no_space";
                    return false;
                }

                next.Stacks.Add(row);
                count -= row.Count;
            }

            reason = null;
            return true;
        }

        internal static bool Place(InventoryStorageState next, InventoryStackData row)
        {
            if (!next.Grids.TryGetValue(row.Container, out var grid))
            {
                return false;
            }

            if (grid.Width == 0)
            {
                row.X = row.Y = 0;
                return true;
            }

            for (var rotation = 0; rotation < (row.Size.Rotatable ? 2 : 1); rotation++)
            {
                row.Rotated = rotation == 1;
                for (var y = 0; y < grid.Height; y++)
                {
                    for (var x = 0; x < grid.Width; x++)
                    {
                        row.X = x;
                        row.Y = y;
                        if (Fits(next, row, grid, row.Id))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool Remove(InventoryStorageState next, string item, int count, string selected, List<InventoryStackData> removed)
        {
            foreach (var row in new List<InventoryStackData>(next.Stacks))
            {
                if (row.ItemId != item || (selected == null ? row.Container != "player" : row.Id != selected))
                {
                    continue;
                }

                var amount = Math.Min(count, row.Count);
                removed.Add(Portion(row, amount));
                count -= amount;
                if (row.Count == 0)
                {
                    next.Stacks.Remove(row);
                }

                if (count == 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static InventoryStackData Portion(InventoryStackData row, int count)
        {
            var portion = Copy(row);
            portion.Count = count;
            portion.Origins.Clear();
            foreach (var pair in new List<KeyValuePair<string, int>>(row.Origins))
            {
                var amount = Math.Min(count, pair.Value);
                if (amount == 0)
                {
                    continue;
                }

                portion.Origins[pair.Key] = amount;
                if (amount == pair.Value)
                {
                    row.Origins.Remove(pair.Key);
                }
                else
                {
                    row.Origins[pair.Key] -= amount;
                }

                row.Count -= amount;
                count -= amount;
                if (count == 0)
                {
                    break;
                }
            }

            return portion;
        }

        private static bool Compatible(InventoryStackData left, InventoryStackData right)
        {
            return left.ExternalOwner == null && right.ExternalOwner == null && left.ItemId == right.ItemId
                && JsonSerializer.Serialize(left.Size) == JsonSerializer.Serialize(right.Size) && JsonNode.DeepEquals(left.Properties,
                right.Properties);
        }

        private static void MergeOrigins(InventoryStackData into, InventoryStackData from)
        {
            foreach (var pair in from.Origins)
            {
                into.Origins.TryGetValue(pair.Key, out var current);
                into.Origins[pair.Key] = checked(current + pair.Value);
            }

            into.Count = checked(into.Count + from.Count);
        }

        internal static InventoryStackData Find(InventoryStorageState next, string id)
        {
            return Find(next.Stacks, id);
        }

        internal static InventoryStackData Find(IEnumerable<InventoryStackData> rows, string id)
        {
            foreach (var row in rows)
            {
                if (row.Id == id)
                {
                    return row;
                }
            }

            return null;
        }

        private static InventoryStorageReceipt Receipt(string operation, object request, string result)
        {
            return new InventoryStorageReceipt
            {
                Operation = operation,
                Request = Digest(request),
                Result = result
            };
        }

        private static string Digest(object value)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
        }

        internal static T Copy<T>(T value)
        {
            return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
        }
    }
}
