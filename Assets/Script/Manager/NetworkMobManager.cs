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
        [Tooltip("[CẦN ĐIỀN] Kéo Assets/SO/MobDatabase.asset (đã là Addressable, group Core)")]
        public AssetReferenceT<MobDatabaseSO> mobDatabaseRef;
        [Tooltip("[CẦN ĐIỀN] Kéo Assets/Prefabs/Mob.prefab (prefab gốc dùng chung cho mọi quái)")]
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
            RegisterNetworkHandlers();   // nghe ngay; gói đến trước khi tải xong dữ liệu hình thì giữ lại (Defer)
            _dbHandle = Addressables.LoadAssetAsync<MobDatabaseSO>(mobDatabaseRef);
            _dbHandle.Completed += handle => {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    _mobDatabase = handle.Result;
                    _mobDatabase.Init();
                    _isDatabaseReady = true;
                    foreach (var (h, d) in _early) h(d);
                    _early.Clear();
                }
                else Debug.LogError("[Mob Manager] Lỗi tải MobDatabaseSO!");
            };
        }

        // Gói quái đến khi chưa tải xong MobDatabase: giữ lại, tải xong xử lý theo đúng thứ tự
        // (trước đây bị bỏ → vào map đầu tiên không thấy quái).
        private readonly List<(System.Action<byte[]> h, byte[] d)> _early = new List<(System.Action<byte[]>, byte[])>();
        private bool Defer(System.Action<byte[]> handler, byte[] data)
        {
            if (_isDatabaseReady) return false;
            _early.Add((handler, data));
            return true;
        }

        private void RegisterNetworkHandlers()
        {
            NetworkEventDispatcher.Instance.AddHandler(Cmd.MOB_LIST, OnMobList);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.MOB_MOVE_BATCH, OnMobMoveBatch);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.MOB_ADD, OnMobAdd);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.MOB_DIE, OnMobDie);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.MOB_REMOVE, OnMobRemove);
        }

        private void UnregisterNetworkHandlers()
        {
            if (NetworkEventDispatcher.Instance != null)
            {
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.MOB_LIST, OnMobList);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.MOB_MOVE_BATCH, OnMobMoveBatch);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.MOB_ADD, OnMobAdd);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.MOB_DIE, OnMobDie);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.MOB_REMOVE, OnMobRemove);
            }
        }

        private void OnMobList(byte[] data)
        {
            if (Defer(OnMobList, data)) return;

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

        // ==========================================
        // QUÁI HỒI SINH / CHẾT
        // ==========================================

        /// <summary>MOB_ADD: quái hồi sinh (cùng id cũ). Payload giống 1 phần tử MOB_LIST.</summary>
        private void OnMobAdd(byte[] data)
        {
            if (Defer(OnMobAdd, data)) return;
            MessageReader reader = new MessageReader(data);
            try
            {
                int mobId = reader.ReadInt();
                int templateId = reader.ReadInt();
                float x = reader.ReadFloat();
                float y = reader.ReadFloat();
                int hp = reader.ReadInt();
                int maxHp = reader.ReadInt();
                byte isDead = reader.ReadByte();

                RemoveMob(mobId); // xác cũ còn nằm đó thì dọn trước
                if (isDead == 0) SpawnMob(mobId, (short)templateId, x, y, hp, maxHp);
            }
            catch (Exception e) { Debug.LogError("Lỗi MOB_ADD: " + e.Message); }
            finally { reader.Cleanup(); }
        }

        /// <summary>MOB_DIE: hiện xác 1 giây rồi thu về Pool.</summary>
        /// <summary>MOB_REMOVE: int mobId — quái không hồi sinh (boss thế giới, quái phó bản) → xoá luôn.</summary>
        private void OnMobRemove(byte[] data)
        {
            if (Defer(OnMobRemove, data)) return;
            var reader = new MessageReader(data);
            int mobId = reader.ReadInt();
            reader.Cleanup();
            RemoveMob(mobId);
        }

        private void OnMobDie(byte[] data)
        {
            if (Defer(OnMobDie, data)) return;
            MessageReader reader = new MessageReader(data);
            int mobId = reader.ReadInt();
            reader.Cleanup();

            if (!activeMobs.TryGetValue(mobId, out var mob)) return;
            mob.PlayDeath();
            FindAnyObjectByType<Assets.Script.Player.PlayerTargeting>()?.ClearIfTarget(mob);
            StartCoroutine(RemoveCorpseLater(mobId, mob, 1.0f));
        }

        private System.Collections.IEnumerator RemoveCorpseLater(int mobId, MobController mob, float delay)
        {
            yield return new WaitForSeconds(delay);
            // Chỉ xoá nếu id này vẫn đang là đúng con quái đó (chưa bị thay bằng con hồi sinh)
            if (activeMobs.TryGetValue(mobId, out var current) && current == mob) RemoveMob(mobId);
        }

        public void SpawnMob(int mobId, short templateId, float x, float y, int hp, int maxHp)
        {
            if (_mobDatabase == null) return;

            // Không có cấu hình hình ảnh → vẫn tạo quái với hình placeholder (xem MobController.SetPlaceholder)
            var visual = _mobDatabase.GetMobVisual(templateId);
            if (visual == null)
                Debug.LogWarning($"[CẦN ĐIỀN] Quái templateId={templateId} chưa có trong Assets/SO/MobDatabase.asset → hiện placeholder.");

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
                if (visual != null) mob.SetVisual(visual);
                else mob.SetPlaceholder();

                activeMobs[mobId] = mob;
                obj.name = $"Mob_{Assets.Script.Data.GameData.MobName(templateId)}_{mobId}";
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

        private void OnMobMoveBatch(byte[] data)
        {
            if (!_isDatabaseReady) return;   // vị trí cũ, bỏ cũng được (gói sau sẽ tới)
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

        /// <summary>Tìm quái CÒN SỐNG gần vị trí nhất trong bán kính (dùng để tự chọn mục tiêu khi bấm đánh).</summary>
        public MobController FindNearestAlive(Vector3 pos, float radius)
        {
            MobController best = null;
            float bestSqr = radius * radius;
            foreach (var mob in activeMobs.Values)
            {
                if (mob == null || mob.IsDead) continue;
                float sqr = ((Vector2)(mob.transform.position - pos)).sqrMagnitude;
                if (sqr <= bestSqr) { bestSqr = sqr; best = mob; }
            }
            return best;
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