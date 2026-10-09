using System.Threading.Tasks;
using Assets.Script.Core;
using Assets.Script.Manager;
using Assets.Script.Map;
using Assets.Script.Models;
using Spine.Unity;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Assets.Script.Player
{
    /// <summary>
    /// TẢI HÌNH NHÂN VẬT: thân (prefab BasePlayer qua Addressables; người khác lấy lại từ pool) + xương Spine theo hệ phái,
    /// rồi gắn script điều khiển (mình: PlayerMovement · người khác: RemotePlayer). Không giữ danh sách — việc đó của NetworkPlayerManager.
    /// </summary>
    public static class PlayerSpawner
    {
        private const string BasePrefab = AddressKeys.BasePlayer;

        /// <summary>Tạo thân tại (x, y). Nhân vật mình đợi map tải xong trước. Lỗi tải → null.</summary>
        public static async Task<GameObject> CreateBody(PlayerData p, bool isLocal)
        {
            if (isLocal)
                while (MapManager.Instance == null || MapManager.Instance.currentMapInstance == null) await Task.Yield();

            GameObject obj = isLocal ? null : ObjectPoolManager.Instance.GetFromPool(BasePrefab);
            if (obj != null) return obj;
            var handle = Addressables.InstantiateAsync(BasePrefab, new Vector3(p.x, p.y, 0), Quaternion.identity);
            await handle.Task;
            return handle.Status == AsyncOperationStatus.Succeeded ? handle.Result : null;
        }

        /// <summary>Xương Spine của hệ phái; không có → null (nhân vật vẫn chạy, chỉ thiếu hình).</summary>
        public static async Task<SkeletonDataAsset> LoadSkeleton(int classType)
        {
            string key = AddressKeys.CharacterSkeleton(classType);
            var handle = Addressables.LoadAssetAsync<SkeletonDataAsset>(key);
            await handle.Task;
            if (handle.Status == AsyncOperationStatus.Succeeded) return handle.Result;
            Debug.LogWarning($"[Spine] Không tìm thấy dữ liệu xương cho key: {key}");
            return null;
        }

        /// <summary>Trả thân về: của mình thì huỷ hẳn, người khác thì cất vào pool để dùng lại.</summary>
        public static void Release(GameObject obj, bool isLocal)
        {
            if (obj == null) return;
            if (isLocal) Addressables.ReleaseInstance(obj);
            else ObjectPoolManager.Instance.ReturnToPool(BasePrefab, obj);
        }

        public static PlayerMovement SetupLocal(GameObject obj, PlayerData p, SkeletonDataAsset spine)
        {
            obj.name = $"LocalPlayer_{p.name}";
            if (!obj.TryGetComponent(out PlayerMovement pm)) pm = obj.AddComponent<PlayerMovement>();
            if (spine != null && obj.TryGetComponent(out PlayerVisualController vc))
            {
                vc.SetupVisual(spine, null);
                obj.GetComponentInChildren<MeshRenderer>().sortingOrder = 2;   // mình vẽ đè lên người khác
            }
            pm.Initialize(p, 1);
            if (obj.TryGetComponent(out Rigidbody2D rb)) rb.velocity = Vector2.zero;
            Object.FindAnyObjectByType<PlayerTargeting>()?.SetPlayer(obj.transform);
            return pm;
        }

        public static RemotePlayer SetupRemote(GameObject obj, PlayerData p, SkeletonDataAsset spine)
        {
            obj.name = $"RemotePlayer_{p.name}";
            obj.transform.SetParent(null);
            obj.transform.position = new Vector3(p.x, p.y, 0);
            if (!obj.TryGetComponent(out RemotePlayer rp)) rp = obj.AddComponent<RemotePlayer>();
            rp.poolKey = BasePrefab;
            rp.Initialize(p);
            if (spine != null && obj.TryGetComponent(out PlayerVisualController vc)) vc.SetupVisual(spine, null);

            // Người khác chỉ đi theo vị trí server gửi (RemotePlayer nội suy) → không chịu vật lý:
            // body Kinematic (không rơi), collider Trigger (không đẩy nhân vật mình, vẫn bấm chọn được)
            if (obj.TryGetComponent(out Rigidbody2D rb))
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.velocity = Vector2.zero;
            }
            if (obj.TryGetComponent(out BoxCollider2D box)) box.isTrigger = true;
            return rp;
        }
    }
}
