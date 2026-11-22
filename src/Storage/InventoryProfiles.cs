namespace Cthangover.Core.Items
{
    using System;
    using Cthangover.Core.Games;
    using Cthangover.Core.Mods.Resolvers;

    /// <summary>
    /// Resolves profiles from the currently mounted mod set on each request. No static game choice survives a game switch.
    /// Saved compartments remain authoritative on load; new games read the collection's inventory.profile setting.
    /// </summary>
    public static class InventoryProfiles
    {
        /// <summary>Runtime factory used only when a fresh game inventory is requested.</summary>
        public static Inventory Create()
        {
            var inventory = new Inventory();
            var profile = ActiveGame.GetSetting("inventory.profile");
            if (!string.IsNullOrWhiteSpace(profile) && !TryApply(inventory, "initial-profile", profile, out var reason))
            {
                throw new InvalidOperationException("Inventory profile '" + profile + "' refused: " + reason);
            }

            return inventory;
        }

        /// <summary>Changes an existing bag atomically, retaining identities and rejecting any capacity loss.</summary>
        public static bool TryApply(Inventory inventory, string operation, string profile, out string reason,
            string container = "player")
        {
            reason = "inventory/storage/profile_missing";
            var definitions = Entities.Collect<InventoryProfileInfo>(required: false);
            if (inventory == null || profile == null || !definitions.TryGetValue(profile, out var definition))
            {
                return false;
            }

            try
            {
                return inventory.TryConfigureGrid(operation, definition.ToGrid(container), out reason, true);
            }
            catch (ArgumentException)
            {
                reason = "inventory/storage/invalid";
                return false;
            }
        }
    }
}
