using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// KHUNG MẸO (GUIDE_TIP) — hiện khi lên tới cấp mở tính năng mới. [Đã hiểu] đóng; [Xem cẩm nang] mở đúng chủ đề.
    /// Nhiều mẹo tới cùng lúc (lên nhiều cấp 1 lần) → xếp hàng, đóng cái này hiện cái sau.
    /// </summary>
    public class TipWindow : GameWindow
    {
        private TextMeshProUGUI _text;
        private int _topic;
        private readonly System.Collections.Generic.Queue<(string, string, int)> _queue = new System.Collections.Generic.Queue<(string, string, int)>();

        protected override void Build()
        {
            Title = "Mẹo";
            Size = new Vector2(480, 250);
            var body = CreateBody();
            _text = UIKit.Text("Text", body, "", 17, TextAlignmentOptions.TopLeft);
            _text.rectTransform.Fill(6, 6, 4, 56);
            var ok = UIKit.Button("Ok", body, "Đã hiểu", Next, 16);
            ((RectTransform)ok.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-4, 4), new Vector2(140, 44), new Vector2(1, 0));
            ok.image.color = UIKit.ButtonHot;
            var more = UIKit.Button("More", body, "Xem cẩm nang", () => { int t = _topic; Next(); Get<GuideWindow>().ShowTopic(t); }, 16);
            ((RectTransform)more.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(4, 4), new Vector2(170, 44), Vector2.zero);
        }

        public void ShowTip(string title, string text, int topic)
        {
            _queue.Enqueue((title, text, topic));
            if (!IsOpen) Next();
        }

        private void Next()
        {
            if (_queue.Count == 0) { Hide(); return; }
            var (title, text, topic) = _queue.Dequeue();
            _topic = topic;
            Show();
            SetTitle("Mẹo: " + title);
            _text.text = text;
            ((RectTransform)transform).anchoredPosition = new Vector2(0, 160); // phía trên giữa màn hình, không che nhân vật
        }
    }
}
