using System;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// TRANG TRÍ LẠI MÀN ĐĂNG NHẬP lúc chạy (2026-11-15) — prefab AuthCanvas giữ nguyên, chỉ thêm / sửa bằng code:
    ///   · icon trong ô nhập (người / ổ khoá), nút con mắt hiện / ẩn mật khẩu;
    ///   · ô tích "Ghi nhớ đăng nhập" thật (hộp + dấu tích) thay cho chữ "[X]";
    ///   · nút đóng / quay lại tròn có icon ✕ / ← thay cho hình thoi chữ x;
    ///   · icon trên nút Đăng nhập / Đăng ký; nút "Chơi ngay (khách)" có icon tam giác.
    /// Icon vẽ bằng code (UIIcons) — không cần file ảnh.
    /// </summary>
    public static class AuthSkin
    {
        public static readonly Color IconColor = new Color(0.25f, 0.55f, 0.42f);      // xanh lá đậm (hợp viền ô nhập)
        public static readonly Color GuestColor = new Color(0.98f, 0.62f, 0.20f);     // cam: nút chơi khách nổi bật

        /// <summary>Icon bên trái trong ô nhập; đẩy chữ + chữ gợi ý sang phải cho khỏi đè icon.</summary>
        public static void FieldIcon(TMP_InputField f, Sprite icon)
        {
            if (f == null || f.transform.Find("FieldIcon") != null) return;
            var img = UIIcons.Add(f.transform, icon, 28, IconColor);
            img.name = "FieldIcon";
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.anchoredPosition = new Vector2(22, 0);
            if (f.textViewport != null) f.textViewport.offsetMin = new Vector2(f.textViewport.offsetMin.x + 30, f.textViewport.offsetMin.y);
        }

        /// <summary>Nút con mắt bên phải ô mật khẩu: bấm = hiện / ẩn chữ.</summary>
        public static void EyeToggle(TMP_InputField f)
        {
            if (f == null || f.transform.Find("Eye") != null) return;
            Image icon = null;
            var b = UIKit.Button("Eye", f.transform, "", () =>
            {
                bool hidden = f.contentType == TMP_InputField.ContentType.Password;
                f.contentType = hidden ? TMP_InputField.ContentType.Standard : TMP_InputField.ContentType.Password;
                f.ForceLabelUpdate();
                if (icon != null) icon.sprite = hidden ? UIIcons.EyeOff : UIIcons.Eye;
            }, 12);
            var rt = (RectTransform)b.transform;
            rt.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-6, 0), new Vector2(30, 30), new Vector2(1, 0.5f));
            UIStyle.SetRound(b.image, new Color(0, 0, 0, 0));   // hình bo góc trong suốt → bộ skin chung không phủ hình nút lên
            icon = UIIcons.Add(b.transform, UIIcons.Eye, 28, IconColor);
            if (f.textViewport != null) f.textViewport.offsetMax = new Vector2(f.textViewport.offsetMax.x - 30, f.textViewport.offsetMax.y);
        }

        /// <summary>Nút tròn đỏ có icon (✕ đóng / ← quay lại) thay cho hình cũ. Giữ vị trí, đặt cỡ {size}.</summary>
        public static void RoundIconButton(Button b, Sprite icon, Color bg, float size = 44f)
        {
            if (b == null || b.transform.Find("Icon") != null) return;
            foreach (Transform c in b.transform) c.gameObject.SetActive(false);   // chữ / hình con cũ
            var img = b.image;
            if (img != null)
            {
                img.sprite = UIStyle.Round;
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 0.6f;   // góc bo lớn → gần tròn
                img.color = bg;
            }
            if (b.GetComponent<NoSkin>() == null) b.gameObject.AddComponent<NoSkin>();
            ((RectTransform)b.transform).sizeDelta = new Vector2(size, size);
            UIStyle.AddRing(b.transform, new Color(1f, 0.92f, 0.75f, 0.95f), 0.6f);
            UIIcons.Add(b.transform, icon, size * 0.5f, Color.white);
        }

        /// <summary>Icon nhỏ bên trái chữ của 1 nút có sẵn (chữ dời phải cho cân).</summary>
        public static void ButtonIcon(Button b, Sprite icon, Color color, float size = 20f)
        {
            if (b == null || b.transform.Find("Icon") != null) return;
            var label = b.GetComponentInChildren<TextMeshProUGUI>(true);
            float w = ((RectTransform)b.transform).rect.width;
            var img = UIIcons.Add(b.transform, icon, size, color);
            float textW = label != null ? Mathf.Min(label.preferredWidth, w - size - 16) : 60f;
            img.rectTransform.anchoredPosition = new Vector2(-(textW + size + 6) / 2f + size / 2f, 0);
            if (label != null) label.margin = new Vector4(size + 6, 0, 0, 0);
        }

        /// <summary>
        /// Ô tích "Ghi nhớ đăng nhập": hộp bo góc + dấu tích (bật) ở đầu nút, chữ bên phải. Gọi lại mỗi lần đổi trạng thái.
        /// </summary>
        public static void Checkbox(Button b, bool on, string text, Color textColor)
        {
            if (b == null) return;
            if (b.image != null && b.image.sprite != UIStyle.Round) UIStyle.SetRound(b.image, new Color(0, 0, 0, 0));   // nền trong suốt, skin không phủ
            var box = b.transform.Find("Box") as RectTransform;
            Image tick;
            if (box == null)
            {
                box = UIKit.Rect("Box", b.transform);
                box.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(2, 0), new Vector2(22, 22), new Vector2(0, 0.5f));
                var bg = box.gameObject.AddComponent<Image>();
                UIStyle.SetRound(bg, Color.white, 2.2f);
                bg.raycastTarget = false;
                box.gameObject.AddComponent<NoSkin>();
                UIStyle.AddRing(box, IconColor, 2.2f);
                tick = UIIcons.Add(box, UIIcons.Check, 18, IconColor);
                tick.name = "Tick";
            }
            else tick = box.Find("Tick").GetComponent<Image>();
            tick.enabled = on;
            box.GetComponent<Image>().color = on ? new Color(0.86f, 0.97f, 0.90f) : Color.white;
            b.SetLabel(text);
            var label = b.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.margin = new Vector4(30, 0, 0, 0);
                label.color = textColor;
            }
        }

        /// <summary>
        /// Nút "Chơi ngay" (tài khoản khách) dưới 2 nút Đăng ký / Đăng nhập: dời 2 nút lên 1 chút rồi đặt nút cam rộng bằng cả 2 nút
        /// ngay bên dưới. Toạ độ tính từ khung thật của 2 nút (không phụ thuộc số đo trong prefab).
        /// </summary>
        public static Button GuestButton(GameObject panel, Button left, Button right, Action onClick)
        {
            if (panel == null || left == null || right == null) return null;
            var existing = panel.transform.Find("GuestPlay");
            if (existing != null) return existing.GetComponent<Button>();
            var prt = (RectTransform)panel.transform;
            var lrt = (RectTransform)left.transform; var rrt = (RectTransform)right.transform;
            const float lift = 26f;
            lrt.anchoredPosition += new Vector2(0, lift);
            rrt.anchoredPosition += new Vector2(0, lift);
            // khung 2 nút trong toạ độ của panel
            var c = new Vector3[4];
            lrt.GetWorldCorners(c); Vector3 l0 = prt.InverseTransformPoint(c[0]);
            rrt.GetWorldCorners(c); Vector3 r2 = prt.InverseTransformPoint(c[2]); Vector3 r0 = prt.InverseTransformPoint(c[0]);
            float x0 = Mathf.Min(l0.x, r0.x), x1 = r2.x, bottom = Mathf.Min(l0.y, r0.y);
            var b = UIKit.Button("GuestPlay", panel.transform, "Chơi ngay (khách)", onClick, 18);
            var rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 1f);
            Vector2 center = prt.rect.center;
            rt.anchoredPosition = new Vector2((x0 + x1) / 2f - center.x, bottom - 8f - center.y);
            rt.sizeDelta = new Vector2(x1 - x0, 46f);
            UIStyle.SetRound(b.image, GuestColor, 1.4f);
            b.gameObject.AddComponent<NoSkin>();
            var label = b.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) { label.color = new Color(0.25f, 0.12f, 0.02f); label.fontStyle = FontStyles.Bold; }
            ButtonIcon(b, UIIcons.Play, new Color(0.25f, 0.12f, 0.02f), 22);
            return b;
        }
    }
}
