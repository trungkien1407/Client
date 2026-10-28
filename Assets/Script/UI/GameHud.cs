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
                _v.zoneBtn.SetLabel(string.IsNullOrEmpty(_shownMap) ? zone + " (đổi)" : $"{_shownMap} · {zone}"); // GĐ8: tên map
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
            // Nút "Rời" phó bản (góc phải khung sự kiện, chỉ hiện khi đang ở phó bản) — map phó bản không có cổng ra
            _leaveDungeon = UIKit.Button("LeaveDungeon", _v.eventPanel.transform, "Rời", () =>
                ConfirmWindow.Ask("Rời phó bản về làng? Lượt hôm nay đã dùng, không được trả lại.", "Rời", GameActions.DungeonLeave), 14);
            ((RectTransform)_leaveDungeon.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-4, -4), new Vector2(56, 30), new Vector2(1, 1));
            HudMenuIcons.Apply(_v, MinimapWidth);   // có hình kho → 12 nút menu thành icon (không có → giữ nút chữ)   // icon choáng / chậm / bỏng / tăng sức mạnh / phân thân (cạnh ô cấp)

            // Hành động nút menu — cùng thứ tự GameHudView.MenuLabels
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
            TutorialWindow.ShowIfFirstTime(); // lần đầu nhân vật này vào game
        }

        private void UpdateExp()
        {
            if (_v == null) return;
            var c = GameData.Me;
            float pct = c.expToNext > 0 ? Mathf.Clamp01((float)c.exp / c.expToNext) : 0;
            _v.expFill.fillAmount = pct;
            // "12  76.4%" — số cấp vàng to, % EXP trắng nhỏ ngay sau (đỡ phải nhìn xuống thanh EXP dưới màn hình)
            _v.levelText.text = c.level > 0 ? $"{c.level} <size=72%><color=#f2f2f2>{pct * 100:F1}%</color></size>" : "";
            _v.expText.text = $"Cấp {c.level}   EXP {pct * 100:F1}%   ({c.exp:N0}/{c.expToNext:N0})" +
                            (c.potential > 0 ? $"   <color=#fd5>+{c.potential} điểm tiềm năng (C)</color>" : "") +
                            (c.skillPoints > 0 ? $"   <color=#7cf>+{c.skillPoints} điểm kỹ năng (K)</color>" : "");
        }

        private void UpdateMoney()
        {
            if (_v == null) return;
            var c = GameData.Me;
            _v.money.text = $"Yên {c.yen:N0}   <color=#7cf>Xu {c.xu:N0}</color>   <color=#f8c>Lượng {c.luong:N0}</color>" +
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
                string bosses = GameData.EventType == 1 && GameData.EventState == 2 ? BossList(GameData.EventText) : null;
                if (!string.IsNullOrEmpty(bosses)) sb.Append($"\n<size=13>{bosses}</size>");
                if (GameData.EventType == 2 && GameData.EventState == 2 && GameData.ArenaScore.Count > 0)
                    for (int i = 0; i < Mathf.Min(5, GameData.ArenaScore.Count); i++)
                        sb.Append($"\n{i + 1}. {GameData.ArenaScore[i].Key} — {GameData.ArenaScore[i].Value} hạ");
            }
            // Sự kiện đã hết giờ mà chưa nhận gói kết thúc (vd đổi map) → tự ẩn sau 5 giây
            if (GameData.EventState != 0 && GameData.EventEndTime > 0 && Time.time > GameData.EventEndTime + 5 && GameData.EventType == 1 && GameData.EventState == 1)
                GameData.EventState = 0;
            _v.eventText.text = sb.ToString();
            _v.eventPanel.gameObject.SetActive(sb.Length > 0);
            if (_leaveDungeon != null) _leaveDungeon.gameObject.SetActive(GameData.DungeonState == 2);
        }

        /// <summary>
        /// Danh sách boss đang sống trong chữ EVENT_STATE của server ("… Đang có: A (map, khu 1), B …" / "… Còn: …" /
        /// "… đang xuất hiện: …"). Không có → null.
        /// </summary>
        private static string BossList(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (var key in new[] { "Còn: ", "Đang có: ", "đang xuất hiện: " })
            {
                int i = text.LastIndexOf(key, System.StringComparison.Ordinal);
                if (i >= 0) return text.Substring(i + key.Length).TrimEnd('.', '!', ' ');
            }
            return null;
        }

        /// <summary>Bảng theo dõi nhiệm vụ: tối đa 3 nhiệm vụ đang làm + tiến độ; chưa có thì nhắc gặp Hokage.</summary>
        private void UpdateQuests()
        {
            if (_v == null) return;
            var sb = new StringBuilder("<color=#fd5><b>Nhiệm vụ</b></color>  <size=12><color=#9aa>(bấm để xem)</color></size>");
            int shown = 0;
            foreach (var kv in GameData.MyQuests)
            {
                if (shown >= 3) break;
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
                sb.Append($"\n<b>{q.name}</b>\n  ");
                sb.Append(done ? $"<color=#7f7>Xong → trả cho {GameData.NpcName(q.npcId)}</color>" : $"<color=#ddd>{goal}</color>");
                shown++;
            }
            if (shown == 0) sb.Append("\n<color=#ddd>Gặp <b>Hokage</b> ở Làng Lá (bấm Nói / F) để nhận nhiệm vụ.</color>");
            _v.questText.text = sb.ToString();
        }

        private void UpdateParty()
        {
            if (_v == null) return;
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

        private void UpdateInteract()
        {
            var t = CurrentTarget();
            bool show = t is Component c && c != null && t.GetTargetType() == TargetType.Player;
            if (_v.interactBtn.gameObject.activeSelf != show) _v.interactBtn.gameObject.SetActive(show);
        }

        private void OpenInteract()
        {
            var t = CurrentTarget();
            if (t is Component c && c != null && t.GetTargetType() == TargetType.Player)
                GameWindow.Get<PlayerActionWindow>().ShowFor(t.GetId(), t.GetTargetName());
        }
    }
}
