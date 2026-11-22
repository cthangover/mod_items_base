namespace Cthangover.Core.UI.Inventory
{
    using Cthangover.Core.Items;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Scenes;
    using Godot;
    using StorageInventory = Cthangover.Core.Items.Inventory;

    public partial class InventoryItemBehaviour
    {
        /// <summary>Starts a drag of this exact group with its owner and revision, never just a shared item ID.</summary>
        public override Variant _GetDragData(Vector2 atPosition)
        {
            if (GameData.Instance?.Runtime?.Inventory is not StorageInventory inventory)
            {
                return default;
            }

            var id = StorageInventory.StackId(Model);
            if (id == null || !ReferenceEquals(inventory.FindStack(id), Model))
            {
                return default;
            }

            var state = inventory.CaptureStorage();
            if (state.Grids["player"].Width == 0)
            {
                return default;
            }

            if (GetViewport().GuiIsDragging())
            {
                if (TscnScenes.LoadAndBuild("scenes/ui/inventory_drag_preview.tscn") is Control preview)
                {
                    preview.GetNode<TextureRect>("Icon").Texture = Model.Item.Sprite;
                    preview.GetNode<Label>("Name").Text = InventoryItemUi.Translate(Model.Item.Name);
                    SetDragPreview(preview);
                }
            }

            return new Godot.Collections.Dictionary
            {
                ["inventory_stack"] = id,
                ["inventory_revision"] = inventory.Revision,
                ["inventory_generation"] = inventory.StorageGeneration.ToString("N")
            };
        }
    }
}
