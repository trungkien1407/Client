using Assets.Script.Constants;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.Player;
using UnityEngine;

namespace Assets.Script.Map
{
    /// <summary>
    /// CHUYỂN MAP / KHU + KÉO VỊ TRÍ:
    ///   CHANGE_MAP  → màn chờ, ẩn nhân vật, xoá người khác, tải map mới (MapManager tự gửi CLIENT_READY khi tải xong)
    ///   CHANGE_ZONE → cùng map: xoá người khác, gửi CLIENT_READY để server gửi danh sách khu mới
    ///   FORCE_MOVE  → server thấy vị trí sai (xuyên tường / chạy quá nhanh / bay) → kéo về
    /// Map tải xong (MapManager.OnMapLoaded) → hiện lại nhân vật + tắt màn chờ.
    /// </summary>
    public class MapTransferNetwork : NetworkListener
    {
        private static NetworkPlayerManager Players => NetworkPlayerManager.Instance;

        private void Awake() => MapManager.OnMapLoaded += OnMapLoaded;

        protected override void OnDestroy()
        {
            MapManager.OnMapLoaded -= OnMapLoaded;
            base.OnDestroy();
        }

        protected override void RegisterHandlers()
        {
            Listen(Cmd.CHANGE_MAP, OnChangeMap);
            Listen(Cmd.CHANGE_ZONE, OnChangeZone);
            Listen(Cmd.FORCE_MOVE, OnForceMove);
        }

        // GameMaster (camera) cũng nghe OnMapLoaded và đăng ký sớm hơn → camera đã chỉnh xong khi tới đây
        private void OnMapLoaded(GameObject map)
        {
            if (Players != null) Players.ShowLocalPlayer();
            PopupAndLoad.Instance?.HideLoading();
        }

        /// <summary>CHANGE_MAP: short mapId, byte zoneId, float x, float y</summary>
        private void OnChangeMap(byte[] data)
        {
            var r = new MessageReader(data);
            short mapId = r.ReadShort();
            LocalPlayerState.ZoneId = r.ReadByte();
            float x = r.ReadFloat(), y = r.ReadFloat();
            r.Cleanup();

            BeginTransfer();
            SnapLocal(x, y);
            MapManager.Instance?.LoadMap(mapId);
        }

        /// <summary>CHANGE_ZONE: byte zoneId — cùng map, không tải lại.</summary>
        private void OnChangeZone(byte[] data)
        {
            var r = new MessageReader(data);
            LocalPlayerState.ZoneId = r.ReadByte();
            r.Cleanup();

            BeginTransfer();
            Data.GameActions.ClientReady();
            if (Players != null) Players.ShowLocalPlayer();
            PopupAndLoad.Instance?.HideLoading();
        }

        /// <summary>FORCE_MOVE: float x, float y. SnapTo xoá cả vận tốc, không thì nhân vật trượt tiếp.</summary>
        private void OnForceMove(byte[] data)
        {
            var r = new MessageReader(data);
            float x = r.ReadFloat(), y = r.ReadFloat();
            r.Cleanup();
            SnapLocal(x, y);
        }

        private static void SnapLocal(float x, float y)
        {
            var me = Players != null ? Players.localPlayer : null;
            if (me != null) me.SnapTo(new Vector2(x, y));
        }

        /// <summary>Màn chờ + ẩn nhân vật mình + xoá người chơi khác của map / khu cũ.</summary>
        private static void BeginTransfer()
        {
            PopupAndLoad.Instance?.ShowLoading();
            if (Players == null) return;
            if (Players.localPlayer != null) Players.localPlayer.gameObject.SetActive(false);
            Players.ClearAllRemotePlayers();
        }
    }
}
