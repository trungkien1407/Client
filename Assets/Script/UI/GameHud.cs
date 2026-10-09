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
    /// HUD DỰNG BẰNG CODE (nằm trên HUD cũ của scene) — chỉ VẼ dữ liệu GameData, không đọc gói tin:
    ///  - Thanh EXP + số cấp, 2 hàng nút menu góc phải trên, dòng tiền, minimap + nút khu.
    ///  - Banner giữa màn hình + khung đồng hồ phó bản / boss / Lôi đài, khung máu đồng đội, chữ "CHOÁNG".
    ///  - Nút [Tương tác] khi đang chọn 1 người chơi khác. Phím F / nút "Nói" → NpcNetwork.TalkToNearest.
    /// Tạo tự động trong GameplayBootstrap.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        /// <summary>Bề rộng dành cho minimap góc phải trên (MinimapHud) — menu xếp sang trái nó.</summary>
        public static float MinimapWidth = MinimapHud.W + 16f;

        private static GameHud _instance;
        private GameObject _root;
        private Image _expFill;
        private TextMeshProUGUI _expText, _levelText, _money, _banner, _eventText, _partyText, _stun, _questText;
        private Button _pkBtn, _mailBtn, _interactBtn, _zoneBtn, _activityBtn;
        private string _shownMap;
        private int _shownZone = -2;
        private float _bannerUntil;
        private PlayerTargeting _targeting;

        private void Awake() => _instance = this;

        private void Update()
        {
            bool inGame = LocalPlayerState.Id >= 0;
            if (inGame && _root == null) BuildHud();
            if (_root != null && _root.activeSelf != inGame) _root.SetActive(inGame);
            if (!inGame) return;

            var kb = Keyboard.current;
            if (kb != null && !Combat.ChatBox.IsTyping && kb.fKey.wasPressedThisFrame) NpcNetwork.TalkToNearest();

            ConfirmWindow.Pump();
            UpdateBanner();
            UpdateEventPanel();
            UpdateInteract();
            _stun.gameObject.SetActive(LocalPlayerState.IsStunned);
            if (_shownZone != LocalPlayerState.ZoneId || _shownMap != GameData.MapName)
            {
                _shownZone = LocalPlayerState.ZoneId;
                _shownMap = GameData.MapName;
                string zone = _shownZone == 255 ? "Khu riêng" : $"Khu {_shownZone + 1}";
                _zoneBtn.SetLabel(string.IsNullOrEmpty(_shownMap) ? zone + " (đổi)" : $"{_shownMap} · {zone}"); // GĐ8: tên map
            }
        }

        private void BuildHud()
        {
            var hud = UIRoot.Instance.HudLayer;
            _root = UIKit.Rect("GameHud", hud).Fill().gameObject;

            // ---- Thanh EXP ----
            _expFill = UIKit.Bar("ExpBar", _root.transform, new Color(0.95f, 0.75f, 0.15f));
            ((RectTransform)_expFill.transform.parent).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), new Vector2(-4, 14), new Vector2(0.5f, 0));
            _expText = UIKit.Text("ExpText", _expFill.transform.parent, "", 12, TextAlignmentOptions.Center);
            _expText.rectTransform.Fill();

            // Số cấp đè lên ô "LV" của khung avatar (ảnh khung có sẵn chữ LV, chưa có số)
            _levelText = UIKit.Text("Level", _root.transform, "", 20, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.3f));
            _levelText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -102), new Vector2(80, 26), new Vector2(0, 0.5f));
            _levelText.fontStyle = FontStyles.Bold;
            _levelText.raycastTarget = false;

            // ---- Nút menu: 2 hàng x 6 (GĐ9 thêm Hoạt động + Cẩm nang) ----
            string[] labels = { "Nhân vật", "Túi", "Kỹ năng", "Nhiệm vụ", "Nói (F)", "Hoạt động", "Xã hội", "Thư", "Xếp hạng", "Cẩm nang", "Cài đặt", "Hoà bình" };
            System.Action[] actions =
            {
                () => GameWindow.Get<CharacterWindow>().Toggle(),
                () => GameWindow.Get<InventoryWindow>().Toggle(),
                () => GameWindow.Get<SkillWindow>().Toggle(),
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
            for (int i = 0; i < labels.Length; i++)
            {
                int row = i / 6, col = i % 6;
                // 6 cột x 78px (rộng bằng 5 cột x 92px cũ) → không lấn khung mục tiêu giữa màn hình ở 1280x720
                var b = UIKit.Button("Menu" + i, _root.transform, labels[i], actions[i], 14);
                ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-MinimapWidth - col * 78, -8 - row * 40), new Vector2(74, 34), new Vector2(1, 1));
                if (i == 5) _activityBtn = b;
                if (i == 7) _mailBtn = b;
                if (i == 11) _pkBtn = b;
            }

            // ---- Minimap (phím B = bản đồ lớn) + nút đổi khu ngay dưới ----
            MinimapHud.Create(_root.transform);
            _zoneBtn = UIKit.Button("Zone", _root.transform, "Khu", () => GameWindow.Get<ZoneWindow>().Toggle(), 14);
            ((RectTransform)_zoneBtn.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -MinimapHud.H - 14), new Vector2(MinimapHud.W, 30), new Vector2(1, 1));

            _money = UIKit.Text("Money", _root.transform, "", 15, TextAlignmentOptions.MidlineRight, new Color(1f, 0.85f, 0.3f));
            _money.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-MinimapWidth, -88), new Vector2(460, 24), new Vector2(1, 1));

            // ---- Banner + khung sự kiện (giữa trên) ----
            _banner = UIKit.Text("Banner", _root.transform, "", 26, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            _banner.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(900, 80), new Vector2(0.5f, 1));
            _banner.fontStyle = FontStyles.Bold;
            _banner.enableAutoSizing = true;   // thông báo GM dài tới 200 ký tự → tự thu nhỏ chữ cho vừa khung
            _banner.fontSizeMin = 16;
            _banner.fontSizeMax = 26;
            _banner.outlineWidth = 0.25f;
            var evBg = UIKit.Panel("EventPanel", _root.transform, new Color(0, 0, 0, 0.55f));
            // góc phải, dưới dòng tiền (giữa trên đã có khung mục tiêu của scene)
            evBg.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-MinimapWidth, -116), new Vector2(380, 120), new Vector2(1, 1));
            _eventText = UIKit.Text("Text", evBg.transform, "", 15, TextAlignmentOptions.Top);
            _eventText.rectTransform.Fill(6, 6, 4, 4);
            evBg.raycastTarget = false;

            // ---- Đồng đội (trái, dưới khung máu của scene) ----
            _partyText = UIKit.Text("Party", _root.transform, "", 14, TextAlignmentOptions.TopLeft);
            _partyText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -300), new Vector2(260, 160), new Vector2(0, 1));

            // ---- Theo dõi nhiệm vụ (trái, dưới khung máu) — bấm vào để mở bảng Nhiệm vụ ----
            var questBtn = UIKit.Button("QuestTracker", _root.transform, "", () => GameWindow.Get<QuestWindow>().Toggle(), 14);
            questBtn.image.color = new Color(0, 0, 0, 0.35f);
            ((RectTransform)questBtn.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -146), new Vector2(300, 140), new Vector2(0, 1));
            _questText = questBtn.GetComponentInChildren<TextMeshProUGUI>();
            _questText.alignment = TextAlignmentOptions.TopLeft;
            _questText.rectTransform.Fill(8, 6, 4, 4);

            // ---- Choáng ----
            _stun = UIKit.Text("Stun", _root.transform, "CHOÁNG!", 30, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.2f));
            _stun.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(300, 50));
            _stun.fontStyle = FontStyles.Bold;
            _stun.gameObject.SetActive(false);

            // ---- Tương tác với người chơi đang chọn ----
            _interactBtn = UIKit.Button("Interact", _root.transform, "Tương tác", OpenInteract, 16);
            ((RectTransform)_interactBtn.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -138), new Vector2(140, 38), new Vector2(0.5f, 1));
            _interactBtn.image.color = UIKit.ButtonHot;
            _interactBtn.gameObject.SetActive(false);

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
            SettingsWindow.ApplySaved();

            GameData.OnChanged += k =>
            {
                if (k == DataKind.Character || k == DataKind.Money) { UpdateExp(); UpdateMoney(); }
                if (k == DataKind.Pvp) UpdatePk();
                if (k == DataKind.Mails) _mailBtn.SetLabel(GameData.MailUnread > 0 ? $"Thư ({GameData.MailUnread})" : "Thư");
                if (k == DataKind.Party) UpdateParty();
                if (k == DataKind.Quests || k == DataKind.Inventory || k == DataKind.Templates) UpdateQuests();
                if (k == DataKind.Activity) _activityBtn.image.color = GameData.ActivityClaimable ? UIKit.ButtonHot : UIKit.ButtonColor; // có rương chưa nhận → nút cam
            };
            GameActions.ActivityRequest(); // lấy điểm hoạt động ngay khi vào game → nút hiện "!" nếu có rương chưa nhận
            UpdateExp(); UpdateMoney(); UpdatePk(); UpdateParty(); UpdateQuests();
            TutorialWindow.ShowIfFirstTime(); // lần đầu nhân vật này vào game
        }

        private void UpdateExp()
        {
            if (_expFill == null) return;
            var c = GameData.Me;
            float pct = c.expToNext > 0 ? Mathf.Clamp01((float)c.exp / c.expToNext) : 0;
            _expFill.fillAmount = pct;
            _levelText.text = c.level > 0 ? c.level.ToString() : "";
            _expText.text = $"Cấp {c.level}   EXP {pct * 100:F1}%   ({c.exp:N0}/{c.expToNext:N0})" +
                            (c.potential > 0 ? $"   <color=#fd5>+{c.potential} điểm tiềm năng (C)</color>" : "") +
                            (c.skillPoints > 0 ? $"   <color=#7cf>+{c.skillPoints} điểm kỹ năng (K)</color>" : "");
        }

        private void UpdateMoney()
        {
            if (_money == null) return;
            var c = GameData.Me;
            _money.text = $"Yên {c.yen:N0}   <color=#7cf>Xu {c.xu:N0}</color>   <color=#f8c>Lượng {c.luong:N0}</color>" +
                          (c.pkPoint > 0 ? $"   <color=#f55>PK {c.pkPoint}</color>" : "");
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
            if (_pkBtn == null) return;
            bool on = GameData.MyPkMode == 1;
            _pkBtn.SetLabel(on ? "Đồ sát" : "Hoà bình");
            _pkBtn.image.color = on ? new Color(0.75f, 0.2f, 0.15f, 1f) : UIKit.ButtonColor;
            UpdateMoney();
        }

        // ================= Banner / sự kiện =================

        /// <summary>Hiện dòng chữ lớn giữa màn hình trong vài giây (gọi được từ mọi nơi).</summary>
        public static void Banner(string text, float seconds = 3f)
        {
            if (_instance == null || _instance._banner == null || string.IsNullOrEmpty(text)) return;
            _instance._banner.text = text;
            _instance._bannerUntil = Time.time + seconds;
        }

        private void UpdateBanner()
        {
            float left = _bannerUntil - Time.time;
            _banner.gameObject.SetActive(left > 0);
            if (left > 0) _banner.alpha = Mathf.Clamp01(left / 0.6f);
        }

        private static string Clock(float endTime)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(endTime - Time.time));
            return $"{s / 60:00}:{s % 60:00}";
        }

        private void UpdateEventPanel()
        {
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
                string name = GameData.EventType == 1 ? "Boss thế giới" : "Lôi đài";
                string phase = GameData.EventState == 1 ? (GameData.EventType == 2 ? "đăng ký" : "sắp xuất hiện") : "đang diễn ra";
                sb.Append($"<b>{name}</b> {phase}");
                if (GameData.EventEndTime > Time.time) sb.Append($"  <color=#fd5>{Clock(GameData.EventEndTime)}</color>");
                if (GameData.EventType == 2 && GameData.EventState == 2 && GameData.ArenaScore.Count > 0)
                    for (int i = 0; i < Mathf.Min(5, GameData.ArenaScore.Count); i++)
                        sb.Append($"\n{i + 1}. {GameData.ArenaScore[i].Key} — {GameData.ArenaScore[i].Value} hạ");
            }
            // Sự kiện đã hết giờ mà chưa nhận gói kết thúc (vd đổi map) → tự ẩn sau 5 giây
            if (GameData.EventState != 0 && GameData.EventEndTime > 0 && Time.time > GameData.EventEndTime + 5 && GameData.EventType == 1 && GameData.EventState == 1)
                GameData.EventState = 0;
            _eventText.text = sb.ToString();
            _eventText.transform.parent.gameObject.SetActive(sb.Length > 0);
        }

        /// <summary>Bảng theo dõi nhiệm vụ: tối đa 3 nhiệm vụ đang làm + tiến độ; chưa có thì nhắc gặp Hokage.</summary>
        private void UpdateQuests()
        {
            if (_questText == null) return;
            var sb = new StringBuilder("<color=#fd5><b>Nhiệm vụ</b></color>  <size=12><color=#9aa>(bấm để xem)</color></size>");
            int shown = 0;
            foreach (var kv in GameData.MyQuests)
            {
                if (shown >= 3) break;
                if (!GameData.Quests.TryGetValue(kv.Key, out var q)) continue;
                int progress = q.type == 2 ? Mathf.Min(q.targetCount, GameData.CountItem(q.targetId)) : kv.Value[0];
                bool done = kv.Value[1] == 1 || (q.type == 2 && progress >= q.targetCount);
                string goal = q.type switch
                {
                    0 => $"Hạ {GameData.MobName(q.targetId)} {progress}/{q.targetCount}",
                    1 => $"Gặp {GameData.NpcName(q.targetId)}",
                    2 => $"{GameData.ItemName(q.targetId)} {progress}/{q.targetCount}",
                    _ => ""
                };
                sb.Append($"\n<b>{q.name}</b>\n  ");
                sb.Append(done ? $"<color=#7f7>Xong → trả cho {GameData.NpcName(q.npcId)}</color>" : $"<color=#ddd>{goal}</color>");
                shown++;
            }
            if (shown == 0) sb.Append("\n<color=#ddd>Gặp <b>Hokage</b> ở Làng Lá (bấm Nói / F) để nhận nhiệm vụ.</color>");
            _questText.text = sb.ToString();
        }

        private void UpdateParty()
        {
            if (_partyText == null) return;
            if (GameData.Party.Count == 0) { _partyText.text = ""; return; }
            var sb = new StringBuilder("<color=#fd5>Nhóm</color>\n");
            foreach (var m in GameData.Party)
            {
                if (m.id == LocalPlayerState.Id) continue;
                float pct = m.maxHp > 0 ? (float)m.hp / m.maxHp : 0;
                string col = pct > 0.5f ? "#6f6" : pct > 0.2f ? "#fd5" : "#f55";
                sb.Append($"{m.name} <color=#9aa>Lv{m.level}</color>  <color={col}>{Mathf.RoundToInt(pct * 100)}%</color>\n");
            }
            _partyText.text = sb.ToString();
        }

        // ================= Tương tác người chơi =================
        private ITargetable CurrentTarget()
        {
            if (_targeting == null) _targeting = FindAnyObjectByType<PlayerTargeting>();
            return _targeting != null ? _targeting.currentTarget : null;
        }

        private void UpdateInteract()
        {
            var t = CurrentTarget();
            bool show = t is Component c && c != null && t.GetTargetType() == TargetType.Player;
            if (_interactBtn.gameObject.activeSelf != show) _interactBtn.gameObject.SetActive(show);
        }

        private void OpenInteract()
        {
            var t = CurrentTarget();
            if (t is Component c && c != null && t.GetTargetType() == TargetType.Player)
                GameWindow.Get<PlayerActionWindow>().ShowFor(t.GetId(), t.GetTargetName());
        }
    }
}
