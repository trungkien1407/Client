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
    /// GÓI KINH TẾ: nâng cấp, rương, giao dịch, chợ, khảm ngọc, giftcode → ghi GameData + mở cửa sổ tương ứng.
    /// Payload: docs/ky-thuat/PROTOCOL.md (repo server). Lời mời giao dịch hiện ConfirmWindow, KHÔNG tự đồng ý.
    /// </summary>
    public class EconomyNetwork : NetworkListener
    {
        protected override void RegisterHandlers()
        {
            Listen(Cmd.UPGRADE_OPEN, OnUpgradeOpen);
            Listen(Cmd.UPGRADE_RESULT, OnUpgradeResult);
            Listen(Cmd.STORAGE_DATA, OnStorage);
            Listen(Cmd.TRADE_INVITE, OnTradeInvite);
            Listen(Cmd.TRADE_UPDATE, OnTradeUpdate);
            Listen(Cmd.TRADE_CLOSE, OnTradeClose);
            Listen(Cmd.MARKET_OPEN, OnMarketOpen);
            Listen(Cmd.MARKET_LIST, OnMarketList);
            Listen(Cmd.MARKET_MINE, OnMarketMine);
            Listen(Cmd.MARKET_RESULT, OnMarketResult);
            Listen(Cmd.GEM_OPEN, OnGemOpen);
            Listen(Cmd.GEM_RESULT, OnGemResult);
            Listen(Cmd.GIFTCODE_RESULT, OnGiftcodeResult);
        }

        private static void ReadSide(MessageReader r, TradeSide s)
        {
            s.locked = r.ReadByte() != 0; s.confirmed = r.ReadByte() != 0; s.yen = r.ReadInt();
            s.items.Clear(); s.items.AddRange(BagSlot.ReadList(r));
        }

        /// <summary>
        /// UPGRADE_OPEN: int npcId, byte maxLv, [byte rate, int stoneId, short stoneQty, int yen] x maxLv,
        ///               int protectItemId, byte n, [short statPercent] x n
        /// </summary>
        private void OnUpgradeOpen(byte[] data)
        {
            var r = new MessageReader(data);
            var t = new UpgradeTable { npcId = r.ReadInt(), maxLv = r.ReadByte() };
            t.rate = new int[t.maxLv + 1]; t.stoneId = new int[t.maxLv + 1]; t.stoneQty = new int[t.maxLv + 1]; t.yen = new int[t.maxLv + 1];
            for (int lv = 1; lv <= t.maxLv; lv++)
            {
                t.rate[lv] = r.ReadByte(); t.stoneId[lv] = r.ReadInt(); t.stoneQty[lv] = r.ReadShort(); t.yen[lv] = r.ReadInt();
            }
            t.protectItemId = r.ReadInt();
            int n = r.ReadByte();
            t.statPercent = new int[n];
            for (int i = 0; i < n; i++) t.statPercent[i] = r.ReadShort();
            r.Cleanup();
            GameData.Upgrade = t;
            GameData.Notify(DataKind.Upgrade);
            GameWindow.Get<UpgradeWindow>().ShowFor(t.npcId);
        }

        /// <summary>UPGRADE_RESULT: byte result(0 thành công/1 trượt giữ cấp/2 trượt tụt cấp/3 lỗi), byte level, UTF msg</summary>
        private void OnUpgradeResult(byte[] data)
        {
            var r = new MessageReader(data);
            int result = r.ReadByte(), level = r.ReadByte();
            string msg = r.ReadUTF();
            r.Cleanup();
            GameWindow.Get<UpgradeWindow>().ShowResult(result, level, msg);
        }

        /// <summary>STORAGE_DATA: int npcId, short capacity, short n, [ô đồ] x n</summary>
        private void OnStorage(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.StorageNpc = r.ReadInt();
            GameData.StorageCapacity = r.ReadShort();
            var list = BagSlot.ReadList(r);
            r.Cleanup();
            GameData.Storage.Clear();
            GameData.Storage.AddRange(list);
            GameData.Notify(DataKind.Storage);
            var w = GameWindow.Get<StorageWindow>();
            if (!w.IsOpen) w.OpenWithBag();
        }

        private void OnTradeInvite(byte[] data)
        {
            var inv = Packets.ReadInvite(data);
            ConfirmWindow.Ask($"<b>{inv.name}</b> muốn giao dịch với bạn.", "Đồng ý", () => GameActions.TradeAccept(inv.fromId), "Từ chối", null);
        }

        /// <summary>TRADE_UPDATE: int otherId, UTF otherName, [mình: byte locked, byte confirmed, int yen, ô đồ], [bên kia: như trên]</summary>
        private void OnTradeUpdate(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.TradeWith = r.ReadInt();
            GameData.TradeWithName = r.ReadUTF();
            ReadSide(r, GameData.TradeMine);
            ReadSide(r, GameData.TradeTheirs);
            r.Cleanup();
            GameData.Notify(DataKind.Trade);
            var w = GameWindow.Get<TradeWindow>();
            if (!w.IsOpen) w.OpenWithBag();
        }

        /// <summary>TRADE_CLOSE: byte result(0 huỷ/1 xong), UTF lý do</summary>
        private void OnTradeClose(byte[] data)
        {
            var r = new MessageReader(data);
            int result = r.ReadByte(); string reason = r.ReadUTF();
            r.Cleanup();
            GameData.TradeWith = 0;
            GameData.Notify(DataKind.Trade);
            GameWindow.Get<TradeWindow>().Hide();
            ChatBox.AddSystem(result == 1 ? "Giao dịch thành công!" : "Giao dịch đã huỷ" + (string.IsNullOrEmpty(reason) ? "" : ": " + reason));
        }

        // ---- CHỢ ----
        private void OnMarketOpen(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.MarketNpc = r.ReadInt();
            GameData.MarketMax = r.ReadByte();
            GameData.MarketFeePermil = r.ReadShort();
            GameData.MarketTaxPercent = r.ReadByte();
            GameData.MarketHours = r.ReadShort();
            r.Cleanup();
            GameData.MarketMessage = "";
            GameWindow.Get<MarketWindow>().OpenWithBag();
        }

        private void OnMarketList(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.MarketTotal = r.ReadShort();
            GameData.MarketPage = r.ReadShort();
            int n = r.ReadShort();
            GameData.MarketRows.Clear();
            for (int i = 0; i < n; i++)
                GameData.MarketRows.Add(new MarketRow { id = r.ReadLong(), slot = BagSlot.Read(r), price = r.ReadInt(), seller = r.ReadUTF(), secondsLeft = r.ReadInt() });
            r.Cleanup();
            GameData.Notify(DataKind.Market);
        }

        private void OnMarketMine(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.MarketMine.Clear();
            for (int i = 0; i < n; i++)
                GameData.MarketMine.Add(new MarketRow { id = r.ReadLong(), slot = BagSlot.Read(r), price = r.ReadInt(), secondsLeft = r.ReadInt(), status = r.ReadByte() });
            r.Cleanup();
            GameData.Notify(DataKind.Market);
        }

        private void OnMarketResult(byte[] data)
        {
            GameData.MarketMessage = Packets.ReadResult(data).Colored;
            GameData.Notify(DataKind.Market);
        }

        // ---- KHẢM NGỌC ----
        private void OnGemOpen(byte[] data)
        {
            var r = new MessageReader(data);
            GameData.GemNpc = r.ReadInt();
            GameData.GemSocketCost = r.ReadShort();
            GameData.GemRemoveCost = r.ReadShort();
            GameData.GemCombineCost = r.ReadShort();
            r.Cleanup();
            GameData.GemMessage = "";
            GameWindow.Open<GemWindow>();
        }

        private void OnGemResult(byte[] data)
        {
            GameData.GemMessage = Packets.ReadResult(data).Colored;
            GameData.Notify(DataKind.Gem);
        }

        private void OnGiftcodeResult(byte[] data)
        {
            var res = Packets.ReadResult(data);
            GameData.GiftcodeMessage = res.Colored;
            GameData.Notify(DataKind.Activity);
            if (res.ok) UI.GameHud.Banner(res.msg, 3f);
        }
    }
}
