using System.Collections.Generic;
using System.IO;
using Assets.Script.Constants;
using Assets.Script.Network;
using UnityEngine;

namespace Assets.Script.Data
{
    /// <summary>
    /// LƯU DỮ LIỆU TĨNH TRÊN MÁY THEO PHIÊN BẢN (học từ NSO / G4M: itemVersion, skillVersion... trong bộ đệm):
    ///   server gửi GAME_DATA_VERSION (cmd → phiên bản) →
    ///     bản trên máy cùng phiên bản → đưa lại vào hàng đợi gói như vừa nhận (GameDataNetwork đọc như thường)
    ///     thiếu / khác → GAME_DATA_REQUEST → gói về thì lưu lại kèm phiên bản.
    /// File: {persistentDataPath}/gamedata/{cmd}.bin = int phiên bản + payload. Xoá thư mục này = tải lại hết.
    /// </summary>
    public class GameDataCache : NetworkListener
    {
        private static readonly short[] Kinds =
        {
            Cmd.GAME_DATA_ITEMS, Cmd.GAME_DATA_SKILLS, Cmd.GAME_DATA_QUESTS,
            Cmd.GAME_DATA_MOBS, Cmd.GAME_DATA_NPCS, Cmd.GAME_DATA_EFFECTS
        };

        private readonly Dictionary<short, int> _waiting = new Dictionary<short, int>();   // cmd đã xin → phiên bản để lưu

        private static string Dir => Path.Combine(Application.persistentDataPath, "gamedata");
        private static string FileOf(short cmd) => Path.Combine(Dir, cmd + ".bin");

        protected override void RegisterHandlers()
        {
            Listen(Cmd.GAME_DATA_VERSION, OnVersions);
            foreach (var k in Kinds) { short cmd = k; Listen(cmd, data => Save(cmd, data)); }
        }

        /// <summary>GAME_DATA_VERSION: byte n, [short cmd, int version] x n</summary>
        private void OnVersions(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadByte();
            var need = new List<short>();
            int hits = 0;
            for (int i = 0; i < n; i++)
            {
                short cmd = r.ReadShort();
                int ver = r.ReadInt();
                var cached = Load(cmd, ver);
                if (cached != null)
                {
                    NetworkEventDispatcher.Instance.EnqueuePacket(new PacketDecoder.Packet { cmd = cmd, data = cached });
                    hits++;
                }
                else
                {
                    need.Add(cmd);
                    _waiting[cmd] = ver;
                }
            }
            r.Cleanup();
            if (need.Count > 0) GameActions.RequestGameData(need);
            Debug.Log($"[Dữ liệu tĩnh] dùng bản trên máy {hits}, tải mới {need.Count}");
        }

        /// <summary>Bản trên máy đúng phiên bản → payload; không có / khác / lỗi đọc → null.</summary>
        private static byte[] Load(short cmd, int version)
        {
            try
            {
                string f = FileOf(cmd);
                if (!File.Exists(f)) return null;
                byte[] all = File.ReadAllBytes(f);
                if (all.Length < 4 || System.BitConverter.ToInt32(all, 0) != version) return null;
                var payload = new byte[all.Length - 4];
                System.Buffer.BlockCopy(all, 4, payload, 0, payload.Length);
                return payload;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Dữ liệu tĩnh] đọc bộ đệm {cmd} lỗi: {e.Message}");
                return null;
            }
        }

        /// <summary>Chỉ lưu gói mình vừa xin (gói đọc từ bộ đệm cũng đi qua đây — bỏ qua).</summary>
        private void Save(short cmd, byte[] data)
        {
            if (!_waiting.TryGetValue(cmd, out int ver)) return;
            _waiting.Remove(cmd);
            try
            {
                Directory.CreateDirectory(Dir);
                using (var fs = new FileStream(FileOf(cmd), FileMode.Create))
                {
                    fs.Write(System.BitConverter.GetBytes(ver), 0, 4);
                    fs.Write(data, 0, data.Length);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Dữ liệu tĩnh] lưu {cmd} lỗi: {e.Message}");
            }
        }
    }
}
