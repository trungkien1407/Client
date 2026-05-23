using UnityEngine;
using Cinemachine;
using Assets.Script.Map;


public class GameMaster : MonoBehaviour
{
    public static GameMaster Instance;

    [Header("Main Camera Setup")]
    public CinemachineVirtualCamera vcam;

    [Header("Minimap Setup")]
    // [MỚI] Khai báo các biến cho Minimap
    public MinimapController minimapController;
    public CinemachineConfiner2D minimapConfiner;

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

            // --- 1. SETUP MAIN CAMERA ---
            CinemachineConfiner2D confiner = vcam.GetComponent<CinemachineConfiner2D>();
            if (confiner != null && presetCollider != null)
            {
                confiner.m_BoundingShape2D = presetCollider;
                confiner.InvalidateCache();
            }

            // --- 2. SETUP MINIMAP CAMERA ---
            if (minimapConfiner != null && presetCollider != null)
            {
                minimapConfiner.m_BoundingShape2D = presetCollider;
                minimapConfiner.InvalidateCache();
            }

            // --- 3. TẠO TÂM MAP CHO MINIMAP PHÓNG TO ---
            if (minimapController != null && presetCollider != null)
            {
                // Lấy trung tâm của khung giới hạn
                Vector3 centerPoint = presetCollider.bounds.center;

                // Tạo 1 object tàng hình tên là "MapCenterTarget" để Minimap Vcam bám vào khi phóng to
                GameObject centerGo = new GameObject("MapCenterTarget");
                centerGo.transform.SetParent(mapVisual.transform);
                centerGo.transform.position = centerPoint;

                // Nạp vào Minimap Controller
                minimapController.SetMapCenter(centerGo.transform);
            }

            Debug.Log("Cả 2 Camera đã thiết lập ranh giới theo Map mới thành công!");
        }
        else
        {
            Debug.LogWarning("Không tìm thấy object 'CameraBounds' trong Prefab Map!");
        }
    }

    // Cổng để NetworkPlayerManager gọi khi đẻ xong Nhân Vật Chính
    public void SetCameraFollow(Transform target)
    {
        // 1. Chĩa Main Camera
        if (vcam != null)
        {
            vcam.Follow = target;
        }

        // 2. Chĩa Minimap Camera
        if (minimapController != null && target != null)
        {
            // Báo cho script controller biết ai là nhân vật chính
            minimapController.playerTarget = target;

            // Chỉ bắt camera bám theo nếu minimap đang ở góc (không phải lúc đang phóng to)
            // (Nếu đang phóng to thì kệ nó, lát nó thu nhỏ lại code ToggleMinimap sẽ tự gán lại)
            if (minimapController.minimapVcam != null)
            {
                minimapController.minimapVcam.Follow = target;
            }
        }

        if (target != null)
        {
            Debug.Log("Cả 2 Camera đang chĩa vào: " + target.name);
        }
    }
}