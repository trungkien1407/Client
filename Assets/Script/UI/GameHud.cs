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
    /// HUD BỔ SUNG (dựng bằng code, nằm trên HUD cũ của scene):
    ///  - Thanh EXP dưới đáy màn hình + chữ "Cấp N  x%".
    ///  - 2 hàng nút menu góc phải trên (để chơi trên điện thoại): Nhân vật / Túi / Kỹ năng / Nhiệm vụ / Nói (F)
    ///    và Xã hội (O) / Thư (M) / Xếp hạng / Cài đặt / Hoà bình↔Đồ sát.
    ///  - Dòng tiền: Yên · Xu · Lượng (dưới hàng nút).
    ///  - Banner giữa màn hình (sự kiện, tỉ thí, phó bản) + khung đồng hồ phó bản / boss / lôi đài + bảng điểm lôi đài (góc phải).
    ///  - Khung máu đồng đội (bên trái), chữ "CHOÁNG" khi bị choáng.
    ///  - Nút [Tương tác] khi đang chọn 1 người chơi khác → mời nhóm/giao dịch/kết bạn/tỉ thí...
    ///  - Phím F (hoặc nút "Nói") : nói chuyện với NPC gần nhất. NPC_MENU → hội thoại; SHOP_DATA → cửa hàng.
    /// Tạo tự động trong GameplayBootstrap.
    /// </summary>
    public class GameHud : NetworkListener
    {
        private const float TalkRange = 3.5f;
        /// <summary>[CẦN ĐIỀN nếu đổi minimap] bề rộng minimap góc phải trên của scene — menu xếp sang trái nó.</summary>
        public static float MinimapWidth = 176f;

        private static GameHud _instance;
        private GameObject _root;
        private Image _expFill;
        private TextMeshProUGUI _expText, _money, _banner, _eventText, _partyText, _stun, _questText;
        private Button _pkBtn, _mailBtn, _interactBtn, _zoneBtn;
        private int _shownZone = -2;
        private float _bannerUntil;
        private PlayerTargeting _targeting;

        private void Awake() => _instance = this;

        protected override void RegisterHandlers()
        {
            Listen(Cmd.NPC_MENU, OnNpcMenu);
            Listen(Cmd.SHOP_DATA, OnShopData);
        }

        protected override void Update()
        {
            base.Update();
            bool inGame = LocalPlayerState.Id >= 0;
            if (inGame && _root == null) BuildHud();
            if (_root != null && _root.activeSelf != inGame) _root.SetActive(inGame);
            if (!inGame) return;

            var kb = Keyboard.current;
            if (kb != null && !Combat.ChatBox.IsTyping && kb.fKey.wasPressedThisFrame) TalkToNearestNpc();

            ConfirmWindow.Pump();
            UpdateBanner();
            UpdateEventPanel();
            UpdateInteract();
            _stun.gameObject.SetActive(LocalPlayerState.IsStunned);
            if (_shownZone != LocalPlayerState.ZoneId)
            {
                _shownZone = LocalPlayerState.ZoneId;
                _zoneBtn.SetLabel(_shownZone == 255 ? "Khu riêng" : $"Khu {_shownZone + 1} (đổi)");
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

            // ---- Nút menu: 2 hàng x 5 ----
            string[] labels = { "Nhân vật", "Túi", "Kỹ năng", "Nhiệm vụ", "Nói (F)", "Xã hội", "Thư", "Xếp hạng", "Cài đặt", "Hoà bình" };
            System.Action[] actions =
            {
                () => GameWindow.Get<CharacterWindow>().Toggle(),
                () => GameWindow.Get<InventoryWindow>().Toggle(),
                () => GameWindow.Get<SkillWindow>().Toggle(),
                () => GameWindow.Get<QuestWindow>().Toggle(),
                TalkToNearestNpc,
                () => GameWindow.Get<SocialWindow>().Toggle(),
                () => GameWindow.Get<MailWindow>().Toggle(),
                () => GameWindow.Get<LeaderboardWindow>().Toggle(),
                () => GameWindow.Get<SettingsWindow>().Toggle(),
                TogglePk,
            };
            for (int i = 0; i < labels.Length; i++)
            {
                int row = i / 5, col = i % 5;
                var b = UIKit.Button("Menu" + i, _root.transform, labels[i], actions[i], 15);
                ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-MinimapWidth - col * 92, -8 - row * 40), new Vector2(86, 34), new Vector2(1, 1));
                if (i == 6) _mailBtn = b;
                if (i == 9) _pkBtn = b;
            }

            // ---- Nút đổi khu (ngay dưới minimap) ----
            _zoneBtn = UIKit.Button("Zone", _root.transform, "Khu", () => GameWindow.Get<ZoneWindow>().Toggle(), 14);
            ((RectTransform)_zoneBtn.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -174), new Vector2(MinimapWidth - 16, 30), new Vector2(1, 1));

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
            SettingsWindow.ApplySaved();

            GameData.OnChanged += k =>
            {
                if (k == DataKind.Character || k == DataKind.Money) { UpdateExp(); UpdateMoney(); }
                if (k == DataKind.Pvp) UpdatePk();
                if (k == DataKind.Mails) _mailBtn.SetLabel(GameData.MailUnread > 0 ? $"Thư ({GameData.MailUnread})" : "Thư");
                if (k == DataKind.Party) UpdateParty();
                if (k == DataKind.Quests || k == DataKind.Inventory || k == DataKind.Templates) UpdateQuests();
            };
            UpdateExp(); UpdateMoney(); UpdatePk(); UpdateParty(); UpdateQuests();
            TutorialWindow.ShowIfFirstTime(); // lần đầu nhân vật này vào game
        }

        private void UpdateExp()
        {
            if (_expFill == null) return;
            var c = GameData.Me;
            float pct = c.expToNext > 0 ? Mathf.Clamp01((float)c.exp / c.expToNext) : 0;
            _expFill.fillAmount = pct;
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

        // ================= NPC =================

        /// <summary>Nói chuyện với NPC gần nhân vật nhất (trong tầm). Server kiểm tra lại tầm.</summary>
        public static void TalkToNearestNpc()
        {
            var me = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (me == null) return;
            NpcEntity best = null;
            float bestSqr = TalkRange * TalkRange;
            foreach (var npc in FindObjectsByType<NpcEntity>(FindObjectsSortMode.None))
            {
                float sqr = ((Vector2)(npc.transform.position - me.transform.position)).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = npc; }
            }
            if (best != null) GameActions.NpcTalk(best.GetId());
            else Combat.ChatBox.AddSystem("Không có NPC nào ở gần.");
        }

        /// <summary>NPC_MENU: int npcId, UTF tên, UTF lời thoại, byte n, [UTF lựa chọn] x n</summary>
        private void OnNpcMenu(byte[] data)
        {
            var r = new MessageReader(data);
            int npcId = r.ReadInt();
            string name = r.ReadUTF();
            string text = r.ReadUTF();
            int n = r.ReadByte();
            var opts = new List<string>();
            for (int i = 0; i < n; i++) opts.Add(r.ReadUTF());
            r.Cleanup();
            GameWindow.Get<NpcDialogWindow>().ShowMenu(npcId, name, text, opts);
        }

        /// <summary>SHOP_DATA: int npcId, short count, [int itemId, int price, byte currency(0 yên/1 xu/2 lượng)] x count</summary>
        private void OnShopData(byte[] data)
        {
            var r = new MessageReader(data);
            int npcId = r.ReadInt();
            int n = r.ReadShort();
            var goods = new List<ShopGood>();
            for (int i = 0; i < n; i++) goods.Add(new ShopGood { itemId = r.ReadInt(), price = r.ReadInt(), currency = r.ReadByte() });
            r.Cleanup();
            GameWindow.Get<ShopWindow>().ShowShop(npcId, goods);
        }
    }
}
