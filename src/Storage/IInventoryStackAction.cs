namespace Cthangover.Core.Items
{
    using Cthangover.Core.Settings;

    /// <summary>Identity-aware use actions own their transfer; the inventory panel must not consume a second item afterwards.</summary>
    public interface IInventoryStackAction
    {
        /// <summary>Transfers or uses the exact selected group and owns any resulting inventory mutation.</summary>
        bool UseStack(Inventory inventory, string stackId, CharacterInfoData target);
    }
}
