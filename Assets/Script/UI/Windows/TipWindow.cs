using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// KHUNG MẸO (GUIDE_TIP) — hiện khi lên tới cấp mở tính năng mới. [Đã hiểu] đóng; [Xem cẩm nang] mở đúng chủ đề.
    /// Nhiều mẹo tới cùng lúc (lên nhiều cấp 1 lần) → xếp hàng, đóng cái này hiện cái sau.
    /// </summary>
    public class TipWindow : GameWindow
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private Button _ok, _more;
        private int _topic;
        private readonly System.Collections.Generic.Queue<(string, string, int)> _queue = new System.Collections.Generic.Queue<(string, string, int)>();

        protected override void Configure()
        {
            Title = "Mẹo";
            Size = new Vector2(480, 250);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _text = UIKit.Text("Text", body, "", 17, TextAlignmentOptions.TopLeft);
            _text.rectTransform.Fill(6, 6, 4, 56);
            _ok = UIKit.Button("Ok", body, "Đã hiểu", null, 16);
            ((RectTransform)_ok.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-4, 4), new Vector2(140, 44), new Vector2(1, 0));
            _ok.image.color = UIKit.ButtonHot;
            _more = UIKit.Button("More", body, "Xem cẩm nang", null, 16);
            ((RectTransform)_more.transform).Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(4, 4), new Vector2(170, 44), Vector2.zero);
        }

        protected override void Bind()
        {
            _ok.onClick.AddListener(Next);
            _more.onClick.AddListener(() => { int t = _topic; Next(); Get<GuideWindow>().ShowTopic(t); });
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
