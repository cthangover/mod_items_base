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
            var profile = ActiveGame.GetSetting("inventory.profile");
            if (string.IsNullOrWhiteSpace(profile))
            {
                return new Inventory();
            }

            var definitions = Entities.Collect<InventoryProfileInfo>(required: false);
            if (!definitions.TryGetValue(profile, out var definition))
            {
                throw new InvalidOperationException("Inventory profile '" + profile + "' refused: inventory/storage/profile_missing");
            }

            // Seed a detached owner before publication, including while a save dispatcher creates its fresh runtime.
            // Public mutations remain forbidden throughout that dispatch; saved storage replaces this initial policy.
            return new Inventory(definition.ToGrid());
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
