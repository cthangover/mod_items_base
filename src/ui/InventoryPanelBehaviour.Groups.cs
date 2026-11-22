namespace Cthangover.Core.UI.Inventory
{
    using System.Collections.Generic;
    using Cthangover.Core.Items;
    using Cthangover.Core.Settings;
    using Godot;

    public partial class InventoryPanelBehaviour
    {
        private InventoryGroupDropToken groupDrop;

        private void DropOrdinary(IItemContainer row, int count)
        {
            var runtime = GameData.Instance.Runtime;
            var name = row.Item.Name;
            var token = groupDrop;
            groupDrop = null;
            _dropOpen = false;
            var success = InventoryGroupDropActions.TryDrop(token, count, out var handled, out var reason);
            if (success && !handled)
            {
                if (!HasActionPoint())
                {
                    return;
                }

                success = runtime.Inventory is Cthangover.Core.Items.Inventory storage
                    ? storage.TryExchange(new Dictionary<string, int> { [row.Item.ID] = count },
                        new Dictionary<string, int>(), out reason, Cthangover.Core.Items.Inventory.StackId(row))
                    : runtime.Inventory.Remove(row.Item, count);
            }

            if (!GodotObject.IsInstanceValid(this) || !ReferenceEquals(runtime, GameData.Instance.Runtime))
            {
                return;
            }

            if (!success)
            {
                SetMessage(MessageKind.Error, InventoryItemUi.Translate(reason ?? "inventory/storage/insufficient"));
                BuildPassport();
                return;
            }

            if (!handled)
            {
                SpendActionPoint();
            }

            SetMessage(MessageKind.Ok, string.Format(InventoryItemUi.Translate("ui/inventory/dropped"),
                InventoryItemUi.Translate(name), count));
            PlaySound("ui/close_click");
            BuildPassport();
        }
    }
}
