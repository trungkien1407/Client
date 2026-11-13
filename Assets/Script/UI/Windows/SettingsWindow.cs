using Assets.Script.Core;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// CÀI ĐẶT (lưu PlayerPrefs trên máy): âm lượng nhạc/hiệu ứng + bật / tắt (bấm vào ô số %), chất lượng hình, giới hạn FPS (pin điện thoại),
    /// thú cưng người khác Hiện / Ẩn (GĐ27 — khu đông người: ẩn để nhẹ máy; thú của mình luôn hiện).
    /// Áp dụng lúc mở game qua SettingsWindow.ApplySaved() (GameHud gọi khi dựng HUD).
    /// </summary>
    public class SettingsWindow : GameWindow
    {
        private const string PrefFps = "fps_limit", PrefQuality = "quality";
        [SerializeField] private TextMeshProUGUI _music, _sfx, _fps, _quality;
        // Nút [-] / [+] của 4 dòng (0 nhạc, 1 hiệu ứng, 2 FPS, 3 đồ hoạ)
        [SerializeField] private Button[] _minus = new Button[4], _plus = new Button[4];
        [SerializeField] private Button _tutorial;
        [SerializeField, Optional] private Button _logout;   // prefab cũ chưa có → Bind() tự tạo
        [SerializeField, Optional] private Button _password; // Đổi mật khẩu — như trên
        [SerializeField, Optional] private Button _auto;     // GĐ22 mở cửa sổ Tự động — như trên
        [SerializeField, Optional] private Button _pets;     // GĐ27 thú người khác Hiện / Ẩn — như trên

        protected override void Configure()
        {
            Title = "Cài đặt";
            Size = new Vector2(460, 476);   // GĐ22 thêm dòng nút Tự động · GĐ27 dòng thú người khác
        }

        protected override void Build()
        {
            var body = CreateBody();
            _music = Line(body, 0, "Nhạc nền");
            _sfx = Line(body, 1, "Hiệu ứng");
            _fps = Line(body, 2, "Giới hạn FPS", "30", "60");
            _quality = Line(body, 3, "Đồ hoạ");
            var note = UIKit.Text("Note", body, NoteText, 13, TextAlignmentOptions.Center, UIKit.DimText);
            note.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 44), new Vector2(0, 30), new Vector2(0.5f, 0));
            _tutorial = UIKit.Button("Tutorial", body, "Hướng dẫn tân thủ", null, 15);
            _logout = UIKit.Button("Logout", body, "Đăng xuất", null, 15);
            _password = UIKit.Button("Password", body, "Đổi mật khẩu", null, 15);
            _auto = UIKit.Button("Auto", body, "Tự động (U)", null, 16);
            _pets = UIKit.Button("Pets", body, "", null, 15);
            PlaceAutoButton();
            PlaceBottomButtons();
        }

        private const string NoteText = "Bấm vào số % để bật / tắt âm. Máy yếu: 30 FPS + đồ hoạ thấp.";

        /// <summary>3 nút dưới cùng: [Hướng dẫn tân thủ] [Đổi mật khẩu] [Đăng xuất].</summary>
        private void PlaceBottomButtons()
        {
            Bottom(_tutorial, -144);
            Bottom(_password, 0);
            Bottom(_logout, 144);
        }

        /// <summary>Dòng thứ 5 (dưới Đồ hoạ): nút mở cửa sổ Tự động.</summary>
        private void PlaceAutoButton()
        {
            if (_auto != null) ((RectTransform)_auto.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -228), new Vector2(240, 40), new Vector2(0.5f, 1));
            if (_pets != null) ((RectTransform)_pets.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -274), new Vector2(300, 38), new Vector2(0.5f, 1));
        }

        private static void Bottom(Button b, float x)
        {
            if (b != null) ((RectTransform)b.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(x, 4), new Vector2(138, 36), new Vector2(0.5f, 0));
        }

        protected override void Bind()
        {
            _minus[0].onClick.AddListener(() => Step(-0.1f, true));
            _plus[0].onClick.AddListener(() => Step(0.1f, true));
            _minus[1].onClick.AddListener(() => Step(-0.1f, false));
            _plus[1].onClick.AddListener(() => Step(0.1f, false));
            _minus[2].onClick.AddListener(() => SetFps(30));
            _plus[2].onClick.AddListener(() => SetFps(60));
            _minus[3].onClick.AddListener(() => SetQuality(-1));
            _plus[3].onClick.AddListener(() => SetQuality(1));
            // Bấm ô giá trị nhạc / hiệu ứng = bật / tắt (prefab cũ chỉ có chữ → gắn nút lúc chạy)
            MakeToggle(_music, () => { AudioManager.MusicOn = !AudioManager.MusicOn; Refresh(); });
            MakeToggle(_sfx, () => { AudioManager.SfxOn = !AudioManager.SfxOn; Refresh(); });
            foreach (var t in Body.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (t.name == "Note") t.text = NoteText;   // prefab cũ còn câu cũ
            _tutorial.onClick.AddListener(() => Get<TutorialWindow>().ShowFromStart());
            if (_logout == null || _password == null)   // prefab xuất trước khi có các nút này
            {
                if (_logout == null) _logout = UIKit.Button("Logout", Body, "Đăng xuất", null, 15);
                if (_password == null) _password = UIKit.Button("Password", Body, "Đổi mật khẩu", null, 15);
                _tutorial.SetLabel("Hướng dẫn tân thủ");   // 3 nút 1 hàng → nhãn ngắn hơn
                PlaceBottomButtons();
            }
            if (_auto == null)   // prefab xuất trước GĐ22: thêm nút + nới cửa sổ cho đủ chỗ
            {
                _auto = UIKit.Button("Auto", Body, "Tự động (U)", null, 16);
                var win = (RectTransform)transform;
                if (win.sizeDelta.y < 430f) win.sizeDelta = new Vector2(win.sizeDelta.x, 430f);
                PlaceAutoButton();
            }
            if (_pets == null)   // prefab xuất trước GĐ27: thêm dòng + nới cửa sổ
            {
                _pets = UIKit.Button("Pets", Body, "", null, 15);
                var w27 = (RectTransform)transform;
                if (w27.sizeDelta.y < 476f) w27.sizeDelta = new Vector2(w27.sizeDelta.x, 476f);
                PlaceAutoButton();
            }
            _pets.onClick.AddListener(() => { Fx.PetMount.ShowOthers = !Fx.PetMount.ShowOthers; Refresh(); });
            _auto.onClick.AddListener(() => { Hide(); Open<AutoWindow>(); });
            _password.onClick.AddListener(() => { Hide(); Get<ChangePasswordWindow>().Open(); });
            _logout.onClick.AddListener(() =>
                ConfirmWindow.Ask("Đăng xuất về màn hình đăng nhập?", "Đăng xuất", () => { Hide(); GameActions.Logout(); }));
        }

        /// <summary>Dựng 1 dòng: nhãn · [a] · giá trị · [b]. Hai nút lưu vào _minus[row] / _plus[row]; trả về ô giá trị.</summary>
        private TextMeshProUGUI Line(RectTransform body, int row, string label, string a = "-", string b = "+")
        {
            float y = -row * 56;
            var l = UIKit.Text("L" + row, body, label, 17);
            l.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(150, 44), new Vector2(0, 1));
            var v = UIKit.Text("V" + row, body, "", 17, TextAlignmentOptions.Center, UIKit.ButtonHot);
            v.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(210, y), new Vector2(120, 44), new Vector2(0, 1));
            _minus[row] = UIKit.Button("M" + row, body, a, null, 18);
            ((RectTransform)_minus[row].transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(155, y - 2), new Vector2(50, 40), new Vector2(0, 1));
            _plus[row] = UIKit.Button("P" + row, body, b, null, 18);
            ((RectTransform)_plus[row].transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(335, y - 2), new Vector2(50, 40), new Vector2(0, 1));
            return v;
        }

        private static void MakeToggle(TextMeshProUGUI t, UnityEngine.Events.UnityAction onClick)
        {
            t.raycastTarget = true;
            var b = t.GetComponent<Button>();
            if (b == null) b = t.gameObject.AddComponent<Button>();   // Unity: không dùng ?? với Component
            b.targetGraphic = t;
            b.onClick.AddListener(onClick);
        }

        /// <summary>Công tắc để autotest bấm như người chơi.</summary>
        public void ToggleMusic() => _music.GetComponent<Button>().onClick.Invoke();
        public void ToggleSfx() => _sfx.GetComponent<Button>().onClick.Invoke();
        public string MusicText => _music.text;
        public string SfxText => _sfx.text;

        private void Step(float d, bool music)
        {
            // Đang tắt mà bấm -/+ → bật lại rồi chỉnh
            if (music) AudioManager.MusicOn = true; else AudioManager.SfxOn = true;
            if (music) AudioManager.MusicVolume = Mathf.Clamp01(Mathf.Round((AudioManager.MusicVolume + d) * 10) / 10f);
            else AudioManager.SfxVolume = Mathf.Clamp01(Mathf.Round((AudioManager.SfxVolume + d) * 10) / 10f);
            Refresh();
        }

        private void SetFps(int fps) { PlayerPrefs.SetInt(PrefFps, fps); ApplySaved(); Refresh(); }

        private void SetQuality(int d)
        {
            int q = Mathf.Clamp(QualitySettings.GetQualityLevel() + d, 0, QualitySettings.names.Length - 1);
            PlayerPrefs.SetInt(PrefQuality, q);
            ApplySaved();
            Refresh();
        }

        /// <summary>Áp dụng cài đặt đã lưu (gọi 1 lần khi vào game).</summary>
        public static void ApplySaved()
        {
            Application.targetFrameRate = PlayerPrefs.GetInt(PrefFps, 60);
            if (PlayerPrefs.HasKey(PrefQuality))
                QualitySettings.SetQualityLevel(Mathf.Clamp(PlayerPrefs.GetInt(PrefQuality), 0, QualitySettings.names.Length - 1), true);
        }

        protected override void Refresh()
        {
            _music.text = AudioManager.MusicOn ? $"{Mathf.RoundToInt(AudioManager.MusicVolume * 100)}%" : "TẮT";
            _sfx.text = AudioManager.SfxOn ? $"{Mathf.RoundToInt(AudioManager.SfxVolume * 100)}%" : "TẮT";
            _fps.text = $"{PlayerPrefs.GetInt(PrefFps, 60)}";
            var names = QualitySettings.names;
            _quality.text = names.Length > 0 ? names[QualitySettings.GetQualityLevel()] : "-";
            if (_pets != null) _pets.SetLabel(Fx.PetMount.ShowOthers ? $"Thú người khác: HIỆN (tối đa {Fx.PetMount.MaxOthers} con gần nhất)" : "Thú người khác: ẨN (nhẹ máy)");
        }
    }
}
