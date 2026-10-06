using System.Collections.Generic;
using Assets.Script.Constants;
using Assets.Script.Network;
using Assets.Script.Player;
using UnityEngine;

namespace Assets.Script.Data
{
    /// <summary>
    /// Nhận các gói DỮ LIỆU (không phải hình ảnh) rồi ghi vào GameData + báo UI vẽ lại:
    /// GAME_DATA_ITEMS/SKILLS/QUESTS/MOBS/NPCS, CHARACTER_INFO, SKILL_LIST, INVENTORY, EQUIPMENT, QUEST_LIST/UPDATE.
    /// Payload từng gói: xem docs/PROTOCOL.md (repo server).
    /// </summary>
    public class GameDataNetwork : NetworkListener
    {
        protected override void RegisterHandlers()
        {
            Listen(Cmd.GAME_DATA_ITEMS, OnItems);
            Listen(Cmd.GAME_DATA_SKILLS, OnSkills);
            Listen(Cmd.GAME_DATA_QUESTS, OnQuests);
            Listen(Cmd.GAME_DATA_MOBS, OnMobs);
            Listen(Cmd.GAME_DATA_NPCS, OnNpcs);
            Listen(Cmd.CHARACTER_INFO, OnCharacterInfo);
            Listen(Cmd.SKILL_LIST, OnSkillList);
            Listen(Cmd.INVENTORY, OnInventory);
            Listen(Cmd.EQUIPMENT, OnEquipment);
            Listen(Cmd.QUEST_LIST, OnQuestList);
            Listen(Cmd.QUEST_UPDATE, OnQuestUpdate);
        }

        private void OnItems(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.Items.Clear();
            for (int i = 0; i < n; i++)
            {
                var t = new ItemTpl
                {
                    id = r.ReadInt(), name = r.ReadUTF(), type = r.ReadSByte(), slot = r.ReadSByte(), iconId = r.ReadInt(),
                    levelRequire = r.ReadShort(), classRequire = r.ReadSByte(), price = r.ReadInt(), maxStack = r.ReadShort(),
                    bonusHp = r.ReadInt(), bonusMp = r.ReadInt(), bonusDamage = r.ReadInt(),
                    hpRestore = r.ReadInt(), mpRestore = r.ReadInt(), tradeable = r.ReadByte() != 0, description = r.ReadUTF()
                };
                GameData.Items[t.id] = t;
            }
            r.Cleanup();
            GameData.Notify(DataKind.Templates);
        }

        private void OnSkills(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.Skills.Clear();
            for (int i = 0; i < n; i++)
            {
                var t = new SkillTpl
                {
                    id = r.ReadInt(), name = r.ReadUTF(), classId = r.ReadSByte(), maxLevel = r.ReadShort(),
                    type = r.ReadSByte(), iconId = r.ReadInt(), description = r.ReadUTF()
                };
                int lv = r.ReadByte();
                for (int j = 0; j < lv; j++)
                    t.levels.Add(new SkillLevel
                    {
                        point = r.ReadShort(), manaUse = r.ReadInt(), coolDown = r.ReadInt(), damage = r.ReadInt(),
                        range = r.ReadFloat(), aoe = r.ReadFloat(), info = r.ReadUTF()
                    });
                GameData.Skills[t.id] = t;
            }
            r.Cleanup();
            GameData.Notify(DataKind.Templates);
        }

        private void OnQuests(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.Quests.Clear();
            for (int i = 0; i < n; i++)
            {
                var q = new QuestTpl
                {
                    id = r.ReadInt(), name = r.ReadUTF(), description = r.ReadUTF(), type = r.ReadSByte(),
                    targetId = r.ReadInt(), targetCount = r.ReadInt(), npcId = r.ReadInt(), levelRequire = r.ReadShort(),
                    prevQuestId = r.ReadInt(), rewardExp = r.ReadLong(), rewardYen = r.ReadInt(),
                    rewardItemId = r.ReadInt(), rewardItemQty = r.ReadInt()
                };
                GameData.Quests[q.id] = q;
            }
            r.Cleanup();
            GameData.Notify(DataKind.Templates);
        }

