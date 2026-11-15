using System.Collections.Generic;
using Assets.Script.Core;
using Assets.Script.Manager;
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
    /// nhỏ treo trên sợi dây HẠ XUỐNG từ mép trên màn hình, dừng NGANG ĐẦU nhân vật (lệch trái chút), bong bóng chữ trên đầu thú,
    /// vài giây sau kéo lên lại. Thú đi theo nhân vật khi nhân vật chạy (vị trí tính lại mỗi khung hình).
    ///
    /// Nguồn tin: tin "Hệ thống" server gửi (ChatBox.OnChat kênh 3) CÓ cụm quan trọng (ImportantKeys) + tin client tự gọi Show.
    /// Nhiều tin dồn dập → xếp hàng (tối đa MaxQueue, bỏ tin cũ nhất), tin trùng tin đang hiện thì chỉ kéo dài thời gian.
    /// Bấm vào bong bóng = cất ngay. Hình thú: luôn 1 con cố định nhỏ dễ thương (MascotArt = gấu mèo); không có hình
    /// (bản không có art demo) → vòng tròn "!" vẽ bằng code.
    /// </summary>
    public class NoticeMascot : MonoBehaviour, IPointerClickHandler
    {
        private const int MaxQueue = 4;
        private const int MascotArt = 1088;            // gấu mèo (Racoon) — nhỏ, mặt tròn dễ thương
        private const float DropTime = 0.7f, RiseTime = 0.45f;
        private const float BubbleW = 280f;
        private static readonly Vector2 PetSize = new Vector2(92, 62);    // gấu mèo 4 chân → khung ngang (nhỏ cho dễ thương, 2026-11-15)
        private const float SideGap = 70f;             // thú đứng lệch sang trái nhân vật bao nhiêu (đơn vị UI)

        /// <summary>
        /// CHỈ tin có 1 trong các cụm này mới cho thú mang xuống (2026-11-15: trước nhận hết trừ vài loại → hạ xuống liên tục).
        /// Tin khác (tinh anh / thủ lĩnh xuất hiện, người khác nâng cấp / nở thú hiếm, kết bạn, hoạt động +điểm…) chỉ vào chat.
        /// So không phân biệt hoa thường.
        /// </summary>
        private static readonly string[] ImportantKeys =
        {
            "boss ", "lôi đài", "bảo trì", "máy chủ",              // sự kiện / máy chủ
            "chưa cầm vũ khí", "hành trang đã đầy", "không đủ chỗ", "rương đã đầy",
            "hết thể lực", "cấm chat", "quyền tài khoản", "khỏi gia tộc", "đã giải tán",
            "chinh phục", "thất bại", "bị bỏ dở", "hết hạn", "nạp thành công", "người vô tội",
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
        private float _hideY;             // y nằm khuất trên mép màn hình (tính theo cỡ canvas)
        private BoxCollider2D _meCol;     // khung va chạm nhân vật mình (để biết đầu đang ở đâu trên màn hình)
        private Object _meOwner;
        private int _phase;               // 0 ẩn · 1 hạ xuống · 2 đứng · 3 kéo lên

        /// <summary>Tin đang hiện (autotest kiểm).</summary>
        public static string Current => _instance != null && _instance._phase != 0 ? _instance._current : null;

        /// <summary>Tin "Hệ thống" từ server: lọc tin lặt vặt rồi đưa thú xuống.</summary>
        public static void Offer(string text)
        {
            if (!IsImportant(text)) return;
            Show(text);
        }

        public static bool IsImportant(string text)
        {
            if (string.IsNullOrEmpty(text) || text.StartsWith("[GM]")) return false;
            string t = text.ToLowerInvariant();
            foreach (var k in ImportantKeys) if (t.Contains(k)) return true;
            return false;
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
            // gốc = điểm chân thú (giữa đáy); bong bóng xếp chồng lên trên
            rt.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(BubbleW, 140), new Vector2(0.5f, 0));
            _instance = rt.gameObject.AddComponent<NoticeMascot>();
            _instance.Build(rt);
            return _instance;
        }

        private void Build(RectTransform rt)
        {
            _root = rt;
            // sợi dây từ mép trên màn hình xuống đỉnh bong bóng (bong bóng che chân dây)
            _rope = UIKit.Rect("Rope", rt);
            _rope.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -4), new Vector2(2, 1600), new Vector2(0.5f, 0));
            var ri = _rope.gameObject.AddComponent<Image>();
            ri.sprite = UIKit.WhiteSprite;
            ri.color = new Color(0.95f, 0.85f, 0.6f, 0.9f);
            ri.raycastTarget = false;
            _rope.gameObject.AddComponent<NoSkin>();

            // thú nhỏ ở dưới cùng
            _petBox = UIKit.Rect("Pet", rt);
            _petBox.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, PetSize, new Vector2(0.5f, 0));

            // bong bóng chữ ngay trên đầu thú, mũi nhọn chỉ xuống đầu thú
            _bubble = UIKit.Rect("Bubble", rt);
            _bubble.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, PetSize.y + 10), new Vector2(BubbleW, 60), new Vector2(0.5f, 0));
            var bg = _bubble.gameObject.AddComponent<Image>();
            UIStyle.SetRound(bg, new Color(0.10f, 0.08f, 0.16f, 0.94f), 1.5f);
            UIStyle.AddRing(_bubble, new Color(1f, 0.82f, 0.40f, 0.9f), 1.5f);
            var tip = UIKit.Rect("Tip", _bubble);
            tip.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(14, 14), new Vector2(0.5f, 0.5f));
            tip.localRotation = Quaternion.Euler(0, 0, 45f);
            var ti = tip.gameObject.AddComponent<Image>();
            ti.sprite = UIKit.WhiteSprite;
            ti.color = new Color(0.10f, 0.08f, 0.16f, 0.94f);
            ti.raycastTarget = false;
            tip.SetAsFirstSibling();
            _text = UIKit.Text("Text", _bubble, "", 15, TextAlignmentOptions.Midline, Color.white);
            _text.rectTransform.Fill(12, 12, 8, 8);
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
            // bong bóng cao vừa chữ (rộng cố định BubbleW); khung gốc cao = thú + bong bóng
            var pref = _text.GetPreferredValues(_text.text, BubbleW - 24f, 0f);
            float bh = Mathf.Clamp(pref.y + 18f, 56f, 220f);
            _bubble.sizeDelta = new Vector2(BubbleW, bh);
            _root.sizeDelta = new Vector2(BubbleW, PetSize.y + 10f + bh);
            var parent = (RectTransform)_root.parent;
            _hideY = parent.rect.height / 2f + 40f;   // đáy khung nằm trên mép trên màn hình
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
            Vector2 rest = RestPoint();
            float y;
            switch (_phase)
            {
                case 1:   // hạ xuống, nảy nhẹ cuối đường
                    float k = Mathf.Clamp01(_t / DropTime);
                    y = Mathf.LerpUnclamped(_hideY, rest.y, EaseOutBack(k));
                    if (k >= 1f) { _phase = 2; _t = 0f; }
                    break;
                case 2:   // đứng, nhún nhẹ trên dây
                    y = rest.y + Mathf.Abs(Mathf.Sin(_t * 3f)) * 4f;
                    _root.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_t * 2.2f) * 1.5f);
                    if (_t >= _hold) { _phase = 3; _t = 0f; }
                    break;
                default:  // kéo lên
                    float u = Mathf.Clamp01(_t / RiseTime);
                    y = Mathf.Lerp(rest.y, _hideY, u * u);
                    if (u >= 1f) { _root.localRotation = Quaternion.identity; Next(); return; }
                    break;
            }
            _root.anchoredPosition = new Vector2(rest.x, y);
        }

        /// <summary>
        /// Chỗ thú dừng (toạ độ UI, gốc giữa màn hình): ngang đầu nhân vật, lệch sang trái SideGap. Không tìm được nhân vật
        /// → hơi dưới giữa màn hình (camera vốn bám nhân vật). Kẹp để bong bóng không lọt ra ngoài màn hình.
        /// </summary>
        private Vector2 RestPoint()
        {
            var parent = (RectTransform)_root.parent;
            Vector2 half = parent.rect.size / 2f;
            Vector2 p = new Vector2(-SideGap, 0f);
            var cam = Camera.main;
            var me = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (cam != null && me != null)
            {
                // đỉnh đầu = mép trên khung va chạm (BoxCollider2D) — khung hình Renderer có cả hiệu ứng / thanh máu nên cao quá
                if (_meOwner != me || _meCol == null) { _meOwner = me; _meCol = me.GetComponent<BoxCollider2D>(); }
                Vector3 head = _meCol != null ? new Vector3(_meCol.bounds.center.x, _meCol.bounds.max.y + 0.15f, 0)
                                              : me.transform.position + Vector3.up * 1.6f;
                Vector2 sp = cam.WorldToScreenPoint(head);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, sp, null, out var lp))
                    p = new Vector2(lp.x - SideGap, lp.y);
            }
            float hw = BubbleW / 2f + 8f;
            p.x = Mathf.Clamp(p.x, -half.x + hw, half.x - hw);
            p.y = Mathf.Clamp(p.y, -half.y + 8f, half.y - _root.sizeDelta.y - 70f);   // chừa hàng nút trên cùng
            return p;
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

        private void LoadPet()
        {
            int art = MascotArt;
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

        /// <summary>Thu / phóng hình vừa khung (giống PetWindow.Fit), chân chạm đáy khung. Ảnh gốc nhìn sang phải = về phía nhân vật.</summary>
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
            rt.localScale = new Vector3(s, s, 1f);   // ảnh gốc nhìn sang phải = về phía nhân vật
            // giữa ngang, đáy hình chạm đáy khung (thú "đứng" trên mặt đất cạnh nhân vật)
            rt.anchoredPosition = new Vector2(-(x + w / 2f) * ppu * s, -box.y / 2f - y * ppu * s);
        }
    }
}
