namespace Cthangover.Core.UI.Inventory
{
    using System;
    using Cthangover.Core.Items;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI.Theming;
    using Godot;
    using StorageInventory = Cthangover.Core.Items.Inventory;

    /// <summary>
    /// The bag's cell surface keeps filtered items at their real coordinates and validates drag placement against every hidden
    /// occupant.
    /// </summary>
    public partial class InventoryGridSurface : Control
    {
        private int columns;
        private int rows;
        private float step;
        /// <summary>Sets the visible grid geometry; zero dimensions restore the legacy list presentation.</summary>
        public void Configure(int width, int height, float cellStep)
        {
            columns = width;
            rows = height;
            step = cellStep;
            QueueRedraw();
        }

        /// <summary>Draws the actual cell lattice behind the positioned item rectangles.</summary>
        public override void _Draw()
        {
            if (columns == 0 || rows == 0)
            {
                return;
            }

            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    DrawRect(new Rect2(x * step, y * step, step - 2, step - 2), UiTokens.Surface2);
                    DrawRect(new Rect2(x * step, y * step, step - 2, step - 2), UiTokens.LineSoft, false);
                }
            }
        }

        /// <summary>
        /// Checks owner generation, exact group, every occupied cell and the requested rotation without changing storage.
        /// </summary>
        public override bool _CanDropData(Vector2 atPosition, Variant data)
        {
            if (!Drag(data, out var inventory, out var id, out var rotated))
            {
                return false;
            }

            var store = new InventoryStorage(inventory.CaptureStorage());
            var x = Mathf.FloorToInt(atPosition.X / step);
            var y = Mathf.FloorToInt(atPosition.Y / step);
            if (store.TryMove("preview/" + Guid.NewGuid().ToString("N"), id, "player", x, y, rotated, out _, out _))
            {
                return true;
            }

            var target = Target(store.Capture(), id, x, y);
            return target != null && store.TryMerge("preview/" + Guid.NewGuid().ToString("N"), id, target, out _, out _);
        }

        /// <summary>Commits only a still-current drag; a failed drop keeps the previous placement intact.</summary>
        public override void _DropData(Vector2 atPosition, Variant data)
        {
            if (!Drag(data, out var inventory, out var id, out var rotated))
            {
                return;
            }

            var x = Mathf.FloorToInt(atPosition.X / step);
            var y = Mathf.FloorToInt(atPosition.Y / step);
            if (!inventory.TryMoveStack("drag/" + Guid.NewGuid().ToString("N"), id, "player", x, y, rotated, out _))
            {
                var target = Target(inventory.CaptureStorage(), id, x, y);
                if (target != null)
                {
                    inventory.TryMergeStacks("merge/" + Guid.NewGuid().ToString("N"), id, target, out _);
                }
            }
        }

        private static string Target(InventoryStorageState state, string source, int x, int y)
        {
            foreach (var row in state.Stacks)
            {
                if (row.Id != source && row.Container == "player" && x >= row.X && x < row.X + row.OccupiedWidth && y >= row.Y
                    && y < row.Y + row.OccupiedHeight)
                {
                    return row.Id;
                }
            }

            return null;
        }

        private bool Drag(Variant data, out StorageInventory inventory, out string id, out bool rotated)
        {
            inventory = GameData.Instance?.Runtime?.Inventory as StorageInventory;
            id = null;
            rotated = false;
            if (columns == 0 || step <= 0 || inventory == null || data.VariantType != Variant.Type.Dictionary)
            {
                return false;
            }

            var payload = data.AsGodotDictionary();
            if (!payload.ContainsKey("inventory_stack") || !payload.ContainsKey("inventory_revision")
                || !payload.ContainsKey("inventory_generation") || payload["inventory_revision"].AsInt64() != inventory.Revision
                || payload["inventory_generation"].AsString() != inventory.StorageGeneration.ToString("N"))
            {
                return false;
            }

            id = payload["inventory_stack"].AsString();
            var row = inventory.Stack(id);
            if (row == null || row.Container != "player")
            {
                return false;
            }

            rotated = Input.IsPhysicalKeyPressed(Key.R) ? !row.Rotated : row.Rotated;
            return true;
        }
    }
}
