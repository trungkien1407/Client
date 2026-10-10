using System.Collections.Generic;
using Assets.Script.Combat;
using Assets.Script.Constants;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using UnityEngine;

namespace Assets.Script.Data
{
    /// <summary>
    /// GÓI THẾ GIỚI / SỰ KIỆN: hiệu ứng (choáng, chậm...), phó bản, sự kiện, Lôi đài, thông báo server, danh sách khu,
    /// tên map + cổng, hoạt động hằng ngày, sổ tay nhiệm vụ, mẹo theo cấp. Payload: docs/PROTOCOL.md (repo server).
    /// </summary>
    public class EventNetwork : NetworkListener
    {
        protected override void RegisterHandlers()
        {
            Listen(Cmd.EFFECT, OnEffect);
            Listen(Cmd.DUNGEON_STATE, OnDungeonState);
            Listen(Cmd.EVENT_STATE, OnEventState);
            Listen(Cmd.ARENA_SCORE, OnArenaScore);
            Listen(Cmd.ZONE_LIST, OnZoneList);
            Listen(Cmd.SERVER_NOTICE, OnServerNotice);
            Listen(Cmd.MAP_INFO, OnMapInfo);
            Listen(Cmd.ACTIVITY_INFO, OnActivityInfo);
            Listen(Cmd.QUEST_GUIDE, OnQuestGuide);
            Listen(Cmd.GUIDE_TIP, OnGuideTip);
        }

        private static readonly string[] EffectNames = { "", "Choáng", "Chậm", "Bỏng", "Tăng sức mạnh" };

        private static readonly Color[] EffectColors = { Color.white, new Color(1f, 0.9f, 0.2f), new Color(0.4f, 0.8f, 1f), new Color(1f, 0.45f, 0.1f), new Color(0.5f, 1f, 0.5f) };

        /// <summary>EFFECT: byte targetType(0 quái/1 người), int targetId, byte effect(1 choáng/2 chậm/3 bỏng/4 tăng sức mạnh — GĐ8), int durationMs</summary>
        private void OnEffect(byte[] data)
        {
            var r = new MessageReader(data);
            int type = r.ReadByte(), id = r.ReadInt(), effect = r.ReadByte(), ms = r.ReadInt();
            r.Cleanup();
            if (effect < 1 || effect >= EffectNames.Length) return;

            Transform t = null;
            if (type == (int)AttackTarget.Player)
            {
                if (id == LocalPlayerState.Id)
                {
                    t = NetworkPlayerManager.Instance != null && NetworkPlayerManager.Instance.localPlayer != null
                        ? NetworkPlayerManager.Instance.localPlayer.transform : null;
                    if (effect == (int)StatusEffect.Stun) LocalPlayerState.StunnedUntil = Time.time + ms / 1000f;
                    if (effect == (int)StatusEffect.Slow) LocalPlayerState.SlowedUntil = Time.time + ms / 1000f;
                    if (effect == (int)StatusEffect.Buff) LocalPlayerState.BuffedUntil = Time.time + ms / 1000f;
                }
                else
                {
                    var rp = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.GetRemotePlayer(id) : null;
                    if (rp != null) t = rp.transform;
                }
            }
            else
            {
                var mob = NetworkMobManager.Instance != null ? NetworkMobManager.Instance.GetMob(id) : null;
                if (mob != null) t = mob.transform;
            }
            // [CẦN ĐIỀN khi có art] thay chữ nổi bằng hiệu ứng hình (sao quay trên đầu = choáng, băng = chậm, lửa = bỏng)
            if (t != null) DamagePopup.Show(t.position + Vector3.up * 1.4f, EffectNames[effect], EffectColors[effect], 4f);
        }

        /// <summary>DUNGEON_STATE: byte state(2 đang chơi/3 thắng/4 thua/0 rời), int secondsLeft, short mobsLeft, UTF name</summary>
        private void OnDungeonState(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.DungeonState = r.ReadByte();
            int secs = r.ReadInt();
            GameData.DungeonMobsLeft = r.ReadShort();
            GameData.DungeonName = r.ReadUTF();
            r.Cleanup();
            GameData.DungeonEndTime = Time.time + secs;
            GameData.Notify(DataKind.Event);
            if (GameData.DungeonState == 3) UI.GameHud.Banner($"CHINH PHỤC {GameData.DungeonName.ToUpper()}!", 4f);
            if (GameData.DungeonState == 4) UI.GameHud.Banner("Phó bản thất bại — hết giờ", 4f);
        }

        /// <summary>EVENT_STATE: byte eventType(1 boss thế giới/2 lôi đài), byte state(0 kết thúc/1 báo trước/2 đang diễn ra), int secs, UTF text</summary>
        private void OnEventState(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.EventType = r.ReadByte();
            GameData.EventState = r.ReadByte();
            int secs = r.ReadInt();
            GameData.EventText = r.ReadUTF();
            r.Cleanup();
            GameData.EventEndTime = Time.time + secs;
            if (GameData.EventType == 2 && GameData.EventState != 2) GameData.ArenaScore.Clear();
            GameData.Notify(DataKind.Event);
            UI.GameHud.Banner(GameData.EventText, 4f);
        }

        /// <summary>ARENA_SCORE: short n, [UTF name, short kills] — top 10 Lôi đài</summary>
        private void OnArenaScore(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.ArenaScore.Clear();
            for (int i = 0; i < n; i++) GameData.ArenaScore.Add(new KeyValuePair<string, int>(r.ReadUTF(), r.ReadShort()));
            r.Cleanup();
            GameData.Notify(DataKind.Event);
        }

