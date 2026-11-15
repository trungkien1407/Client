using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Script.Entities;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// HƯỚNG DẪN LẦN ĐẦU KIỂU "CHỈ TAY" (2026-11-15, thay 5 trang chữ TutorialWindow lúc mới vào game):
    /// cả màn hình tối lại, chừa 1 "lỗ" sáng đúng chỗ đang giới thiệu (nút, thanh máu, NPC Hokage...) có viền vàng nhấp nháy,
    /// cạnh đó là thẻ lời giải thích có mũi chỉ vào lỗ. Tiếp / Trước / Bỏ qua (phím Enter / ← / Esc).
    ///
    /// Cách làm lỗ sáng: không cần shader — 4 tấm đen mờ bao quanh 1 hình chữ nhật (trên / dưới / trái / phải).
    /// Toạ độ chỗ cần chỉ: RectTransform (HUD scene hay HUD code đều được) → 4 góc ra điểm màn hình → đổi sang toạ độ lớp phủ;
    /// NPC (vật trong thế giới) → khung các SpriteRenderer → Camera.WorldToScreenPoint. Bước nào không tìm thấy chỗ chỉ thì tự bỏ qua.
    ///
    /// Tự chạy lần đầu mỗi nhân vật vào game (PlayerPrefs "tutorial_seen_{id}"); xem lại: Cài đặt → "Xem hướng dẫn".
    /// Thêm bước: thêm 1 dòng vào Steps() — (tiêu đề, lời PC, lời điện thoại, hàm tìm chỗ chỉ; null = không chỉ gì, chỉ hiện thẻ giữa màn hình).
    /// </summary>
    public class GuidedTour : MonoBehaviour
    {
        private sealed class Step
        {
            public string title, pc, touch;
            public Func<Rect?> target;   // khung chỗ chỉ, toạ độ màn hình (pixel, gốc dưới trái); null = không chỉ
        }

        private static GuidedTour _instance;
        public static bool Active => _instance != null && _instance.gameObject.activeSelf && _instance._steps != null;
        /// <summary>Bước đang hiện (0..) — autotest đọc.</summary>
        public static int StepIndex => _instance != null ? _instance._index : -1;
        public static int StepCount => _instance != null && _instance._steps != null ? _instance._steps.Count : 0;
        /// <summary>Bước đang hiện có lỗ sáng chỉ vào đâu đó không (autotest kiểm).</summary>
        public static bool HasHole => _instance != null && _instance._ring.gameObject.activeSelf;

        private static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
        private const float Pad = 8f;   // lỗ rộng hơn chỗ chỉ mỗi bên 8 px

        private RectTransform _rt, _ring, _card, _tip;
        private readonly Image[] _dim = new Image[4];
        private TextMeshProUGUI _title, _body, _count;
        private Button _prev, _next, _skip;
        private List<Step> _steps;
        private int _index;
        private float _t;

        private static bool Touch => Touchscreen.current != null && Keyboard.current == null;

        // ------------------------------------------------------------------ mở / đóng

        /// <summary>GameHud gọi khi vào game: nhân vật này chưa xem thì chạy (chờ HUD + NPC hiện ra).</summary>
        public static void StartIfFirstTime()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-autotest") >= 0) return;   // autotest tự gọi Begin để kiểm
            string key = "tutorial_seen_" + LocalPlayerState.Id;
            try
            {
                if (PlayerPrefs.GetInt(key, 0) == 1) return;
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();
            }
            catch { /* PlayerPrefs lỗi thì vẫn chạy */ }
            Begin(1.5f);
        }

        /// <summary>Chạy từ bước đầu (Cài đặt → Xem hướng dẫn). {delay} giây sau mới hiện (đợi HUD dựng xong).</summary>
        public static void Begin(float delay = 0f)
        {
            var t = Ensure();
            t.StopAllCoroutines();
            t.gameObject.SetActive(true);   // phải bật TRƯỚC StartCoroutine (object ẩn thì Unity không chạy coroutine)
            t.StartCoroutine(t.Run(delay));
        }

        /// <summary>Sang bước kế (autotest dùng — như bấm "Tiếp").</summary>
        public static void NextStep() { if (Active && _instance._steps != null) _instance.Show(_instance._index + 1, +1); }

        public static void Stop()
        {
            if (_instance == null) return;
            _instance.StopAllCoroutines();
            _instance.gameObject.SetActive(false);
        }

        private IEnumerator Run(float delay)
        {
            _steps = null;   // trong lúc chờ: chưa tính là đang xem (không chặn thao tác)
            _rt.SetAsLastSibling();
            SetVisible(false);
            if (delay > 0f) yield return new WaitForSecondsRealtime(delay);
            KitCloseWindows();
            _steps = Steps();
            _index = 0;
            SetVisible(true);
            Show(0, +1);
        }

        private static GuidedTour Ensure()
        {
            if (_instance != null) return _instance;
            var rt = UIKit.Rect("GuidedTour", UIRoot.Instance.transform).Fill();
            _instance = rt.gameObject.AddComponent<GuidedTour>();
            _instance.Build(rt);
            return _instance;
        }

        private static void KitCloseWindows() => UIRoot.CloseAll();   // cửa sổ đang mở che mất HUD

        // ------------------------------------------------------------------ dựng giao diện

        private void Build(RectTransform rt)
        {
            _rt = rt;
            for (int i = 0; i < 4; i++)
            {
                var d = UIKit.Rect("Dim" + i, rt);
                d.anchorMin = d.anchorMax = Vector2.zero;
                d.pivot = Vector2.zero;
                _dim[i] = d.gameObject.AddComponent<Image>();
                _dim[i].sprite = UIKit.WhiteSprite;
                _dim[i].color = Dim;   // nhận chuột → bấm ra ngoài không trúng game phía sau
                d.gameObject.AddComponent<NoSkin>();
            }
            _ring = UIKit.Rect("Ring", rt);
            _ring.anchorMin = _ring.anchorMax = Vector2.zero;
            _ring.pivot = new Vector2(0.5f, 0.5f);
            var ringImg = _ring.gameObject.AddComponent<Image>();
            UIStyle.SetRound(ringImg, UIStyle.Gold, 1.2f, ring: true);
            ringImg.raycastTarget = false;

            _card = UIKit.Rect("Card", rt);
            _card.anchorMin = _card.anchorMax = Vector2.zero;
            _card.pivot = new Vector2(0.5f, 0.5f);
            _card.sizeDelta = new Vector2(420, 190);
            // mũi chỉ (hình vuông xoay 45°) — vẽ trước thẻ để nửa trong bị thẻ che
            _tip = UIKit.Rect("Tip", _card);
            _tip.sizeDelta = new Vector2(22, 22);
            _tip.localRotation = Quaternion.Euler(0, 0, 45f);
            var tipImg = _tip.gameObject.AddComponent<Image>();
            tipImg.sprite = UIKit.WhiteSprite;
            tipImg.color = UIStyle.Gold;
            tipImg.raycastTarget = false;
            var bg = UIKit.Rect("Bg", _card).Fill();
            UIStyle.SetRound(bg.gameObject.AddComponent<Image>(), new Color(0.11f, 0.09f, 0.17f, 0.97f), 1.2f);
            UIStyle.AddRing(bg, UIStyle.Gold, 1.2f);

            _title = UIKit.Text("Title", _card, "", 19, TextAlignmentOptions.TopLeft, UIStyle.Gold);
            _title.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -12), new Vector2(-28, 28), new Vector2(0.5f, 1));
            _title.fontStyle = FontStyles.Bold;
            _count = UIKit.Text("Count", _card, "", 13, TextAlignmentOptions.TopRight, UIKit.DimText);
            _count.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -14), new Vector2(-28, 24), new Vector2(0.5f, 1));
            _body = UIKit.Text("Body", _card, "", 16, TextAlignmentOptions.TopLeft, Color.white);
            _body.rectTransform.Fill(14, 14, 44, 54);
            _body.enableWordWrapping = true;

            _skip = UIKit.Button("Skip", _card, "Bỏ qua", Finish, 14);
            ((RectTransform)_skip.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(12, 10), new Vector2(86, 34), Vector2.zero);
            _prev = UIKit.Button("Prev", _card, "Trước", () => Show(_index - 1, -1), 14);
            ((RectTransform)_prev.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-122, 10), new Vector2(80, 34), new Vector2(1, 0));
            _next = UIKit.Button("Next", _card, "Tiếp", () => Show(_index + 1, +1), 15);
            ((RectTransform)_next.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-12, 10), new Vector2(104, 34), new Vector2(1, 0));
            _next.image.color = UIKit.ButtonHot;
            UISkin.SkinTree(rt);
            gameObject.SetActive(false);
        }

        private void SetVisible(bool on)
        {
            foreach (var d in _dim) d.gameObject.SetActive(on);
            _ring.gameObject.SetActive(on);
            _card.gameObject.SetActive(on);
        }

        // ------------------------------------------------------------------ từng bước

        /// <summary>Hiện bước {i}; bước không tìm thấy chỗ chỉ thì nhảy tiếp theo hướng {dir}.</summary>
        private void Show(int i, int dir)
        {
            if (_steps == null) return;
            while (i >= 0 && i < _steps.Count && _steps[i].target != null && _steps[i].target() == null) i += dir;
            if (i >= _steps.Count) { Finish(); return; }
            if (i < 0) i = 0;
            _index = i;
            var s = _steps[i];
            _title.text = s.title;
            _count.text = $"{i + 1} / {_steps.Count}";
            _body.text = Touch ? s.touch : s.pc;
            _prev.gameObject.SetActive(i > 0);
            _next.SetLabel(i == _steps.Count - 1 ? "Bắt đầu chơi" : "Tiếp");
            float h = Mathf.Clamp(_body.GetPreferredValues(_body.text, 420f - 28f, 0f).y + 44f + 54f + 6f, 150f, 300f);
            _card.sizeDelta = new Vector2(420f, h);
            _t = 0f;
            Layout();
        }

        private void LateUpdate()
        {
            if (_steps == null || !_card.gameObject.activeSelf) return;
            _t += Time.unscaledDeltaTime;
            Layout();   // NPC / camera có thể dịch → bám theo mỗi khung
            float a = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(_t * 3f));
            var img = _ring.GetComponent<Image>();
            img.color = new Color(UIStyle.Gold.r, UIStyle.Gold.g, UIStyle.Gold.b, a);

            var kb = Keyboard.current;
            if (kb == null || Core.GameInput.Blocked) return;
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame || kb.rightArrowKey.wasPressedThisFrame)
                Show(_index + 1, +1);
            else if (kb.leftArrowKey.wasPressedThisFrame && _index > 0) Show(_index - 1, -1);
            else if (kb.escapeKey.wasPressedThisFrame) Finish();
        }

        /// <summary>Đặt 4 tấm tối quanh lỗ, viền sáng, thẻ chữ + mũi chỉ theo chỗ chỉ hiện tại.</summary>
        private void Layout()
        {
            Vector2 size = _rt.rect.size;
            var s = _steps[_index];
            Rect? screen = s.target?.Invoke();
            Rect hole;
            bool has = screen.HasValue;
            if (has)
            {
                hole = ToLocal(screen.Value);
                hole = Rect.MinMaxRect(Mathf.Max(0, hole.xMin - Pad), Mathf.Max(0, hole.yMin - Pad),
                                       Mathf.Min(size.x, hole.xMax + Pad), Mathf.Min(size.y, hole.yMax + Pad));
            }
            else hole = new Rect(size.x / 2f, size.y / 2f, 0, 0);   // không chỉ gì: tối đều cả màn hình

            SetDim(0, 0, hole.yMax, size.x, size.y - hole.yMax);          // trên
            SetDim(1, 0, 0, size.x, hole.yMin);                            // dưới
            SetDim(2, 0, hole.yMin, hole.xMin, hole.height);               // trái
            SetDim(3, hole.xMax, hole.yMin, size.x - hole.xMax, hole.height); // phải
            _ring.gameObject.SetActive(has);
            if (has)
            {
                float grow = 4f * Mathf.Abs(Mathf.Sin(_t * 3f));
                _ring.anchoredPosition = hole.center;
                _ring.sizeDelta = hole.size + new Vector2(6 + grow, 6 + grow);
            }

            // Thẻ chữ: dưới lỗ nếu lỗ ở nửa trên màn hình, ngược lại phía trên; không chỉ gì → giữa màn hình
            Vector2 cs = _card.sizeDelta;
            Vector2 pos;
            _tip.gameObject.SetActive(has);
            if (!has) pos = size / 2f;
            else
            {
                bool below = hole.center.y > size.y / 2f;
                float y = below ? hole.yMin - 22f - cs.y / 2f : hole.yMax + 22f + cs.y / 2f;
                // lỗ quá to (thanh nút dài) mà không đủ chỗ trên / dưới → đặt cạnh bên
                if (y - cs.y / 2f < 6f || y + cs.y / 2f > size.y - 6f)
                {
                    bool right = hole.center.x < size.x / 2f;
                    pos = new Vector2(right ? hole.xMax + 22f + cs.x / 2f : hole.xMin - 22f - cs.x / 2f, hole.center.y);
                    _tip.anchorMin = _tip.anchorMax = new Vector2(right ? 0f : 1f, 0.5f);
                    _tip.anchoredPosition = Vector2.zero;
                }
                else
                {
                    pos = new Vector2(hole.center.x, y);
                    _tip.anchorMin = _tip.anchorMax = new Vector2(0.5f, below ? 1f : 0f);
                    _tip.anchoredPosition = Vector2.zero;
                }
                pos.x = Mathf.Clamp(pos.x, cs.x / 2f + 8f, size.x - cs.x / 2f - 8f);
                pos.y = Mathf.Clamp(pos.y, cs.y / 2f + 8f, size.y - cs.y / 2f - 8f);
                // mũi chỉ luôn hướng về giữa lỗ (thẻ bị kẹp mép → dời mũi theo)
                if (_tip.anchorMin.y != 0.5f)
                    _tip.anchoredPosition = new Vector2(Mathf.Clamp(hole.center.x - pos.x, -cs.x / 2f + 24f, cs.x / 2f - 24f), 0f);
                else
                    _tip.anchoredPosition = new Vector2(0f, Mathf.Clamp(hole.center.y - pos.y, -cs.y / 2f + 20f, cs.y / 2f - 20f));
            }
            _card.anchoredPosition = pos;
        }

        private void SetDim(int i, float x, float y, float w, float h)
        {
            var r = _dim[i].rectTransform;
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(Mathf.Max(0, w), Mathf.Max(0, h));
        }

        /// <summary>Khung màn hình (pixel) → toạ độ trong lớp phủ (gốc dưới trái, đơn vị canvas).</summary>
        private Rect ToLocal(Rect screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screen.min, null, out var a);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rt, screen.max, null, out var b);
            Vector2 off = _rt.rect.min;   // gốc lớp phủ ở giữa → dời về góc dưới trái
            return Rect.MinMaxRect(a.x - off.x, a.y - off.y, b.x - off.x, b.y - off.y);
        }

        private void Finish()
        {
            StopAllCoroutines();
            gameObject.SetActive(false);
            NoticeMascot.Show("Chúc bạn chơi vui! Thông báo quan trọng mình sẽ mang xuống đây. Xem lại hướng dẫn: Cài đặt → Xem hướng dẫn.");
        }

        // ------------------------------------------------------------------ tìm chỗ chỉ

        /// <summary>Khung màn hình của 1 RectTransform đang hiện (HUD scene hay HUD code, canvas nào cũng được).</summary>
        private static Rect? ScreenOf(RectTransform t)
        {
            if (t == null || !t.gameObject.activeInHierarchy) return null;
            var corners = new Vector3[4];
            t.GetWorldCorners(corners);
            var canvas = t.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.rootCanvas.worldCamera : null;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            foreach (var c in corners)
            {
                Vector2 p = RectTransformUtility.WorldToScreenPoint(cam, c);
                min = Vector2.Min(min, p);
                max = Vector2.Max(max, p);
            }
            if (max.x - min.x < 2f || max.y - min.y < 2f) return null;
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect? ScreenOf(Component c) => c == null ? null : ScreenOf(c.transform as RectTransform);

        private static Rect? Union(params Rect?[] rs)
        {
            Rect? u = null;
            foreach (var r in rs)
            {
                if (!r.HasValue) continue;
                u = u.HasValue ? Rect.MinMaxRect(Mathf.Min(u.Value.xMin, r.Value.xMin), Mathf.Min(u.Value.yMin, r.Value.yMin),
                                                 Mathf.Max(u.Value.xMax, r.Value.xMax), Mathf.Max(u.Value.yMax, r.Value.yMax)) : r;
            }
            return u;
        }

        private static Rect? Named(string path) => ScreenOf(GameObject.Find(path)?.transform as RectTransform);

        /// <summary>NPC theo loại (1 Hokage) đang trong khung hình → khung các hình (thân + chân + tên).</summary>
        private static Rect? Npc(int templateId)
        {
            var cam = Camera.main;
            if (cam == null) return null;
            foreach (var n in FindObjectsByType<NpcEntity>(FindObjectsSortMode.None))
            {
                if (n.TemplateId != templateId || !n.gameObject.activeInHierarchy) continue;
                Bounds? b = null;
                foreach (var r in n.GetComponentsInChildren<Renderer>())
                {
                    if (!r.enabled) continue;
                    if (b.HasValue) { var bb = b.Value; bb.Encapsulate(r.bounds); b = bb; } else b = r.bounds;
                }
                if (!b.HasValue) continue;
                Vector2 a = cam.WorldToScreenPoint(b.Value.min), c = cam.WorldToScreenPoint(b.Value.max);
                var rect = Rect.MinMaxRect(a.x, a.y, c.x, c.y);
                if (rect.xMax < 0 || rect.xMin > Screen.width || rect.yMax < 0 || rect.yMin > Screen.height) return null;   // ngoài màn hình
                return rect;
            }
            return null;
        }

        /// <summary>
        /// Cụm góc trên trái: ảnh đại diện + thanh máu / chakra / cấp. Tìm thanh theo tên trong HUD scene không chắc (trùng tên "HP"
        /// với khung to khác) → lấy ảnh đại diện rồi kéo sang phải 2,9 lần bề rộng (bố cục HUD cố định: thanh nằm ngay bên phải ảnh).
        /// </summary>
        private static Rect? AvatarAndBars()
        {
            var av = ScreenOf(UISetup.Instance != null ? UISetup.Instance.avatarSlot : null);
            if (!av.HasValue) return null;
            var a = av.Value;
            return new Rect(a.xMin, a.yMin, a.width * 2.9f, a.height);
        }

        private static Rect? Menu()
        {
            var v = GameHud.View;
            if (v == null) return null;
            var list = new List<Rect?>();
            foreach (var b in v.menu) if (b != null && b.gameObject.activeInHierarchy) list.Add(ScreenOf(b));
            return Union(list.ToArray());
        }

        private static List<Step> Steps()
        {
            var v = GameHud.View;
            return new List<Step>
            {
                new Step { title = "Chào mừng đến Làng Lá!", target = null,
                    pc = "Hướng dẫn nhanh khoảng 1 phút: mình sẽ chỉ vào từng phần trên màn hình.\n\nBấm <b>Tiếp</b> (hoặc <b>Enter</b>) để xem tiếp, <b>Bỏ qua</b> (Esc) nếu đã biết chơi.",
                    touch = "Hướng dẫn nhanh khoảng 1 phút: mình sẽ chỉ vào từng phần trên màn hình.\n\nBấm <b>Tiếp</b> để xem tiếp, <b>Bỏ qua</b> nếu đã biết chơi." },
                new Step { title = "Máu và chakra", target = AvatarAndBars,
                    pc = "Thanh <color=#ff8a8a>đỏ</color> là máu, thanh <color=#8ac8ff>xanh</color> là chakra (dùng chiêu tốn chakra). Bên dưới là cấp và % kinh nghiệm.\n\nBấm <b>ảnh đại diện</b> (hoặc phím <b>C</b>) để mở bảng Nhân vật, cộng điểm tiềm năng.",
                    touch = "Thanh <color=#ff8a8a>đỏ</color> là máu, thanh <color=#8ac8ff>xanh</color> là chakra (dùng chiêu tốn chakra).\n\nChạm <b>ảnh đại diện</b> để mở bảng Nhân vật, cộng điểm tiềm năng." },
                new Step { title = "Di chuyển", target = () => Named("HUDCanvas/Joystick"),
                    pc = "<b>A / D</b> (hoặc mũi tên ← / →) để đi, <b>W</b> (↑) để nhảy, <b>S</b> (↓) để rơi xuống bục dưới.\nBấm các nút mũi tên này bằng chuột cũng được.",
                    touch = "Giữ mũi tên <b>trái / phải</b> để đi, mũi tên <b>lên</b> để nhảy lên bục cao." },
                new Step { title = "Hokage — người giao nhiệm vụ", target = () => Npc(1),
                    pc = "Đây là <b>Hokage</b>. Bấm vào NPC (hoặc đứng gần) rồi nhấn <b>Enter</b> / <b>F</b> để nói chuyện, nhận nhiệm vụ đầu tiên.\nNPC khác: Thợ Rèn (nâng cấp, rương đồ), Chủ Chợ (mua bán).",
                    touch = "Đây là <b>Hokage</b>. Chạm vào NPC rồi bấm nút <b>Nói chuyện</b> để nhận nhiệm vụ đầu tiên.\nNPC khác: Thợ Rèn (nâng cấp, rương đồ), Chủ Chợ (mua bán)." },
                new Step { title = "Nhiệm vụ đang làm", target = () => ScreenOf(v != null ? v.questBtn : null),
                    pc = "Nhiệm vụ đang làm hiện ở đây (đánh mấy con, nhặt món gì). Bấm vào (phím <b>L</b>) để xem chi tiết và nút <b>Tự đi</b> — nhân vật tự chạy tới nơi cần đến.",
                    touch = "Nhiệm vụ đang làm hiện ở đây. Chạm vào để xem chi tiết và nút <b>Tự đi</b> — nhân vật tự chạy tới nơi cần đến." },
                new Step { title = "Nút Đánh", target = () => Named("AttackBtn"),
                    pc = "Đánh quái đang chọn — phím <b>Enter</b> (giữ để đánh liên tục). Chưa chọn thì tự nhắm quái gần nhất.\nChọn NPC / người chơi thì nút đổi thành <b>nói chuyện</b>.\nNhớ <b>mặc vũ khí</b> — tay không thì không đánh được.",
                    touch = "Chạm để đánh quái đang chọn. Chưa chọn thì tự nhắm quái gần nhất.\nChọn NPC / người chơi thì nút đổi thành <b>nói chuyện</b>.\nNhớ <b>mặc vũ khí</b> — tay không thì không đánh được." },
                new Step { title = "Thanh kỹ năng", target = () => Named("HUDCanvas/SkillList") ?? Named("SkillList"),
                    pc = "Các chiêu đã học. Phím <b>1–9</b> dùng chiêu ở ô đó (hoặc bấm vào ô). Lên cấp được điểm kỹ năng — học / nâng chiêu ở bảng <b>Kỹ năng (K)</b>.",
                    touch = "Các chiêu đã học — chạm ô để dùng. Lên cấp được điểm kỹ năng — học / nâng chiêu ở bảng <b>Kỹ năng</b>." },
                new Step { title = "Các bảng chức năng", target = Menu,
                    pc = "Nhân vật · Nhiệm vụ · Hoạt động hằng ngày · Xã hội (nhóm, bạn, gia tộc) · Thư · Xếp hạng · Cẩm nang · Cài đặt…\nNút <b>Ẩn</b> thu gọn hàng nút này.",
                    touch = "Nhân vật · Nhiệm vụ · Hoạt động hằng ngày · Xã hội (nhóm, bạn, gia tộc) · Thư · Xếp hạng · Cẩm nang · Cài đặt…\nNút <b>Ẩn</b> thu gọn hàng nút này." },
                new Step { title = "Bản đồ và khu", target = () => Union(Named("Minimap"), ScreenOf(v != null ? v.zoneBtn : null)),
                    pc = "Bản đồ nhỏ của map đang đứng — phím <b>B</b> mở bản đồ thế giới. Nút bên dưới là tên map + <b>khu</b>: bấm để đổi khu khi map đông.",
                    touch = "Bản đồ nhỏ của map đang đứng — chạm để mở bản đồ thế giới. Nút bên dưới là tên map + <b>khu</b>: chạm để đổi khu khi map đông." },
                new Step { title = "Tự động", target = () => Named("AutoBtn"),
                    pc = "Bật <b>Tự động</b> (phím <b>Z</b>): nhân vật tự đánh quái, nhặt đồ, uống bình khi máu thấp. Phím <b>U</b> để chỉnh.",
                    touch = "Bật <b>Tự động</b>: nhân vật tự đánh quái, nhặt đồ, uống bình khi máu thấp." },
                new Step { title = "Trò chuyện", target = () => Named("ChatOverlay/ChatBtn") ?? FindChat(),
                    pc = "Phím <b>X</b> để chat: /a thế giới, /g gia tộc, /w Tên nhắn riêng.\nThông báo quan trọng sẽ có <b>chú thú đưa tin</b> mang xuống giữa màn hình — khỏi sợ bỏ lỡ.",
                    touch = "Chạm <b>Chat</b> để trò chuyện.\nThông báo quan trọng sẽ có <b>chú thú đưa tin</b> mang xuống giữa màn hình — khỏi sợ bỏ lỡ." },
                new Step { title = "Sẵn sàng!", target = null,
                    pc = "Gặp <b>Hokage</b> nhận nhiệm vụ đầu tiên nhé. Quên cách chơi thì mở <b>Cẩm nang (G)</b>; xem lại hướng dẫn này ở <b>Cài đặt → Xem hướng dẫn</b>.",
                    touch = "Gặp <b>Hokage</b> nhận nhiệm vụ đầu tiên nhé. Quên cách chơi thì mở <b>Cẩm nang</b>; xem lại hướng dẫn này ở <b>Cài đặt → Xem hướng dẫn</b>." },
            };
        }

        private static Rect? FindChat()
        {
            foreach (var b in FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (b.name == "ChatBtn" && b.gameObject.activeInHierarchy) return ScreenOf(b);
            return null;
        }
    }
}
