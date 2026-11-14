using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// 12 NÚT MENU HUD DẠNG ICON (kiểu NSO): nền tròn viền vàng + icon + chữ nhỏ bên dưới, thay cho nút chữ.
    /// Hình: Resources/UISkin/menu_bg + menu_&lt;tên&gt; (demo: Tools → Naruto → Demo art, lấy từ 08_UI/IconMenu, imggame).
    /// Thiếu hình → giữ nút chữ như cũ. Màu nền vẫn báo trạng thái: vàng = hoạt động có quà chưa nhận, đỏ = đang bật Đồ sát
    /// (GameHud đổi image.color như trước). Đồ sát đổi luôn icon (SetPk).
    /// </summary>
    public static class HudMenuIcons
    {
        public const string Bg = "menu_bg", PkOn = "menu_pk";
        /// <summary>Tên hình theo đúng thứ tự GameHudView.MenuLabels.</summary>
        public static readonly string[] Keys =
        {
            "menu_char", "menu_bag", "menu_skill", "menu_quest", "menu_talk", "menu_activity",
            "menu_social", "menu_mail", "menu_top", "menu_guide", "menu_settings", "menu_peace",
        };
        private const float Cell = 62f, Size = 44f, RowH = 62f;
        public const int Columns = 5;

        /// <summary>
        /// Nút ẨN khỏi HUD: Túi (1) và Kỹ năng (2) đã gộp vào nút "Nhân vật" (cửa sổ có tab — CharacterTabs). Phím I / K vẫn dùng được.
        /// Mảng nút 12 ô giữ nguyên (prefab GameHudView không phải xuất lại) — chỉ tắt đi và xếp lại các nút còn lại.
        /// </summary>
        public static bool IsHidden(int i) => i == 1 || i == 2;

        /// <summary>Không có hình kho (nút chữ): tắt nút ẩn + xếp lại {Columns} nút / hàng sang trái minimap.</summary>
        public static void LayoutText(GameHudView v, float minimapW)
        {
            int slot = 0;
            for (int i = 0; i < v.menu.Length; i++)
            {
                var b = v.menu[i];
                if (b == null) continue;
                if (IsHidden(i)) { b.gameObject.SetActive(false); continue; }
                int row = slot / Columns, col = slot % Columns;
                slot++;
                ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-minimapW - col * 78, -8 - row * 40), new Vector2(74, 34), new Vector2(1, 1));
            }
        }

        /// <summary>Đổi các nút chữ thành nút icon. Trả false nếu không có hình (giữ nguyên nút chữ).</summary>
        public static bool Apply(GameHudView v, float minimapW)
        {
            if (v == null || UISkin.Get(Bg) == null || UISkin.Get(Keys[0]) == null) return false;
            int slot = 0;
            for (int i = 0; i < v.menu.Length && i < Keys.Length; i++)
            {
                var b = v.menu[i];
                if (b == null) continue;
                if (IsHidden(i)) { b.gameObject.SetActive(false); continue; }
                int row = slot / Columns, col = slot % Columns;
                slot++;
                // nút = vòng tròn {Size}, căn giữa ô {Cell}; chữ nằm dưới vòng (ngoài khung nút)
                ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(1, 1),
                    new Vector2(-minimapW - col * Cell - (Cell - Size) / 2f, -4 - row * RowH), new Vector2(Size, Size), new Vector2(1, 1));
                b.image.sprite = UISkin.Get(Bg);
                b.image.type = Image.Type.Simple;
                b.image.preserveAspect = true;
                if (b.image.color.r > 0.9f && b.image.color.g > 0.9f) b.image.color = Color.white;   // màu trạng thái (vàng / đỏ) giữ nguyên
                UISkin.KeepLabelColor(b);   // chữ nằm ngoài nền nút → luôn trắng có viền, không theo độ sáng nền

                var icon = b.transform.Find("Icon")?.GetComponent<Image>();
                if (icon == null)
                {
                    icon = UIKit.Rect("Icon", b.transform).Fill(7, 7, 7, 7).gameObject.AddComponent<Image>();
                    icon.raycastTarget = false;
                    icon.preserveAspect = true;
                }
                icon.sprite = UISkin.Get(Keys[i]);
                icon.enabled = icon.sprite != null;

                var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
                if (label != null)
                {
                    label.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -15), new Vector2(Cell + 6, 16), new Vector2(0.5f, 0.5f));
                    label.fontSize = 12;
                    label.enableWordWrapping = false;
                    label.alignment = TextAlignmentOptions.Center;
                    label.color = Color.white;
                    label.outlineWidth = 0.25f;
                    label.outlineColor = Color.black;
                    // nền mờ bo góc sau chữ: chữ trắng không chìm khi phía sau là trời / mây trắng
                    var pill = b.transform.Find("LabelBg") as RectTransform;
                    if (pill == null)
                    {
                        pill = UIKit.Rect("LabelBg", b.transform);
                        var img = pill.gameObject.AddComponent<Image>();
                        img.raycastTarget = false;
                        UIStyle.SetRound(img, new Color(0f, 0f, 0f, 0.45f), 2.5f);
                    }
                    pill.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -15), new Vector2(Cell - 2, 17), new Vector2(0.5f, 0.5f));
                    pill.SetSiblingIndex(label.transform.GetSiblingIndex());   // vẽ trước chữ
                }
            }
            // dòng tiền + nút khu nằm dưới 2 hàng icon
            if (v.money != null) v.money.rectTransform.anchoredPosition = new Vector2(-minimapW, -4 - 2 * RowH - 6);
            return true;
        }

        /// <summary>Đồ sát bật / tắt → icon mặt quỷ / khiên.</summary>
        public static void SetPk(GameHudView v, bool on)
        {
            var icon = v?.menu[GameHudView.MenuPk]?.transform.Find("Icon")?.GetComponent<Image>();
            if (icon == null) return;
            var s = UISkin.Get(on ? PkOn : Keys[GameHudView.MenuPk]);
            if (s != null) icon.sprite = s;
        }
    }
}
