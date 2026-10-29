# Items Base — vật phẩm, túi đồ và cách dùng

Items Base cho game có vật phẩm và chiếc túi để đựng chúng. Mod định nghĩa vật phẩm là gì (tên, ảnh, giá, loại và chuyện gì xảy ra khi dùng), hiển thị bảng **Túi đồ**, lưu những gì người chơi mang theo, và cho phép kịch bản trao, lấy và kiểm tra vật phẩm. Mua bán, nấu ăn, chế tạo, trang bị và các bản đồ RPG đều xây trên nó.

## Bảng Túi đồ

Mở bằng biểu tượng túi trên thanh công cụ. Nhấp một vật phẩm ở bên trái để xem chi tiết của nó bên phải.

![Dùng nấm rừng trong RPG Plus: nấm (×2) đang chọn trong lưới, chi tiết bên phải — huy hiệu Có mục tiêu và Đồ ăn, mô tả "Hồi 5 máu", hành động action/heal với thuộc tính heal 5 — và sau Ăn, danh sách mục tiêu hiện Hero đang 10/24 máu.](assets/use-mushroom.jpg)

- **Thẻ** phía trên lọc theo loại (Đồ ăn, Tài nguyên, Công thức, Nhiệm vụ); ô tìm kiếm và danh sách sắp xếp thu hẹp lưới.
- **Chi tiết** hiển thị tên, huy hiệu loại, giá, mô tả, kích thước chồng, id và các thuộc tính riêng của vật phẩm.
- **Nút hành động** do chính vật phẩm đặt tên: **Dùng**, **Ăn** với đồ ăn, **Dùng lên mục tiêu**, **Học công thức**, hoặc một động từ vật phẩm tự định nghĩa (nấm hiển thị **Ăn** nhờ `verb` của nó).
- **Vật phẩm có mục tiêu** hỏi dùng lên ai: chọn một thành viên đội hoặc **Hủy**.
- **Vứt bỏ** ném cả chồng đi sau khi xác nhận; vật phẩm then chốt không thể vứt.
- Trong trận, mỗi lần dùng hay vứt tốn của nhân vật đang hành động một điểm hành động; ngoài trận thì miễn phí.

![Sau khi chọn Hero: chồng còn lại ×1 và bảng báo "Đã dùng: Nấm rừng → Hero".](assets/after-use.jpg)

Nếu hành động không thực hiện được — ví dụ mục tiêu đã đủ máu — vật phẩm không bị tiêu và bảng nói rõ lý do.

## Định nghĩa vật phẩm

Vật phẩm là tệp JSON kind `item` trong thư mục `items/` của mod, ảnh đặt ngay cạnh. Đây là cây nấm trong các hình trên (thuộc RPG Demo):

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

- `Name`, `Description` — khóa dịch, hiện trong phần chi tiết.
- `Cost` — giá, cửa hàng dùng đến.
- `Sprite` — ảnh `items/rpg_demo/forest_mushroom.png`.
- `ItemType` — một hoặc nhiều loại, cách nhau bằng dấu phẩy: `Food`, `Resource`, `Quest`, `Recipe`, `Used` (dùng không cần mục tiêu), `TargetUsed` (dùng lên thành viên đội), `CantDrop`.
- `ItemAction` — dùng vào việc gì. `action/heal` hồi máu theo giá trị `heal` của vật phẩm, không vượt quá mức tối đa và không bao giờ dùng lên người đã gục. Các mod khác thêm hành động riêng, ví dụ `action/equip` của equipment.
- Khóa nào khác (`heal`, `verb`) là thuộc tính của vật phẩm: hành động đọc chúng, chi tiết cũng liệt kê. `verb` đặt tên cho nút hành động.

## Vật phẩm trong kịch bản

```scenario
action item.add id=rpg_demo/mushroom count=2
when "item('rpg_demo/mushroom') >= 2" -> :enough
action item.remove id=rpg_demo/mushroom count=2
```

- `item.add` bỏ vật phẩm vào túi (rương, món quà, đồ nhặt trên bản đồ); `count` mặc định là 1.
- `item('id')` so sánh người chơi có bao nhiêu cái; dùng được trong `when`, điều kiện lựa chọn, nhiệm vụ và sự kiện bản đồ.
- `item.remove` lấy vật phẩm đi; từ chối nếu người chơi có ít hơn, nên hãy kiểm tra bằng `item('id')` trước.

## Công thức

Công thức (kind `recipe`) liệt kê vật phẩm `Input` và `Output` kèm số lượng. Mod này giữ danh sách công thức người chơi đã biết; nấu ăn và chế tạo dùng đến chúng.

## Khi có trục trặc

1. **Vật phẩm hiện id thay vì tên.** Thêm khóa `Name` của nó vào các tệp `locale` của mod.
2. **Không có nút hành động.** Vật phẩm cần `Used` hoặc `TargetUsed` trong `ItemType` và một `ItemAction`.
3. **Dùng không có tác dụng mà vật phẩm vẫn còn.** Hành động bị từ chối: khi hồi máu, mục tiêu đã đủ máu hoặc đã gục.
4. **`item.add` không thêm gì cả.** Id không tồn tại; đối chiếu `Id` trong tệp vật phẩm.
