namespace Cthangover.Core.Items
{
    using System;

    /// <summary>Location-local position of a reward that remained at the player's feet.</summary>
    public sealed class InventoryGroundPosition
    {
        public string Domain { get; set; }
        public string Location { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        internal void Validate()
        {
            InventoryInstanceData.CheckIdentity(Domain);
            InventoryInstanceData.CheckIdentity(Location);
            if (!float.IsFinite(X) || !float.IsFinite(Y) || !float.IsFinite(Z))
                throw new ArgumentException("Invalid ground reward coordinates");
        }
    }
}
