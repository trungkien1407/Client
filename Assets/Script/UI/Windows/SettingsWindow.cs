using Assets.Script.Core;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// CÀI ĐẶT (lưu PlayerPrefs trên máy): âm lượng nhạc/hiệu ứng, chất lượng hình, giới hạn FPS (pin điện thoại).
    /// Áp dụng lúc mở game qua SettingsWindow.ApplySaved() (GameHud gọi khi dựng HUD).
    /// </summary>
    public class SettingsWindow : GameWindow
    {
        private const string PrefFps = "fps_limit", PrefQuality = "quality";
        private TextMeshProUGUI _music, _sfx, _fps, _quality;

        protected override void Build()
        {
            Title = "Cài đặt";
            Size = new Vector2(460, 370);
            var body = CreateBody();
            _music = Line(body, 0, "Nhạc nền", () => Step(-0.1f, true), () => Step(0.1f, true));
            _sfx = Line(body, 1, "Hiệu ứng", () => Step(-0.1f, false), () => Step(0.1f, false));
            _fps = Line(body, 2, "Giới hạn FPS", () => SetFps(30), () => SetFps(60), "30", "60");
            _quality = Line(body, 3, "Đồ hoạ", () => SetQuality(-1), () => SetQuality(1));
            var note = UIKit.Text("Note", body, "Điện thoại yếu / tiết kiệm pin: chọn 30 FPS + đồ hoạ thấp.", 13, TextAlignmentOptions.Center, UIKit.DimText);
            note.rectTransform.Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 44), new Vector2(0, 30), new Vector2(0.5f, 0));
            var tut = UIKit.Button("Tutorial", body, "Xem hướng dẫn tân thủ", () => Get<TutorialWindow>().ShowFromStart(), 15);
            ((RectTransform)tut.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 4), new Vector2(240, 36), new Vector2(0.5f, 0));
        }

        private TextMeshProUGUI Line(RectTransform body, int row, string label, System.Action minus, System.Action plus, string a = "-", string b = "+")
        {
            float y = -row * 56;
            var l = UIKit.Text("L" + row, body, label, 17);
            l.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, y), new Vector2(150, 44), new Vector2(0, 1));
            var v = UIKit.Text("V" + row, body, "", 17, TextAlignmentOptions.Center, UIKit.ButtonHot);
            v.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(210, y), new Vector2(120, 44), new Vector2(0, 1));
            var bm = UIKit.Button("M" + row, body, a, minus, 18);
            ((RectTransform)bm.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(155, y - 2), new Vector2(50, 40), new Vector2(0, 1));
            var bp = UIKit.Button("P" + row, body, b, plus, 18);
            ((RectTransform)bp.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(335, y - 2), new Vector2(50, 40), new Vector2(0, 1));
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
