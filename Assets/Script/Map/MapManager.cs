using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Collections;
using System;
using Assets.Script.Network;
using Assets.Script.Constants;

namespace Assets.Script.Map
{
    public class MapManager : MonoBehaviour
    {
        public static MapManager Instance { get; private set; }

        [Header("Runtime")]
        public GameObject currentMapInstance;
        private AsyncOperationHandle<GameObject> currentMapHandle;

        // Trả về luôn GameObject của Map vừa load để các hệ thống khác (như Cinemachine/Minimap) dễ setup
        public static event Action<GameObject> OnMapLoaded;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void LoadMap(int mapId)
        {
            StartCoroutine(LoadMapRoutine(mapId));
        }

        private IEnumerator LoadMapRoutine(int mapId)
        {
            // 1. Dọn dẹp map cũ trước khi nhảy sang map mới
            ClearMap();

            // 2. Định nghĩa Key của Prefab trong Addressables (Ví dụ: "Map_1", "Map_2")
            // Hãy đảm bảo bạn đặt tên Prefab trong nhóm Addressables khớp với chuỗi này.
            string prefabKey = $"Map_{mapId}";

            // 3. Tải và sinh ra Prefab Map ngay lập tức
            currentMapHandle = Addressables.InstantiateAsync(prefabKey);
            yield return currentMapHandle;

            if (currentMapHandle.Status == AsyncOperationStatus.Succeeded)
            {
                currentMapInstance = currentMapHandle.Result;
                currentMapInstance.transform.position = Vector3.zero;

                Debug.Log($"[Client] Đã load siêu tốc Prefab: {prefabKey}");

                // 4. Kích hoạt Event (Gửi kèm instance để script Camera tự đi tìm CameraBounds)
                OnMapLoaded?.Invoke(currentMapInstance);

                // 5. Báo cho Server biết để Server bắt đầu gửi data động (Quái, Người chơi khác)
                SendClientReady();
            }
            else
            {
                Debug.LogError($"[Client] LỖI: Không tìm thấy Prefab '{prefabKey}' trong Addressables! Bạn đã tick Addressable cho prefab chưa?");
            }
        }

        private void SendClientReady()
        {
            MessageWriter writer = new MessageWriter();
            // Gửi Cmd báo Server đã load map xong
            NetworkManager.Instance.Send(Cmd.CLIENT_READY, writer.ToArray());
            writer.Cleanup();
        }

        public void ClearMap()
        {
            if (currentMapInstance != null)
            {
                // Giải phóng hoàn toàn Prefab Map cũ khỏi bộ nhớ RAM
                Addressables.ReleaseInstance(currentMapInstance);
                currentMapInstance = null;
            }
        }
    }
}