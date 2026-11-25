namespace Cthangover.Core.Items
{
    using System;

    /// <summary>Unclaimed quantity at a particular world position, sharing its reward's original stock identity.</summary>
    public sealed class InventoryGroundReward
    {
        public string Id { get; set; }
        public string ItemId { get; set; }
        public int Count { get; set; }
        public InventoryGroundPosition Position { get; set; }

        internal void Validate()
        {
            InventoryInstanceData.CheckIdentity(Id);
            InventoryInstanceData.CheckIdentity(ItemId);
            if (Count <= 0 || Position == null) throw new ArgumentException("Invalid ground reward");
            Position.Validate();
        }
    }
}
