using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Assets.Script.Network;
using Assets.Script.Constants;
using Assets.Script.Models;
using Assets.Script.Map;
using System.Collections;
using Assets.Script.Interfaces;
using Spine.Unity;
using Assets.Script.Player;
using Assets.Script.UI;

namespace Assets.Script.Manager
{
    public class NetworkPlayerManager : MonoBehaviour
    {
        public static NetworkPlayerManager Instance;

        public int myPlayerId = -1;
        [HideInInspector] public PlayerMovement localPlayer; // gán lúc chạy khi spawn nhân vật, KHÔNG kéo tay
        private Dictionary<int, RemotePlayer> remotePlayers = new Dictionary<int, RemotePlayer>();
        private HashSet<int> loadingPlayers = new HashSet<int>();

        private const string BASE_PLAYER_PREFAB = Assets.Script.Core.AddressKeys.BasePlayer;

        void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        void Start()
        {
            NetworkEventDispatcher.Instance.AddHandler(Cmd.PLAYER_ADD, OnPlayerAdd);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.PLAYER_LIST, OnPlayerList);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.PLAYER_REMOVE, OnPlayerRemove);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.PLAYER_MOVE_BATCH, OnPlayerMoveBatch);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.FORCE_MOVE, OnForceMove);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.CHANGE_ZONE, OnChangeZone);
            NetworkEventDispatcher.Instance.AddHandler(Cmd.CHANGE_MAP, OnChangeMap);

            // [ĐÃ SỬA] Đăng ký lắng nghe đúng format có tham số GameObject
            MapManager.OnMapLoaded += HandleMapLoaded;
        }

        void OnDestroy()
        {
            if (NetworkEventDispatcher.Instance != null)
            {
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.PLAYER_ADD, OnPlayerAdd);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.PLAYER_LIST, OnPlayerList);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.PLAYER_REMOVE, OnPlayerRemove);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.PLAYER_MOVE_BATCH, OnPlayerMoveBatch);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.FORCE_MOVE, OnForceMove);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.CHANGE_ZONE, OnChangeZone);
                NetworkEventDispatcher.Instance.RemoveHandler(Cmd.CHANGE_MAP, OnChangeMap);
            }
            MapManager.OnMapLoaded -= HandleMapLoaded;
        }

        // [ĐÃ SỬA] Thêm tham số GameObject để khớp với MapManager
        private void HandleMapLoaded(GameObject mapInstance)
        {
            // Do GameMaster đã đăng ký sự kiện OnEnable, nó sẽ chạy trước hàm này.
            // Khi code chạy đến đây, chắc chắn Camera đã được setup xong.
            // Ta chỉ cần gọi player ra và tắt loading!
            RespawnLocalPlayerAfterMapLoad();
            PopupAndLoad.Instance?.HideLoading();
        }

        public void SpawnLocalPlayer(
            int id, string name, short classType, int level, long exp,
            int yen, int xu, int luong,
            int hp, int mp, int maxHp, int maxMp,
            float moveSpeed, float jumpForce, float gravity,
            float x, float y)
        {
            this.myPlayerId = id;
            PlayerData pd = new PlayerData
            {
                id = id,
                name = name,
                class_type = (byte)classType,
                level = (short)level,
                exp = exp,
                yen = yen,
                xu = xu,
                luong = luong,
                hp = hp,
                mp = mp,
                maxHp = maxHp,
                maxMp = maxMp,
                moveSpeed = moveSpeed,
                jumpForce = jumpForce,
                gravity = gravity,
                x = x,
                y = y
            };
            SpawnPlayerAsync(pd, true);
        }

        public void RespawnLocalPlayerAfterMapLoad()
        {
            if (myPlayerId == -1 || localPlayer == null) return;
            localPlayer.gameObject.SetActive(true);
        }

        private void OnPlayerMoveBatch(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            try
            {
                short count = reader.ReadShort();
                for (int i = 0; i < count; i++)
                {
                    int pId = reader.ReadInt();
                    float x = reader.ReadFloat();
                    float y = reader.ReadFloat();
                    byte dir = reader.ReadByte();
                    byte state = reader.ReadByte();

                    if (pId != myPlayerId && remotePlayers.TryGetValue(pId, out RemotePlayer rp))
                    {
                        rp.UpdateNetworkData(x, y, dir, state);
                    }
                }
            }
            finally { reader.Cleanup(); }
        }

        private void OnPlayerList(byte[] data)
        {
            if (data.Length < 2) return;
            MessageReader reader = new MessageReader(data);
            try
            {
                short count = reader.ReadShort();
                if (count > 0 && reader.Available() < 20) return;
                if (count < 0 || count > 100) return;

                for (int i = 0; i < count; i++)
                {
                    PlayerData pd = ReadShortInfo(reader);
                    SpawnIfNotExists(pd);
                }
            }
            catch (Exception e) { Debug.LogError($"PlayerList Error: {e.Message}"); }
            finally { reader.Cleanup(); }
        }

        private void OnPlayerAdd(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            PlayerData pd = ReadShortInfo(reader);
            SpawnIfNotExists(pd);
            reader.Cleanup();
        }

        private PlayerData ReadShortInfo(MessageReader reader)
        {
            return new PlayerData
            {
                id = reader.ReadInt(),
                name = reader.ReadUTF(),
                class_type = reader.ReadByte(),
                level = reader.ReadShort(),
                x = reader.ReadFloat(),
                y = reader.ReadFloat(),
                hp = reader.ReadInt(),
                maxHp = reader.ReadInt(),
                moveSpeed = reader.ReadFloat()
            };
        }

        private void SpawnIfNotExists(PlayerData p)
        {
            if (loadingPlayers.Contains(p.id)) return;
            if (p.id == myPlayerId && localPlayer != null) return;
            if (p.id != myPlayerId && remotePlayers.ContainsKey(p.id)) return;

            SpawnPlayerAsync(p, p.id == myPlayerId);
        }

        private async void SpawnPlayerAsync(PlayerData pData, bool isLocal)
        {
            loadingPlayers.Add(pData.id);
            string spineKey = Assets.Script.Core.AddressKeys.CharacterSkeleton(pData.class_type);

            try
            {
                GameObject playerObj = null;

                if (isLocal)
                {
                    while (MapManager.Instance == null || MapManager.Instance.currentMapInstance == null)
                    {
                        await System.Threading.Tasks.Task.Yield();
                    }

                    var handle = Addressables.InstantiateAsync(BASE_PLAYER_PREFAB, new Vector3(pData.x, pData.y, 0), Quaternion.identity);
                    await handle.Task;
                    if (handle.Status == AsyncOperationStatus.Succeeded) playerObj = handle.Result;
                    FindAnyObjectByType<PlayerTargeting>().SetPlayer(playerObj.transform);
                }
                else
                {
                    playerObj = ObjectPoolManager.Instance.GetFromPool(BASE_PLAYER_PREFAB);

                    if (playerObj == null)
                    {
                        var handle = Addressables.InstantiateAsync(BASE_PLAYER_PREFAB, new Vector3(pData.x, pData.y, 0), Quaternion.identity);
                        await handle.Task;
                        if (handle.Status == AsyncOperationStatus.Succeeded) playerObj = handle.Result;
                    }
                }

                if (!loadingPlayers.Contains(pData.id))
                {
                    if (playerObj != null)
                    {
                        if (isLocal) Addressables.ReleaseInstance(playerObj);
                        else ObjectPoolManager.Instance.ReturnToPool(BASE_PLAYER_PREFAB, playerObj);
                    }
                    return;
                }

                var spineHandle = Addressables.LoadAssetAsync<SkeletonDataAsset>(spineKey);
                await spineHandle.Task;
                SkeletonDataAsset spineData = null;

                if (spineHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    spineData = spineHandle.Result;
                }
                else
                {
                    Debug.LogWarning($"[Spine] Không tìm thấy dữ liệu xương cho key: {spineKey}");
                }

                if (!loadingPlayers.Contains(pData.id))
                {
                    if (isLocal) Addressables.ReleaseInstance(playerObj);
                    else ObjectPoolManager.Instance.ReturnToPool(BASE_PLAYER_PREFAB, playerObj);
                    return;
                }

                // ===================================
                // HOÀN TẤT SETUP PLAYER
                // ===================================

                if (isLocal)
                {
                    playerObj.name = $"LocalPlayer_{pData.name}";

                    if (!playerObj.TryGetComponent(out localPlayer))
                        localPlayer = playerObj.AddComponent<PlayerMovement>();

                    if (playerObj.TryGetComponent(out PlayerVisualController visualCtrl) && spineData != null)
                    {
                        visualCtrl.SetupVisual(spineData, null);
                        playerObj.GetComponentInChildren<MeshRenderer>().sortingOrder = 2;
                    }

                    localPlayer.Initialize(pData, 1);

                    if (playerObj.TryGetComponent(out Rigidbody2D rb)) rb.velocity = Vector2.zero;
                    GameMaster.Instance?.SetCameraFollow(playerObj.transform);

                    // Setup MỌI THỨ XONG XUÔI -> Bật Player và Tắt Màn Hình Chờ ngay lập tức
                    RespawnLocalPlayerAfterMapLoad();
                    PopupAndLoad.Instance?.HideLoading();
                }
                else
                {
                    SetupRemotePlayer(playerObj, pData, BASE_PLAYER_PREFAB, spineData);
                }
            }
            catch (Exception e) { Debug.LogError($"Spawn Error: {e.Message}"); }
            finally { loadingPlayers.Remove(pData.id); }
        }

        private void SetupRemotePlayer(GameObject obj, PlayerData pData, string poolKey, SkeletonDataAsset spineData)
        {
            obj.name = $"RemotePlayer_{pData.name}";
            obj.transform.position = new Vector3(pData.x, pData.y, 0);
            obj.transform.SetParent(null);

            if (!obj.TryGetComponent(out RemotePlayer rp))
            {
                rp = obj.AddComponent<RemotePlayer>();
            }

            rp.poolKey = poolKey;
            rp.Initialize(pData);

            if (spineData != null && obj.TryGetComponent(out PlayerVisualController visualCtrl))
            {
                visualCtrl.SetupVisual(spineData, null);
            }

            if (obj.TryGetComponent(out Rigidbody2D rb))
            {
                rb.velocity = Vector2.zero;
            }

            remotePlayers.Add(pData.id, rp);
        }

        public RemotePlayer GetRemotePlayer(int id)
        {
            if (remotePlayers.TryGetValue(id, out RemotePlayer rp))
                return rp;
            return null;
        }

        // ==========================================
        // QUẢN LÝ POOL & REMOVE
        // ==========================================

        private void OnPlayerRemove(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            int idToDrop = reader.ReadInt();
            reader.Cleanup();
            RemoveRemotePlayer(idToDrop);
        }

        public void RemoveRemotePlayer(int id)
        {
            if (remotePlayers.TryGetValue(id, out RemotePlayer rp))
            {
                ReturnRemotePlayerToPool(rp);
                remotePlayers.Remove(id);
            }
        }

        private void ReturnRemotePlayerToPool(RemotePlayer rp)
        {
            if (rp == null || rp.gameObject == null) return;
            ObjectPoolManager.Instance.ReturnToPool(rp.poolKey, rp.gameObject);
        }

        private void OnForceMove(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            float x = reader.ReadFloat();
            float y = reader.ReadFloat();
            reader.Cleanup();
            // Server phát hiện vị trí sai (xuyên tường / chạy quá nhanh / bay) -> kéo về.
            // Dùng SnapTo để xoá cả vận tốc Rigidbody2D, nếu không nhân vật sẽ trượt tiếp.
            if (localPlayer != null) localPlayer.SnapTo(new Vector2(x, y));
        }

        private void OnChangeZone(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            try
            {
                byte newZoneId = reader.ReadByte();
                LocalPlayerState.ZoneId = newZoneId;
                PopupAndLoad.Instance?.ShowLoading();
                if (localPlayer != null) localPlayer.gameObject.SetActive(false);

                ClearAllRemotePlayers();

                MessageWriter writer = new MessageWriter();
                NetworkManager.Instance.Send(Cmd.CLIENT_READY, writer.ToArray());
                writer.Cleanup();

                // Chuyển khu vực không phải load lại Map
                // Bật người chơi và tắt UI loading ngay
                RespawnLocalPlayerAfterMapLoad();
                PopupAndLoad.Instance?.HideLoading();
            }
            finally { reader.Cleanup(); }
        }

        private void OnChangeMap(byte[] data)
        {
            MessageReader reader = new MessageReader(data);
            try
            {
                short newMapId = reader.ReadShort();
                byte newZoneId = reader.ReadByte();
                LocalPlayerState.ZoneId = newZoneId;
                float newX = reader.ReadFloat();
                float newY = reader.ReadFloat();

                PopupAndLoad.Instance?.ShowLoading();
                if (localPlayer != null) localPlayer.gameObject.SetActive(false);

                ClearAllRemotePlayers();

                if (localPlayer != null) localPlayer.SnapTo(new Vector2(newX, newY));

                if (MapManager.Instance != null)
                {
                    MapManager.Instance.LoadMap(newMapId);
                }
            }
            finally { reader.Cleanup(); }
        }

        // ==========================================
        // DỌN DẸP DỮ LIỆU
        // ==========================================

        public void ClearLocalPlayer()
        {
            if (localPlayer != null)
            {
                GameMaster.Instance?.SetCameraFollow(null);
                Addressables.ReleaseInstance(localPlayer.gameObject);
                localPlayer = null;
            }
            myPlayerId = -1;
            LocalPlayerState.Reset();
        }

        public void ClearAllRemotePlayers()
        {
            foreach (var rp in remotePlayers.Values)
            {
                ReturnRemotePlayerToPool(rp);
            }
            remotePlayers.Clear();
            loadingPlayers.Clear();
        }

        public void ClearAllPlayers()
        {
            ClearLocalPlayer();
            ClearAllRemotePlayers();

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.ClearAllPools();
            }

            Debug.Log("[Client] Đã dọn dẹp toàn bộ Player khỏi Scene và giải phóng bộ nhớ Pool.");
        }
    }
}