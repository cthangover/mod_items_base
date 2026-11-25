namespace Cthangover.Mods.Items.Tests
{
    using System.IO;
    using System.Threading.Tasks;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Events;
    using Cthangover.Core.Items;
    using Cthangover.Core.Localization;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Mods.Testing;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI;
    using Cthangover.Core.UI.Inventory;
    using Cthangover.Core.UI.Messages;
    using Cthangover.Mods.Interface.View;
    using Godot;

    /// <summary>
    /// Windowed checks use the actual inventory prefab, selection signals and native drag/drop callbacks; captures preserve the normal
    /// HDR pipeline.
    /// </summary>
    public sealed class InventoryStorageUiTests
    {
        [ModTest("inventory/rewards_native_notification_and_pickup", TimeoutSeconds = 60)]
        public static async Task GroundReward(ModTestContext context)
        {
            var runtime = GameData.Instance.Runtime;
            var previous = runtime.Inventory;
            var previousLocale = TranslationServer.GetLocale();
            var tree = (SceneTree)Engine.GetMainLoop();
            string notification = null;
            void Capture(NotificationRequestedEvent evt) => notification = evt.Text;
            ModEventBus.Subscribe<NotificationRequestedEvent>(Capture);
            try
            {
                LocaleLoader.LoadLocale("ru");
                var inventory = new Inventory();
                runtime.Inventory = inventory;
                ModAssert.True(inventory.TryConfigureGrid("small", new InventoryGridData { Width = 1, Height = 1 }, out _));
                using var ids = Entities.Collect<ItemInfo>().Keys.GetEnumerator();
                ModAssert.True(ids.MoveNext());
                var blocker = ItemFactory.Instance.Get(ids.Current);
                blocker.Properties.SetStr("Inventory", "{\"Width\":1,\"Height\":1}");
                ModAssert.True(inventory.TryAdd(blocker, 1, out _));
                ModAssert.True(ids.MoveNext());
                var reward = ItemFactory.Instance.Get(ids.Current);
                var nativeSize = InventoryItemSize.From(reward);
                ModAssert.True(nativeSize.Width == 1 && nativeSize.Height == 1, "test reward fits an empty cell");

                InventoryRewards.Grant(inventory, reward, 1);
                if (notification == null && NotificationBuffer.TryDequeue(out var queued)) notification = queued;
                ModAssert.True(notification?.Contains("В сумке не хватило места") == true, notification);
                ModAssert.Equal(0, inventory.CheckCount(reward.ID));
                ModAssert.Equal(1, inventory.CaptureStorage().GroundRewards.Count);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                var view = tree.Root.GetNode<InventoryGroundRewardsView>("InventoryGroundRewards");
                Button marker = null;
                foreach (var child in view.GetChildren())
                    if (child is Button button && button.Visible) marker = button;
                ModAssert.NotNull(marker, "ground loot is visible in the active scene");

                ModAssert.True(inventory.TryExchange(new() { [blocker.ID] = 1 }, new(), out _));
                marker.EmitSignal(Button.SignalName.Pressed);
                ModAssert.Equal(1, inventory.CheckCount(reward.ID));
                ModAssert.Equal(0, inventory.CaptureStorage().GroundRewards.Count);
            }
            finally
            {
                runtime.Inventory = previous;
                TranslationServer.SetLocale(previousLocale);
                ModEventBus.Unsubscribe<NotificationRequestedEvent>(Capture);
            }
        }

        /// <summary>
        /// The actual prefab sizes rectangles, moves the selected group, refuses stale drag data and splits through its button.
        /// </summary>
        [ModTest("inventory/storage_ui_real_grid_drag_split_and_capture", TimeoutSeconds = 60)]
        public static async Task Grid(ModTestContext context)
        {
            var runtime = GameData.Instance.Runtime;
            var previous = runtime.Inventory;
            var tree = (SceneTree)Engine.GetMainLoop();
            Control panel = null;
            try
            {
                var inventory = new Inventory();
                runtime.Inventory = inventory;
                ModAssert.True(inventory.TryConfigureGrid("grid", new InventoryGridData
                {
                    Width = 6,
                    Height = 4
                }, out _));
                var ids = Entities.Collect<ItemInfo>().Keys.GetEnumerator();
                ModAssert.True(ids.MoveNext());
                var first = ItemFactory.Instance.Get(ids.Current);
                first.Properties.SetStr("Inventory", "{\"Width\":2,\"Height\":3,\"StackLimit\":1,\"Rotatable\":true}");
                ModAssert.True(inventory.TryAdd(first, 1, out _));
                ModAssert.True(ids.MoveNext());
                var second = ItemFactory.Instance.Get(ids.Current);
                second.Properties.SetStr("Inventory", "{\"StackLimit\":3}");
                ModAssert.True(inventory.TryAdd(second, 6, out _));
                var firstId = Inventory.StackId(inventory.Items[0]);
                UiRegistry.Toggle("inventory");
                ModAssert.True(UiHost.Instance.TryGetInstance("inventory", out panel));
                ModAssert.True(panel.Visible);
                var behavior = (InventoryPanelBehaviour)panel;
                behavior.Refresh();
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                var surface = panel.GetNode<InventoryGridSurface>("Panel/Margin/Root/Body/Left/GridScroll/GridContent");
                var cell = Cell(surface, firstId);
                ModAssert.NotNull(cell);
                ModAssert.True(cell.Size.Y > cell.Size.X, "A 2x3 item must occupy a tall rectangle");
                var step = surface.CustomMinimumSize.X / 6;
                ModAssert.CloseTo(step * 2 - 2, cell.Size.X, .01);
                ModAssert.CloseTo(step * 3 - 2, cell.Size.Y, .01);
                var drag = cell._GetDragData(Vector2.One);
                var destination = new Vector2(step * 4 + 1, 1);
                ModAssert.True(surface._CanDropData(destination, drag));
                surface._DropData(destination, drag);
                ModAssert.Equal(4, inventory.Stack(firstId).X);
                ModAssert.Equal(0, inventory.Stack(firstId).Y);
                ModAssert.False(surface._CanDropData(Vector2.One, drag), "The previous drag revision is now stale");
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                var groupId = Inventory.StackId(inventory.Items[1]);
                var group = Cell(surface, groupId);
                using (var click = new InputEventMouseButton
                {
                    ButtonIndex = MouseButton.Left,
                    Pressed = true
                }

                )
                {
                    group.EmitSignal(Control.SignalName.GuiInput, click);
                }

                var split = panel.FindChild("SplitStack", true, false) as Button;
                ModAssert.NotNull(split);
                split.EmitSignal(Button.SignalName.Pressed);
                ModAssert.Equal(6, inventory.CheckCount(second.ID));
                ModAssert.Equal(4, inventory.Items.Count);
                await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
                await tree.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var path = Path.Combine(context.SandboxDir, "inventory_grid.png");
                ViewportCapture.SavePng(tree.Root, path);
                ModAssert.True(File.Exists(path));
                context.Log("Actual grid capture: " + path);
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

        private static InventoryItemBehaviour Cell(Node parent, string id)
        {
            foreach (var child in parent.GetChildren())
            {
                if (child is InventoryItemBehaviour cell && Inventory.StackId(cell.Model) == id)
                {
                    return cell;
                }
            }

            return null;
        }
    }
}
