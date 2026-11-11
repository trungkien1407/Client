using Assets.Script.Core;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// THÚ CƯNG / THÚ CƯỠI (GĐ27 + GĐ28, phím P · nút ở bảng Nhân vật) — server gửi PET_LIST (GameData.Pets).
    ///   Trái : các con đang có (tối đa 6) — màu tên theo độ hiếm, [Cưỡi] = con đang mang (ngồi trên lưng, phím R lên / xuống).
    ///   Phải : hình động (Spine trong UI, dáng theo cấp) + chỉ số + DÒNG TIỀM NĂNG (GĐ28, tăng theo cấp) + [Cưỡi / Cất] · [Phóng sinh] (hỏi lại) ·
    ///          cho ăn bằng thức ăn đang có trong túi (Ăn 1 / Ăn 10 — server dừng sớm nếu chạm trần cấp chủ).
    /// Có trứng: bấm "Dùng" trong Hành trang → nở → cửa sổ này tự mở (PetNetwork).
    /// </summary>
    public class PetWindow : GameWindow
    {
        private static readonly int[] Foods = { 143, 144 };   // khớp server PetRules.FOOD_EXP
        private static readonly int[] FoodExp = { 300, 2000 };
        private static readonly int[] Herbs = { 145, 146 };   // GĐ28 hồi thể lực — khớp server PetRules.STAMINA_ITEMS
        private static readonly int[] HerbSta = { 200, 500 };

        [SerializeField] private RectTransform _list, _preview, _foods;
        [SerializeField] private TextMeshProUGUI _head, _detail, _note;
        [SerializeField] private Button _equip, _release;
        private int _selUid = -1;
        private SkeletonGraphic _sg;
        private int _sgArt = -1, _sgStage = -1;
        private static Material _uiMat;

        protected override void Configure()
        {
            Title = "Thú cưng";
            HotKey = Key.P;
            Size = new Vector2(720, 540);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _head = UIKit.Text("Head", body, "", 16, TextAlignmentOptions.MidlineLeft, UIKit.ButtonHot);
            _head.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(270, 24), new Vector2(0, 1));
            _list = UIKit.ScrollList("List", body, 4);
            ((RectTransform)_list.parent).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -26), new Vector2(270, 380), new Vector2(0, 1));

            var box = UIKit.Panel("PreviewBox", body, new Color(0, 0, 0, 0.35f)).rectTransform;
            box.Place(new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(400, 150), new Vector2(1, 1));
            _preview = UIKit.Rect("Preview", box).Fill(6, 6, 6, 6);
            box.gameObject.AddComponent<RectMask2D>();

            _detail = UIKit.Text("Detail", body, "", 14, TextAlignmentOptions.TopLeft);
            _detail.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -154), new Vector2(400, 162), new Vector2(1, 1));
            _equip = UIKit.Button("Equip", body, "Cưỡi con này", null, 15);
            ((RectTransform)_equip.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-206, -320), new Vector2(190, 36), new Vector2(1, 1));
            _release = UIKit.Button("Release", body, "Phóng sinh", null, 15);
            ((RectTransform)_release.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -320), new Vector2(190, 36), new Vector2(1, 1));
            _foods = UIKit.ScrollList("Foods", body, 4);
            ((RectTransform)_foods.parent).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -362), new Vector2(400, 94), new Vector2(1, 1));

            _note = UIKit.Text("Note", body, "", 13, TextAlignmentOptions.MidlineLeft, UIKit.DimText);
            _note.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), Vector2.zero, new Vector2(0, 40), new Vector2(0.5f, 0));
        }

        protected override void Bind()
        {
            _equip.onClick.AddListener(() =>
            {
                var p = Selected();
                if (p != null) GameActions.PetEquip(p.on ? 0 : p.uid);
            });
            _release.onClick.AddListener(() =>
            {
                var p = Selected();
                if (p == null) return;
                ConfirmWindow.Ask($"Phóng sinh <b>{p.ColoredName}</b> cấp {p.level}? Thú sẽ mất hẳn, không lấy lại được.", "Phóng sinh",
                    () => GameActions.PetRelease(p.uid));
            });
            GameData.OnChanged += k => { if (k == DataKind.Pets || k == DataKind.Inventory || k == DataKind.Templates || k == DataKind.Character) RefreshIfOpen(); };
        }

        /// <summary>Mỗi lần mở: xin danh sách mới (EXP từ quái server không gửi từng con, chỉ gửi khi lên cấp).</summary>
        private void OnEnable()
        {
            if (Player.LocalPlayerState.Id >= 0) GameActions.PetListRequest();
        }

        /// <summary>Vừa nở → mở cửa sổ, chọn con mới nhất.</summary>
        public void ShowNewest()
        {
            if (GameData.Pets.Count > 0) _selUid = GameData.Pets[GameData.Pets.Count - 1].uid;
            if (!IsOpen) Show(); else Refresh();
        }

        private PetInfo Selected()
        {
            foreach (var p in GameData.Pets) if (p.uid == _selUid) return p;
            if (GameData.Pets.Count == 0) return null;
            var on = GameData.MyPet ?? GameData.Pets[0];
            _selUid = on.uid;
            return on;
        }

        protected override void Refresh()
        {
            _head.text = $"Thú cưng {GameData.Pets.Count}/{GameData.PetMax}";
            _note.text = "Trứng: Thợ Rèn (yên), Hokage (xu / lượng), rơi từ quái tinh anh / boss. Con đang mang nhận 10% EXP quái bạn hạ, "
                       + "cấp thú không vượt cấp bạn; mỗi 10 cấp đổi dáng.";
            var sel = Selected();

            // ---- danh sách ----
            UIKit.Clear(_list);
            foreach (var p in GameData.Pets)
            {
                var pp = p;
                bool hot = sel != null && p.uid == sel.uid;   // ô đang chọn nền cam → chữ tối cho dễ đọc
                string tag = p.on ? (hot ? " <color=#0a5a0a>[Cưỡi]</color>" : " <color=#7f7>[Cưỡi]</color>") : "";
                string name = hot ? $"<b><color=#2a1400>{p.name}</color></b>" : p.ColoredName;
                string sub = hot ? "#4a3010" : "#9aa";
                var b = UIKit.Button("P", _list, $"{name}{tag}\n<size=12><color={sub}>Cấp {p.level} · {PetInfo.RarityNames[Mathf.Clamp(p.rarity, 0, 3)]} · {PetInfo.KindNames[Mathf.Clamp(p.kind, 0, 3)]}</color></size>",
                    () => { _selUid = pp.uid; Refresh(); }, 15).Height(52);
                b.image.color = hot ? UIKit.ButtonHot : UIKit.SlotColor;
            }
            if (GameData.Pets.Count == 0)
                UIKit.Text("None", _list, "<color=#888>Chưa có thú cưng. Dùng Trứng Thú Cưng trong Hành trang để nở.</color>", 14).Height(60);

            // ---- chi tiết ----
            _equip.gameObject.SetActive(sel != null);
            _release.gameObject.SetActive(sel != null);
            if (sel == null)
            {
                _detail.text = "";
                SetPreview(0, 0);
                UIKit.Clear(_foods);
                return;
            }
            _equip.SetLabel(sel.on ? "Cất (thôi cưỡi)" : "Cưỡi con này");
            string exp = sel.expNext > 0 ? $"EXP {sel.exp:N0} / {sel.expNext:N0} ({100f * sel.exp / sel.expNext:F0}%)" : "Cấp tối đa";
            bool capped = sel.expNext > 0 && sel.level >= GameData.Me.level && sel.exp >= sel.expNext - 1;
            var sb = new System.Text.StringBuilder();
            sb.Append($"<b>{sel.ColoredName}</b>  <color=#9aa>{PetInfo.RarityNames[Mathf.Clamp(sel.rarity, 0, 3)]} · loại {PetInfo.KindNames[Mathf.Clamp(sel.kind, 0, 3)]} · dáng {sel.stage}/4</color>\n");
            sb.Append($"Cấp {sel.level}   {exp}{(capped ? "  <color=#f77>(chờ bạn lên cấp)</color>" : "")}\n");
            if (sel.stamina >= 0)   // GĐ28 thể lực: hết = không cưỡi được
            {
                string sc = sel.stamina <= 0 ? "#ff6b6b" : sel.stamina < 200 ? "#ffb35a" : "#9fe39f";
                sb.Append($"Thể lực <color={sc}>{sel.stamina} / {sel.staminaMax}</color> <size=12><color=#888>(−50 mỗi ngày{(sel.stamina <= 0 ? " · HẾT: không cưỡi được" : "")})</color></size>\n");
            }
            sb.Append(sel.on ? "<color=#7f7>Đang cưỡi (chạy nhanh hơn 30%, phím R lên / xuống) — cộng cho bạn:</color>\n" : "<color=#9aa>Khi cưỡi cộng cho bạn:</color>\n");
            if (sel.bonus[2] > 0) sb.Append($"+{sel.bonus[2]} sát thương  ");
            if (sel.bonus[0] > 0) sb.Append($"+{sel.bonus[0]} HP tối đa  ");
            if (sel.bonus[1] > 0) sb.Append($"+{sel.bonus[1]} MP tối đa  ");
            if (sel.bonus[3] > 0) sb.Append($"+{sel.bonus[3]} phòng thủ");
            // GĐ28 dòng tiềm năng: tung lúc nở, mạnh dần theo cấp thú; chất cao (≥ 70%) tô vàng
            if (sel.pot.Count > 0)
            {
                sb.Append("\n<color=#fd5>Tiềm năng</color> <size=12><color=#9aa>(tăng theo cấp thú)</color></size>");
                for (int i = 0; i < sel.pot.Count; i++)
                {
                    var l = sel.pot[i];
                    string c = l.quality >= 70 ? "#ffd75a" : l.quality >= 40 ? "#9fe39f" : "#dddddd";
                    sb.Append(i % 2 == 0 ? "\n" : "    ");
                    sb.Append($"<color={c}>{PetInfo.PotText(l)}</color> <size=11><color=#888>chất {l.quality}%</color></size>");
                }
            }
            _detail.text = sb.ToString();
            SetPreview(sel.art, sel.stage);

            // ---- thức ăn trong túi ----
            UIKit.Clear(_foods);
            int shown = 0;
            // GĐ28 hồi thể lực (chỉ con đang cưỡi — server hồi cho con đang mang)
            for (int i = 0; i < Herbs.Length && sel.on; i++)
            {
                int herb = Herbs[i], have = GameData.CountItem(herb);
                if (have <= 0) continue;
                var row = UIKit.Panel("H", _foods, UIKit.SlotColor).Height(38);
                UIKit.Text("T", row.transform, $"{GameData.ColoredName(herb, 0)} <color=#fd5>x{have}</color> <size=12><color=#9aa>+{HerbSta[i]} thể lực</color></size>", 14)
                    .rectTransform.Fill(44, 80, 2, 2);
                ItemIcon.ForItem(row, herb, ItemIcon.Place.Left, 30);
                var use = UIKit.Button("U", row.transform, "Dùng", () => GameActions.UseItem(herb), 13);
                ((RectTransform)use.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(70, 30), new Vector2(1, 0.5f));
                use.interactable = sel.stamina < sel.staminaMax;
                shown++;
            }
            for (int i = 0; i < Foods.Length; i++)
            {
                int food = Foods[i], have = GameData.CountItem(food);
                if (have <= 0) continue;
                var row = UIKit.Panel("F", _foods, UIKit.SlotColor).Height(38);
                UIKit.Text("T", row.transform, $"{GameData.ColoredName(food, 0)} <color=#fd5>x{have}</color> <size=12><color=#9aa>+{FoodExp[i]} EXP</color></size>", 14)
                    .rectTransform.Fill(44, 150, 2, 2);
                ItemIcon.ForItem(row, food, ItemIcon.Place.Left, 30);
                int uid = sel.uid;
                var e1 = UIKit.Button("E1", row.transform, "Ăn 1", () => GameActions.PetFeed(uid, food, 1), 13);
                ((RectTransform)e1.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-78, 0), new Vector2(66, 30), new Vector2(1, 0.5f));
                var e10 = UIKit.Button("E10", row.transform, "Ăn 10", () => GameActions.PetFeed(uid, food, Mathf.Min(10, have)), 13);
                ((RectTransform)e10.transform).Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-4, 0), new Vector2(70, 30), new Vector2(1, 0.5f));
                e1.interactable = e10.interactable = !capped && sel.expNext > 0;
                shown++;
            }
            if (shown == 0) UIKit.Text("NoFood", _foods, "<color=#888>Chưa có thức ăn (Thịt Tươi bán ở Thợ Rèn, Đùi Thịt Hun Khói ở Hokage / rơi từ boss). Hồi thể lực: Linh Thảo (rơi ở phó bản), Cỏ Hồi Sức (Hokage).</color>", 13).Height(40);
        }

        // ==========================================
        // HÌNH ĐỘNG TRONG UI (SkeletonGraphic) — chỉ dựng lại khi đổi con / đổi dáng
        // ==========================================
        private void SetPreview(int art, int stage)
        {
            if (art == _sgArt && stage == _sgStage && (_sg != null || art <= 0)) return;
            _sgArt = art; _sgStage = stage;
            if (_sg != null) { Destroy(_sg.gameObject); _sg = null; }
            UIKit.Clear(_preview);
            if (art <= 0) return;
            AssetSource.Load<SkeletonDataAsset>("Pet_" + art, $"Pets/pet{art}/pet{art}_SkeletonData", data =>
            {
                if (this == null || art != _sgArt) return;   // đã chọn con khác trong lúc tải
                if (data == null || UiMaterial() == null)
                {
                    UIKit.Text("NoArt", _preview, "<color=#888>(chưa có hình thú — máy dev: Tools → Naruto → Demo art → 4)</color>", 13);
                    return;
                }
                _sg = SkeletonGraphic.NewSkeletonGraphicGameObject(data, _preview, UiMaterial());
                _sg.raycastTarget = false;
                var skin = _sg.Skeleton.Data.FindSkin("lv" + Mathf.Clamp(stage, 1, 4));
                if (skin != null) { _sg.Skeleton.SetSkin(skin); _sg.Skeleton.SetSlotsToSetupPose(); }
                if (_sg.Skeleton.Data.FindAnimation("idle") != null) _sg.AnimationState.SetAnimation(0, "idle", true);
                _sg.Update(0f);
                Fit(_sg, _preview.rect.size);
            });
        }

        /// <summary>Thu / phóng hình vừa khung xem trước (khung bộ xương ở tư thế đầu × 100 px / đơn vị của canvas).</summary>
        private static void Fit(SkeletonGraphic sg, Vector2 box)
        {
            var sk = sg.Skeleton;
            sk.UpdateWorldTransform();
            float[] buf = null;
            sk.GetBounds(out float x, out float y, out float w, out float h, ref buf);
            float ppu = sg.canvas != null ? sg.canvas.referencePixelsPerUnit : 100f;
            if (w <= 0 || h <= 0) return;
            float s = Mathf.Min(box.x / (w * ppu), box.y / (h * ppu)) * 0.9f;
            var rt = sg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.localScale = new Vector3(s, s, 1f);
            rt.anchoredPosition = -new Vector2(x + w / 2f, y + h / 2f) * ppu * s;
        }

        /// <summary>Vật liệu Spine cho UI (bản sao của spine-unity trong Resources/SpineUI để chắc chắn có trong bản build).</summary>
        private static Material UiMaterial()
        {
            if (_uiMat == null) _uiMat = Resources.Load<Material>("SpineUI/SkeletonGraphicDefault");
            return _uiMat;
        }
    }
}
