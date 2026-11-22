namespace Cthangover.Core.Items
{
    using System.Text.Json.Serialization;
    using Cthangover.Core.Factories;

    /// <summary>
    /// Mod-authored bag policy collected by the ordinary entity resolver, including overrides, Extends and patches.
    /// </summary>
    [EntityKind("inventory_profile")]
    public sealed class InventoryProfileInfo : IIdentifiable
    {
        /// <summary>Selected by inventory.profile in a game's collection settings.</summary>
        [JsonPropertyName("Id")]
        public string ID { get; set; }

        /// <summary>One of grid, slots or list. Grouping and identity are shared by all three layouts.</summary>
        public string Layout { get; set; } = "slots";

        /// <summary>Finite cell columns, or zero together with Height for an unbounded bag.</summary>
        public int Width { get; set; }

        /// <summary>Finite cell rows. A list always has zero dimensions.</summary>
        public int Height { get; set; }

        /// <summary>Creates detached validated compartment data; cached entity definitions are never mutated.</summary>
        public InventoryGridData ToGrid(string container = "player")
        {
            InventoryInstanceData.CheckIdentity(ID);
            var result = new InventoryGridData
            {
                Id = container,
                Layout = Layout,
                Width = Width,
                Height = Height
            };
            result.Validate();
            return result;
        }
    }
}
