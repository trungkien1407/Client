# VocVach Sprite Stack Fit

Cửa sổ Editor **ghép 2 ảnh chồng nhau** (ví dụ thân + chân NPC, đầu + mình…). Dùng khi thuật toán đặt tự động ghép lệch vài bộ hình.

Cách dùng: kéo chuột / bấm phím mũi tên để dời ảnh trên theo từng điểm ảnh, xem bóng mờ của vị trí tự động để so, rồi lưu cấu hình.

- Tên package: `com.vocvach.sprite-stack-fit` · phiên bản `1.0.0` · Unity 2022.3 trở lên.
- Chỉ chạy trong Editor (asmdef `VocVach.StackFit.Editor`, `includePlatforms: Editor`), nên **không lọt vào bản build game**.
- Không phụ thuộc code riêng của game nào. Dữ liệu đọc / ghi qua 1 lớp **nguồn dữ liệu** do dự án tự viết.

## Mở

Menu **Tools → VocVach → Ghép ảnh thân + chân**.

| Vùng | Làm gì |
|---|---|
| Thanh trên | Chọn nguồn dữ liệu (nếu dự án có nhiều), tìm theo tên, lọc "Chỉ bộ chỉnh tay", bật / tắt bóng tự động, nền tối / sáng, phóng to ×1–×8, [Lưu tất cả], [Tải lại] |
| Danh sách trái | Mọi bộ hình. **✎** = đang dùng số chỉnh tay (đã lưu). **\*** (chữ cam) = đã kéo, chưa lưu |
| Xem trước | Ảnh dưới đứng yên, ảnh trên dời theo số đang chỉnh. Vạch đỏ = mặt đất (đáy ảnh dưới), vạch mờ dọc = giữa. Bóng xanh = vị trí tự động |
| Dưới | Ô số X (phải +) / Y (lên +), [Lưu (chỉnh tay)], [Hoàn tác], [Về số tự động] |

Thao tác:
- **Kéo chuột** trong vùng xem trước: dời ảnh trên. Kéo chậm vẫn nhích được từng điểm ảnh.
- **← ↑ → ↓**: dời 1 điểm ảnh. Giữ **Shift**: 5 điểm ảnh.
- **Ctrl + S**: lưu bộ đang chọn.
- Đóng cửa sổ khi còn bộ chưa lưu → cửa sổ hỏi có lưu không.

## Gắn vào dự án

1. Chép thư mục `com.vocvach.sprite-stack-fit` vào `Packages/` của dự án (gọi là embedded package, Unity tự nhận).

   Hoặc thêm vào `Packages/manifest.json`:
   `"com.vocvach.sprite-stack-fit": "file:../../duong-dan/com.vocvach.sprite-stack-fit"`.
2. Viết **1 lớp nguồn dữ liệu** trong thư mục `Editor` của dự án:

```csharp
using System.Collections.Generic;
using UnityEngine;
using VocVach.StackFit;

public class MyFitProvider : StackFitProvider
{
    public override string Title => "Nhân vật (đầu + mình)";

    // Danh sách bộ hình: ảnh trên, ảnh dưới, độ lệch đang dùng (điểm ảnh, pivot trên so với pivot dưới)
    public override List<StackFitItem> Load() { /* đọc từ asset / JSON của dự án */ }

    // Lưu LÂU DÀI (nên ghi vào 1 file nằm trong git) + áp vào asset đang dùng
    public override void Save(StackFitItem item, Vector2 offset) { /* ... */ item.Offset = offset; item.Manual = true; }

    // Bỏ số chỉnh tay, dùng lại số tự động
    public override void ResetToAuto(StackFitItem item) { /* ... */ }

    // (Không bắt buộc) số tự động để vẽ bóng so sánh
    public override Vector2? AutoOffset(StackFitItem item) => null;
}
```

Cửa sổ tự tìm mọi lớp con của `StackFitProvider` (có hàm dựng không tham số), nên không phải đăng ký gì thêm.

## Ví dụ có sẵn: game Naruto (NPC thân + chân)

- **Nguồn dữ liệu:** `Assets/Editor/NpcFit/NpcStackFitProvider.cs`.
- **File cấu hình lâu dài (trong git):** `Assets/Editor/NpcFit/npc_fit_overrides.json` (đọc / ghi bằng `NpcFitOverrides.cs`).
- **`DemoArtTool` đọc file này** khi nhập kho hình. Sang máy khác:
  1. Pull mã nguồn.
  2. Liên kết kho tài nguyên.
  3. **Tools → Naruto → Demo art → 1**.

  Số chỉnh tay được áp lại, không phải kéo lại.
- **Tự kiểm** (Editor phải đóng):
  `Unity -batchmode -quit -projectPath <dự án> -executeMethod NpcStackFitProvider.SelfTest -logFile fit.log`
