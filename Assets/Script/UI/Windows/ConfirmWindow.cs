using System;
using System.Collections.Generic;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// HỘP XÁC NHẬN 2 NÚT (lời mời nhóm / giao dịch / kết bạn / tỉ thí / gia tộc, hỏi trước khi làm việc quan trọng).
    /// Nhiều lời mời tới cùng lúc → xếp hàng, xử lý lần lượt. Lời mời tự huỷ sau 30 giây (server cũng hết hạn).
    ///
    ///   ConfirmWindow.Ask("A mời bạn vào nhóm", "Đồng ý", () => GameActions.PartyAccept(id), "Từ chối", null);
    /// </summary>
    public class ConfirmWindow : GameWindow
    {
        private class Item { public string text, yes, no; public Action onYes, onNo; public float expire; }

        private static readonly Queue<Item> Pending = new Queue<Item>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook() => Data.GameData.OnSessionReset += () => Pending.Clear();   // lời mời của phiên cũ
        private Item _current;
        private TextMeshProUGUI _text, _timer;
        private Button _yes, _no;

        public static void Ask(string text, string yes, Action onYes, string no = "Huỷ", Action onNo = null, float timeout = 30f)
        {
            Pending.Enqueue(new Item { text = text, yes = yes, no = no, onYes = onYes, onNo = onNo, expire = Time.time + timeout });
            var w = Get<ConfirmWindow>();
            if (w._current == null) w.Next();
        }

        protected override void Build()
        {
            Title = "Xác nhận";
            Size = new Vector2(440, 230);
            var body = CreateBody();
            _text = UIKit.Text("Text", body, "", 19, TextAlignmentOptions.Center);
            _text.rectTransform.Fill(6, 6, 4, 64);
            _timer = UIKit.Text("Timer", body, "", 13, TextAlignmentOptions.TopRight, UIKit.DimText);
            _timer.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(80, 18), new Vector2(1, 1));
            _yes = UIKit.Button("Yes", body, "Đồng ý", () => Answer(true));
            ((RectTransform)_yes.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-80, 6), new Vector2(150, 44), new Vector2(0.5f, 0));
            _yes.image.color = UIKit.ButtonHot;
            _no = UIKit.Button("No", body, "Huỷ", () => Answer(false));
            ((RectTransform)_no.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(80, 6), new Vector2(150, 44), new Vector2(0.5f, 0));
        }

        private void Next()
        {
            _current = null;
            while (Pending.Count > 0)
            {
                var it = Pending.Dequeue();
                if (Time.time < it.expire) { _current = it; break; }
            }
            if (_current == null) { Hide(); return; }
            _text.text = _current.text;
            _yes.SetLabel(_current.yes);
            _no.SetLabel(_current.no);
            Show();
        }

        private void Answer(bool yes)
        {
            var it = _current;
            _current = null;
            if (it != null) (yes ? it.onYes : it.onNo)?.Invoke();
            Next();
        }

        private void Update()
        {
            if (_current == null) return;
            float left = _current.expire - Time.time;
            _timer.text = $"{Mathf.CeilToInt(Mathf.Max(0, left))}s";
            if (left <= 0) Answer(false);
        }

        private void OnDisable()
        {
            // Bấm X / Esc = từ chối lời mời đang hiện, chuyển sang lời mời kế tiếp (nếu có)
            if (_current != null)
            {
                var it = _current;
                _current = null;
                it.onNo?.Invoke();
            }
        }

        /// <summary>GameHud gọi mỗi frame: cửa sổ đang đóng mà còn lời mời chờ → hiện lời mời kế tiếp.</summary>
        public static void Pump()
        {
            if (Pending.Count == 0) return;
            var w = Get<ConfirmWindow>();
            if (!w.IsOpen) w.Next();
        }
    }
}
