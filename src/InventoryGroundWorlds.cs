namespace Cthangover.Core.Items
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Mods.Consumers;
    using Godot;

    /// <summary>Connects a world representation to the common saved reward ledger.</summary>
    public interface IInventoryGroundWorld
    {
        /// <summary>Stable namespace for saved positions.</summary>
        string Domain { get; }

        /// <summary>Finds the player's ground position at the current or battle return scene.</summary>
        bool TryCapture(string scene, out InventoryGroundPosition point);

        /// <summary>Projects a saved point into viewport pixels and checks whether it is within reach.</summary>
        bool TryProject(InventoryGroundPosition point, out Vector2 screen, out bool inReach);

        /// <summary>Plays presentation for a newly committed drop. Restored piles never replay it.</summary>
        void PresentDrop(InventoryGroundPosition point, IItem item) { }
    }

    /// <summary>Resolves coordinates for worlds with a player avatar, with a scene-local fallback for visual novels.</summary>
    public sealed class InventoryGroundWorlds : ModTypeConsumer<IInventoryGroundWorld>
    {
        internal const string SceneDomain = "scene";
        private static readonly Dictionary<string, IInventoryGroundWorld> worlds = new(StringComparer.Ordinal);

        public override string Id => "inventory_ground_worlds";

        protected override void Add(IInventoryGroundWorld instance, string modId) => worlds.Add(instance.Domain, instance);

        protected override void Remove(IInventoryGroundWorld instance, string modId) => worlds.Remove(instance.Domain);

        internal static InventoryGroundPosition Capture(string scene)
        {
            if (string.IsNullOrWhiteSpace(scene)) return null;
            foreach (var world in worlds.Values)
                if (world.TryCapture(scene, out var point)) return point;

            return new InventoryGroundPosition { Domain = SceneDomain, Location = scene };
        }

        internal static bool TryProject(InventoryGroundPosition point, out Vector2 screen, out bool inReach)
        {
            screen = default;
            inReach = false;
            return worlds.TryGetValue(point.Domain, out var world) && world.TryProject(point, out screen, out inReach);
        }

        internal static void PresentDrop(InventoryGroundPosition point, IItem item)
        {
            if (worlds.TryGetValue(point.Domain, out var world))
                world.PresentDrop(point, item);
        }
    }
}
