namespace Cthangover.Core.UI.Inventory
{
    using System;
    using System.Collections.Generic;
    using Cthangover.Core.Items;
    using Cthangover.Core.Mods;
    using Cthangover.Core.Settings;
    using Cthangover.Core.Scenes;
    using Godot;
    using StorageInventory = Cthangover.Core.Items.Inventory;

    public partial class InventoryPanelBehaviour
    {
        private bool CanConsumeSelected(IItemContainer row)
        {
            if (row?.Item == null)
            {
                return false;
            }

            if (GameData.Instance.Runtime.Inventory is not StorageInventory owner || row.Item.ItemAction is IInventoryStackAction)
            {
                return true;
            }

            return owner.CanExchange(new Dictionary<string, int> { [row.Item.ID] = 1 }, new Dictionary<string, int>(),
                StorageInventory.StackId(row));
        }

        private bool ConsumeSelection(IItemContainer row)
        {
            if (row.Item.ItemAction is IInventoryStackAction)
            {
                return true;
            }

            if (GameData.Instance.Runtime.Inventory is StorageInventory owner)
            {
                return owner.TryExchange(new Dictionary<string, int> { [row.Item.ID] = 1 }, new Dictionary<string, int>(), out _,
                    StorageInventory.StackId(row));
            }

            return GameData.Instance.Runtime.Inventory.Remove(row.Item, 1);
        }

        private void BindStorageForm()
        {
            var button = GetNode<Button>("Panel/Margin/Root/Toolbar/StorageButton");
            button.Visible = ModManager.Instance.Mods.ContainsKey("items_editor");
            button.Text = InventoryItemUi.Translate("inventory/storage/settings");
            button.Pressed += ToggleStorageForm;
            var layout = GetNode<OptionButton>("Panel/Margin/Root/StorageSettings/Layout");
            layout.AddItem(InventoryItemUi.Translate("inventory/storage/grid"));
            layout.AddItem(InventoryItemUi.Translate("inventory/storage/slots"));
            layout.AddItem(InventoryItemUi.Translate("inventory/storage/list"));
            layout.ItemSelected += OnStorageLayoutChanged;
            GetNode<Label>("Panel/Margin/Root/StorageSettings/WidthLabel").Text = InventoryItemUi.Translate("inventory/storage/columns");
            GetNode<Label>("Panel/Margin/Root/StorageSettings/HeightLabel").Text = InventoryItemUi.Translate("inventory/storage/rows");
            var apply = GetNode<Button>("Panel/Margin/Root/StorageSettings/Apply");
            apply.Text = InventoryItemUi.Translate("inventory/storage/apply");
            apply.Pressed += ApplyStorageForm;
        }

        private void ToggleStorageForm()
        {
            var form = GetNode<Control>("Panel/Margin/Root/StorageSettings");
            form.Visible = !form.Visible;
            if (form.Visible && GameData.Instance.Runtime.Inventory is StorageInventory inventory)
            {
                var grid = inventory.CaptureStorage().Grids["player"];
                var index = grid.Layout == "grid" ? 0 : grid.Layout == "slots" ? 1 : 2;
                GetNode<OptionButton>("Panel/Margin/Root/StorageSettings/Layout").Selected = index;
                OnStorageLayoutChanged(index);
                GetNode<SpinBox>("Panel/Margin/Root/StorageSettings/Width").Value = grid.Width;
                GetNode<SpinBox>("Panel/Margin/Root/StorageSettings/Height").Value = grid.Height;
            }
        }

        private void ApplyStorageForm()
        {
            if (GameData.Instance.Runtime.Inventory is not StorageInventory inventory)
            {
                return;
            }

            var width = (int)GetNode<SpinBox>("Panel/Margin/Root/StorageSettings/Width").Value;
            var height = (int)GetNode<SpinBox>("Panel/Margin/Root/StorageSettings/Height").Value;
            var index = GetNode<OptionButton>("Panel/Margin/Root/StorageSettings/Layout").Selected;
            var layout = index == 0 ? "grid" : index == 1 ? "slots" : "list";
            var success = inventory.TryConfigureGrid("layout/" + Guid.NewGuid().ToString("N"), new InventoryGridData
            {
                Layout = layout,
                Width = layout == "list" ? 0 : width,
                Height = layout == "list" ? 0 : height
            }, out var reason, true);
            _hintLabel.Text = InventoryItemUi.Translate(success ? "inventory/storage/drag_hint" : reason);
            if (success)
            {
                GetNode<Control>("Panel/Margin/Root/StorageSettings").Visible = false;
            }
        }

        private void OnStorageLayoutChanged(long index)
        {
            GetNode<SpinBox>("Panel/Margin/Root/StorageSettings/Width").Editable = index != 2;
            GetNode<SpinBox>("Panel/Margin/Root/StorageSettings/Height").Editable = index != 2;
        }

        private void AddSplitAction(VBoxContainer block, IItemContainer container)
        {
            if (container.Count < 2 || container is InventoryInstanceContainer)
            {
                return;
            }

            var form = TscnScenes.LoadAndBuild("scenes/ui/inventory_storage_actions.tscn") as Control;
            if (form == null)
            {
                return;
            }

            var button = form.GetNode<Button>("SplitStack");
            button.Text = InventoryItemUi.Translate("inventory/storage/split");
            button.Pressed += SplitSelected;
            block.AddChild(form);
        }

        private void SplitSelected()
        {
            if (_selectedContainer == null || GameData.Instance.Runtime.Inventory is not StorageInventory inventory)
            {
                return;
            }

            var id = StorageInventory.StackId(_selectedContainer);
            if (inventory.TrySplitStack("split/" + Guid.NewGuid().ToString("N"), id, Math.Max(1, _selectedContainer.Count / 2),
                "player", -1, -1, false, out var child, out var reason))
            {
                SelectItem(inventory.FindStack(child));
            }
            else
            {
                SetMessage(MessageKind.Error, InventoryItemUi.Translate(reason));
                BuildPassport();
            }
        }
    }
}
