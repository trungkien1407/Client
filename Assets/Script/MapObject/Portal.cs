using UnityEngine;

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

        /// <summary>
        /// Cổng được đặt sẵn trong prefab map → tự khởi tạo: hướng nhấp nhô lấy theo góc xoay / lật (scale X âm)
        /// của mũi tên đã chỉnh trong Editor; vị trí gốc = baseOffset lùi ngược hướng chỉ pushBackDistance.
        /// (Trước đây cần gọi InitData(PortalData) nhưng không nơi nào gọi → mũi tên đứng im.)
        /// </summary>
        private void Start()
        {
            if (arrowTransform == null) return;
            moveAxis = arrowTransform.localRotation * Vector3.right;
            if (arrowTransform.localScale.x < 0) moveAxis = -moveAxis;
            moveAxis.z = 0f;
            moveAxis.Normalize();
            startPos = new Vector3(baseOffset.x, baseOffset.y, 0f) - moveAxis * pushBackDistance;
            isReady = true;
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