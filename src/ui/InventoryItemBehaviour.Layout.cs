namespace Cthangover.Core.UI.Inventory
{
    using Godot;
    using Cthangover.Core.Scenes;

    public partial class InventoryItemBehaviour
    {
        /// <summary>
        /// Presents this data-driven item row as a compact named list entry. Selection and item actions use the same model.
        /// Called once on a newly constructed row; the panel owns and rebuilds the row when its profile changes.
        /// </summary>
        public void PresentAsList()
        {
            _icon.SetAnchorsAndOffsetsPreset(LayoutPreset.TopLeft);
            _icon.Position = new Vector2(8, 6);
            _icon.Size = new Vector2(44, 44);
            _dot.Visible = false;
            _tag.Visible = false;
            var name = TscnScenes.LoadAndBuild("scenes/ui/inventory_list_name.tscn") as Label;
            if (name == null)
            {
                return;
            }

            name.Text = InventoryItemUi.Translate(Model.Item.Name);
            AddChild(name);
        }
    }
}
