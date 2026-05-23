using UnityEngine;
using UnityEngine.UI;
using Cinemachine; // Bắt buộc phải có thư viện này

namespace Assets.Script.Map
{
    public class MinimapController : MonoBehaviour
    {
        [Header("UI References")]
        public RectTransform minimapPanel;
        public Mask minimapMask;
        public RectTransform rawImageRect;

        [Header("Cinemachine References")]
        public CinemachineVirtualCamera minimapVcam;
        public CinemachineConfiner2D minimapConfiner;

        // [HideInInspector] để ẩn trên Unity vì GameMaster sẽ tự động gán biến này bằng code
        [HideInInspector]
        public Transform playerTarget;
        private Transform dynamicMapCenter;

        [Header("Zoom Settings")]
        public float uiZoomScale = 3f;
        public float normalCamSize = 15f;
        public float fullMapCamSize = 50f;

        private bool isZoomed = false;

        // Biến lưu UI
        private Vector2 originalPanelAnchoredPos;
        private Vector2 originalPanelAnchorMin;
        private Vector2 originalPanelAnchorMax;
        private Vector3 originalRawImageScale;

        void Start()
        {
            // Lưu lại thông số UI mặc định
            originalPanelAnchoredPos = minimapPanel.anchoredPosition;
            originalPanelAnchorMin = minimapPanel.anchorMin;
            originalPanelAnchorMax = minimapPanel.anchorMax;
            originalRawImageScale = rawImageRect.localScale;

            // Set size mặc định
            if (minimapVcam != null)
            {
                minimapVcam.m_Lens.OrthographicSize = normalCamSize;
            }
        }

        // GameMaster sẽ gọi hàm này và truyền vào điểm giữa map
        public void SetMapCenter(Transform centerTransform)
        {
            dynamicMapCenter = centerTransform;
        }

        public void ToggleMinimap()
        {
            isZoomed = !isZoomed;

            if (isZoomed)
            {
                // --- 1. PHÓNG TO UI ---
                minimapMask.enabled = false;
                minimapPanel.anchorMin = new Vector2(0.5f, 0.5f);
                minimapPanel.anchorMax = new Vector2(0.5f, 0.5f);
                minimapPanel.anchoredPosition = Vector2.zero;
                rawImageRect.localScale = new Vector3(uiZoomScale, uiZoomScale, 1f);

                // --- 2. CAMERA QUAN SÁT TOÀN MAP ---
                if (minimapVcam != null)
                {
                    // Bắt buộc tắt Confiner khi phóng to để không bị lỗi màn hình to hơn map
                    if (minimapConfiner != null) minimapConfiner.enabled = false;

                    minimapVcam.m_Lens.OrthographicSize = fullMapCamSize;
                    minimapVcam.Follow = dynamicMapCenter;
                }
            }
            else
            {
                // --- 1. THU NHỎ UI VỀ GÓC ---
                minimapMask.enabled = true;
                minimapPanel.anchorMin = originalPanelAnchorMin;
                minimapPanel.anchorMax = originalPanelAnchorMax;
                minimapPanel.anchoredPosition = originalPanelAnchoredPos;
                rawImageRect.localScale = originalRawImageScale;

                // --- 2. CAMERA BÁM LẠI NHÂN VẬT ---
                if (minimapVcam != null)
                {
                    minimapVcam.m_Lens.OrthographicSize = normalCamSize;
                    minimapVcam.Follow = playerTarget;

                    // Bật lại tính năng cản viền sau khi camera đã thu nhỏ an toàn
                    if (minimapConfiner != null) minimapConfiner.enabled = true;
                }
            }
        }
    }
}