using Assets.Script.Core;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// CÀI ĐẶT (lưu PlayerPrefs trên máy): âm lượng nhạc/hiệu ứng, chất lượng hình, giới hạn FPS (pin điện thoại).
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

        protected override void Configure()
        {
            Title = "Cài đặt";
            Size = new Vector2(460, 370);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _music = Line(body, 0, "Nhạc nền");
            _sfx = Line(body, 1, "Hiệu ứng");
            _fps = Line(body, 2, "Giới hạn FPS", "30", "60");
            _quality = Line(body, 3, "Đồ hoạ");
            var note = UIKit.Text("Note", body, "Điện thoại yếu / tiết kiệm pin: chọn 30 FPS + đồ hoạ thấp.", 13, TextAlignmentOptions.Center, UIKit.DimText);
            note.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 44), new Vector2(0, 30), new Vector2(0.5f, 0));
            _tutorial = UIKit.Button("Tutorial", body, "Xem hướng dẫn tân thủ", null, 15);
            _logout = UIKit.Button("Logout", body, "Đăng xuất", null, 15);
            PlaceBottomButtons();
        }

        /// <summary>2 nút dưới cùng: [Hướng dẫn tân thủ] [Đăng xuất].</summary>
        private void PlaceBottomButtons()
        {
            ((RectTransform)_tutorial.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-105, 4), new Vector2(200, 36), new Vector2(0.5f, 0));
            ((RectTransform)_logout.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(105, 4), new Vector2(200, 36), new Vector2(0.5f, 0));
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
            _tutorial.onClick.AddListener(() => Get<TutorialWindow>().ShowFromStart());
            if (_logout == null)   // prefab xuất trước khi có nút này
            {
                _logout = UIKit.Button("Logout", Body, "Đăng xuất", null, 15);
                PlaceBottomButtons();
            }
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

        private void Step(float d, bool music)
        {
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
            _music.text = $"{Mathf.RoundToInt(AudioManager.MusicVolume * 100)}%";
            _sfx.text = $"{Mathf.RoundToInt(AudioManager.SfxVolume * 100)}%";
            _fps.text = $"{PlayerPrefs.GetInt(PrefFps, 60)}";
            var names = QualitySettings.names;
            _quality.text = names.Length > 0 ? names[QualitySettings.GetQualityLevel()] : "-";
        }
    }
}
