using Assets.Script.Constants;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// ICON TRẠNG THÁI của nhân vật mình (dưới khung máu): choáng · chậm · bỏng · tăng sức mạnh · phân thân,
    /// mỗi icon có vòng tối quay theo thời gian còn lại + số giây. Server gửi EFFECT → EventNetwork ghi vào LocalPlayerState.Effects.
    /// Hình: Resources/UISkin/fx_1 … fx_5 (demo: Tools → Naruto → Demo art, icon trong 08_UI/IconMenu) nhuộm màu hiệu ứng;
    /// không có hình → ô màu + chữ viết tắt. Dựng bằng code (GameHud.CreateHud), không cần prefab.
    /// </summary>
    public class StatusIconsHud : MonoBehaviour
    {
        private const int Kinds = 5;             // mã 1..5 (StatusEffect)
        private const float Size = 34f, Gap = 4f;

        public static readonly string[] Short = { "", "Ch", "Cm", "B", "+", "PT" };
        public static readonly Color[] Colors =
        {
            Color.white, new Color(1f, 0.9f, 0.2f), new Color(0.4f, 0.8f, 1f), new Color(1f, 0.45f, 0.1f), new Color(0.5f, 1f, 0.5f), new Color(0.75f, 0.6f, 1f),
        };

        private readonly Image[] _icons = new Image[Kinds + 1];
        private readonly Image[] _masks = new Image[Kinds + 1];
        private readonly TextMeshProUGUI[] _secs = new TextMeshProUGUI[Kinds + 1];

        /// <summary>Tạo dưới {parent}, góc trên-trái tại {pos}.</summary>
        public static StatusIconsHud Create(Transform parent, Vector2 pos)
        {
            var rt = UIKit.Rect("StatusIcons", parent);
            rt.Place(new Vector2(0, 1), new Vector2(0, 1), pos, new Vector2(Kinds * (Size + Gap), Size + 14), new Vector2(0, 1));
            return rt.gameObject.AddComponent<StatusIconsHud>();
        }

        private void Awake()
        {
            for (int k = 1; k <= Kinds; k++)
            {
                var sprite = UISkin.Get("fx_" + k);
                var bg = UIKit.Panel("Fx" + k, transform, sprite != null ? Colors[k] : Colors[k] * new Color(0.55f, 0.55f, 0.55f, 1f));
                bg.raycastTarget = false;
                if (sprite != null) { bg.sprite = sprite; bg.type = Image.Type.Simple; }
                bg.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(Size, Size), new Vector2(0, 1));
                if (sprite == null)
                {
                    var t = UIKit.Text("Name", bg.transform, Short[k], 14, TextAlignmentOptions.Center, Color.white);
                    t.rectTransform.Fill();
                }
                var mask = UIKit.Panel("Mask", bg.transform, new Color(0, 0, 0, 0.55f));
                mask.raycastTarget = false;
                mask.rectTransform.Fill();
                mask.sprite = UIKit.WhiteSprite;
                mask.type = Image.Type.Filled;
                mask.fillMethod = Image.FillMethod.Radial360;
                mask.fillOrigin = (int)Image.Origin360.Top;
                mask.fillClockwise = false;
                var secs = UIKit.Text("Secs", bg.transform, "", 13, TextAlignmentOptions.Bottom, Color.white);
                secs.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, -14), new Vector2(0, 14), new Vector2(0.5f, 0));
                secs.fontStyle = FontStyles.Bold;
                _icons[k] = bg; _masks[k] = mask; _secs[k] = secs;
                bg.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            float x = 0f;
            for (int k = 1; k <= Kinds; k++)
            {
                float left = LocalPlayerState.EffectLeft((StatusEffect)k), total = LocalPlayerState.EffectTotal((StatusEffect)k);
                bool on = left > 0f && !LocalPlayerState.IsDead;
                var go = _icons[k].gameObject;
                if (go.activeSelf != on) go.SetActive(on);
                if (!on) continue;
                _icons[k].rectTransform.anchoredPosition = new Vector2(x, 0);   // xếp liền nhau theo thứ tự đang có
                x += Size + Gap;
                _masks[k].fillAmount = 1f - Mathf.Clamp01(left / Mathf.Max(0.01f, total));
                _secs[k].text = left >= 1f ? Mathf.CeilToInt(left).ToString() : left.ToString("0.0");
            }
        }
    }
}
