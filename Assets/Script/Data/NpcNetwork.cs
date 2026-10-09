using System.Collections.Generic;
using Assets.Script.Constants;
using Assets.Script.Entities;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using UnityEngine;

namespace Assets.Script.Data
{
    /// <summary>
    /// NÓI CHUYỆN VỚI NPC: phím F / nút "Nói" → NPC gần nhất → NPC_TALK; server trả
    ///   NPC_MENU  → hộp thoại (NpcDialogWindow) — menu do server quyết, client chỉ gửi số thứ tự lựa chọn
    ///   SHOP_DATA → cửa hàng (ShopWindow)
    /// </summary>
    public class NpcNetwork : NetworkListener
    {
        /// <summary>Tầm nói chuyện (server kiểm lại).</summary>
        public const float TalkRange = 3.5f;

        protected override void RegisterHandlers()
        {
            Listen(Cmd.NPC_MENU, OnNpcMenu);
            Listen(Cmd.SHOP_DATA, OnShopData);
        }

        /// <summary>Nói chuyện với NPC gần nhân vật nhất (trong tầm).</summary>
        public static void TalkToNearest()
        {
            var me = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (me == null || NetworkNpcManager.Instance == null) return;
            NpcEntity best = null;
            float bestSqr = TalkRange * TalkRange;
            foreach (var npc in NetworkNpcManager.Instance.All)
            {
                float sqr = ((Vector2)(npc.transform.position - me.transform.position)).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = npc; }
            }
            if (best != null) GameActions.NpcTalk(best.GetId());
            else Combat.ChatBox.AddSystem("Không có NPC nào ở gần.");
        }

        /// <summary>NPC_MENU: int npcId, UTF tên, UTF lời thoại, byte n, [UTF lựa chọn] x n</summary>
        private void OnNpcMenu(byte[] data)
        {
            var r = new MessageReader(data);
            int npcId = r.ReadInt();
            string name = r.ReadUTF();
            string text = r.ReadUTF();
            int n = r.ReadByte();
            var opts = new List<string>();
            for (int i = 0; i < n; i++) opts.Add(r.ReadUTF());
            r.Cleanup();
            GameWindow.Get<NpcDialogWindow>().ShowMenu(npcId, name, text, opts);
        }

        /// <summary>SHOP_DATA: int npcId, short count, [int itemId, int price, byte currency(0 yên/1 xu/2 lượng)] x count</summary>
        private void OnShopData(byte[] data)
        {
            var r = new MessageReader(data);
            int npcId = r.ReadInt();
            int n = r.ReadShort();
            var goods = new List<ShopGood>();
            for (int i = 0; i < n; i++) goods.Add(new ShopGood { itemId = r.ReadInt(), price = r.ReadInt(), currency = r.ReadByte() });
            r.Cleanup();
            GameWindow.Get<ShopWindow>().ShowShop(npcId, goods);
        }
    }
}
