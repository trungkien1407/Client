using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// HÌNH CỦA HUD CHÍNH (prefab Resources/UI/Hud/GameHudView): thanh EXP, số cấp, 2 hàng nút menu, nút khu, dòng tiền,
    /// banner, khung sự kiện, đồng đội, theo dõi nhiệm vụ, chữ CHOÁNG, nút Tương tác. Chỉ giữ tham chiếu — GameHud đổ dữ liệu.
    /// Minimap là prefab riêng (MinimapHud), GameHud đặt vào trong HUD này.
    /// </summary>
    public class GameHudView : HudPanel
    {
        /// <summary>Thứ tự nút menu — GameHud nối hành động theo đúng thứ tự này.</summary>
        public static readonly string[] MenuLabels =
            { "Nhân vật", "Túi", "Kỹ năng", "Nhiệm vụ", "Nói (F)", "Hoạt động", "Xã hội", "Thư", "Xếp hạng", "Cẩm nang", "Cài đặt", "Hoà bình" };
        public const int MenuActivity = 5, MenuMail = 7, MenuPk = 11;

        public Image expFill;
        public TextMeshProUGUI expText, levelText, money, banner, eventText, partyText, questText, stun;
        public Image eventPanel;
        public Button[] menu = new Button[12];
        public Button zoneBtn, questBtn, interactBtn;

        protected override void Build()
        {
            var root = transform;
            float minimapW = GameHud.MinimapWidth;

            // ---- Thanh EXP ----
            expFill = UIKit.Bar("ExpBar", root, new Color(0.95f, 0.75f, 0.15f));
            ((RectTransform)expFill.transform.parent).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), new Vector2(-4, 14), new Vector2(0.5f, 0));
            expText = UIKit.Text("ExpText", expFill.transform.parent, "", 12, TextAlignmentOptions.Center);
            expText.rectTransform.Fill();

            // Số cấp đè lên ô "LV" của khung avatar (ảnh khung có sẵn chữ LV, chưa có số)
            levelText = UIKit.Text("Level", root, "", 20, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.3f));
            levelText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -102), new Vector2(80, 26), new Vector2(0, 0.5f));
            levelText.fontStyle = FontStyles.Bold;
            levelText.raycastTarget = false;

            // ---- Nút menu: 2 hàng x 6, xếp sang trái minimap ----
            for (int i = 0; i < MenuLabels.Length; i++)
            {
                int row = i / 6, col = i % 6;
                menu[i] = UIKit.Button("Menu" + i, root, MenuLabels[i], null, 14);
                ((RectTransform)menu[i].transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-minimapW - col * 78, -8 - row * 40), new Vector2(74, 34), new Vector2(1, 1));
            }

            // ---- Nút đổi khu ngay dưới minimap ----
            zoneBtn = UIKit.Button("Zone", root, "Khu", null, 14);
            ((RectTransform)zoneBtn.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8, -MinimapHud.H - 14), new Vector2(MinimapHud.W, 30), new Vector2(1, 1));

            money = UIKit.Text("Money", root, "", 15, TextAlignmentOptions.MidlineRight, new Color(1f, 0.85f, 0.3f));
            money.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-minimapW, -88), new Vector2(460, 24), new Vector2(1, 1));

            // ---- Banner + khung sự kiện ----
            banner = UIKit.Text("Banner", root, "", 26, TextAlignmentOptions.Center, new Color(1f, 0.85f, 0.3f));
            banner.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -190), new Vector2(900, 80), new Vector2(0.5f, 1));
            banner.fontStyle = FontStyles.Bold;
            banner.enableAutoSizing = true;   // thông báo GM dài → tự thu nhỏ chữ
            banner.fontSizeMin = 16;
            banner.fontSizeMax = 26;
            banner.outlineWidth = 0.25f;
            banner.gameObject.SetActive(false);
            eventPanel = UIKit.Panel("EventPanel", root, new Color(0, 0, 0, 0.55f));
            eventPanel.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-minimapW, -116), new Vector2(380, 120), new Vector2(1, 1));
            eventPanel.raycastTarget = false;
            eventText = UIKit.Text("Text", eventPanel.transform, "", 15, TextAlignmentOptions.Top);
            eventText.rectTransform.Fill(6, 6, 4, 4);
            eventPanel.gameObject.SetActive(false);

            // ---- Đồng đội (trái, dưới khung máu của scene) ----
            partyText = UIKit.Text("Party", root, "", 14, TextAlignmentOptions.TopLeft);
            partyText.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -300), new Vector2(260, 160), new Vector2(0, 1));

            // ---- Theo dõi nhiệm vụ — bấm để mở bảng Nhiệm vụ ----
            questBtn = UIKit.Button("QuestTracker", root, "", null, 14);
            questBtn.image.color = new Color(0, 0, 0, 0.35f);
            ((RectTransform)questBtn.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -146), new Vector2(300, 140), new Vector2(0, 1));
            questText = questBtn.GetComponentInChildren<TextMeshProUGUI>();
            questText.alignment = TextAlignmentOptions.TopLeft;
            questText.rectTransform.Fill(8, 6, 4, 4);

            // ---- Choáng ----
            stun = UIKit.Text("Stun", root, "CHOÁNG!", 30, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.2f));
            stun.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 90), new Vector2(300, 50));
            stun.fontStyle = FontStyles.Bold;
            stun.gameObject.SetActive(false);

            // ---- Tương tác với người chơi đang chọn ----
            interactBtn = UIKit.Button("Interact", root, "Tương tác", null, 16);
            ((RectTransform)interactBtn.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -138), new Vector2(140, 38), new Vector2(0.5f, 1));
            interactBtn.image.color = UIKit.ButtonHot;
            interactBtn.gameObject.SetActive(false);
        }
    }
}
