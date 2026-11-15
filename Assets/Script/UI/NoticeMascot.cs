using System.Collections.Generic;
using Assets.Script.Core;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using Spine.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// THÚ ĐƯA TIN (2026-11-15): thông báo quan trọng không chỉ nằm trong khung chat (nhiều người không đọc) mà có 1 con thú
    /// treo trên sợi dây HẠ XUỐNG từ mép trên màn hình, cầm bong bóng chữ, vài giây sau kéo lên lại.
    ///
    /// Nguồn tin: mọi tin "Hệ thống" server gửi (ChatBox.OnChat kênh 3) trừ vài loại lặt vặt (QuietPrefixes) + tin client tự gọi Show.
    /// Nhiều tin dồn dập → xếp hàng (tối đa MaxQueue, bỏ tin cũ nhất), tin trùng tin đang hiện thì chỉ kéo dài thời gian.
    /// Bấm vào bong bóng = cất ngay. Hình thú: thú đang mang → con đầu tiên trong túi → DefaultArt; không có hình (bản không có
    /// art demo) → vòng tròn "!" vẽ bằng code.
    /// </summary>
    public class NoticeMascot : MonoBehaviour, IPointerClickHandler
    {
        private const int MaxQueue = 4;
        private const int DefaultArt = 1024;           // thú mặc định khi người chơi chưa có thú
        private const float DropTime = 0.55f, RiseTime = 0.4f;
        private const float RestY = -118f;             // vị trí dừng (dưới hàng nút menu, so với mép trên)
        private const float HideY = 260f;              // nằm khuất trên mép màn hình

        /// <summary>Tin bắt đầu bằng các chữ này chỉ vào chat (lặt vặt / lặp liên tục khi tự động đánh).</summary>
        private static readonly string[] QuietPrefixes =
        {
            "Chờ ", "Hoạt động: +", "[GM]", "Vật phẩm của người khác", "Cho ăn ", "Đã nhận ", "Đã mời ", "Đã gửi lời mời",
        };

        private static NoticeMascot _instance;
        private readonly List<string> _queue = new List<string>();
        private RectTransform _root, _petBox, _bubble, _rope;
        private TextMeshProUGUI _text;
        private SkeletonGraphic _sg;
        private int _sgArt = -1;
        private string _current;
        private float _t;                 // thời gian trong pha hiện tại
        private float _hold;              // thời gian đứng yên của tin hiện tại
        private int _phase;               // 0 ẩn · 1 hạ xuống · 2 đứng · 3 kéo lên

        /// <summary>Tin đang hiện (autotest kiểm).</summary>
        public static string Current => _instance != null && _instance._phase != 0 ? _instance._current : null;

        /// <summary>Tin "Hệ thống" từ server: lọc tin lặt vặt rồi đưa thú xuống.</summary>
        public static void Offer(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            foreach (var q in QuietPrefixes) if (text.StartsWith(q)) return;
            Show(text);
        }

        /// <summary>Đưa 1 tin cho thú mang xuống (gọi được từ mọi nơi, kể cả khi chưa vào game — khi đó bỏ qua).</summary>
        public static void Show(string text)
        {
            if (string.IsNullOrEmpty(text) || Player.LocalPlayerState.Id < 0) return;
            var m = Ensure();
            if (m == null) return;
            if (m._phase != 0 && m._current == text) { if (m._phase == 3) m.Begin(text); else m._hold = Mathf.Max(m._hold, m._t + 2f); return; }
            if (m._queue.Contains(text)) return;
            m._queue.Add(text);
            if (m._queue.Count > MaxQueue) m._queue.RemoveAt(0);
            if (m._phase == 0) m.Next();
        }

        private static NoticeMascot Ensure()
        {
            if (_instance != null) return _instance;
            var root = UIRoot.Instance;
            if (root == null) return null;
            var rt = UIKit.Rect("NoticeMascot", root.transform);
            rt.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-60, HideY), new Vector2(560, 120), new Vector2(0.5f, 1));
            _instance = rt.gameObject.AddComponent<NoticeMascot>();
            _instance.Build(rt);
            return _instance;
        }

        private void Build(RectTransform rt)
        {
            _root = rt;
            // sợi dây từ mép trên xuống đầu thú
            _rope = UIKit.Rect("Rope", rt);
            _rope.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(58, 0), new Vector2(2, 800), new Vector2(0.5f, 0));
            var ri = _rope.gameObject.AddComponent<Image>();
            ri.sprite = UIKit.WhiteSprite;
            ri.color = new Color(0.95f, 0.85f, 0.6f, 0.9f);
            ri.raycastTarget = false;
            _rope.gameObject.AddComponent<NoSkin>();

            _petBox = UIKit.Rect("Pet", rt);
            _petBox.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(116, 104), new Vector2(0, 1));

            _bubble = UIKit.Rect("Bubble", rt);
            _bubble.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(124, -8), new Vector2(420, 60), new Vector2(0, 1));
            var bg = _bubble.gameObject.AddComponent<Image>();
            UIStyle.SetRound(bg, new Color(0.10f, 0.08f, 0.16f, 0.94f), 1.5f);
            UIStyle.AddRing(_bubble, new Color(1f, 0.82f, 0.40f, 0.9f), 1.5f);
            // mũi bong bóng chỉ về con thú
            var tip = UIKit.Rect("Tip", _bubble);
            tip.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -30), new Vector2(14, 14), new Vector2(0.5f, 0.5f));
            tip.localRotation = Quaternion.Euler(0, 0, 45f);
            var ti = tip.gameObject.AddComponent<Image>();
            ti.sprite = UIKit.WhiteSprite;
            ti.color = new Color(0.10f, 0.08f, 0.16f, 0.94f);
            ti.raycastTarget = false;
            tip.SetAsFirstSibling();
            _text = UIKit.Text("Text", _bubble, "", 17, TextAlignmentOptions.MidlineLeft, Color.white);
            _text.rectTransform.Fill(14, 12, 8, 8);
            _text.enableWordWrapping = true;
            _text.raycastTarget = false;
            _root.gameObject.SetActive(false);
        }

        private void Next()
        {
            if (_queue.Count == 0) { _phase = 0; _root.gameObject.SetActive(false); return; }
            string t = _queue[0];
            _queue.RemoveAt(0);
            Begin(t);
        }

        private void Begin(string text)
        {
            _current = text;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();   // trên HUD + cửa sổ
            _text.text = "<color=#ffd56b><b>Thông báo</b></color>\n" + text;
            // bong bóng cao vừa chữ (rộng cố định 420)
            var pref = _text.GetPreferredValues(_text.text, 420f - 26f, 0f);
            _bubble.sizeDelta = new Vector2(420f, Mathf.Clamp(pref.y + 18f, 56f, 200f));
            _hold = Mathf.Clamp(2.6f + text.Length * 0.045f, 3f, 7f);
            if (_queue.Count > 0) _hold = Mathf.Min(_hold, 3f);   // còn tin chờ → mỗi tin ngắn lại
            if (_phase == 0 || _phase == 3) { _phase = 1; _t = 0f; }
            else { _phase = 2; _t = 0f; }
            LoadPet();
        }

        private void Update()
        {
            if (_phase == 0) return;
            _t += Time.unscaledDeltaTime;
            float y;
            switch (_phase)
            {
                case 1:   // hạ xuống, nảy nhẹ cuối đường
                    float k = Mathf.Clamp01(_t / DropTime);
                    y = Mathf.LerpUnclamped(HideY, RestY, EaseOutBack(k));
                    if (k >= 1f) { _phase = 2; _t = 0f; }
                    break;
                case 2:   // đứng, đung đưa nhẹ trên dây
                    y = RestY + Mathf.Sin(_t * 3f) * 3f;
                    _root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_t * 2.2f) * 1.5f);
                    if (_t >= _hold) { _phase = 3; _t = 0f; }
                    break;
                default:  // kéo lên
                    float u = Mathf.Clamp01(_t / RiseTime);
                    y = Mathf.Lerp(RestY, HideY, u * u);
                    if (u >= 1f) { _root.localRotation = Quaternion.identity; Next(); return; }
                    break;
            }
            _root.anchoredPosition = new Vector2(_root.anchoredPosition.x, y);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (_phase == 1 || _phase == 2) { _phase = 3; _t = 0f; }
        }

        private static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        // ------------------------------------------------------------------ hình thú

        private static int PickArt()
        {
            if (GameData.MyPet != null && GameData.MyPet.art > 0) return GameData.MyPet.art;
            if (GameData.Pets.Count > 0 && GameData.Pets[0].art > 0) return GameData.Pets[0].art;
            return DefaultArt;
        }

        private void LoadPet()
        {
            int art = PickArt();
            if (art == _sgArt && (_sg != null || _petBox.childCount > 0)) return;
            _sgArt = art;
            if (_sg != null) { Destroy(_sg.gameObject); _sg = null; }
            UIKit.Clear(_petBox);
            AssetSource.Load<SkeletonDataAsset>("Pet_" + art, $"Pets/pet{art}/pet{art}_SkeletonData", data =>
            {
                if (this == null || art != _sgArt) return;
                var mat = Resources.Load<Material>("SpineUI/SkeletonGraphicDefault");
                if (data == null || mat == null) { Fallback(); return; }
                _sg = SkeletonGraphic.NewSkeletonGraphicGameObject(data, _petBox, mat);
                _sg.raycastTarget = false;
                var skin = _sg.Skeleton.Data.FindSkin("lv1");
                if (skin != null) { _sg.Skeleton.SetSkin(skin); _sg.Skeleton.SetSlotsToSetupPose(); }
                if (_sg.Skeleton.Data.FindAnimation("idle") != null) _sg.AnimationState.SetAnimation(0, "idle", true);
                _sg.Update(0f);
                Fit(_sg, _petBox.rect.size);
            });
        }

        /// <summary>Không có hình thú → vòng tròn vàng có dấu "!".</summary>
        private void Fallback()
        {
            var c = UIKit.Rect("Badge", _petBox);
            c.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72, 72));
            UIStyle.SetRound(c.gameObject.AddComponent<Image>(), new Color(1f, 0.78f, 0.25f), 0.6f);
            var t = UIKit.Text("Mark", c, "!", 44, TextAlignmentOptions.Center, new Color(0.3f, 0.15f, 0.05f));
            t.rectTransform.Fill();
            t.fontStyle = FontStyles.Bold;
        }

        /// <summary>Thu / phóng hình vừa khung (giống PetWindow.Fit). Ảnh thú vốn nhìn sang phải = về phía bong bóng.</summary>
        private static void Fit(SkeletonGraphic sg, Vector2 box)
        {
            var sk = sg.Skeleton;
            sk.UpdateWorldTransform();
            float[] buf = null;
            sk.GetBounds(out float x, out float y, out float w, out float h, ref buf);
            float ppu = sg.canvas != null ? sg.canvas.referencePixelsPerUnit : 100f;
            if (w <= 0 || h <= 0) return;
            float s = Mathf.Min(box.x / (w * ppu), box.y / (h * ppu)) * 0.95f;
            var rt = sg.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.localScale = new Vector3(s, s, 1f);
            rt.anchoredPosition = -new Vector2(x + w / 2f, y + h / 2f) * ppu * s;
        }
    }
}
