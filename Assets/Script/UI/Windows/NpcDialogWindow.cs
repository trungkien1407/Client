using System.Collections.Generic;
using Assets.Script.Data;
using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// HỘI THOẠI NPC: hiện lời thoại + các lựa chọn server gửi (NPC_MENU).
    /// Bấm 1 lựa chọn → gửi NPC_SELECT(index). Server quyết định làm gì (mở shop, nhận/trả nhiệm vụ...)
    /// và có thể gửi menu mới → cửa sổ tự hiện lại.
    /// </summary>
    public class NpcDialogWindow : GameWindow
    {
        [SerializeField] private TextMeshProUGUI _text;
        [SerializeField] private RectTransform _options;
        private int _npcId;

        protected override void Configure()
        {
            Title = "NPC";
            Size = new Vector2(560, 420);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _text = UIKit.Text("Text", body, "", 18, TextAlignmentOptions.TopLeft);
            _text.rectTransform.Place(new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, new Vector2(0, 150), new Vector2(0.5f, 1));
            _options = UIKit.ScrollList("Options", body, 6);
            ((RectTransform)_options.parent).Fill(0, 0, 158, 0);
        }

        /// <summary>Không có nút cố định: các lựa chọn sinh lúc chạy trong ShowMenu.</summary>
        protected override void Bind() { }

        private readonly List<string> _optionTexts = new List<string>();
        public int NpcId => _npcId;
        /// <summary>Lời thoại đang hiện (AutoTest kiểm hội thoại nhiều trang).</summary>
        public string Text => _text != null ? _text.text : "";

        /// <summary>Chọn lựa chọn đầu tiên có chứa chữ này (dùng cho AutoTest). Trả về false nếu không có.</summary>
        public bool Choose(string contains)
        {
            int i = _optionTexts.FindIndex(o => o.Contains(contains));
            if (i < 0) return false;
            Hide();
            GameActions.NpcSelect(_npcId, i);
            return true;
        }

        public void ShowMenu(int npcId, string npcName, string text, List<string> options)
        {
            _optionTexts.Clear();
            _optionTexts.AddRange(options);
            _npcId = npcId;
            Show();
            SetTitle(npcName);
            _text.text = text;
            UIKit.Clear(_options);
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                UIKit.Button("Opt" + i, _options, options[i], () =>
                {
                    Hide();
                    GameActions.NpcSelect(_npcId, index);
                }, 17).Height(40);
            }
        }
    }
}
