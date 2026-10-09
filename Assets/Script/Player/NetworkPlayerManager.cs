using System;
using System.Collections.Generic;
using UnityEngine;
using Assets.Script.Network;
using Assets.Script.Constants;
using Assets.Script.Models;
using Assets.Script.Player;

namespace Assets.Script.Manager
{
    /// <summary>
    /// SỔ NGƯỜI CHƠI TRONG MAP: nhân vật mình (localPlayer) + người khác (remotePlayers theo id).
    ///   Nhận: PLAYER_LIST / PLAYER_ADD (tạo), PLAYER_REMOVE (xoá), PLAYER_MOVE_BATCH (vị trí người khác).
    /// Tải hình: PlayerSpawner · chuyển map / khu, FORCE_MOVE: Map/MapTransferNetwork.
    /// </summary>
    public class NetworkPlayerManager : NetworkListener
    {
        public static NetworkPlayerManager Instance;

        public int myPlayerId = -1;
        [HideInInspector] public PlayerMovement localPlayer; // gán lúc chạy khi spawn nhân vật, KHÔNG kéo tay
        private readonly Dictionary<int, RemotePlayer> remotePlayers = new Dictionary<int, RemotePlayer>();
        // Đang tải hình (bất đồng bộ). Bị xoá giữa chừng (rời map / đổi map) → tải xong thì trả lại, không thêm vào sổ.
        private readonly HashSet<int> loadingPlayers = new HashSet<int>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        protected override void RegisterHandlers()
        {
            Listen(Cmd.PLAYER_LIST, OnPlayerList);
            Listen(Cmd.PLAYER_ADD, OnPlayerAdd);
            Listen(Cmd.PLAYER_REMOVE, OnPlayerRemove);
            Listen(Cmd.PLAYER_MOVE_BATCH, OnPlayerMoveBatch);
        }

        // ==========================================
        // TRA CỨU
        // ==========================================

        public RemotePlayer GetRemotePlayer(int id) => remotePlayers.TryGetValue(id, out var rp) ? rp : null;

        /// <summary>Transform của người chơi {id} (mình hoặc người khác), null nếu không có trong map.</summary>
        public Transform GetPlayerTransform(int id)
        {
            if (id == myPlayerId) return localPlayer != null ? localPlayer.transform : null;
            var rp = GetRemotePlayer(id);
            return rp != null ? rp.transform : null;
        }

        // ==========================================
        // NHÂN VẬT MÌNH
        // ==========================================

        /// <summary>Vào game (LOGIN / CREATE_CHARACTER): tạo nhân vật mình, đợi map tải xong mới hiện.</summary>
        public void SpawnLocalPlayer(PlayerData p)
        {
            myPlayerId = p.id;
            SpawnAsync(p, true);
        }

        /// <summary>Hiện nhân vật mình (sau khi map / khu mới sẵn sàng).</summary>
        public void ShowLocalPlayer()
        {
            if (myPlayerId != -1 && localPlayer != null) localPlayer.gameObject.SetActive(true);
        }

        // ==========================================
        // NHẬN GÓI
        // ==========================================

        /// <summary>PLAYER_LIST: short n, [ShortInfo] x n — mọi người đang ở khu khi mình vừa vào.</summary>
        private void OnPlayerList(byte[] data)
        {
            var r = new MessageReader(data);
            try
            {
                short n = r.ReadShort();
                if (n < 0 || n > 100) return;
                for (int i = 0; i < n; i++) SpawnIfNew(ReadShortInfo(r));
            }
            finally { r.Cleanup(); }
        }

        private void OnPlayerAdd(byte[] data)
        {
            var r = new MessageReader(data);
            try { SpawnIfNew(ReadShortInfo(r)); }
            finally { r.Cleanup(); }
        }

        /// <summary>ShortInfo: int id, UTF tên, byte hệ phái, short cấp, float x, float y, int hp, int maxHp, float tốc chạy</summary>
        private static PlayerData ReadShortInfo(MessageReader r) => new PlayerData
        {
            id = r.ReadInt(), name = r.ReadUTF(), class_type = r.ReadByte(), level = r.ReadShort(),
            x = r.ReadFloat(), y = r.ReadFloat(), hp = r.ReadInt(), maxHp = r.ReadInt(), moveSpeed = r.ReadFloat()
        };

