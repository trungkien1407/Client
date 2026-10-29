# VocVach Spine Tools

Công cụ Editor cho nhân vật Spine. Dùng lại được ở dự án khác.

## Có gì

| Menu | Làm gì |
|---|---|
| Tools/VocVach/Spine/Chụp bảng anim (tất cả) | Chụp mọi `SkeletonDataAsset` trong `Assets/` |
| Tools/VocVach/Spine/Chụp bảng anim (đang chọn) | Chỉ các SkeletonDataAsset đang chọn ở cửa sổ Project |
| Tools/VocVach/Spine/Chụp bảng skin (tất cả) | Mỗi bộ xương → `_SKINS.png`: mỗi hàng 1 skin (bỏ `default`, `WEAPON_*`), 8 khung anim `Idle` — xem skin biến thân / trang phục (batch: `-executeMethod VocVach.SpineTools.AnimSheet.RunSkins`) |

Kết quả nằm ở `Builds/AnimSheets/{tên bộ xương}/`:
- `{anim}.png`: 8 khung hình chia đều độ dài anim.
  - Khung 1 ở thời điểm 0, khung 8 ở 7/8 độ dài anim.
  - Xem khung nào "trúng đòn" để điền mốc trúng (`hit`) cho chiêu.
- `_ALL.png` + `_ALL.txt`: mọi anim xếp thành hàng; file txt ghi thứ tự hàng và độ dài từng anim.

Chạy không mở Editor (batch):

```bash
Unity.exe -batchmode -quit -projectPath <dự án> -executeMethod VocVach.SpineTools.AnimSheet.RunAll -logFile anim.log
```

## Mang sang dự án khác

1. Chép cả thư mục `com.vocvach.spine-tools` vào `Packages/` của dự án mới. Unity tự nhận đây là "embedded package".
2. Hoặc để một chỗ chung (ví dụ `D:/UnityPackages/com.vocvach.spine-tools`) rồi thêm vào `Packages/manifest.json`:
   `"com.vocvach.spine-tools": "file:D:/UnityPackages/com.vocvach.spine-tools"`.

Dự án đó cần có spine-unity 3.8 với assembly tên `spine-unity`. Nếu bản Spine khác tên assembly, sửa mục `references` trong `Editor/VocVach.SpineTools.Editor.asmdef`.

## Vì sao là package

- Code trong package có **asmdef riêng**, chỉ biên dịch trong Editor nên không lọt vào bản build game.
- Package không phụ thuộc code riêng của game. Ai cần chỉ việc chép thư mục sang.
