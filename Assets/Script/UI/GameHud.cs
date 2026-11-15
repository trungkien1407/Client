using System.Collections.Generic;
using System.Text;
using Assets.Script.Constants;
using Assets.Script.Data;
using Assets.Script.Entities;
using Assets.Script.Interfaces;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// HUD CHÍNH (nằm trên HUD cũ của scene) — chỉ đổ dữ liệu GameData vào hình GameHudView (prefab UI/Hud), không đọc gói tin:
    ///  - Thanh EXP + số cấp, 2 hàng nút menu góc phải trên, dòng tiền, minimap + nút khu.
    ///  - Banner giữa màn hình + khung đồng hồ phó bản / boss / Lôi đài, khung máu đồng đội, chữ "CHOÁNG".
    ///  - Nút [Tương tác] khi đang chọn 1 người chơi khác. Phím F / nút "Nói" → NpcNetwork.TalkToNearest.
    /// Tạo tự động trong GameplayBootstrap.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        /// <summary>Bề rộng dành cho minimap góc phải trên (MinimapHud) — menu xếp sang trái nó.</summary>
        public static float MinimapWidth = MinimapHud.W + 16f;

        /// <summary>Bố cục HUD đang dùng (null khi chưa vào game) — GuidedTour tìm nút để chỉ vào.</summary>
        public static GameHudView View => _instance != null ? _instance._v : null;

        private static GameHud _instance;
        private GameHudView _v;
        private Button _leaveDungeon;
        private string _shownMap;
        private int _shownZone = -2;
        private float _bannerUntil;
        private PlayerTargeting _targeting;

        private void Awake() => _instance = this;

        private void Update()
        {
            bool inGame = LocalPlayerState.Id >= 0;
            if (inGame && _v == null) CreateHud();
            if (_v != null && _v.gameObject.activeSelf != inGame) _v.gameObject.SetActive(inGame);
            if (!inGame) return;

            if (Core.GameInput.Pressed(Core.GameKey.TalkNpc)) NpcNetwork.TalkToNearest();

            ConfirmWindow.Pump();
            UpdateBanner();
            UpdateEventPanel();
            UpdateInteract();
            _v.stun.gameObject.SetActive(LocalPlayerState.IsStunned);
            if (_shownZone != LocalPlayerState.ZoneId || _shownMap != GameData.MapName)
            {
                _shownZone = LocalPlayerState.ZoneId;
                _shownMap = GameData.MapName;
                string zone = _shownZone == Constants.ZoneIds.Private ? "Khu riêng" : $"Khu {_shownZone + 1}";
                _v.zoneBtn.SetLabel(string.IsNullOrEmpty(_shownMap) ? zone + " (đổi)" : $"<b>{_shownMap}</b>  <color=#ffd56b>{zone}</color>"); // GĐ8: tên map
            }
        }

        private void CreateHud()
        {
            _v = HudPanel.Create<GameHudView>(UIRoot.Instance.HudLayer);
            // Ô cấp: số cấp cách vạch sau chữ "LV" 1 khoảng (trước dính sát), có chỗ cho % EXP (prefab cũ đặt x = 146)
            var lv = _v.levelText.rectTransform;
            lv.anchoredPosition = new Vector2(154f, lv.anchoredPosition.y);
            lv.sizeDelta = new Vector2(82f, lv.sizeDelta.y);
            _v.levelText.enableWordWrapping = false;
            _v.levelText.enableAutoSizing = true;
            _v.levelText.fontSizeMin = 12;
            _v.levelText.fontSizeMax = 20;
            HudPanel.Create<MinimapHud>(_v.transform);   // phím B = bản đồ lớn
            StatusIconsHud.Create(_v.transform, new Vector2(252, -86));
            AutoHud.Create(_v.transform);                // nút Auto (bật / tắt tự động, như phím Z)
            MountHud.Create(_v.transform);               // nút Cưỡi (lên / xuống thú cưỡi, như phím R) — GĐ28
            // Nút "Rời" phó bản (góc phải khung sự kiện, chỉ hiện khi đang ở phó bản) — map phó bản không có cổng ra
            _leaveDungeon = UIKit.Button("LeaveDungeon", _v.eventPanel.transform, "Rời", () =>
                ConfirmWindow.Ask("Rời phó bản về làng? Lượt hôm nay đã dùng, không được trả lại.", "Rời", GameActions.DungeonLeave), 14);
            ((RectTransform)_leaveDungeon.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -4), new Vector2(56, 30), new Vector2(1, 1));
            if (!HudMenuIcons.Apply(_v, MinimapWidth)) HudMenuIcons.LayoutText(_v, MinimapWidth);
            CharacterTabs.Init();                          // Nhân vật · Hành trang · Kỹ năng · Thú cưng = 1 cửa sổ có tab
            StyleRightColumn();
            BuildHudExtras();   // có hình kho → 12 nút menu thành icon (không có → giữ nút chữ)   // icon choáng / chậm / bỏng / tăng sức mạnh / phân thân (cạnh ô cấp)

            // Hành động nút menu — cùng thứ tự GameHudView.MenuLabels
            System.Action[] actions =
            {
                CharacterTabs.Toggle,                           // mở tab gần nhất (Nhân vật / Hành trang / Kỹ năng / Thú cưng)
                () => CharacterTabs.Open(1),                    // (nút đã ẩn — giữ đúng thứ tự)
                () => CharacterTabs.Open(2),
                () => GameWindow.Get<QuestWindow>().Toggle(),
                NpcNetwork.TalkToNearest,
                () => GameWindow.Get<ActivityWindow>().Toggle(),
                () => GameWindow.Get<SocialWindow>().Toggle(),
                () => GameWindow.Get<MailWindow>().Toggle(),
                () => GameWindow.Get<LeaderboardWindow>().Toggle(),
                () => GameWindow.Get<GuideWindow>().Toggle(),
                () => GameWindow.Get<SettingsWindow>().Toggle(),
                TogglePk,
            };
            for (int i = 0; i < actions.Length; i++)
            {
                var act = actions[i];
                if (_v.menu[i] != null) _v.menu[i].onClick.AddListener(() => act());
            }
            _v.zoneBtn.onClick.AddListener(() => GameWindow.Get<ZoneWindow>().Toggle());
            _v.questBtn.onClick.AddListener(() => GameWindow.Get<QuestWindow>().Toggle());
            _v.interactBtn.onClick.AddListener(OpenInteract);

            // Tạo sẵn cửa sổ để phím tắt C/I/K/L/O/M dùng được ngay
            GameWindow.Preload<CharacterWindow>();
            GameWindow.Preload<InventoryWindow>();
            GameWindow.Preload<SkillWindow>();
            GameWindow.Preload<QuestWindow>();
            GameWindow.Preload<SocialWindow>();
            GameWindow.Preload<MailWindow>();
            GameWindow.Preload<ChatWindow>();
            GameWindow.Preload<ActivityWindow>();   // phím H
            GameWindow.Preload<GuideWindow>();      // phím G
            GameWindow.Preload<WorldMapWindow>();   // phím B
            GameWindow.Preload<AutoWindow>();       // phím U (tự động)
            GameWindow.Preload<PetWindow>();        // phím P (thú cưng, GĐ27)
            SettingsWindow.ApplySaved();

            GameData.OnChanged += k =>
            {
                if (k == DataKind.Character || k == DataKind.Money) { UpdateExp(); UpdateMoney(); }
                if (k == DataKind.Pvp) UpdatePk();
                if (k == DataKind.Mails) _v.menu[GameHudView.MenuMail].SetLabel(GameData.MailUnread > 0 ? $"Thư ({GameData.MailUnread})" : "Thư");
                if (k == DataKind.Party || k == DataKind.PartyHp) UpdateParty();
                if (k == DataKind.Quests || k == DataKind.Inventory || k == DataKind.Templates) UpdateQuests();
                if (k == DataKind.Activity) _v.menu[GameHudView.MenuActivity].image.color = GameData.ActivityClaimable ? UIKit.ButtonHot : UIKit.ButtonColor; // có rương chưa nhận → nút cam
            };
            GameActions.ActivityRequest(); // lấy điểm hoạt động ngay khi vào game → nút hiện "!" nếu có rương chưa nhận
            UpdateExp(); UpdateMoney(); UpdatePk(); UpdateParty(); UpdateQuests();
            GuidedTour.StartIfFirstTime();    // lần đầu nhân vật này vào game: hướng dẫn chỉ vào từng nút / NPC (2026-11-15, thay 5 trang chữ)
        }

        /// <summary>
        /// Cột phải dưới minimap: nút "Tên map · Khu" thành chip tối viền vàng (trước là nút kem vuông thô), minimap có viền bo góc.
        /// </summary>
        private void StyleRightColumn()
        {
            UIStyle.Chip(_v.zoneBtn, 14);
            var mini = _v.transform.Find("Minimap") ?? _v.transform.Find("MinimapHud");
            if (mini != null) UIStyle.AddRing(mini, UIStyle.ChipBorder, 2f);
        }

        // ================= Phần HUD thêm 2026-11-15 =================
        private GameObject _partyPanel;
        private Button _menuToggle;
        private bool _menuHidden;

        /// <summary>
        /// Dựng lúc chạy (không phải xuất lại prefab): nút ẩn / hiện thanh chức năng, khung sự kiện dưới 2 hàng nút (trước đè lên hàng dưới),
        /// khung nhiệm vụ cùng kiểu, danh sách nhóm dưới nút Auto / Cưỡi, bấm avatar mở cửa sổ nhân vật.
        /// </summary>
        private void BuildHudExtras()
        {
            float menuW = HudMenuIcons.Columns * HudMenuIcons.Cell;
            // 1. Nút ẩn / hiện thanh chức năng — bên trái 2 hàng nút
            _menuToggle = UIKit.Button("MenuToggle", _v.transform, "Ẩn", ToggleMenu, 13);
            ((RectTransform)_menuToggle.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-MinimapWidth - menuW - 6, -10), new Vector2(46, 28), new Vector2(1, 1));
            UIStyle.Chip(_menuToggle, 13);
            // 2. Khung sự kiện (boss / phó bản / bảo trì): kiểu khung HUD, nằm DƯỚI 2 hàng nút
            UIStyle.HudPanel(_v.eventPanel);
            UIStyle.Readable(_v.eventText, 0.18f);
            PlaceEventPanel();
            // 3. Khung nhiệm vụ (trái): cùng kiểu
            UIStyle.HudPanel(_v.questBtn.image);
            UISkin.KeepLabelColor(_v.questBtn);
            UIStyle.Readable(_v.questText, 0.15f);
            // Thu gọn / mở rộng khung nhiệm vụ (2026-11-15): mặc định chỉ việc cần làm của 1 nhiệm vụ, nút "Xem" mở hết
            _questToggle = UIKit.Button("QuestExpand", _v.questBtn.transform, "Xem", ToggleQuests, 12);
            ((RectTransform)_questToggle.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -4), new Vector2(58, 22), new Vector2(1, 1));
            UIStyle.Chip(_questToggle, 12);
            try { _questExpanded = PlayerPrefs.GetInt("quest_expanded", 0) == 1; } catch { _questExpanded = false; }
            // 4. Nhóm: khung dưới nút Cưỡi (cột phải), chỉ hiện khi có nhóm
            var panel = UIKit.Panel("PartyPanel", _v.transform, UIStyle.PanelBg);
            panel.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), MountHud.Pos + new Vector2(0, -MountHud.Size.y - 8), new Vector2(MinimapHud.W + 20, 150), new Vector2(1, 1));
            UIStyle.HudPanel(panel);
            panel.raycastTarget = false;
            _v.partyText.transform.SetParent(panel.transform, false);
            _v.partyText.rectTransform.Fill(8, 6, 6, 6);
            _v.partyText.fontSize = 13;
            UIStyle.Readable(_v.partyText, 0.15f);
            _partyPanel = panel.gameObject;
            _partyPanel.SetActive(GameData.Party.Count > 0);
            // 5. Bấm avatar (khung máu góc trái) → cửa sổ nhân vật
            var av = UISetup.Instance != null ? UISetup.Instance.avatarSlot : null;
            // Nút cũ "BtnTarget" trong scene (gọi script "test" đã xoá) phủ cả khung avatar → nuốt mất click. Giữ hình, bỏ nhận click.
            var dead = av != null && av.transform.parent != null ? av.transform.parent.Find("BtnTarget") : null;
            if (dead != null)
            {
                foreach (var g in dead.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
                var db = dead.GetComponent<Button>();
                if (db != null) db.enabled = false;
            }
            if (av != null && av.GetComponent<Button>() == null)
            {
                av.raycastTarget = true;
                var b = av.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => CharacterTabs.Open(0));
            }
        }

        /// <summary>Ẩn / hiện các nút chức năng (phím tắt vẫn dùng được). Khung sự kiện dời lên khi ẩn.</summary>
        private void ToggleMenu()
        {
            _menuHidden = !_menuHidden;
            for (int i = 0; i < _v.menu.Length; i++)
                if (_v.menu[i] != null && !HudMenuIcons.IsHidden(i)) _v.menu[i].gameObject.SetActive(!_menuHidden);
            _menuToggle.SetLabel(_menuHidden ? "Menu" : "Ẩn");
            PlaceEventPanel();
        }

        /// <summary>Khung sự kiện ngay dưới 2 hàng nút (hoặc sát trên cùng khi đã ẩn nút).</summary>
        private void PlaceEventPanel()
        {
            float y = _menuHidden ? -44f : -8f - 2 * HudMenuIcons.RowH - 8f;
            if (_v.money != null) _v.money.rectTransform.anchoredPosition = new Vector2(-MinimapWidth, y);   // dòng PK (nếu có) ngay trên khung sự kiện
            var rt = _v.eventPanel.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, y - 26f);
        }

        private void UpdateExp()
        {
            if (_v == null) return;
            var c = GameData.Me;
            float pct = c.expToNext > 0 ? Mathf.Clamp01((float)c.exp / c.expToNext) : 0;
            _v.expFill.fillAmount = pct;
            // "12  76.4%" — số cấp vàng to, % EXP trắng nhỏ ngay sau (đỡ phải nhìn xuống thanh EXP dưới màn hình)
            _v.levelText.text = c.level <= 0 ? "" : c.expToNext > 0 ? $"{c.level} <size=72%><color=#f2f2f2>{pct * 100:F1}%</color></size>"
                : $"{c.level} <size=72%><color=#f2f2f2>MAX</color></size>";   // cấp tối đa: không còn % EXP
            // Thanh EXP dưới cùng chỉ còn MÀU (cấp + % đã có ở góc trên, cạnh avatar) — 2026-11-13
            _v.expText.text = "";
        }

        /// <summary>
        /// Dòng nhỏ dưới minimap: chỉ còn điểm PK (khi > 0). Yên / Xu / Lượng xem trong Hành trang (dải tiền dưới túi) —
        /// không hiện ngoài màn hình cho gọn (2026-11-13).
        /// </summary>
        private void UpdateMoney()
        {
            if (_v == null) return;
            var c = GameData.Me;
            _v.money.text = c.pkPoint > 0 ? $"<color=#f55>PK {c.pkPoint}</color>" : "";
        }

        // ================= PvP =================
        private void TogglePk()
        {
            int next = GameData.MyPkMode == 1 ? 0 : 1;
            if (next == 1)
                ConfirmWindow.Ask("Bật ĐỒ SÁT: được tấn công người khác ngoài khu an toàn (cấp 10 trở lên).\nGiết người sẽ bị cộng điểm PK — chết khi có điểm PK mất nhiều EXP hơn.",
                    "Bật", () => GameActions.SetPkMode(1), "Thôi");
            else GameActions.SetPkMode(0);
        }

        private void UpdatePk()
        {
            if (_v == null) return;
            bool on = GameData.MyPkMode == 1;
            _v.menu[GameHudView.MenuPk].SetLabel(on ? "Đồ sát" : "Hoà bình");
            _v.menu[GameHudView.MenuPk].image.color = on ? new Color(0.75f, 0.2f, 0.15f, 1f) : UIKit.ButtonColor;
            HudMenuIcons.SetPk(_v, on);
            UpdateMoney();
        }

        // ================= Banner / sự kiện =================

        /// <summary>Hiện dòng chữ lớn giữa màn hình trong vài giây (gọi được từ mọi nơi).</summary>
        public static void Banner(string text, float seconds = 3f)
        {
            if (_instance == null || _instance._v == null || string.IsNullOrEmpty(text)) return;
            _instance._v.banner.text = text;
            _instance._bannerUntil = Time.time + seconds;
        }

        private void UpdateBanner()
        {
            float left = _bannerUntil - Time.time;
            _v.banner.gameObject.SetActive(left > 0);
            if (left > 0) _v.banner.alpha = Mathf.Clamp01(left / 0.6f);
        }

        private static string Clock(float endTime)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(endTime - Time.time));
            return $"{s / 60:00}:{s % 60:00}";
        }

        private float _nextEventPanel;
        private string _eventShown;

        /// <summary>Khung sự kiện (bảo trì / phó bản / boss / Lôi đài): đồng hồ chỉ tới giây → vẽ lại 4 lần/giây, chữ không đổi thì không gán lại.</summary>
        private void UpdateEventPanel()
        {
            if (Time.unscaledTime < _nextEventPanel) return;
            _nextEventPanel = Time.unscaledTime + 0.25f;
            var sb = new StringBuilder();
            if (GameData.MaintEndTime > Time.time)
                sb.Append($"<color=#f66><b>Bảo trì máy chủ</b></color> sau <color=#fd5>{Clock(GameData.MaintEndTime)}</color>");
            if (GameData.DungeonState == 2)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append($"<b>{GameData.DungeonName}</b>  <color=#fd5>{Clock(GameData.DungeonEndTime)}</color>\nCòn {GameData.DungeonMobsLeft} quái");
            }
            if (GameData.EventState == 1 || GameData.EventState == 2)
            {
                if (sb.Length > 0) sb.Append('\n');
                string name = GameData.EventType == 1 ? "<color=#ff9a4a>Boss thế giới</color>" : "<color=#7fd4ff>Lôi đài</color>";
                string phase = GameData.EventState == 1 ? (GameData.EventType == 2 ? " đăng ký" : " sắp ra") : "";
                sb.Append($"<b>{name}</b>{phase}");
                if (GameData.EventEndTime > Time.time) sb.Append($"  <color=#fd5>{Clock(GameData.EventEndTime)}</color>");
                // Boss: mỗi boss 1 dòng "[TG] Tên - Map" (server không gửi khu — chống tool dò)
                if (GameData.EventType == 1)
                    foreach (var (boss, map) in BossList(GameData.EventText))
                        sb.Append($"\n<color=#ff6b6b>[TG]</color> <b><color=#ffe08a>{boss}</color></b> <color=#9fd8ff>- {map}</color>");
                if (GameData.EventType == 2 && GameData.EventState == 2 && GameData.ArenaScore.Count > 0)
                    for (int i = 0; i < Mathf.Min(5, GameData.ArenaScore.Count); i++)
                        sb.Append($"\n{i + 1}. {GameData.ArenaScore[i].Key} — {GameData.ArenaScore[i].Value} hạ");
            }
            // Sự kiện đã hết giờ mà chưa nhận gói kết thúc (vd đổi map) → tự ẩn sau 5 giây
            if (GameData.EventState != 0 && GameData.EventEndTime > 0 && Time.time > GameData.EventEndTime + 5 && GameData.EventType == 1 && GameData.EventState == 1)
                GameData.EventState = 0;
            string txt = sb.ToString();
            if (txt != _eventShown)
            {
                _v.eventText.text = txt;
                _eventShown = txt;
                // Khung hẹp (260) + cao vừa đủ chữ (trước 380 × 120 cố định, che nhiều màn hình)
                _v.eventText.fontSize = 14;
                _v.eventText.alignment = TextAlignmentOptions.TopLeft;
                float h = txt.Length > 0 ? _v.eventText.GetPreferredValues(txt, EventPanelW - 12f, 0f).y + 10f : 0f;
                _v.eventPanel.rectTransform.sizeDelta = new Vector2(EventPanelW, Mathf.Max(28f, h));
            }
            if (_v.eventPanel.gameObject.activeSelf != (txt.Length > 0)) _v.eventPanel.gameObject.SetActive(txt.Length > 0);
            if (_leaveDungeon != null) _leaveDungeon.gameObject.SetActive(GameData.DungeonState == 2);
        }

        private const float EventPanelW = 290f;
        private static readonly System.Text.RegularExpressions.Regex BossItem =
            new System.Text.RegularExpressions.Regex(@"\s*([^,(]+?)\s*\(([^)]*)\)");

        /// <summary>
        /// Danh sách boss trong chữ EVENT_STATE của server ("… Đang có: A (map), B (map)." / "… Còn: …" / "… xuất hiện: …")
        /// → các cặp (tên boss, tên map). Server cũ còn ghi ", khu 1" trong ngoặc → bỏ phần đó.
        /// </summary>
        public static List<(string, string)> BossList(string text)
        {
            var list = new List<(string, string)>();
            if (string.IsNullOrEmpty(text)) return list;
            foreach (var key in new[] { "Còn: ", "Đang có: ", "xuất hiện: " })
            {
                int i = text.LastIndexOf(key, System.StringComparison.Ordinal);
                if (i < 0) continue;
                foreach (System.Text.RegularExpressions.Match m in BossItem.Matches(text.Substring(i + key.Length)))
                {
                    string map = m.Groups[2].Value;
                    int k = map.IndexOf(", khu", System.StringComparison.Ordinal);
                    if (k >= 0) map = map.Substring(0, k);
                    list.Add((m.Groups[1].Value.Trim(), map.Trim()));
                }
                break;
            }
            return list;
        }

        /// <summary>Bảng theo dõi nhiệm vụ: tối đa 3 nhiệm vụ đang làm + tiến độ; chưa có thì nhắc gặp Hokage.</summary>
        private Button _questToggle;
        private bool _questExpanded;

        /// <summary>Nút "Xem" / "Gọn" trên khung nhiệm vụ: mở rộng ↔ thu gọn, nhớ lựa chọn cho lần sau.</summary>
        private void ToggleQuests()
        {
            _questExpanded = !_questExpanded;
            try { PlayerPrefs.SetInt("quest_expanded", _questExpanded ? 1 : 0); } catch { }
            UpdateQuests();
        }

        private void UpdateQuests()
        {
            if (_v == null) return;
            var sb = new StringBuilder("<color=#fd5><b>Nhiệm vụ</b></color>");
            int max = _questExpanded ? 3 : 1;   // thu gọn: chỉ nhiệm vụ đầu
            int shown = 0;
            foreach (var kv in GameData.MyQuests)
            {
                if (shown >= max) break;
                if (!GameData.Quests.TryGetValue(kv.Key, out var q)) continue;
                int progress = q.type == (int)Constants.QuestType.Collect ? Mathf.Min(q.targetCount, GameData.CountItem(q.targetId)) : kv.Value[0];
                bool done = kv.Value[1] == 1 || (q.type == (int)Constants.QuestType.Collect && progress >= q.targetCount);
                string goal = q.type switch
                {
                    0 => $"Hạ {GameData.MobName(q.targetId)} {progress}/{q.targetCount}",
                    1 => $"Gặp {GameData.NpcName(q.targetId)}",
                    2 => $"{GameData.ItemName(q.targetId)} {progress}/{q.targetCount}",
                    _ => ""
                };
                string state = done ? $"<color=#7f7>Xong → trả cho {GameData.NpcName(q.npcId)}</color>" : $"<color=#ddd>{goal}</color>";
                if (_questExpanded) sb.Append($"\n<b>{q.name}</b>\n  ").Append(state);
                else sb.Append('\n').Append(state);   // thu gọn: chỉ việc cần làm
                shown++;
            }
            int total = GameData.MyQuests.Count;
            if (shown == 0) sb.Append(_questExpanded ? "\n<color=#ddd>Gặp <b>Hokage</b> ở Làng Lá (bấm Nói / F) để nhận nhiệm vụ.</color>"
                                                     : "\n<color=#ddd>Gặp <b>Hokage</b> để nhận nhiệm vụ</color>");
            else if (!_questExpanded && total > 1) sb.Append($"  <color=#9aa><size=12>(+{total - 1})</size></color>");
            _v.questText.text = sb.ToString();
            _v.questText.enableWordWrapping = _questExpanded;
            _v.questText.overflowMode = _questExpanded ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis;
            // Cao vừa nội dung: thu gọn 2 dòng (50), mở rộng tối đa 150 (trước cố định 300 × 140)
            var rt = (RectTransform)_v.questBtn.transform;
            float w = _questExpanded ? 300f : 260f;
            float h = _questExpanded ? Mathf.Clamp(_v.questText.GetPreferredValues(_v.questText.text, w - 12f, 0f).y + 12f, 56f, 150f) : 50f;
            rt.sizeDelta = new Vector2(w, h);
            if (_questToggle != null) _questToggle.SetLabel(_questExpanded ? "Gọn" : "Xem");
        }

        private void UpdateParty()
        {
            if (_v == null) return;
            if (_partyPanel != null) _partyPanel.SetActive(GameData.Party.Count > 0);
            if (GameData.Party.Count == 0) { _v.partyText.text = ""; return; }
            var sb = new StringBuilder("<color=#fd5>Nhóm</color>\n");
            foreach (var m in GameData.Party)
            {
                if (m.id == LocalPlayerState.Id) continue;
                float pct = m.maxHp > 0 ? (float)m.hp / m.maxHp : 0;
                string col = pct > 0.5f ? "#6f6" : pct > 0.2f ? "#fd5" : "#f55";
                string mp = m.maxMp > 0 ? $"  <color=#7bf>{Mathf.RoundToInt(100f * m.mp / m.maxMp)}%</color>" : "";   // chakra (có sau PARTY_HP đầu)
                sb.Append($"{m.name} <color=#9aa>Lv{m.level}</color>  <color={col}>{Mathf.RoundToInt(pct * 100)}%</color>{mp}\n");
            }
            _v.partyText.text = sb.ToString();
        }

        // ================= Tương tác người chơi =================
        private ITargetable CurrentTarget()
        {
            if (_targeting == null) _targeting = FindAnyObjectByType<PlayerTargeting>();
            return _targeting != null ? _targeting.currentTarget : null;
        }

        /// <summary>Nút "Tương tác" giữa trên màn hình: BỎ (2026-11-15) — chọn người rồi bấm Enter / nút Đánh (đổi thành hình chat) là mở bảng tương tác.</summary>
        private void UpdateInteract()
        {
            if (_v.interactBtn.gameObject.activeSelf) _v.interactBtn.gameObject.SetActive(false);
        }

        private void OpenInteract()
        {
            var t = CurrentTarget();
            if (t is Component c && c != null && t.GetTargetType() == TargetType.Player)
                GameWindow.Get<PlayerActionWindow>().ShowFor(t.GetId(), t.GetTargetName());
        }
    }
}
