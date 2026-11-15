using System.Collections.Generic;
using UnityEngine;

namespace VocVach.StackFit
{
    /// <summary>
    /// 1 bộ hình cần ghép: ảnh DƯỚI (vd chân) đứng yên ở gốc, ảnh TRÊN (vd thân) đặt lệch {Offset} điểm ảnh.
    /// Quy ước toạ độ: điểm pivot của ảnh trên so với điểm pivot của ảnh dưới, đơn vị điểm ảnh gốc, x sang phải, y lên trên.
    /// </summary>
    public class StackFitItem
    {
        public string Id;          // khoá duy nhất (vd "12")
        public string Label;       // chữ hiện trong danh sách (vd "Bộ 12 — Hokage")
        public Sprite Top;         // ảnh trên (thân)
        public Sprite Bottom;      // ảnh dưới (chân) — có thể null (bộ 1 ảnh: không có gì để ghép)
        public Vector2 Offset;     // độ lệch đang lưu
        public bool Manual;        // true = đang dùng số chỉnh tay (đã lưu vào cấu hình của dự án)
        public object Tag;         // dữ liệu riêng của nguồn (vd asset gốc)
    }

    /// <summary>
    /// NGUỒN DỮ LIỆU cho cửa sổ ghép ảnh — mỗi dự án tự viết 1 lớp con (trong thư mục Editor của dự án).
    /// Cửa sổ tự tìm mọi lớp con (TypeCache) → hiện danh sách nguồn ở thanh trên cùng.
    /// Package không biết gì về game: đọc / ghi ở đâu (JSON, ScriptableObject, DB…) là việc của lớp con.
    /// </summary>
    public abstract class StackFitProvider
    {
        /// <summary>Tên hiện ở thanh chọn nguồn.</summary>
        public abstract string Title { get; }

        /// <summary>Đọc lại toàn bộ danh sách (gọi khi mở cửa sổ / bấm "Tải lại").</summary>
        public abstract List<StackFitItem> Load();

        /// <summary>Vị trí do thuật toán tự động tính (không tính số chỉnh tay) — vẽ bóng mờ để so. null = không có.</summary>
        public virtual Vector2? AutoOffset(StackFitItem item) => null;

        /// <summary>Lưu số chỉnh tay LÂU DÀI (vào cấu hình của dự án) + áp vào asset đang dùng. Cập nhật item.Offset / item.Manual.</summary>
        public abstract void Save(StackFitItem item, Vector2 offset);

        /// <summary>Bỏ số chỉnh tay, quay về số tự động. Cập nhật item.Offset / item.Manual.</summary>
        public abstract void ResetToAuto(StackFitItem item);

        /// <summary>Ảnh trên vẽ đè ảnh dưới (đúng thứ tự vẽ trong game)?</summary>
        public virtual bool TopDrawnOver => true;
    }
}
