# Items Base — 아이템, 인벤토리, 아이템 사용

Items Base는 게임에 아이템과 그것을 담을 가방을 줍니다. 아이템이 무엇인지(이름, 그림, 가격, 종류, 사용하면 무슨 일이 일어나는지)를 정의하고, **인벤토리** 패널을 보여 주고, 플레이어가 지닌 것을 저장하며, 시나리오가 아이템을 주고 빼고 확인할 수 있게 합니다. 거래, 요리, 제작, 장비, RPG 맵이 모두 이 위에 세워집니다.

## 인벤토리 패널

툴바의 가방 아이콘으로 엽니다. 왼쪽에서 아이템을 클릭하면 오른쪽에 자세한 정보가 나옵니다.

![RPG Plus에서 숲 버섯 사용하기: 격자에서 선택된 버섯(×2), 오른쪽의 세부 정보 — "대상 지정"과 "음식" 배지, 설명 "체력 5 회복", heal 5 속성을 가진 action/heal — 그리고 "먹기" 후 대상 목록에 10/24 체력의 영웅.](assets/use-mushroom.jpg)

- 위쪽의 **탭**은 종류로 걸러 줍니다(음식, 자원, 레시피, 퀘스트). 검색창과 정렬 목록이 격자를 좁혀 줍니다.
- **세부 정보**에는 이름, 종류 배지, 가격, 설명, 묶음 크기, id, 그리고 아이템 고유의 속성이 나옵니다.
- **액션 버튼**의 이름은 아이템이 정합니다. **사용**, 음식이라면 **먹기**, **대상에게 사용**, **레시피 익히기**, 혹은 아이템이 정한 동사(버섯은 `verb` 때문에 **먹기**로 나옵니다).
- **대상 지정 아이템**은 누구에게 쓸지 물어봅니다. 파티원을 고르거나 **취소**하세요.
- **버리기**는 확인 후 묶음을 버립니다. 중요한 아이템은 버릴 수 없습니다.
- 전투 중에는 사용이나 버리기마다 행동 중인 캐릭터의 액션 포인트가 1씩 듭니다. 전투 밖에서는 공짜입니다.

![영웅을 고른 뒤: 묶음은 ×1로 줄고, 패널은 "사용됨: 숲 버섯 → 영웅"이라고 알립니다.](assets/after-use.jpg)

액션이 불가능할 때 — 예를 들어 대상의 체력이 이미 가득 찼을 때 — 아이템은 소모되지 않고, 패널이 이유를 알려 줍니다.

## 아이템 정의

아이템은 mod의 `items/` 폴더에 있는 kind `item` JSON 파일이고, 그림은 그 옆에 둡니다. 위 사진의 버섯(RPG Demo 것)입니다.

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

- `Name`, `Description` — 번역 키. 세부 정보에 표시됩니다.
- `Cost` — 가격. 상점이 사용합니다.
- `Sprite` — 그림 `items/rpg_demo/forest_mushroom.png`.
- `ItemType` — 하나 이상의 종류를 쉼표로 나열합니다. `Food`, `Resource`, `Quest`, `Recipe`, `Used`(대상 없이 사용), `TargetUsed`(파티원에게 사용), `CantDrop`.
- `ItemAction` — 사용하면 무엇을 하는지. `action/heal`은 아이템의 `heal` 값만큼 체력을 회복합니다. 최대치를 넘지 않고, 쓰러진 캐릭터에게는 결코 쓰이지 않습니다. 다른 mod도 액션을 추가합니다. 예를 들어 equipment의 `action/equip`.
- 그 밖의 키(`heal`, `verb`)는 아이템 속성입니다. 액션이 읽고 세부 정보에도 나열됩니다. `verb`는 액션 버튼의 이름을 정합니다.

## 시나리오 속 아이템

```scenario
action item.add id=rpg_demo/mushroom count=2
when "item('rpg_demo/mushroom') >= 2" -> :enough
action item.remove id=rpg_demo/mushroom count=2
```

- `item.add`는 아이템을 가방에 넣습니다(상자, 선물, 맵의 줍기 아이템). `count`의 기본값은 1입니다.
- `item('id')`는 플레이어가 몇 개 가졌는지 비교합니다. `when`, 선택지 조건, 퀘스트, 맵 이벤트에서 작동합니다.
- `item.remove`는 아이템을 거둡니다. 부족하면 거부하므로, 먼저 `item('id')`로 확인하세요.

## 레시피

레시피(kind `recipe`)는 `Input`과 `Output` 아이템과 수량을 나열합니다. 플레이어가 어떤 레시피를 알고 있는지는 이 모드가 기억하며, 요리와 제작이 사용합니다.

## 문제가 생겼을 때

1. **아이템이 이름 대신 id를 보여 줍니다.** `Name` 키를 mod의 `locale` 파일에 추가하세요.
2. **액션 버튼이 없습니다.** 아이템에는 `ItemType`의 `Used`나 `TargetUsed`와 `ItemAction`이 필요합니다.
3. **사용해도 아무 일 없고 아이템이 그대로입니다.** 액션이 거부했습니다. 회복이라면 대상의 체력이 가득 찼거나 쓰러진 경우입니다.
4. **`item.add`가 아무것도 추가하지 않습니다.** id를 알 수 없습니다. 아이템 파일의 `Id`를 확인하세요.
