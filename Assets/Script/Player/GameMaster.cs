using UnityEngine;
using Cinemachine;
using Assets.Script.Map;
using Assets.Script.Manager;


namespace Assets.Script.Player
{
    public class GameMaster : MonoBehaviour
    {
        public static GameMaster Instance;

        [Header("Main Camera Setup")]
        public CinemachineVirtualCamera vcam;

        void Awake()
        {
            if (Instance == null) Instance = this;
        }

        private void OnEnable()
        {
            MapManager.OnMapLoaded += SetupCameraBounds;
        }

        private void OnDisable()
        {
            MapManager.OnMapLoaded -= SetupCameraBounds;
        }

        void SetupCameraBounds(GameObject mapVisual)
        {

            if (mapVisual == null) return;

            Transform boundsObj = mapVisual.transform.Find("CameraBounds");

            if (boundsObj != null)
            {
                PolygonCollider2D presetCollider = boundsObj.GetComponent<PolygonCollider2D>();

                // Camera chính không đi ra ngoài khung map (minimap vẽ từ tilemap — MapImage, không cần camera)
                CinemachineConfiner2D confiner = vcam.GetComponent<CinemachineConfiner2D>();
                if (confiner != null && presetCollider != null)
                {
                    confiner.m_BoundingShape2D = presetCollider;
                    confiner.InvalidateCache();
                }
            }
            else
            {
                Debug.LogWarning("Không tìm thấy object 'CameraBounds' trong Prefab Map!");
            }
        }

        /// <summary>NetworkPlayerManager gọi khi tạo xong nhân vật chính → camera bám theo.</summary>
        public void SetCameraFollow(Transform target)
        {
            if (vcam != null) vcam.Follow = target;
        }
    }
}