        private void OnMobs(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            for (int i = 0; i < n; i++)
            {
                var m = new MobTpl { id = r.ReadInt(), name = r.ReadUTF(), level = r.ReadShort(), rank = r.ReadByte(), aggressive = r.ReadByte() != 0 };
                GameData.Mobs[m.id] = m;
            }
            r.Cleanup();
            GameData.Notify(DataKind.Templates);
        }

        private void OnNpcs(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            for (int i = 0; i < n; i++) GameData.NpcNames[r.ReadInt()] = r.ReadUTF();
            r.Cleanup();
            GameData.Notify(DataKind.Templates);
        }

        private void OnCharacterInfo(byte[] data)
        {
            var r = new MessageReader(data);
            var c = GameData.Me;
            c.level = r.ReadShort(); c.exp = r.ReadLong(); c.expToNext = r.ReadLong();
            c.potential = r.ReadShort(); c.skillPoints = r.ReadShort();
            c.sucManh = r.ReadShort(); c.thanPhap = r.ReadShort(); c.chakra = r.ReadShort(); c.theLuc = r.ReadShort();
            c.maxHp = r.ReadInt(); c.maxMp = r.ReadInt(); c.bonusDamage = r.ReadInt();
            c.dodge = r.ReadFloat(); c.crit = r.ReadFloat(); c.moveSpeed = r.ReadFloat();
            c.yen = r.ReadInt(); c.xu = r.ReadInt(); c.luong = r.ReadInt(); c.pkPoint = r.ReadShort();
            r.Cleanup();

            LocalPlayerState.Level = c.level;
            LocalPlayerState.Exp = c.exp;
            LocalPlayerState.Yen = c.yen;
            LocalPlayerState.MaxHp = c.maxHp;
            LocalPlayerState.MaxMp = c.maxMp;
            LocalPlayerState.RefreshHud();
            GameData.Notify(DataKind.Character);
        }

        private void OnSkillList(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.MySkills.Clear();
            for (int i = 0; i < n; i++) GameData.MySkills[r.ReadInt()] = r.ReadShort();
            r.Cleanup();
            GameData.Notify(DataKind.Skills);
        }

        private void OnInventory(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.Inventory.Clear();
            for (int i = 0; i < n; i++) GameData.Inventory.Add(new KeyValuePair<int, int>(r.ReadInt(), r.ReadInt()));
            r.Cleanup();
            GameData.Notify(DataKind.Inventory);
        }

        private void OnEquipment(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.Equipment.Clear();
            for (int i = 0; i < n; i++) GameData.Equipment[r.ReadInt()] = r.ReadInt();
            r.Cleanup();
            GameData.Notify(DataKind.Equipment);
        }

        private void OnQuestList(byte[] data)
        {
            var r = new MessageReader(data);
            int n = r.ReadShort();
            GameData.MyQuests.Clear();
            for (int i = 0; i < n; i++) GameData.MyQuests[r.ReadInt()] = new[] { r.ReadInt(), (int)r.ReadByte() };
            r.Cleanup();
            GameData.Notify(DataKind.Quests);
        }

        private void OnQuestUpdate(byte[] data)
        {
            var r = new MessageReader(data);
            int id = r.ReadInt();
            int progress = r.ReadInt();
            int done = r.ReadByte();
            r.Cleanup();
            bool isNew = !GameData.MyQuests.ContainsKey(id);
            GameData.MyQuests[id] = new[] { progress, done };
            GameData.Notify(DataKind.Quests);

            if (GameData.Quests.TryGetValue(id, out var q))
            {
                if (isNew) Assets.Script.Combat.ChatBox.AddSystem($"Nhận nhiệm vụ: {q.name}");
                else if (done == 1) Assets.Script.Combat.ChatBox.AddSystem($"Hoàn thành: {q.name} — về gặp {GameData.NpcName(q.npcId)} để trả");
                else Assets.Script.Combat.ChatBox.AddSystem($"{q.name}: {progress}/{q.targetCount}");
            }
        }
    }
}
