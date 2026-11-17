namespace Cthangover.Core.UI.Inventory
{
    using System;
    using Cthangover.Core.Items;
    using Cthangover.Core.Settings;
    using Godot;
    using ItemInventory = Cthangover.Core.Items.Inventory;

    /// <summary>
    /// Identity-specific bag affordances retain the selected occurrence beside equal item blueprints. Unique
    /// rows cannot invoke count-only effects; their domain supplies placement and atomic identity-aware drop.
    /// </summary>
    public partial class InventoryPanelBehaviour
    {
        private InventoryInstanceDropToken occurrenceDrop;

        private bool RestoreOccurrenceSelection(string id)
        {
            if (id == null || GameData.Instance.Runtime.Inventory is not ItemInventory owner)
            {
                return false;
            }

            var row = owner.FindInstanceRow(id);
            if (row == null)
            {
                return false;
            }

            SelectItem(row);
            return true;
        }

        private static void AddOccurrenceIdentity(Node grid, IItemContainer container)
        {
            if (container is InventoryInstanceContainer occurrence)
            {
                grid.AddChild(MakeStatCell(InventoryItemUi.Translate("ui/inventory/instance_id"), occurrence.InstanceId));
            }
        }

        private bool CanDropOccurrence(InventoryInstanceContainer occurrence, out string reason)
        {
            reason = "inventory/instances/unavailable";
            if (HasActiveActor)
            {
                reason = "inventory/instances/world_drop_required";
                return false;
            }

            if (!InventoryItemUi.CanDrop(occurrence.Item))
            {
                reason = "ui/inventory/drop_cant";
                return false;
            }

            var owner = GameData.Instance.Runtime.Inventory as ItemInventory;
            if (owner == null || !ReferenceEquals(owner.FindInstanceRow(occurrence.InstanceId), occurrence))
            {
                reason = "inventory/instances/stale_revision";
                return false;
            }

            return InventoryInstanceActions.CanDrop(owner, occurrence.InstanceId, out reason);
        }

        private void RefuseOccurrenceUse()
        {
            _targetOpen = false;
            SetMessage(MessageKind.Error, InventoryItemUi.Translate("ui/inventory/instance_use_unavailable"));
            BuildPassport();
        }

        private bool PrepareOccurrenceDrop(InventoryInstanceContainer occurrence, out string reason)
        {
            occurrenceDrop = null;
            if (!CanDropOccurrence(occurrence, out reason))
            {
                return false;
            }

            return InventoryInstanceActions.TryPrepareDrop(GameData.Instance.Runtime.Inventory as ItemInventory,
                occurrence, out occurrenceDrop, out reason);
        }

        private void DropOccurrence(InventoryInstanceContainer occurrence)
        {
            if (!CanDropOccurrence(occurrence, out var reason))
            {
                SetMessage(MessageKind.Error, InventoryItemUi.Translate(reason));
                BuildPassport();
                return;
            }

            var runtime = GameData.Instance.Runtime;
            var name = occurrence.Item.Name;
            var token = occurrenceDrop;
            occurrenceDrop = null;
            _dropOpen = false;
            var success = InventoryInstanceActions.TryDrop(token, out reason);
            if (!GodotObject.IsInstanceValid(this) || !ReferenceEquals(runtime, GameData.Instance.Runtime))
            {
                return;
            }

            if (!success)
            {
                SetMessage(MessageKind.Error, InventoryItemUi.Translate(reason));
                BuildPassport();
                return;
            }

            SetMessage(MessageKind.Ok, string.Format(InventoryItemUi.Translate("ui/inventory/dropped"),
                InventoryItemUi.Translate(name), 1));
            PlaySound("ui/close_click");
            BuildPassport();
        }
    }
}
