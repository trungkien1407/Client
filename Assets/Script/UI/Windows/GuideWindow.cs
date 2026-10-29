using Assets.Script.UI.Kit;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Assets.Script.UI.Windows
{
    /// <summary>
    /// CẨM NANG NHẪN GIẢ (phím G) — GĐ9. Trái: danh sách chủ đề; phải: nội dung.
    /// Khác TutorialWindow (5 trang lần đầu vào game): đây là "sách tra cứu" mở lúc nào cũng được,
    /// và khung Mẹo khi lên cấp (TipWindow) mở thẳng tới đúng chủ đề (số thứ tự = topic server gửi trong GUIDE_TIP).
    ///
    /// [CẦN ĐIỀN khi đổi luật chơi] sửa chữ ở mảng Topics bên dưới cho khớp server (CAN_BANG.md, NOI_DUNG.md).
    /// </summary>
    public class GuideWindow : GameWindow
    {
        /// <summary>(tiêu đề, nội dung). THỨ TỰ = số "topic" server dùng (GuideService.TIPS) — chỉ thêm vào cuối.</summary>
        public static readonly (string title, string body)[] Topics =
        {
            ("Điều khiển",
             "<b>Máy tính:</b> A/D hoặc ← → để đi, W/↑ để nhảy. Đến gần quái là <b>tự chọn</b> con gần nhất (ra xa thì tự đổi con khác); Click / Tab để chọn tay, Esc bỏ chọn. <b>Enter</b>: quái → đánh (giữ để đánh liên tục), NPC → nói chuyện, người → tương tác; " +
             "ở xa thì nhân vật tự chạy lại. J đánh bằng chiêu đang chọn (cả người khi bật Đồ sát). Phím 1–5 dùng chiêu ở ô đó: chiêu đánh thì thành chiêu J / Enter đánh tiếp, chiêu hỗ trợ (hồi máu, buff) chỉ dùng 1 lần. Q/E uống bình máu/chakra. F nói chuyện với NPC gần nhất. T để chat. R hồi sinh — về điểm đã lưu ở Trưởng làng / Hiệu trưởng (\"Lưu điểm hồi sinh\"), chưa lưu thì về Làng Lá.\n" +
             "<b>Z</b> (hoặc nút Auto) bật tự động: tự tìm quái, đánh bằng chiêu đang chọn, nhặt đồ, uống bình — chỉnh ở <b>U</b> (Cài đặt → Tự động), chọn cả chiêu buff tự dùng. Có <b>Tự động luyện tập</b> còn hạn (bán ở Hokage) thì dịch chuyển thẳng tới quái, không có thì chạy bộ.\n\n" +
             "<b>Điện thoại:</b> cần điều khiển góc trái, nút Nhảy/Đánh bên phải; chạm quái để chọn, chạm NPC rồi bấm Nói.\n\n" +
             "<b>Phím tắt cửa sổ:</b> C nhân vật · I túi · K kỹ năng · L nhiệm vụ · H hoạt động · G cẩm nang · O xã hội · M thư."),
            ("Nhiệm vụ",
             "<b>Chính tuyến</b> kể câu chuyện Hắc Vân Hội, dẫn ngươi đi từ Làng Lá tới Núi Tuyết — làm theo thứ tự, mỗi nhiệm vụ mở nhiệm vụ sau.\n\n" +
             "<b>Hằng ngày</b> [Ngày]: làm lại mỗi ngày (reset 0h), thưởng EXP theo cấp của ngươi.\n\n" +
             "<b>Nhiệm vụ trường</b>: chỉ hệ của ngươi thấy, thưởng vũ khí đúng hệ.\n\n" +
             "Mở <b>Nhiệm vụ (L)</b> để xem việc đang làm, <b>nơi cần tới</b>, và các nhiệm vụ <b>có thể nhận</b> ở đâu. " +
             "Nói chuyện với NPC: bấm <b>Tiếp theo</b> để nghe hết câu chuyện, hoặc bấm nút bỏ qua."),
            ("Nhân vật & tiềm năng",
             "Mỗi cấp được <b>5 điểm tiềm năng</b> (Nhân vật – C):\n" +
             "• <b>Sức mạnh</b>: tăng sát thương.\n• <b>Thân pháp</b>: tăng né tránh và chí mạng.\n" +
             "• <b>Chakra</b>: tăng MP (dùng chiêu).\n• <b>Thể lực</b>: tăng HP.\n\n" +
             "Chết sẽ mất một ít EXP (nhiều hơn nếu điểm PK cao). Ngoài chiến đấu máu/chakra tự hồi."),
            ("Kỹ năng & 3 hệ",
             "Mỗi cấp được <b>1 điểm kỹ năng</b> (Kỹ năng – K): học chiêu mới hoặc nâng cấp chiêu cũ, rồi gán vào ô 1–5.\n" +
             "Ra chiêu thì nhân vật đứng yên tới hết đòn; bấm tiếp lúc đòn sắp xong sẽ tự ra ngay sau đó. Số sát thương hiện đúng lúc đòn trúng.\n\n" +
             "<b>Đấu sĩ</b> (đấm đá cận chiến): máu và phòng thủ cao nhất, Trảm Phong đánh diện rộng, Thiết Thể tăng phòng thủ.\n" +
             "<b>Hỗ trợ</b> (phi tiêu): đánh xa 5 ô, Hồi Phục cho cả nhóm, Cổ Vũ tăng sức mạnh nhóm.\n" +
             "<b>Sát thủ</b> (đấm đá tốc độ): ra đòn nhanh, chí mạng cao, Ám Sát kết liễu kẻ ít máu, nhưng máu mỏng.\n\n" +
             "Về trường của hệ mình (3 cổng trên thềm cao ở Làng Lá) để nhận nhiệm vụ trường."),
            ("Thế giới & đường đi",
             "Làng Lá → Đồi Hoa Cúc (cấp 2–8) → Rừng Trúc (9–13) → <b>Làng Đá</b> → Thung Lũng Đá (14–20) → Đầm Lầy Sương Mù (20–25) " +
             "→ <b>Làng Tuyết</b> → Núi Tuyết (26–32).\n\n" +
             "Đi qua cổng ở mép map (có chữ chỉ đường). Ở làng có <b>xe</b> đi nhanh giữa các làng (tốn yên, cần đủ cấp). " +
             "Nút <b>Khu</b> dưới bản đồ nhỏ để đổi sang khu vắng hơn. Làng và trường là <b>khu an toàn</b> (không PK)."),
            ("Trang bị & nâng cấp",
             "Mặc đồ trong <b>Túi (I)</b>: 10 ô trang bị — vũ khí, áo, quần, găng, nón, dây chuyền, ngọc bội, nhẫn, giày, phù. " +
             "Đồ chia thành <b>bộ</b> ở cấp 1 / 10 / 20 / 30: mỗi món trong bộ cần cao hơn món trước 1 cấp (VD bộ 10: vũ khí cấp 10, áo 11 … phù 19) " +
             "nên gần như cấp nào cũng có đồ mới để mặc. Vũ khí theo hệ (kiếm / quạt / kunai), còn lại dùng chung. " +
             "Đồ rơi từ quái, mua ở cửa hàng, hoặc thưởng nhiệm vụ.\n\n" +
             "<b>Thợ Rèn</b> (có ở mọi làng) nâng cấp đồ +1 → +16 bằng đá cường hoá + yên. Cấp càng cao càng dễ thất bại, thất bại có thể <b>tụt cấp</b> — " +
             "dùng <b>Bùa bảo hộ</b> để giữ cấp. Tên đồ đổi màu theo cấp. Thợ Rèn còn giữ <b>Rương đồ</b> cho ngươi.\n\n" +
             "<b>Đồ khoá</b> (thưởng nhiệm vụ, mua bằng xu/lượng) không giao dịch được.\n\n" +
             "<b>Phẩm chất</b>: mọi trang bị (nhặt, mua, thưởng) được tăng ngẫu nhiên <b>0–15%</b> mọi chỉ số gốc (chữ xanh \"+N%\" dưới tên; 12% trở lên màu cam, rất hiếm).\n" +
             "<b>Khảm ngọc</b> (Thợ Rèn → Khảm ngọc): Xích Ngọc +sát thương · Lục Ngọc +HP · Lam Ngọc +MP · Hoàng Ngọc +phòng thủ, mỗi loại 3 cấp. " +
             "Đồ dưới cấp 15 có 1 lỗ, từ cấp 15 có 2 lỗ. 3 viên cùng cấp <b>ghép</b> thành 1 viên cấp cao hơn. Tháo ngọc ra vẫn giữ lại ngọc. " +
             "Ngọc khoá khảm vào đồ thường thì đồ thành đồ khoá."),
            ("Phó bản & hoạt động ngày",
             "<b>Phó bản</b>: khu riêng cho nhóm, hạ hết quái trước khi hết giờ. 2 lượt/ngày. Hang Ốc Sên (cấp 5, Hokage), Động Xà Vương (cấp 18, Trưởng Làng Đá).\n\n" +
             "<b>Hoạt động (H)</b>: điểm danh, nhiệm vụ ngày, phó bản, boss, lôi đài, diệt quái, nâng cấp... mỗi việc cho điểm. " +
             "Đủ 20 / 40 / 60 / 80 / 100 điểm thì bấm nhận rương (yên, xu, đá, Bùa bảo hộ). Reset 0h mỗi ngày.\n\n" +
             "<b>Quà online</b>: online đủ 10 / 30 / 60 / 120 phút trong ngày → nhận quà ngay trong cửa sổ Hoạt động.\n" +
             "<b>Giftcode</b>: mã quà Ban quản trị phát trên fanpage / sự kiện — gõ vào ô dưới cùng cửa sổ Hoạt động, quà về Hòm thư. Mỗi mã dùng 1 lần."),
            ("Boss thế giới & sự kiện",
             "<b>Boss thế giới</b> xuất hiện mỗi ngày (mặc định 12h và 20h), báo trước 5 phút:\n" +
             "• Cửu Vĩ Ốc (cấp 15) – Đồi Hoa Cúc\n• Thạch Ma Vương (cấp 25) – Thung Lũng Đá\n• Băng Hồ Vương (cấp 34) – Núi Tuyết\n" +
             "Ai cũng đánh được. EXP chia theo sát thương; quà theo hạng gửi qua <b>Thư</b>.\n\n" +
             "<b>Lôi đài sinh tồn</b> (21h): đăng ký ở Hokage, ai hạ nhiều đối thủ nhất thắng. Lịch chính xác xem ở cửa sổ Hoạt động."),
            ("Nhóm, bạn bè, gia tộc",
             "Click 1 người chơi → <b>Tương tác</b>: mời nhóm, giao dịch, kết bạn, tỉ thí.\n\n" +
             "<b>Nhóm</b>: chia EXP (+10% mỗi người thêm), cùng vào phó bản, Hỗ trợ hồi máu cho cả nhóm.\n" +
             "<b>Gia tộc</b> (Xã hội – O): lập bằng yên, mời người, góp quỹ, chat riêng bằng /g.\n" +
             "<b>Thư (M)</b>: gửi kèm đồ/yên cho bạn; phần thưởng sự kiện cũng tới qua thư."),
            ("Giao dịch & tiền tệ",
             "<b>Yên</b>: tiền thường, kiếm từ quái/nhiệm vụ, giao dịch được.\n<b>Xu</b>: kiếm trong game (điểm danh, boss...), mua đồ ở cửa hàng xu.\n" +
             "<b>Lượng</b>: nạp, mua ở cửa hàng lượng.\n\n" +
             "<b>Giao dịch an toàn</b>: hai bên bỏ đồ → cùng bấm <b>Khoá</b> → cùng bấm <b>Xác nhận</b>. Sau khi khoá không ai đổi được đồ — kiểm tra kỹ trước khi xác nhận.\n\n" +
             "<b>Chợ</b> (Chủ Chợ ở mọi làng): treo bán đồ với giá tự đặt — người khác mua được cả khi ngươi offline, tiền về <b>hòm thư</b>. " +
             "Phí treo 1% giá (tối thiểu 100 yên), thuế 5% khi bán được, tối đa 8 món, mỗi món 48 giờ — hết hạn đồ tự về hòm thư."),
            ("PK & tỉ thí",
             "Nút <b>Hoà bình / Đồ sát</b>: bật đồ sát mới đánh được người khác (ngoài khu an toàn). Giết người tăng <b>điểm PK</b> — tên đổi màu, chết mất nhiều EXP hơn.\n\n" +
             "<b>Tỉ thí</b>: mời 1 người đấu tay đôi, không ai chết thật, không mất gì."),
        };

        [SerializeField] private RectTransform _topics;
        [SerializeField] private TextMeshProUGUI _body;
        private int _sel;

        protected override void Configure()
        {
            Title = "Cẩm nang nhẫn giả";
            HotKey = Key.G;
            Size = new Vector2(760, 520);
        }

        protected override void Build()
        {
            var body = CreateBody();
            _topics = UIKit.ScrollList("Topics", body, 4);
            ((RectTransform)_topics.parent).Place(new Vector2(0, 0), new Vector2(0, 1), Vector2.zero, new Vector2(220, 0), new Vector2(0, 0.5f));
            var right = UIKit.ScrollList("Body", body, 0);
            ((RectTransform)right.parent).Fill(228, 0, 0, 0);
            _body = UIKit.Text("Text", right, "", 17, TextAlignmentOptions.TopLeft); // trong ScrollList: chữ dài tự cuộn
        }

        /// <summary>Không có nút cố định — nút chủ đề tạo lại trong Refresh(). Khai báo để tool xuất được prefab.</summary>
        protected override void Bind() { }

        /// <summary>Mở tới chủ đề thứ topic (TipWindow gọi).</summary>
        public void ShowTopic(int topic)
        {
            _sel = Mathf.Clamp(topic, 0, Topics.Length - 1);
            Show();
        }

        protected override void Refresh()
        {
            UIKit.Clear(_topics);
            for (int i = 0; i < Topics.Length; i++)
            {
                int idx = i;
                var b = UIKit.Button("T" + i, _topics, Topics[i].title, () => { _sel = idx; Refresh(); }, 15).Height(38);
                b.image.color = i == _sel ? UIKit.ButtonHot : UIKit.ButtonColor;
            }
            _body.text = $"<size=21><b>{Topics[_sel].title}</b></size>\n\n{Topics[_sel].body}";
        }
    }
}
