using Assets.Script.Constants;
using Assets.Script.Network;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;

namespace Assets.Script.Data
{
    /// <summary>
    /// NHẬN CÁC GÓI GĐ9 ("khung MMORPG chuẩn" phần 1) rồi ghi vào GameData:
    ///   ACTIVITY_INFO — bảng Hoạt động hằng ngày + lịch sự kiện (server tự đẩy khi được cộng điểm)
    ///   QUEST_GUIDE   — sổ tay nhiệm vụ: đang làm / có thể nhận + nơi đến
    ///   GUIDE_TIP     — mẹo khi lên cấp mở tính năng mới → hiện khung TipWindow
    /// Payload: docs/PROTOCOL.md (repo server) mục GĐ9.
    /// </summary>
    public class WorldNetwork : NetworkListener
    {
        protected override void RegisterHandlers()
        {
            Listen(Cmd.ACTIVITY_INFO, OnActivityInfo);
            Listen(Cmd.QUEST_GUIDE, OnQuestGuide);
            Listen(Cmd.GUIDE_TIP, OnGuideTip);
            Listen(Cmd.MARKET_OPEN, OnMarketOpen);
            Listen(Cmd.MARKET_LIST, OnMarketList);
            Listen(Cmd.MARKET_MINE, OnMarketMine);
            Listen(Cmd.MARKET_RESULT, OnMarketResult);
            Listen(Cmd.GEM_OPEN, OnGemOpen);
            Listen(Cmd.GEM_RESULT, OnGemResult);
            Listen(Cmd.GIFTCODE_RESULT, OnGiftcodeResult);
        }

        private void OnGiftcodeResult(byte[] data)
        {
            var res = Packets.ReadResult(data);
            GameData.GiftcodeMessage = res.Colored;
            GameData.Notify(DataKind.Activity);
            if (res.ok) UI.GameHud.Banner(res.msg, 3f);
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
