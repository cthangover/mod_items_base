namespace Cthangover.Core.Items
{
    using System.Collections.Generic;
    using Cthangover.Core.Factories.Impls;
    using Cthangover.Core.Mods.Resolvers;
    using Cthangover.Core.Scenes;
    using Cthangover.Core.Settings;
    using Cthangover.Core.UI.Theming;
    using Cthangover.Mods.Interface.Messages;
    using Cthangover.Mods.Interface.Shell;
    using Godot;

    /// <summary>Projects saved piles into the active scene and makes them reachable from the player.</summary>
    public partial class InventoryGroundRewardsView : CanvasLayer
    {
        private readonly List<(InventoryGroundReward Pile, Button Button)> markers = new();
        private readonly List<Rect2> occupied = new();
        private static InventoryGroundRewardsView active;
        private Inventory bag;
        private long revision = -1;
        private string scene;
        private string locale;

        internal static string CurrentLocation
        {
            get
            {
                var root = SceneContextNode.CurrentScene;
                return root?.HasMeta("shell_kind") == true
                    ? SceneManager.GetCurrentSceneName()
                    : root?.SceneFilePath;
            }
        }

        public override void _EnterTree() => active = this;

        public override void _ExitTree()
        {
            if (active == this) active = null;
        }

        /// <summary>Uses the normal actor interact action to take a reachable pile close to the viewport centre.</summary>
        public static bool TryPickFocused(string domain)
        {
            if (active == null) return false;
            var viewport = active.GetViewport().GetVisibleRect();
            var center = viewport.GetCenter();
            var best = Mathf.Pow(viewport.Size.Y * .25f, 2);
            InventoryGroundReward selected = null;
            foreach (var (pile, _) in active.markers)
            {
                if (pile.Position.Domain != domain || !active.TryLocate(pile.Position, out var point, out var inReach)
                    || !inReach) continue;
                var distance = point.DistanceSquaredTo(center);
                if (distance >= best) continue;
                best = distance;
                selected = pile;
            }

            if (selected == null) return false;
            active.PickUp(active.bag, selected);
            return true;
        }

        public override void _Process(double delta)
        {
            var currentScene = CurrentLocation;
            var currentBag = currentScene == null ? null : GameData.Instance?.Runtime.Inventory as Inventory;
            var currentLocale = TranslationServer.GetLocale();
            if (scene != currentScene || bag != currentBag || revision != (currentBag?.Revision ?? -1) || locale != currentLocale)
            {
                scene = currentScene;
                bag = currentBag;
                revision = bag?.Revision ?? -1;
                locale = currentLocale;
                Rebuild();
            }

            if (markers.Count == 0) return;
            var viewport = GetViewport().GetVisibleRect();
            occupied.Clear();
            foreach (var (pile, button) in markers)
            {
                button.Visible = TryLocate(pile.Position, out var point, out var inReach) && viewport.HasPoint(point);
                if (!button.Visible) continue;

                var offset = new Vector2(button.Size.X / 2, button.Size.Y + 8);
                var rect = new Rect2(point - offset, button.Size);
                rect.Position = new Vector2(
                    Mathf.Clamp(rect.Position.X, viewport.Position.X, Mathf.Max(viewport.Position.X, viewport.End.X - rect.Size.X)),
                    Mathf.Clamp(rect.Position.Y, viewport.Position.Y, Mathf.Max(viewport.Position.Y, viewport.End.Y - rect.Size.Y)));
                foreach (var previous in occupied)
                    if (rect.Intersects(previous)) rect.Position = new Vector2(rect.Position.X, previous.Position.Y - rect.Size.Y - 4);
                occupied.Add(rect);
                button.Position = rect.Position;
                button.Disabled = !inReach || ShellSession.Current?.SimulationPaused == true;
            }
        }

        private void Rebuild()
        {
            foreach (var marker in markers)
            {
                marker.Button.Hide();
                marker.Button.QueueFree();
            }
            markers.Clear();
            if (bag == null) return;

            foreach (var pile in bag.GroundRewards())
            {
                if (pile.Position.Domain == InventoryGroundWorlds.SceneDomain && pile.Position.Location != scene) continue;
                if (!Entities.Collect<ItemInfo>(required: false).ContainsKey(pile.ItemId)) continue;
                var item = ItemFactory.Instance.Get(pile.ItemId);
                var button = new Button
                {
                    Icon = item.Sprite,
                    Text = string.Format(Tr("inventory/reward/pickup").ToString(), InventoryRewards.DisplayName(item), pile.Count),
                    Visible = false,
                };
                CthangoverTheme.Apply(button);
                button.AddThemeConstantOverride("icon_max_width", 24);
                var owner = bag;
                button.Pressed += () => PickUp(owner, pile);
                AddChild(button);
                button.Size = button.GetCombinedMinimumSize();
                markers.Add((pile, button));
            }
        }

        private bool TryLocate(InventoryGroundPosition position, out Vector2 screen, out bool inReach)
        {
            if (position.Domain != InventoryGroundWorlds.SceneDomain)
                return InventoryGroundWorlds.TryProject(position, out screen, out inReach);

            screen = GetViewport().GetVisibleRect().Size * new Vector2(.5f, .72f);
            inReach = position.Location == scene;
            return inReach;
        }

        private void PickUp(Inventory owner, InventoryGroundReward pile)
        {
            if (owner == null || owner != bag || owner != GameData.Instance?.Runtime.Inventory || scene != CurrentLocation
                || ShellSession.Current?.SimulationPaused == true
                || !TryLocate(pile.Position, out _, out var inReach) || !inReach) return;
            if (!owner.TryPickUpGroundReward(pile.Position, pile.Id, out var reason))
                MessagesHelper.AddMessage(Tr(reason == "inventory/storage/no_space"
                    ? "inventory/reward/no_space" : reason).ToString());
        }
    }
}
