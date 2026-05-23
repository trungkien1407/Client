using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Assets.Script.Network;
using Assets.Script.Constants;
using Assets.Script.Database;
using Assets.Script.Entities;
using Assets.Script.Models;

namespace Assets.Script.Manager
{
    public class NetworkMobManager : MonoBehaviour
    {
        public static NetworkMobManager Instance;

        [Header("Databases (Kéo Addressables vào đây)")]
        public AssetReferenceT<MobDatabaseSO> mobDatabaseRef;
        public AssetReferenceGameObject baseMobPrefab;

        // Biến giữ dữ liệu thực tế sau khi load
        private MobDatabaseSO _mobDatabase;
        private AsyncOperationHandle<MobDatabaseSO> _dbHandle;

        // Quản lý Entity
        private Dictionary<int, MobController> activeMobs = new Dictionary<int, MobController>();

        // Hàng rào bảo vệ chống lỗi quái ma (Ghost Spawn)
        private HashSet<int> pendingSpawns = new HashSet<int>();

        private bool _isDatabaseReady = false;

        // [MỚI] Key dùng chung cho toàn bộ Mob trong Pool
        private const string BASE_MOB_POOL_KEY = "BaseMobPrefab_Pool";

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        void Start()
        {
            _dbHandle = Addressables.LoadAssetAsync<MobDatabaseSO>(mobDatabaseRef);
            _dbHandle.Completed += handle => {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    _mobDatabase = handle.Result;
                    _mobDatabase.Init();
                    _isDatabaseReady = true;
                    RegisterNetworkHandlers();
                }
                else Debug.LogError("[Mob Manager] Lỗi tải MobDatabaseSO!");
            };
        }

