namespace Cthangover.Core.Items
{
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Consumers;

    /// <summary>
    /// Lifecycle-owned adapters let the common inventory consume domain occurrences without a dependency on the 3D world.
    /// </summary>
    public sealed class InventoryConsumptionOwners : ModTypeConsumer<IInventoryInstanceConsumptionOwner>
    {
        private static readonly List<IInventoryInstanceConsumptionOwner> owners = new();
        /// <summary>Lifecycle discovery key for occurrence-consumption providers.</summary>
        public override string Id
        {
            get
            {
                return "inventory_consumption_owners";
            }
        }

        protected override void Add(IInventoryInstanceConsumptionOwner value, string mod)
        {
            owners.Add(value);
        }

        protected override void Remove(IInventoryInstanceConsumptionOwner value, string mod)
        {
            owners.Remove(value);
        }

        /// <summary>Requires one unambiguous live provider. Registration is rechecked after its preparation callback.</summary>
        public static bool TryPrepare(Inventory inventory, string operation, string request, List<InventoryInstanceData> instances,
            out IInventoryStorageParticipant participant, out string reason)
        {
            participant = null;
            reason = "inventory/instances/unavailable";
            if (instances.Count == 0)
            {
                return false;
            }

            IInventoryInstanceConsumptionOwner selected = null;
            foreach (var owner in new List<IInventoryInstanceConsumptionOwner>(owners))
            {
                if (owner.Owner == instances[0].Owner)
                {
                    if (selected != null)
                    {
                        reason = "inventory/instances/ambiguous_owner";
                        return false;
                    }

                    selected = owner;
                }
            }

            if (selected == null || !selected.TryPrepare(inventory, operation, request, instances, out participant, out reason))
            {
                return false;
            }

            if (!owners.Contains(selected))
            {
                participant?.Dispose();
                participant = null;
                reason = "inventory/instances/unavailable";
                return false;
            }

            return participant != null;
        }
    }
}
