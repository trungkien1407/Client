# VocVach Spine Tools

Công cụ Editor cho nhân vật Spine: chụp **bảng khung hình** của mọi anim / mọi skin ra ảnh PNG để xem anim nào là chém / ném / buff, khung nào trúng đòn, skin trông thế nào — không cần bấm Play từng anim.

- Tên package: `com.vocvach.spine-tools` · phiên bản `1.0.0` · Unity 2022.3 trở lên.
- Chỉ chạy trong Editor (asmdef `VocVach.SpineTools.Editor`, `includePlatforms: Editor`) → **không lọt vào bản build game**.
- Không phụ thuộc code riêng của game nào, chỉ cần spine-unity.

## Có gì

| Menu | Làm gì |
|---|---|
| Tools/VocVach/Spine/Chụp bảng anim (tất cả) | Chụp mọi `SkeletonDataAsset` trong `Assets/` |
| Tools/VocVach/Spine/Chụp bảng anim (đang chọn) | Chỉ các SkeletonDataAsset đang chọn ở cửa sổ Project |
| Tools/VocVach/Spine/Chụp bảng skin (tất cả) | Mỗi bộ xương → `_SKINS.png`: mỗi hàng 1 skin (bỏ `default`, `WEAPON_*`), 8 khung anim `Idle` — xem skin biến thân / trang phục (batch: `-executeMethod VocVach.SpineTools.AnimSheet.RunSkins`) |

Kết quả nằm ở `Builds/AnimSheets/{tên bộ xương}/` (tính từ thư mục gốc dự án Unity):
- `{anim}.png`: 8 khung hình chia đều độ dài anim.
  - Khung 1 ở thời điểm 0, khung 8 ở 7/8 độ dài anim.
  - Xem khung nào "trúng đòn" để điền mốc trúng (`hit`) cho chiêu.
- `_ALL.png` + `_ALL.txt`: mọi anim xếp thành hàng; file txt ghi thứ tự hàng và độ dài từng anim.
- `_SKINS.png`: (menu chụp skin) mỗi hàng 1 skin.

Chạy không mở Editor (batch, Editor phải đóng):

```bash
Unity.exe -batchmode -quit -projectPath <dự án> -executeMethod VocVach.SpineTools.AnimSheet.RunAll -logFile anim.log
Unity.exe -batchmode -quit -projectPath <dự án> -executeMethod VocVach.SpineTools.AnimSheet.RunSkins -logFile skins.log
```

## Cài vào dự án khác

Điều kiện: dự án đó **đã có spine-unity 3.8** với assembly tên `spine-unity` (spine-unity cài kiểu `.unitypackage` vào `Assets/Spine/` cũng được — asmdef vẫn tên `spine-unity`).

Chọn 1 trong 3 cách:

1. **Add package from disk** (dùng chung 1 bản cho nhiều dự án):
   chép thư mục `com.vocvach.spine-tools` ra một chỗ chung (ví dụ `D:/UnityPackages/`) → Unity mở *Window → Package Manager* → nút **+** → **Add package from disk…** → chọn file `package.json` trong thư mục đó.
   Unity ghi vào `Packages/manifest.json` một dòng `"com.vocvach.spine-tools": "file:D:/UnityPackages/com.vocvach.spine-tools"` (có thể tự gõ dòng này thay cho bấm nút).
2. **Add package from git URL** (repo phải đọc được từ máy đó, máy cần cài git):
   *Package Manager* → **+** → **Add package from git URL…** → dán
   `https://github.com/trungkien1407/Client.git?path=/Packages/com.vocvach.spine-tools`
   Muốn khoá phiên bản: thêm `#<tag hoặc mã commit>` vào cuối, ví dụ `...?path=/Packages/com.vocvach.spine-tools#v1.0.0`.
3. **Nhúng (embedded)**: chép cả thư mục vào `Packages/` của dự án mới. Unity tự nhận, sửa được code ngay tại chỗ.

Cài xong, menu **Tools → VocVach → Spine** xuất hiện. Không thấy menu → mở Console xem lỗi biên dịch (thường do thiếu assembly `spine-unity`).

Nếu bản Spine của dự án đặt tên assembly khác (ví dụ Spine 4.x), sửa mục `references` trong `Editor/VocVach.SpineTools.Editor.asmdef`. API `SkeletonAnimation` / `SkeletonDataAsset` dùng ở đây có cả ở 3.8 lẫn 4.x nhưng **mới chỉ thử với 3.8**.

## Cách dùng

1. **Xem anim của 1 nhân vật**: chọn file `*_SkeletonData.asset` trong cửa sổ Project → *Tools → VocVach → Spine → Chụp bảng anim (đang chọn)* → mở `Builds/AnimSheets/<tên>/_ALL.png` để lướt nhanh, mở `<anim>.png` để xem kỹ từng khung.
2. **Chụp tất cả** (lần đầu, hoặc sau khi nhập nhiều bộ Spine mới): *Chụp bảng anim (tất cả)*. Có thể lâu nếu dự án nhiều bộ xương.
3. **Xem skin**: *Chụp bảng skin (tất cả)* → `_SKINS.png`.

Trước khi chụp, tool hỏi lưu scene đang sửa (nếu có), mở một scene trống để chụp, xong thì mở lại scene cũ.

## Quy ước / giới hạn

- Tên thư mục kết quả = tên asset bỏ đuôi `_SkeletonData`.
- Có skin tên `Normal` thì chụp bằng skin đó, không thì skin mặc định. Bảng skin bỏ `default` và skin bắt đầu bằng `WEAPON` (quy ước của bộ Spine đang dùng — dự án khác đặt tên khác thì sửa trong `AnimSheet.cs`).
- Ô ảnh cố định 320 × 320 px (`_ALL.png` 160 px), 8 khung / anim, nền xanh xám; muốn đổi sửa hằng `Frames`, `Cell`, `RowCell`.
- Đường ra cố định `Builds/AnimSheets/` — nhớ để `Builds/` trong `.gitignore`.
- Chỉ hỗ trợ `SkeletonAnimation` (không chụp `SkeletonGraphic` của UI).
- Package chưa có file `LICENSE`: muốn chia sẻ ra ngoài thì thêm giấy phép trước.

## Vì sao là package

- Code trong package có **asmdef riêng**, chỉ biên dịch trong Editor nên không lọt vào bản build game.
- Package không phụ thuộc code riêng của game. Ai cần chỉ việc cài theo một trong 3 cách ở trên.

Lịch sử thay đổi: [CHANGELOG.md](CHANGELOG.md).
