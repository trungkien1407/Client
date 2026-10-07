using Assets.Script.Player;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// HƯỚNG DẪN TÂN THỦ: tự hiện LẦN ĐẦU mỗi nhân vật vào game (nhớ bằng PlayerPrefs theo id nhân vật),
    /// xem lại được trong Cài đặt → "Xem hướng dẫn". Nội dung tự đổi theo thiết bị (bàn phím hay cảm ứng).
    ///
    /// [CẦN ĐIỀN khi có art] có thể thêm ảnh minh hoạ cho từng trang (Image phía trên chữ).
    /// </summary>
    public class TutorialWindow : GameWindow
    {
        private TextMeshProUGUI _text, _page;
        private Button _prev, _next;
        private int _index;

        private static bool Touch => Touchscreen.current != null && Keyboard.current == null;

        private static string[] Pages() => Touch
            ? new[]
            {
                "<b>1. Di chuyển</b>\n\nKéo <b>cần điều khiển</b> góc trái dưới để đi.\nNút <b>Nhảy</b> bên phải để nhảy lên bậc cao.",
                "<b>2. Đánh quái</b>\n\nChạm vào quái để <b>chọn mục tiêu</b>, rồi bấm nút <b>Đánh</b>.\nChưa chọn ai thì tự đánh quái gần nhất.\nBấm các ô kỹ năng ở dưới để đổi chiêu.",
                "<b>3. Nhận nhiệm vụ</b>\n\nĐến gần <b>Hokage</b> và bấm nút <b>Nói</b>.\nLàm theo bảng <b>Nhiệm vụ</b> bên trái màn hình — làm xong quay lại trả để nhận EXP, yên, đồ.",
                "<b>4. Mạnh lên</b>\n\nLên cấp được <b>điểm tiềm năng</b> (nút <b>Nhân vật</b>) và <b>điểm kỹ năng</b> (nút <b>Kỹ năng</b>).\nMặc đồ trong <b>Túi</b>. Gặp <b>Thợ Rèn</b> để <b>nâng cấp</b> trang bị lên +16.",
                "<b>5. Chơi cùng mọi người</b>\n\nNút <b>Chat</b> góc trái để trò chuyện.\nChạm 1 người chơi → <b>Tương tác</b>: mời nhóm, giao dịch, kết bạn, tỉ thí.\n<b>Xã hội</b>: nhóm, bạn bè, gia tộc. Hằng ngày: <b>điểm danh</b>, phó bản, boss thế giới, lôi đài.",
            }
            : new[]
            {
                "<b>1. Di chuyển</b>\n\n<b>A / D</b> (hoặc phím mũi tên trái / phải) để đi, <b>W</b> (hoặc mũi tên lên) để nhảy.",
                "<b>2. Đánh quái</b>\n\n<b>Click</b> vào quái hoặc <b>Tab</b> để chọn mục tiêu, <b>J</b> để đánh.\nPhím <b>1–5</b> đổi kỹ năng. <b>Q / E</b> uống bình máu / chakra. <b>R</b> hồi sinh khi chết.",
                "<b>3. Nhận nhiệm vụ</b>\n\nĐến gần <b>Hokage</b> và bấm <b>F</b> để nói chuyện.\nLàm theo bảng <b>Nhiệm vụ</b> bên trái màn hình (phím <b>L</b> xem chi tiết) — xong quay lại trả để nhận thưởng.",
                "<b>4. Mạnh lên</b>\n\n<b>C</b> nhân vật (cộng điểm tiềm năng) · <b>K</b> kỹ năng · <b>I</b> túi đồ (mặc trang bị).\nGặp <b>Thợ Rèn</b> để <b>nâng cấp</b> trang bị lên +16 và cất đồ vào <b>rương</b>.",
                "<b>5. Chơi cùng mọi người</b>\n\n<b>Enter</b> để chat (/a thế giới, /g gia tộc, /w Tên nhắn riêng).\nClick 1 người chơi → <b>Tương tác</b>: mời nhóm, giao dịch, kết bạn, tỉ thí.\n<b>O</b> xã hội · <b>M</b> thư. Hằng ngày: điểm danh, phó bản, boss thế giới, lôi đài.",
            };

        protected override void Build()
        {
            Title = "Hướng dẫn tân thủ";
            Size = new Vector2(600, 380);
            var body = CreateBody();
            _text = UIKit.Text("Text", body, "", 19, TextAlignmentOptions.TopLeft);
            _text.rectTransform.Fill(10, 10, 6, 60);
            _page = UIKit.Text("Page", body, "", 14, TextAlignmentOptions.Center, UIKit.DimText);
            _page.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(120, 36), new Vector2(0.5f, 0));
            _prev = UIKit.Button("Prev", body, "Trước", () => { _index--; Refresh(); }, 16);
            ((RectTransform)_prev.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(6, 6), new Vector2(120, 44), Vector2.zero);
            _next = UIKit.Button("Next", body, "Tiếp", Next, 16);
            ((RectTransform)_next.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-6, 6), new Vector2(140, 44), new Vector2(1, 0));
            _next.image.color = UIKit.ButtonHot;
        }

        private void Next()
        {
            if (_index >= Pages().Length - 1) { Hide(); return; }
            _index++;
            Refresh();
        }

        public void ShowFromStart()
        {
            _index = 0;
            Show();
        }

        /// <summary>GameHud gọi khi vào game: nhân vật này chưa xem hướng dẫn thì mở.</summary>
        public static void ShowIfFirstTime()
        {
            string key = "tutorial_seen_" + LocalPlayerState.Id;
            try
            {
                if (PlayerPrefs.GetInt(key, 0) == 1) return;
                PlayerPrefs.SetInt(key, 1);
                PlayerPrefs.Save();
            }
            catch { /* PlayerPrefs lỗi thì vẫn hiện */ }
            Get<TutorialWindow>().ShowFromStart();
        }

        protected override void Refresh()
        {
            var pages = Pages();
            _index = Mathf.Clamp(_index, 0, pages.Length - 1);
            _text.text = pages[_index];
            _page.text = $"{_index + 1} / {pages.Length}";
            _prev.gameObject.SetActive(_index > 0);
            _next.SetLabel(_index == pages.Length - 1 ? "Bắt đầu chơi" : "Tiếp");
        }
    }
}
