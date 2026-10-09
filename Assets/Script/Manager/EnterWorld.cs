using System.Collections.Generic;
using Assets.Script.Data;
using Assets.Script.Map;
using Assets.Script.Models;
using Assets.Script.Network;
using Assets.Script.Player;
using Assets.Script.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace Assets.Script.Manager
{
    /// <summary>
    /// VÀO GAME sau khi LOGIN / CREATE_CHARACTER thành công (status 0):
    ///   1. dọn dữ liệu phiên trước → ghi nhân vật mới (GameData.Me + LocalPlayerState)
    ///   2. màn chờ → tải map → tạo nhân vật mình (hiện khi map xong) → bật HUD, avatar, thanh chiêu
    /// Sau đó server tự gửi CHARACTER_INFO, INVENTORY, SKILL_LIST... (GameDataNetwork nhận).
    /// </summary>
    public static class EnterWorld
    {
        /// <summary>Phần sau status của gói LOGIN (Player.writeTo bên server).</summary>
        public class Info
        {
            public PlayerData player;
            public int mapId;
            public string equipmentJson, skillsJson, settingsJson;
        }

        /// <summary>
        /// int id, UTF tên, short hệ phái, int cấp, long exp, int yên, int xu, int lượng, int hp, int mp, int maxHp, int maxMp,
        /// float tốc chạy, float lực nhảy, float trọng lực, int mapId, int zoneId, float x, float y, UTF trang bị, UTF chiêu, UTF phím tắt
        /// </summary>
        public static Info Read(MessageReader r)
        {
            var p = new PlayerData
            {
                id = r.ReadInt(), name = r.ReadUTF(), class_type = r.ReadShort(), level = r.ReadInt(), exp = r.ReadLong(),
                yen = r.ReadInt(), xu = r.ReadInt(), luong = r.ReadInt(),
                hp = r.ReadInt(), mp = r.ReadInt(), maxHp = r.ReadInt(), maxMp = r.ReadInt(),
                moveSpeed = r.ReadFloat(), jumpForce = r.ReadFloat(), gravity = r.ReadFloat()
            };
            var info = new Info { player = p, mapId = r.ReadInt() };
            p.map_id = info.mapId;
            p.zone_id = r.ReadInt();
            p.x = r.ReadFloat(); p.y = r.ReadFloat();
            info.equipmentJson = r.ReadUTF();
            info.skillsJson = r.ReadUTF();
            info.settingsJson = r.ReadUTF();
            return info;
        }

        public static void Run(Info info, GameObject hud)
        {
            var p = info.player;

            // Dọn phiên trước TRƯỚC, rồi mới ghi nhân vật vừa vào (ngược lại thì bị xoá mất)
            GameData.ClearSession();
            GameData.ClassType = p.class_type;
            LocalPlayerState.Init(p.id, p.name, p.level, p.exp, p.yen, p.xu, p.luong, p.hp, p.maxHp, p.mp, p.maxMp);
            LocalPlayerState.ZoneId = p.zone_id;   // nút "Khu N" trên HUD

            PopupAndLoad.Instance.ShowLoading();
            MapManager.Instance.LoadMap(info.mapId > 0 ? info.mapId : 1);
            NetworkPlayerManager.Instance.SpawnLocalPlayer(p);

            if (hud != null) hud.SetActive(true);
            UISetup.Instance.SetupAvatar(p.class_type);
            InitSkillBar(info.skillsJson, info.settingsJson);
        }

        /// <summary>Chiêu đã học + ô phím tắt (JSON lưu trong DB).</summary>
        private static void InitSkillBar(string skillsJson, string settingsJson)
        {
            if (string.IsNullOrEmpty(skillsJson) || skillsJson == "{}") return;
            var skills = JsonConvert.DeserializeObject<List<PlayerSkillData>>(skillsJson);
            int[] shortcuts = string.IsNullOrEmpty(settingsJson) || settingsJson == "{}"
                ? null : JsonConvert.DeserializeObject<int[]>(settingsJson);
            SkillBarManager.Instance.InitPlayerSkills(skills, shortcuts);
        }
    }
}
