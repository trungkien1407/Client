using UnityEngine;
using Assets.Script.Database;

namespace Assets.Script.Map
{
    public class Portal : MonoBehaviour
    {
        [Header("Arrow Settings")]
        public Transform arrowTransform;

        [Tooltip("Lệch gốc chung (VD: Y = 0.5 để mũi tên luôn nổi lên một chút)")]
        public Vector2 baseOffset = new Vector2(0f, 0.5f);

        [Tooltip("Khoảng cách tự động lùi mông lại (Ngược với hướng đang chỉ)")]
        public float pushBackDistance = 0.5f; // Tự động lùi lại cho khỏi khuất

        public float moveDistance = 0.3f; // Khoảng cách nhấp nhô
        public float speed = 5f;          // Tốc độ nhấp nhô

        private Vector3 startPos;
        private Vector3 moveAxis;
        private bool isReady = false;

        public void InitData(PortalData data)
        {
            if (arrowTransform != null)
            {
                // Reset Scale để chống lỗi lộn ngược
                arrowTransform.localScale = Vector3.one;

                // 1. Xác định hướng chỉ (moveAxis) và xoay/lật ảnh
                switch (data.direction)
                {
                    case 0: // Up
                        arrowTransform.localRotation = Quaternion.Euler(0, 0, 90);
                        moveAxis = Vector3.up;
                        break;
                    case 1: // Down
                        arrowTransform.localRotation = Quaternion.Euler(0, 0, -90);
                        moveAxis = Vector3.down;
                        break;
                    case 2: // Left
                        // Lật ảnh sang trái bằng Scale X = -1
                        arrowTransform.localRotation = Quaternion.Euler(0, 0, 0);
                        arrowTransform.localScale = new Vector3(-1, 1, 1);
                        moveAxis = Vector3.left;
                        break;
                    case 3: // Right
                    default:
                        // Bình thường hướng sang phải
                        arrowTransform.localRotation = Quaternion.Euler(0, 0, 0);
                        arrowTransform.localScale = new Vector3(1, 1, 1);
                        moveAxis = Vector3.right;
                        break;
                }

                // [MỚI] 2. TÍNH TOÁN VỊ TRÍ GỐC THÔNG MINH
                // Bước A: Lấy vị trí baseOffset làm nền tảng
                Vector3 calculatedPos = new Vector3(baseOffset.x, baseOffset.y, 0f);

                // Bước B: Lùi mông lại ngược hướng chỉ (-moveAxis) một khoảng pushBackDistance
                calculatedPos -= moveAxis * pushBackDistance;

                // Gán vị trí cuối cùng
                startPos = calculatedPos;
                isReady = true;
            }
        }

        private void Update()
        {
            if (!isReady || arrowTransform == null) return;

            // Tính toán nhấp nhô và cộng thẳng vào vị trí Offset gốc
            float animOffset = Mathf.Sin(Time.time * speed) * moveDistance;
            arrowTransform.localPosition = startPos + (moveAxis * animOffset);
        }

    }
}