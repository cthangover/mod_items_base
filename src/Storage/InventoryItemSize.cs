namespace Cthangover.Core.Items
{
    using System;
    using System.Text.Json;

    /// <summary>
    /// Blueprint storage rules. A group may use different dimensions from one item; count never implies permission to stack.
    /// </summary>
    public sealed class InventoryItemSize
    {
        /// <summary>Single-item footprint measured in integer cells.</summary>
        public int Width { get; set; } = 1;
        /// <summary>Single-item footprint measured in integer cells.</summary>
        public int Height { get; set; } = 1;
        /// <summary>Grouped footprint width; zero inherits the single-item width.</summary>
        public int GroupWidth { get; set; }
        /// <summary>Grouped footprint height; zero inherits the single-item height.</summary>
        public int GroupHeight { get; set; }
        /// <summary>Maximum units per group; one prevents grouping.</summary>
        public int StackLimit { get; set; } = int.MaxValue;
        /// <summary>Permits a quarter-turn placement that exchanges the footprint axes.</summary>
        public bool Rotatable { get; set; }

        /// <summary>Numeric author-form equivalent of Rotatable: zero fixes orientation, one permits quarter-turn rotation.</summary>
        public int Rotation
        {
            get
            {
                return Rotatable ? 1 : 0;
            }

            set
            {
                if (value != 0 && value != 1)
                {
                    throw new ArgumentException("Inventory rotation must be zero or one");
                }

                Rotatable = value == 1;
            }
        }

        /// <summary>Rejects malformed rules instead of silently shrinking a footprint or expanding a group.</summary>
        public void Validate()
        {
            if (Width < 1 || Height < 1 || GroupWidth < 0 || GroupHeight < 0 || Width > 256 || Height > 256 || GroupWidth > 256
                || GroupHeight > 256 || StackLimit < 1)
            {
                throw new ArgumentException("Invalid inventory item dimensions or group limit");
            }
        }

        /// <summary>Reads the Inventory JSON extension on an item; old blueprints retain one-cell unlimited groups.</summary>
        public static InventoryItemSize From(IItem item)
        {
            var text = item?.Properties?.GetStr("Inventory");
            var result = string.IsNullOrWhiteSpace(text) ? new InventoryItemSize() : JsonSerializer.Deserialize<InventoryItemSize>(text);
            if (result == null)
            {
                throw new ArgumentException("Missing inventory item dimensions");
            }

            result.Validate();
            return result;
        }
    }
}
