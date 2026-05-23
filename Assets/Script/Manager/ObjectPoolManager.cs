using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace Assets.Script.Manager
{
    public class ObjectPoolManager : MonoBehaviour
    {
        public static ObjectPoolManager Instance;

        private Dictionary<string, Queue<GameObject>> poolDictionary = new Dictionary<string, Queue<GameObject>>();

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        // Lấy Object từ Pool
        public GameObject GetFromPool(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (poolDictionary.TryGetValue(key, out Queue<GameObject> queue))
            {
                while (queue.Count > 0)
                {
                    GameObject obj = queue.Dequeue();
                    if (obj != null) // Đề phòng object đã bị Destroy ngoài ý muốn
                    {
                        obj.SetActive(true);
                        return obj;
                    }
                }
            }
            return null; // Pool trống
        }

        // Trả Object về Pool
        public void ReturnToPool(string key, GameObject obj)
        {
            if (obj == null || string.IsNullOrEmpty(key)) return;

            obj.SetActive(false);

            // Gom chung vào ObjectPoolManager để Hierarchy sạch sẽ
            obj.transform.SetParent(this.transform);

            if (!poolDictionary.ContainsKey(key))
            {
                poolDictionary[key] = new Queue<GameObject>();
            }
            poolDictionary[key].Enqueue(obj);
        }

        // [QUAN TRỌNG] Dọn dẹp hoàn toàn (Gọi khi Đăng xuất / Mất kết nối / Về màn hình Panel)
        public void ClearAllPools()
        {
            foreach (var queue in poolDictionary.Values)
            {
                while (queue.Count > 0)
                {
                    GameObject obj = queue.Dequeue();
                    if (obj != null)
                    {
                        // Release thực sự để giải phóng bộ nhớ RAM bằng Addressables
                        Addressables.ReleaseInstance(obj);
                    }
                }
            }
            poolDictionary.Clear();
            Debug.Log("[ObjectPool] Đã dọn dẹp toàn bộ Pool và giải phóng RAM.");
        }
    }
}