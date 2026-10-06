using System.Collections.Generic;
using UnityEngine;

namespace Assets.Script.Database
{
    [CreateAssetMenu(fileName = "NpcDatabase", menuName = "NinjaSchool/Npc Database")]
    public class NpcDatabaseSO : ScriptableObject
    {
        [System.Serializable]
        public class NpcConfig
        {
            public int templateId;
            public string defaultName;

            [Header("Tên Sprite trong Atlas (vd: npc_12_head)")]
            public string headSpriteName;
            public string legSpriteName;
            // public string bodySpriteName;
        }

        public List<NpcConfig> npcs = new List<NpcConfig>();
        public int test;
        public NpcConfig GetNpcConfig(int templateId)
        {
            return npcs.Find(n => n.templateId == templateId);
        }
    }
}