        /// <summary>ZONE_LIST: byte currentZone (255 = khu riêng), byte n, [byte zoneId, byte players, byte max] x n</summary>
        private void OnZoneList(byte[] data)
        {
            var r = new MessageReader(data);
            int current = r.ReadByte();
            int n = r.ReadByte();
            var zones = new List<int[]>(n);
            for (int i = 0; i < n; i++) zones.Add(new[] { (int)r.ReadByte(), r.ReadByte(), r.ReadByte() });
            r.Cleanup();
            LocalPlayerState.ZoneId = current;
            GameWindow.Get<ZoneWindow>().SetData(current, zones);
        }

        /// <summary>
        /// SERVER_NOTICE: byte type, int secs, UTF text — GM gửi từ trang quản trị (server: MaintenanceService).
        ///   1 thông báo → chữ lớn giữa màn hình; 2 đếm ngược bảo trì → thêm đồng hồ ở khung sự kiện;
        ///   3 huỷ bảo trì → tắt đồng hồ; 4 bị đưa ra vì bảo trì → nhớ lý do, server ngắt ngay sau đó.
        /// (Dòng chat "Hệ thống" server gửi riêng bằng gói CHAT.)
        /// </summary>
        private void OnServerNotice(byte[] data)
        {
            var r = new MessageReader(data);
            int type = r.ReadByte();
            int secs = r.ReadInt();
            string text = r.ReadUTF();
            r.Cleanup();
            switch (type)
            {
                case 2:
                    GameData.MaintEndTime = Time.time + secs;
                    GameData.Notify(DataKind.Event);
                    break;
                case 3:
                    GameData.MaintEndTime = 0;
                    GameData.Notify(DataKind.Event);
                    break;
                case 4:
                    GameData.KickReason = text;
                    return; // không hiện banner — popup lúc ngắt kết nối sẽ hiện lý do
            }
            UI.GameHud.Banner(text, type == 3 ? 3f : 6f);
        }

        /// <summary>
        /// MAP_INFO (GĐ8, mỗi lần vào khu): UTF tên map, short cấp quái thấp, short cao (0 = không có quái), byte n,
        /// [float x, float y, short mapId đích, UTF tên map đích, byte kiểu 0 mép trái / 1 mép phải / 2 cổng giữa] x n.
        /// Vào map mới → hiện tên map giữa màn hình; cổng nào cũng có chữ chỉ đường (PortalMarkers).
        /// </summary>
        private void OnMapInfo(byte[] data)
        {
            var r = new MessageReader(data);
            string name = r.ReadUTF();
            int lo = r.ReadShort(), hi = r.ReadShort();
            int n = r.ReadByte();
            var portals = new List<Map.PortalMarkers.Info>(n);
            for (int i = 0; i < n; i++)
            {
                float x = r.ReadFloat(), y = r.ReadFloat();
                r.ReadShort(); // mapId đích (chưa dùng)
                string target = r.ReadUTF();
                int side = r.Available() > 0 ? r.ReadByte() : 2;
                portals.Add(new Map.PortalMarkers.Info { pos = new Vector2(x, y), target = target, side = side });
            }
            r.Cleanup();
            bool newMap = GameData.MapName != name;
            GameData.MapName = name; GameData.MapLevelMin = lo; GameData.MapLevelMax = hi;
            if (newMap && !string.IsNullOrEmpty(name))
                UI.GameHud.Banner(lo > 0 ? name + "\n<size=18><color=#ddd>Quái cấp " + lo + "–" + hi + "</color></size>" : name, 2.5f);
            Map.PortalMarkers.Show(portals);
        }

        private void OnActivityInfo(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.ActivityPoints = r.ReadShort();
            GameData.ActivityClaimed = r.ReadByte();
            GameData.Activities.Clear();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
                GameData.Activities.Add(new ActivityRow { name = r.ReadUTF(), progress = r.ReadShort(), target = r.ReadShort(), pts = r.ReadByte(), maxPts = r.ReadByte() });
            GameData.Milestones.Clear();
            int m = r.ReadByte();
            for (int i = 0; i < m; i++) GameData.Milestones.Add(new ActivityMilestone { need = r.ReadShort(), reward = r.ReadUTF() });
            GameData.EventSchedule.Clear();
            int k = r.ReadByte();
            for (int i = 0; i < k; i++) GameData.EventSchedule.Add(r.ReadUTF());
            if (r.Available() > 0)   // GĐ9 phần 4: quà online
            {
                GameData.OnlineSeconds = r.ReadInt();
                GameData.OnlineSyncTime = UnityEngine.Time.time;
                GameData.OnlineClaimed = r.ReadByte();
                GameData.OnlineGifts.Clear();
                int g = r.ReadByte();
                for (int i = 0; i < g; i++) GameData.OnlineGifts.Add(new ActivityMilestone { need = r.ReadShort(), reward = r.ReadUTF() });
            }
            r.Cleanup();
            GameData.Notify(DataKind.Activity);
        }

        private void OnQuestGuide(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.QuestGuide.Clear();
            int n = r.ReadShort();
            for (int i = 0; i < n; i++)
                GameData.QuestGuide.Add(new QuestGuideRow { questId = r.ReadInt(), status = r.ReadByte(), daily = r.ReadByte() != 0, where = r.ReadUTF() });
            r.Cleanup();
            GameData.Notify(DataKind.QuestGuide);
        }

        private void OnGuideTip(byte[] data)
        {
            var r = new MessageReader(data);
            string title = r.ReadUTF(), text = r.ReadUTF();
            int topic = r.ReadByte();
            r.Cleanup();
            GameWindow.Get<TipWindow>().ShowTip(title, text, topic);
        }
    }
}
