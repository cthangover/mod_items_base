namespace Cthangover.Mods.Items.Tests
{
    using System;
    using System.IO;
    using System.Text.Json;
    using System.Threading.Tasks;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI;
    using Cthangover.Core.UI.Inventory;
    using Cthangover.Mods.Interface.View;
    using Godot;

    /// <summary>Real prefab checks prove profile inheritance/patches, uniform slot hitboxes and a named item list.</summary>
    public sealed class InventoryProfileUiTests
    {
        /// <summary>The same groups move between all three layouts without replacing their identities or their physical rules.</summary>
        [ModTest("inventory/profiles_ui_slots_list_grid_and_mod_patches", TimeoutSeconds = 60)]
        public static async Task Modes(ModTestContext context)
        {
            var runtime = GameData.Instance.Runtime;
            var previous = runtime.Inventory;
            var tree = (SceneTree)Engine.GetMainLoop();
            Control panel = null;
            try
            {
                var inventory = new Inventory();
                runtime.Inventory = inventory;
                ModAssert.True(InventoryProfiles.TryApply(inventory, "slots", "inventory/test/slots", out var reason), reason);
                var configured = inventory.CaptureStorage().Grids["player"];
                ModAssert.Equal("slots", configured.Layout);
                ModAssert.Equal(3, configured.Width);
                ModAssert.Equal(2, configured.Height, "The downstream mod's entity patch changes the inherited height");
                var ids = Entities.Collect<ItemInfo>().Keys.GetEnumerator();
                ModAssert.True(ids.MoveNext());
                var item = ItemFactory.Instance.Get(ids.Current);
                item.Properties.SetStr("Inventory", "{\"Width\":4,\"Height\":3,\"StackLimit\":1,\"Rotatable\":true}");
                ModAssert.True(inventory.TryAdd(item, 2, out _));
                var id = Inventory.StackId(inventory.Items[0]);
                var originalOrigins = JsonSerializer.Serialize(inventory.Stack(id).Origins);
                UiRegistry.Toggle("inventory");
                ModAssert.True(UiHost.Instance.TryGetInstance("inventory", out panel));
                var behavior = (InventoryPanelBehaviour)panel;
                behavior.Refresh();
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                var surface = panel.GetNode<InventoryGridSurface>("Panel/Margin/Root/Body/Left/GridScroll/GridContent");
                var cell = Cell(surface, id);
                ModAssert.CloseTo(cell.Size.X, cell.Size.Y, .01, "A physically large item occupies a square slot");
                var step = surface.CustomMinimumSize.X / 3;
                var drag = cell._GetDragData(Vector2.One);
                ModAssert.False(surface._CanDropData(new Vector2(step + 1, 1), drag), "The second slot is occupied");
                ModAssert.True(surface._CanDropData(new Vector2(step * 2 + 1, 1), drag));
                surface._DropData(new Vector2(step * 2 + 1, 1), drag);
                ModAssert.Equal(2, inventory.Stack(id).X);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                ViewportCapture.SavePng(tree.Root, Path.Combine(context.SandboxDir, "inventory_slots.png"));

                panel.GetNode<Button>("Panel/Margin/Root/Toolbar/StorageButton").EmitSignal(Button.SignalName.Pressed);
                var choice = panel.GetNode<OptionButton>("Panel/Margin/Root/StorageSettings/Layout");
                choice.Selected = 2;
                choice.EmitSignal(OptionButton.SignalName.ItemSelected, 2L);
                ModAssert.False(panel.GetNode<SpinBox>("Panel/Margin/Root/StorageSettings/Width").Editable);
                panel.GetNode<Button>("Panel/Margin/Root/StorageSettings/Apply").EmitSignal(Button.SignalName.Pressed);
                ModAssert.Equal("list", inventory.CaptureStorage().Grids["player"].Layout);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                cell = Cell(surface, id);
                ModAssert.True(cell.Size.X > cell.Size.Y * 4);
                ModAssert.Equal(InventoryItemUi.Translate(item.Name), cell.GetNode<Label>("ItemName").Text);
                ModAssert.True(cell._GetDragData(Vector2.One).VariantType == Variant.Type.Nil);
                ModAssert.False(panel.GetNode<OptionButton>("Panel/Margin/Root/Toolbar/SortOption").Disabled);
                await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                ViewportCapture.SavePng(tree.Root, Path.Combine(context.SandboxDir, "inventory_list.png"));
                ModAssert.True(InventoryProfiles.TryApply(inventory, "patched-list", "inventory/test/list", out reason), reason);
                ModAssert.Equal("list", inventory.CaptureStorage().Grids["player"].Layout);
                ModAssert.True(InventoryProfiles.TryApply(inventory, "grid", "inventory/grid", out reason), reason);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                cell = Cell(surface, id);
                step = surface.CustomMinimumSize.X / 10;
                ModAssert.CloseTo(step * 4 - 2, cell.Size.X, .01);
                ModAssert.CloseTo(step * 3 - 2, cell.Size.Y, .01);
                ModAssert.Equal(originalOrigins, JsonSerializer.Serialize(inventory.Stack(id).Origins));
                ModAssert.Equal(2, inventory.CheckCount(item.ID));
                ModAssert.True(File.Exists(Path.Combine(context.SandboxDir, "inventory_slots.png")));
                ModAssert.True(File.Exists(Path.Combine(context.SandboxDir, "inventory_list.png")));
            }
            finally
            {
                if (GodotObject.IsInstanceValid(panel) && panel.Visible)
                {
                    UiRegistry.Toggle("inventory");
                }

                runtime.Inventory = previous;
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            }
        }

        private static InventoryItemBehaviour Cell(Node surface, string id)
        {
            foreach (var node in surface.GetChildren())
            {
                if (node is InventoryItemBehaviour cell && Inventory.StackId(cell.Model) == id)
                {
                    return cell;
                }
            }

            throw new InvalidOperationException("No visible row for the retained group identity");
        }
    }
}
