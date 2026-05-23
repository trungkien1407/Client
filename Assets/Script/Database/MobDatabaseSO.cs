using System.Collections.Generic;
using UnityEngine;
using Assets.Script.Models;

namespace Assets.Script.Database
{
    [CreateAssetMenu(fileName = "MobDatabase", menuName = "NinjaSchool/Mob Database")]
    public class MobDatabaseSO : ScriptableObject
    {
        [Header("Danh sách cấu hình hình ảnh Quái vật")]
        public List<MobVisualData> mobs = new List<MobVisualData>();

        // Dictionary để tra cứu ID với tốc độ O(1)
        private Dictionary<short, MobVisualData> _mobDict;

        public void Init()
        {
            if (_mobDict != null) return;
            _mobDict = new Dictionary<short, MobVisualData>();
            foreach (var mob in mobs)
            {
                if (!_mobDict.ContainsKey(mob.templateId))
                    _mobDict.Add(mob.templateId, mob);
            }
            Debug.Log($"[Database] Đã khởi tạo {_mobDict.Count} loại quái vật.");
        }

        public MobVisualData GetMobVisual(short templateId)
        {
            if (_mobDict == null) Init(); // Đề phòng chưa gọi Init

            if (_mobDict.TryGetValue(templateId, out MobVisualData visual))
                return visual;

            return null;
        }
    }
}