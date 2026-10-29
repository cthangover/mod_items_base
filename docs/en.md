# Items Base — items, the inventory and using things

Items Base gives a game items and a bag to keep them in. It defines what an item is (name, picture, price, type and what happens when it is used), shows the **Inventory** panel, saves what the player carries, and lets scenarios give, take and check items. Trading, cooking, crafting, equipment and the RPG maps all build on it.

## The Inventory panel

Open it with the bag icon on the toolbar. Click an item on the left to see its details on the right.

![Using a forest mushroom in RPG Plus: the mushroom (×2) selected in the grid, its details on the right — Targeted and Food badges, the description "Restores 5 health", the action action/heal with the property heal 5 — and, after Eat, the target list with the Hero at 10/24 health.](assets/use-mushroom.jpg)

- **Tabs** at the top filter by type (Food, Resources, Recipes, Quests); the search field and the sort list narrow the grid.
- **Details** show the name, type badges, price, description, stack size, id and the item's own properties.
- **The action button** is named by the item: **Use**, **Eat** for food, **Apply to target**, **Learn recipe**, or a verb the item defines (the mushroom says **Eat** because of its `verb`).
- **Targeted items** ask whom to use them on: pick a party member, or **Cancel**.
- **Drop** throws a stack away after a confirmation; key items cannot be dropped.
- In a battle every use or drop costs the active character one action point; outside battle it is free.

![After choosing the Hero: the stack is down to ×1 and the panel reports "Applied: Forest mushroom → Hero".](assets/after-use.jpg)

If an action cannot be done — for example the target already has full health — the item is not spent and the panel says why.

## An item definition

Items are JSON files of kind `item` in a mod's `items/` folder, with the picture next to them. This is the mushroom from the pictures above (from RPG Demo):

```json
{
  "Kind": "item",
  "Items": [
    {
      "Id": "rpg_demo/mushroom",
      "Name": "rpg_demo/item/mushroom/name",
      "Description": "rpg_demo/item/mushroom/desc",
      "Cost": 2,
      "Sprite": "rpg_demo/forest_mushroom",
      "ItemType": "Food,TargetUsed",
      "ItemAction": "action/heal",
      "heal": 5,
      "verb": "ui/inventory/verb_eat"
    }
  ]
}
```

- `Name`, `Description` — translation keys, shown in the details.
- `Cost` — the price, used by shops.
- `Sprite` — the picture `items/rpg_demo/forest_mushroom.png`.
- `ItemType` — one or more types, separated by commas: `Food`, `Resource`, `Quest`, `Recipe`, `Used` (used without a target), `TargetUsed` (used on a party member), `CantDrop`.
- `ItemAction` — what using it does. `action/heal` restores health by the item's `heal` value, never above the maximum and never to a fallen character. Other mods add actions, for example equipment's `action/equip`.
- Any other key (`heal`, `verb`) is an item property: actions read them, and the details list them. `verb` names the action button.

## Items in scenarios

```scenario
action item.add id=rpg_demo/mushroom count=2
when "item('rpg_demo/mushroom') >= 2" -> :enough
action item.remove id=rpg_demo/mushroom count=2
```

- `item.add` puts items into the bag (a chest, a gift, a pickup on a map); `count` defaults to 1.
- `item('id')` compares how many the player has; it works in `when`, in answer conditions, quests and map events.
- `item.remove` takes items away; it refuses when the player has fewer, so check with `item('id')` first.

## Recipes

Recipes (kind `recipe`) list `Input` and `Output` items with counts. This mod keeps which recipes the player knows; cooking and crafting use them.

## If something goes wrong

1. **An item shows its id instead of a name.** Add its `Name` key to the mod's `locale` files.
2. **No action button.** The item needs `Used` or `TargetUsed` in `ItemType` and an `ItemAction`.
3. **Using does nothing and the item stays.** The action refused: for healing, the target is at full health or has fallen.
4. **`item.add` adds nothing.** The id is unknown; check the `Id` in the item file.
