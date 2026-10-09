using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Assets.Script.Network;
using Assets.Script.Constants;
using Assets.Script.Database;
using Assets.Script.Entities;

namespace Assets.Script.Manager
{
    public class NetworkNpcManager : MonoBehaviour
    {
        public static NetworkNpcManager Instance;

        [Header("Databases & Resources (Kéo Addressables vào đây)")]
        [Tooltip("[CẦN ĐIỀN] Kéo Assets/SO/NpcDatabase.asset")]
        public AssetReferenceT<NpcDatabaseSO> npcDatabaseRef;
        [Tooltip("[CẦN ĐIỀN] Kéo Assets/Prefabs/NpcPrefabs.prefab")]
        public AssetReferenceGameObject baseNpcPrefab;
        [Tooltip("[CẦN ĐIỀN] Kéo Assets/Atlas/NPC.spriteatlasv2 (chứa ảnh đầu/chân mọi NPC)")]
        public AssetReferenceT<SpriteAtlas> npcAtlasReference;

        // Lưu trữ dữ liệu thực tế sau khi tải từ RAM/Server
        private NpcDatabaseSO _npcDatabase;
        private SpriteAtlas _loadedAtlas;

        // Handle để giải phóng bộ nhớ khi tắt Manager
        private AsyncOperationHandle<NpcDatabaseSO> _dbHandle;
        private AsyncOperationHandle<SpriteAtlas> _atlasHandle;

        // Quản lý Entity
        private Dictionary<int, NpcEntity> activeNpcs = new Dictionary<int, NpcEntity>();

        // Hàng rào bảo vệ chống lỗi bóng ma (Ghost Spawn)
        private HashSet<int> pendingSpawns = new HashSet<int>();

        // Cờ kiểm tra điều kiện sẵn sàng
        private bool _isReady = false;
        private int _resourcesLoadedCount = 0;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        void Start()
        {
            NetworkEventDispatcher.Instance.AddHandler(Cmd.NPC_LIST, OnNpcList);   // nghe ngay (gói sớm giữ lại ở _earlyList)
            LoadResources();
        }

        private void LoadResources()
        {
            // 1. Tải Database SO
            _dbHandle = Addressables.LoadAssetAsync<NpcDatabaseSO>(npcDatabaseRef);
            _dbHandle.Completed += (handle) => {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    _npcDatabase = handle.Result;
                    CheckReady();
                }
                else Debug.LogError("[NPC Manager] Lỗi tải NpcDatabaseSO!");
            };

            // 2. Tải Global Atlas
            _atlasHandle = Addressables.LoadAssetAsync<SpriteAtlas>(npcAtlasReference);
            _atlasHandle.Completed += (handle) => {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    _loadedAtlas = handle.Result;
                    CheckReady();
                }
                else Debug.LogError("[NPC Manager] Lỗi tải Global Sprite Atlas!");
            };
        }

        private void CheckReady()
        {
            _resourcesLoadedCount++;
            if (_resourcesLoadedCount == 2)
            {
                _isReady = true;
                if (_earlyList != null) { var d = _earlyList; _earlyList = null; OnNpcList(d); }
            }
        }

        // NPC_LIST đến khi chưa tải xong DB + atlas: giữ bản mới nhất, tải xong mới dựng (trước đây bị bỏ → thiếu NPC)
        private byte[] _earlyList;

        private void OnNpcList(byte[] data)
        {
            if (!_isReady) { _earlyList = data; return; }

            ClearAllNpcs();

            MessageReader reader = new MessageReader(data);
            try
            {
                short count = reader.ReadShort();
                for (int i = 0; i < count; i++)
                {
                    int npcId = reader.ReadInt();
                    int templateId = reader.ReadInt();
                    float x = reader.ReadFloat();
                    float y = reader.ReadFloat();

                    SpawnNpc(npcId, templateId, x, y);
                }
            }
            catch (Exception e) { Debug.LogError("Lỗi NPC_LIST: " + e.Message); }
            finally { reader.Cleanup(); }
        }

        public void SpawnNpc(int npcId, int templateId, float x, float y)
        {
            if (_npcDatabase == null || _loadedAtlas == null) return;

            var config = _npcDatabase.GetNpcConfig(templateId);
            if (config == null) return;

            // BẬT BẢO VỆ: Đưa ID vào danh sách đang chờ tải Prefab
            pendingSpawns.Add(npcId);

            baseNpcPrefab.InstantiateAsync(new Vector3(x, y, 0), Quaternion.identity).Completed += (op) =>
            {
                // KIỂM TRA BẢO VỆ: Nếu lúc đang tải bị Clear map thì hủy ngay
                if (!pendingSpawns.Contains(npcId))
                {
                    if (op.Status == AsyncOperationStatus.Succeeded)
                        Addressables.ReleaseInstance(op.Result);
                    return;
                }

                // Tải an toàn, gỡ khỏi danh sách chờ
                pendingSpawns.Remove(npcId);

                if (op.Status == AsyncOperationStatus.Succeeded)
                {
                    NpcEntity entity = op.Result.GetComponent<NpcEntity>();
                    if (entity != null)
                    {
                        entity.Setup(config, npcId, _loadedAtlas);
                        activeNpcs[npcId] = entity;
                    }
                }
            };
        }

        public void ClearAllNpcs()
        {
            // 1. Xóa hàng chờ để các hàm Async đang chạy ngầm tự hủy object
            pendingSpawns.Clear();

            // 2. Thu hồi các object đang hiện trên map
            foreach (var npc in activeNpcs.Values)
            {
                if (npc != null && npc.gameObject != null)
                    Addressables.ReleaseInstance(npc.gameObject);
            }
            activeNpcs.Clear();
        }

        public NpcEntity GetNpc(int npcId)
        {
            activeNpcs.TryGetValue(npcId, out NpcEntity entity);
            return entity;
        }

        /// <summary>NPC đầu tiên thuộc loại templateId trong map hiện tại (null nếu không có) — VD tìm Thợ Rèn ở làng đang đứng.</summary>
        public NpcEntity FindByTemplate(int templateId)
        {
            foreach (var e in activeNpcs.Values)
                if (e != null && e.TemplateId == templateId) return e;
            return null;
        }

        void OnDestroy()
        {
            if (NetworkEventDispatcher.Instance != null)
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.NPC_LIST, OnNpcList);

            // Giải phóng sạch sẽ RAM khi chuyển Scene
            if (_dbHandle.IsValid()) Addressables.Release(_dbHandle);
            if (_atlasHandle.IsValid()) Addressables.Release(_atlasHandle);
        }
    }
}