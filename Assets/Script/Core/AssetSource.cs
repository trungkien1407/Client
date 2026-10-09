using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using Object = UnityEngine.Object;

namespace Assets.Script.Core
{
    /// <summary>
    /// NGUỒN ASSET 2 TẦNG cho asset gọi bằng SỐ / TÊN (kho ảnh imgN, bộ hình quái MobAnim_N...):
    ///   1. Addressables, address = {address}      → asset chính thức (bundle, cập nhật từ xa được)
    ///   2. Resources/{resourcesPath}               → asset demo nội bộ (Assets/_Demo/Resources, gitignore — Tools/Naruto/Demo art)
    /// Không có ở cả 2 → trả null (người gọi hiện hình thay thế). Kết quả (kể cả "không có") nhớ suốt phiên.
    /// </summary>
    public static class AssetSource
    {
        private static readonly Dictionary<string, Object> Loaded = new Dictionary<string, Object>();
        private static readonly HashSet<string> Missing = new HashSet<string>();
        private static readonly Dictionary<string, Action<Object>> Pending = new Dictionary<string, Action<Object>>();

        /// <summary>Đã tải xong thì trả luôn (không chờ); chưa thì null.</summary>
        public static T Cached<T>(string address) where T : Object =>
            Loaded.TryGetValue(address, out var o) ? o as T : null;

        /// <summary>Tải bất đồng bộ; {done} chạy trên luồng chính (có thể ngay lập tức nếu đã tải).</summary>
        public static void Load<T>(string address, string resourcesPath, Action<T> done) where T : Object
        {
            if (Loaded.TryGetValue(address, out var o)) { done?.Invoke(o as T); return; }
            if (Missing.Contains(address)) { done?.Invoke(null); return; }
            if (Pending.TryGetValue(address, out var waiting)) { Pending[address] = waiting + (x => done?.Invoke(x as T)); return; }
            Pending[address] = x => done?.Invoke(x as T);

            Addressables.LoadResourceLocationsAsync(address, typeof(T)).Completed += locs =>
            {
                bool has = locs.Status == AsyncOperationStatus.Succeeded && locs.Result != null && locs.Result.Count > 0;
                IResourceLocation loc = has ? locs.Result[0] : null;
                Addressables.Release(locs);
                if (loc != null)
                    Addressables.LoadAssetAsync<T>(loc).Completed += h =>
                        Finish(address, h.Status == AsyncOperationStatus.Succeeded ? h.Result : null, resourcesPath);
                else
                    FromResources<T>(address, resourcesPath);
            };
        }

        private static void FromResources<T>(string address, string resourcesPath) where T : Object
        {
            if (string.IsNullOrEmpty(resourcesPath)) { Finish(address, null, null); return; }
            var req = Resources.LoadAsync<T>(resourcesPath);
            req.completed += _ => Finish(address, req.asset, null);
        }

        private static void Finish(string address, Object asset, string retryResources)
        {
            // Addressables có địa chỉ nhưng tải hỏng → thử Resources trước khi chịu thua
            if (asset == null && retryResources != null) { var req = Resources.LoadAsync(retryResources); req.completed += _ => Finish(address, req.asset, null); return; }
            if (asset != null) Loaded[address] = asset; else Missing.Add(address);
            if (Pending.TryGetValue(address, out var cb)) { Pending.Remove(address); cb?.Invoke(asset); }
        }
    }
}
