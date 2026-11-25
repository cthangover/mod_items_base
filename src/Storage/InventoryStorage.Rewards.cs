namespace Cthangover.Core.Items
{
    using System;
    using System.Text.Json.Nodes;

    public sealed partial class InventoryStorage
    {
        /// <summary>Commits the bag portion and the ground remainder as one reward, with one origin and retry receipt.</summary>
        public bool TryGrantReward(string operation, string itemId, int count, InventoryItemSize size,
            InventoryGroundPosition position, out int dropped, out string reason)
        {
            dropped = 0;
            if (string.IsNullOrWhiteSpace(itemId) || count <= 0 || size == null)
            {
                reason = "inventory/storage/invalid";
                return false;
            }

            try
            {
                size.Validate();
                position?.Validate();
            }
            catch (ArgumentException)
            {
                reason = "inventory/storage/invalid";
                return false;
            }

            var request = new { Kind = "reward", Item = itemId, Count = count, Size = size, Position = position };
            if (!Begin(operation, request, out var next, out var existing, out reason)) return existing != null;
            var origin = "stock/" + Guid.NewGuid().ToString("N");
            var finite = next.Grids["player"].Width > 0;
            var capacity = int.MaxValue - Count(itemId);
            if (!finite && count > capacity)
            {
                reason = "inventory/storage/overflow";
                return false;
            }

            var fits = Math.Min(count, capacity);
            var remainder = fits;
            if (fits > 0 && !AddToBag(next, itemId, ref remainder, size, new JsonObject(), origin, finite, out reason)
                && (!finite || reason != "inventory/storage/no_space")) return false;

            remainder += count - fits;
            if (remainder > 0)
            {
                if (position == null)
                {
                    reason = "inventory/storage/no_space";
                    return false;
                }
                next.GroundRewards.Add(origin, new InventoryGroundReward
                {
                    Id = origin, ItemId = itemId, Count = remainder, Position = Copy(position)
                });
            }

            var receipt = Receipt(operation, request, origin);
            receipt.CreatedOrigins.Add(origin);
            if (!Commit(next, receipt, out reason)) return false;
            dropped = remainder;
            return true;
        }

        /// <summary>Takes only what fits from the addressed pile, which retains its identity and any remainder.</summary>
        public bool TryTakeGroundReward(string operation, string id, InventoryItemSize size, string domain,
            string location, out string reason)
        {
            var request = new { Kind = "pick_ground_reward", Id = id, Size = size, Domain = domain, Location = location };
            if (!Begin(operation, request, out var next, out var existing, out reason)) return existing != null;
            if (id == null || size == null || !next.GroundRewards.TryGetValue(id, out var pile)
                || pile.Position.Domain != domain || pile.Position.Location != location)
            {
                reason = "inventory/instances/unavailable";
                return false;
            }

            try
            {
                size.Validate();
            }
            catch (ArgumentException)
            {
                reason = "inventory/storage/invalid";
                return false;
            }
            var requested = Math.Min(pile.Count, int.MaxValue - Count(pile.ItemId));
            var remainder = requested;
            if (requested == 0 || (!AddToBag(next, pile.ItemId, ref remainder, size, new JsonObject(), id, true, out reason)
                && reason != "inventory/storage/no_space"))
            {
                reason ??= "inventory/storage/no_space";
                return false;
            }

            if (requested == remainder)
            {
                reason = "inventory/storage/no_space";
                return false;
            }

            pile.Count -= requested - remainder;
            if (pile.Count == 0) next.GroundRewards.Remove(id);
            return Commit(next, Receipt(operation, request, id), out reason);
        }
    }
}