        private void OnPlayerRemove(byte[] data)
        {
            var r = new MessageReader(data);
            int id = r.ReadInt();
            r.Cleanup();
            RemoveRemotePlayer(id);
        }

        /// <summary>PLAYER_MOVE_BATCH: short n, [int id, float x, float y, byte hướng, byte trạng thái] x n</summary>
        private void OnPlayerMoveBatch(byte[] data)
        {
            var r = new MessageReader(data);
            try
            {
                short n = r.ReadShort();
                for (int i = 0; i < n; i++)
                {
                    int id = r.ReadInt();
                    float x = r.ReadFloat(), y = r.ReadFloat();
                    byte dir = r.ReadByte(), state = r.ReadByte();
                    if (id != myPlayerId && remotePlayers.TryGetValue(id, out var rp)) rp.UpdateNetworkData(x, y, dir, state);
                }
            }
            finally { r.Cleanup(); }
        }

        // ==========================================
        // TẠO / XOÁ
        // ==========================================

        private void SpawnIfNew(PlayerData p)
        {
            if (loadingPlayers.Contains(p.id)) return;
            if (p.id == myPlayerId ? localPlayer != null : remotePlayers.ContainsKey(p.id)) return;
            SpawnAsync(p, p.id == myPlayerId);
        }

        private async void SpawnAsync(PlayerData p, bool isLocal)
        {
            loadingPlayers.Add(p.id);
            try
            {
                var obj = await PlayerSpawner.CreateBody(p, isLocal);
                if (obj == null) { Debug.LogError($"[Player] Không tạo được nhân vật {p.name}"); return; }
                var spine = await PlayerSpawner.LoadSkeleton(p.class_type);

                if (!loadingPlayers.Contains(p.id) || (!isLocal && remotePlayers.ContainsKey(p.id)))
                {
                    PlayerSpawner.Release(obj, isLocal);   // bị huỷ trong lúc tải
                    return;
                }

                if (isLocal)
                {
                    localPlayer = PlayerSpawner.SetupLocal(obj, p, spine);
                    GameMaster.Instance?.SetCameraFollow(obj.transform);
                    ShowLocalPlayer();
                    PopupAndLoad.Instance?.HideLoading();
                }
                else remotePlayers.Add(p.id, PlayerSpawner.SetupRemote(obj, p, spine));
            }
            catch (Exception e) { Debug.LogError($"Spawn Error: {e.Message}"); }
            finally { loadingPlayers.Remove(p.id); }
        }

        public void RemoveRemotePlayer(int id)
        {
            if (!remotePlayers.TryGetValue(id, out var rp)) return;
            remotePlayers.Remove(id);
            if (rp != null) PlayerSpawner.Release(rp.gameObject, false);
        }

        // ==========================================
        // DỌN DẸP
        // ==========================================

        public void ClearLocalPlayer()
        {
            if (localPlayer != null)
            {
                GameMaster.Instance?.SetCameraFollow(null);
                PlayerSpawner.Release(localPlayer.gameObject, true);
                localPlayer = null;
            }
            myPlayerId = -1;
            LocalPlayerState.Reset();
        }

        /// <summary>Xoá người chơi khác (đổi map / khu). Giữ lượt tải nhân vật CỦA MÌNH (đổi map ngay sau đăng nhập).</summary>
        public void ClearAllRemotePlayers()
        {
            foreach (var rp in remotePlayers.Values)
                if (rp != null) PlayerSpawner.Release(rp.gameObject, false);
            remotePlayers.Clear();
            loadingPlayers.RemoveWhere(id => id != myPlayerId);
        }

        /// <summary>Hết phiên (mất kết nối): xoá hết + dọn pool.</summary>
        public void ClearAllPlayers()
        {
            ClearLocalPlayer();
            ClearAllRemotePlayers();
            ObjectPoolManager.Instance?.ClearAllPools();
        }
    }
}