        private void RegisterNetworkHandlers()
        {
            NetworkEventDispatcher.Instance.AddHandler(Cmd.MOB_LIST, OnMobList);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.MOB_MOVE, OnMobMove);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.MOB_MOVE_BATCH, OnMobMoveBatch);
        }

        private void UnregisterNetworkHandlers()
        {
            if (NetworkEventDispatcher.Instance != null)
            {
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.MOB_LIST, OnMobList);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.MOB_MOVE, OnMobMove);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.MOB_MOVE_BATCH, OnMobMoveBatch);
            }
        }

        private void OnMobList(byte[] data)
        {
            if (!_isDatabaseReady) return;

            ClearAllMobs();
            MessageReader reader = new MessageReader(data);
            try
            {
                short count = reader.ReadShort();
                for (int i = 0; i < count; i++)
                {
                    int mobId = reader.ReadInt();
                    int templateId = reader.ReadInt();
                    float x = reader.ReadFloat();
                    float y = reader.ReadFloat();
                    int hp = reader.ReadInt();
                    int maxHp = reader.ReadInt();
                    byte isDead = reader.ReadByte();

                    if (isDead == 0)
                    {
                        SpawnMob(mobId, (short)templateId, x, y, hp, maxHp);
                    }
                }
            }
            catch (Exception e) { Debug.LogError("Lỗi MOB_LIST: " + e.Message); }
            finally { reader.Cleanup(); }
        }

        public void SpawnMob(int mobId, short templateId, float x, float y, int hp, int maxHp)
        {
            if (_mobDatabase == null) return;

            var visual = _mobDatabase.GetMobVisual(templateId);
            if (visual == null)
            {
                Debug.LogWarning($"Không tìm thấy Visual cho Mob ID: {templateId}");
                return;
            }

            // [MỚI] 1. KIỂM TRA POOL TRƯỚC
            GameObject poolObj = ObjectPoolManager.Instance.GetFromPool(BASE_MOB_POOL_KEY);

            if (poolObj != null)
            {
                // Nếu Pool có sẵn, Setup ngay lập tức không cần chờ Load
                SetupMob(poolObj, mobId, templateId, x, y, hp, maxHp, visual);
            }
            else
            {
                // [MỚI] 2. NẾU POOL TRỐNG -> Load bằng Addressables
                pendingSpawns.Add(mobId);

                baseMobPrefab.InstantiateAsync(new Vector3(x, y, 0), Quaternion.identity).Completed += (op) => {

                    // Nếu người chơi chuyển map lúc quái đang load dở
                    if (!pendingSpawns.Contains(mobId))
                    {
                        if (op.Status == AsyncOperationStatus.Succeeded)
                        {
                            Addressables.ReleaseInstance(op.Result);
                        }
                        return;
                    }

                    pendingSpawns.Remove(mobId);

                    if (op.Status == AsyncOperationStatus.Succeeded)
                    {
                        SetupMob(op.Result, mobId, templateId, x, y, hp, maxHp, visual);
                    }
                };
            }
        }

        // [MỚI] Tách riêng hàm Setup để dùng chung cho cả khi lấy từ Pool lẫn lúc mới Load xong
        private void SetupMob(GameObject obj, int mobId, short templateId, float x, float y, int hp, int maxHp, MobVisualData visual)
        {
            obj.transform.position = new Vector3(x, y, 0);
            obj.transform.SetParent(null); // Kéo ra khỏi PoolManager Transform

            MobController mob = obj.GetComponent<MobController>();
            if (mob != null)
            {
                mob.Initialize(mobId, templateId, hp, maxHp);
                mob.SetVisual(visual);

                activeMobs[mobId] = mob;
                obj.name = $"Mob_{visual.mobName}_{mobId}";
            }

            // Đảm bảo quái không bị trôi do vật lý cũ còn kẹt lại
            if (obj.TryGetComponent(out Rigidbody2D rb))
            {
                rb.velocity = Vector2.zero;
            }
        }

        // ==========================================
        // QUẢN LÝ POOL & REMOVE
        // ==========================================

        // [MỚI] Dùng khi quái chết hoặc đi quá tầm nhìn
        public void RemoveMob(int mobId)
        {
            if (activeMobs.TryGetValue(mobId, out MobController mob))
            {
                ReturnMobToPool(mob);
                activeMobs.Remove(mobId);
            }
        }

        // [MỚI] Hàm trả riêng lẻ 1 Quái về Pool
        private void ReturnMobToPool(MobController mob)
        {
            if (mob == null || mob.gameObject == null) return;
            ObjectPoolManager.Instance.ReturnToPool(BASE_MOB_POOL_KEY, mob.gameObject);
        }

        public void ClearAllMobs()
        {
            // 1. Xóa hàng chờ để hủy tải dở dang
            pendingSpawns.Clear();

            // 2. Thu hồi toàn bộ quái trên map cất vào Pool (Thay vì ReleaseInstance)
            foreach (var mob in activeMobs.Values)
            {
                ReturnMobToPool(mob);
            }
            activeMobs.Clear();
        }

        private void OnMobMove(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            try
            {
                int id = reader.ReadInt();
                float x = reader.ReadFloat();
                float y = reader.ReadFloat();

                if (activeMobs.TryGetValue(id, out var mob)) mob.MoveTo(x, y);
            }
            finally { reader.Cleanup(); }
        }

        private void OnMobMoveBatch(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            try
            {
                short count = reader.ReadShort();
                for (int i = 0; i < count; i++)
                {
                    int id = reader.ReadInt();
                    float x = reader.ReadFloat();
                    float y = reader.ReadFloat();

                    if (activeMobs.TryGetValue(id, out var mob)) mob.MoveTo(x, y);
                }
            }
            finally { reader.Cleanup(); }
        }

        public MobController GetMob(int mobId)
        {
            activeMobs.TryGetValue(mobId, out MobController mob);
            return mob;
        }

        private void OnDestroy()
        {
            UnregisterNetworkHandlers();

            if (_dbHandle.IsValid())
            {
                Addressables.Release(_dbHandle);
            }
        }
    }
}