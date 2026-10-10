# Naruto — client Unity (2D MMO side-scroll)

Client của game 2D MMO cuộn ngang kiểu Ninja School Online. Ghép với server Java authoritative ở repo **`D:\Server\Server`**:
server quyết định sát thương, va chạm hợp lệ, tiền; client gửi *ý định* và vẽ *kết quả* qua TCP nhị phân.

- **Unity 2022.3.62f3** (2D, Built-in), C#
- Addressables · Input System · TextMeshPro · UGUI · Cinemachine · 2D Tilemap · **Spine 3.8** (nhân vật)
- Chạy trên PC (Windows) và Android

## Yêu cầu

| Thứ | Ghi chú |
|---|---|
| Unity Hub + **Unity 2022.3.62f3** | đúng phiên bản (`ProjectSettings/ProjectVersion.txt`); thêm *Android Build Support* nếu build Android |
| Visual Studio 2022 hoặc Rider | *Edit → Preferences → External Tools* |
| Server đang chạy | xem `D:\Server\Server\docs\van-hanh\CHAY_SERVER.md` — không có server thì client dừng ở "Đang kết nối máy chủ..." |

## Mở và chạy

1. Unity Hub → **Add** → `D:\Game\Naruto` → mở bằng 2022.3.62f3 (lần đầu import vài phút).
2. Mở scene **`Assets/Scenes/SampleScene.unity`** (scene duy nhất trong Build Settings) → **Play**.
3. Tài khoản test: `clientbot` / `test1234`.
4. Địa chỉ server: `Assets/Resources/server_config.json` (`host`, `port`, `tls`, `certSha256`) — đổi không cần sửa code.

Kiểm tra compile không mở Editor (Editor phải đóng, đã đăng nhập Unity Hub):
```bash
unity run "D:\Game\Naruto" --editor-version 2022.3.62f3 -- -logFile compile.log
```
Build: **Tools → Naruto → Build → PC (Windows) / Android (APK)** → thư mục `Builds/`.
Tự chơi bản build: `Builds\PC\NinjaOnline.exe -autotest -user clientbot -pass test1234 -logFile test.log`.

## Cấu trúc `Assets/Script` (namespace = thư mục: `Assets.Script.<Thư mục>`)

| Thư mục | Vai trò |
|---|---|
| `Combat/` | ra chiêu (`SkillCaster`, `CombatInput`), nhận kết quả chiến đấu, đồ rơi, chat, `GameplayBootstrap` (tạo các hệ thống lúc chạy) |
| `Constants/` | `Cmd.cs` (opcode — phải khớp server), `GameConfig.cs` (địa chỉ server), `GameEnums.cs` |
| `Core/` | nền dùng chung: `AssetSource` + `ImageBank` (tải hình theo số), `GameInput` (bảng phím), `AddressKeys`, `AudioManager`, `SavedLogin`, `ErrorReporter`, `AutoTestRunner` |
| `Data/` | `GameData` (kho dữ liệu server gửi), `GameActions` (nơi duy nhất gửi gói), các `*Network` nhận gói, `GameDataCache` |
| `Database/` | ScriptableObject cấu hình hình ảnh quái / NPC / kỹ năng |
| `Entities/` | `MobController` (quái), `NpcEntity` |
| `Fx/` | `PartRenderer` (vẽ khung nhiều mảnh), `EffectPlayer` (hiệu ứng theo dữ liệu server) |
| `Interfaces/` | `ITargetable`, `IHasHealth` |
| `Manager/` | luồng app (`AppFlowManager`, `GameAuthManager`, `EnterWorld`), sổ người chơi / quái / NPC trong map, pool, popup |
| `Map/` | `MapManager` (tải prefab map), đổi map/khu, `MapInfo` + các điểm `Map*Spot`, minimap |
| `Models/` | lớp dữ liệu: `PlayerData`, `MobAnimSO`, `PartAnim`, `NinjaClassData`... |
| `Network/` | TCP, khung gói, đọc/ghi big-endian, hàng đợi về luồng chính, `NetworkListener`, `Heartbeat` |
| `Player/` | `PlayerMovement` (mình), `RemotePlayer` (người khác), `PlayerSpawner`, Spine (`PlayerVisualController`), camera (`GameMaster`) |
| `Skill/` | thanh phím chiêu 1–5 |
| `UI/` | HUD (`GameHud` + `GameHudView`), `Kit/` (khung UI: `UIRoot`, `GameWindow`, `UIKit`, `ItemIcon`...), `Windows/` (25 cửa sổ) |

Thư mục khác trong `Assets/`: `Editor/` (tool Editor), `Prefabs/` (`basePlayer`, `Mob`, `Maps/Map_N.prefab`...), `Resources/` (`server_config.json`, prefab UI),
`SO/` (database), `Character/` (Spine + 3 hệ), `Scenes/`, `AddressableAssetsData/`, `PlayerControls.inputactions`.

> **`Assets/_Demo/`** là hình demo nội bộ — **gitignore, không bao giờ commit**, xoá trước khi build phát hành.

## Menu Tools → Naruto

| Menu | Làm gì |
|---|---|
| 1. Sắp xếp Addressables | đưa asset vào 4 group (Core / Characters / Maps / Atlases) theo bảng `PLAN` |
| 2. Kiểm tra Project | báo ô Inspector trống, key Addressables thiếu, component mất script |
| 3. Dọn component mất script trong scene chính | |
| 4. Build Addressables | |
| 5. Cấu hình hình ảnh quái & NPC | điền `MobDatabase` |
| Map → 1 / 2 / 3 | nhập điểm vào prefab (1 lần) · **xuất map cho server** (`data/maps/map_N.json`) · đổi thư mục |
| UI → 1 / 2 / 3 | tạo bộ prefab mẫu · xuất cửa sổ/HUD còn thiếu ra prefab · xuất lại cửa sổ đang chọn |
| Demo art → 1 / 2 | nhập hình demo nội bộ vào `Assets/_Demo` · đổi thư mục nguồn |
| Build → PC / Android | build Addressables + game ra `Builds/` |

Code các tool: `Assets/Editor/` (`NarutoProjectTools`, `MapDataTool`, `UIPrefabTool`, `ContentSetupTool`, `DemoArtTool`, `BuildTool`).

## Tài liệu (ở repo server)

Mục lục: **`D:\Server\Server\docs\README.md`**. File chính (trong `D:\Server\Server\docs\`):

| File | Đọc để |
|---|---|
| `hoc\HOC_UNITY_CLIENT.md` | **học Unity qua chính client này** — người mới bắt đầu ở đây |
| `ky-thuat\CLIENT.md` | sổ tay quy ước client (luồng dữ liệu, UI bằng prefab, Addressables, bẫy) |
| `ky-thuat\PROTOCOL.md` | hợp đồng gói tin giữa client và server |
| `ky-thuat\KIEN_TRUC.md` | bản đồ code cả 2 phía |
| `ky-thuat\TAI_NGUYEN.md` | hình theo số, hình demo, cách thay hình riêng |
| `noi-dung\CAN_DIEN.md` | việc phải làm tay trong Unity Editor |
| `van-hanh\RELEASE.md`, `van-hanh\TESTING.md` | build, phát hành, kiểm thử |

Quy tắc ngắn khi sửa code: gửi gói chỉ qua `Data/GameActions`; nghe gói bằng lớp kế thừa `Network/NetworkListener`; UI không đọc gói mà đọc `GameData`;
manager mới tạo trong `Combat/GameplayBootstrap`; `Constants/Cmd.cs` luôn khớp `constant/Cmd.java` bên server.
