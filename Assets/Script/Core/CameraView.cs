using UnityEngine;

namespace Assets.Script.Core
{
    /// <summary>
    /// KHUNG NHÌN CAMERA (toạ độ thế giới, camera trực giao 2D) — tính 1 lần mỗi khung hình, dùng chung cho mọi đối tượng hỏi
    /// "mình có đang trên màn hình không" để BỎ việc vẽ khi ở ngoài (tối ưu hiển thị GĐ27):
    ///   quái ngoài màn hình không ghép lại các mảnh hình mỗi khung (MobController), không bật hiệu ứng khung đánh.
    /// Không có camera → coi như mọi thứ đều thấy (không bỏ gì).
    /// </summary>
    public static class CameraView
    {
        private static int _frame = -1;
        private static Rect _view;
        private static bool _has;
        private static Camera _cam;

        /// <summary>Điểm {pos} nằm trong khung nhìn (nới thêm {margin} đơn vị mỗi phía — thân to / sắp đi vào).</summary>
        public static bool Visible(Vector3 pos, float margin = 2f)
        {
            if (_frame != Time.frameCount) Recompute();
            if (!_has) return true;
            return pos.x >= _view.xMin - margin && pos.x <= _view.xMax + margin && pos.y >= _view.yMin - margin && pos.y <= _view.yMax + margin;
        }

        private static void Recompute()
        {
            _frame = Time.frameCount;
            if (_cam == null || !_cam.isActiveAndEnabled) _cam = Camera.main;
            _has = _cam != null && _cam.orthographic;
            if (!_has) return;
            float h = _cam.orthographicSize, w = h * _cam.aspect;
            Vector3 c = _cam.transform.position;
            _view = new Rect(c.x - w, c.y - h, 2f * w, 2f * h);
        }
    }
}
