using System.Collections.Generic;
using UnityEngine;
using Assets.Script.Models;

namespace Assets.Script.Database
{
    [CreateAssetMenu(fileName = "SkillDatabase", menuName = "NinjaSchool/Skill Database")]
    public class SkillDatabaseSO : ScriptableObject
    {
        [Header("Danh sách hình ảnh & hiệu ứng Kỹ năng")]
        public List<SkillVisualData> skills = new List<SkillVisualData>();

        // Dictionary để tra cứu ID với tốc độ O(1)
        private Dictionary<int, SkillVisualData> _skillDict;

        public void Init()
        {
            if (_skillDict != null) return;

            _skillDict = new Dictionary<int, SkillVisualData>();
            foreach (var skill in skills)
            {
                if (!_skillDict.ContainsKey(skill.templateId))
                {
                    _skillDict.Add(skill.templateId, skill);
                }
            }
            Debug.Log($"[Database] Đã khởi tạo {_skillDict.Count} cấu hình Skill.");
        }

        public SkillVisualData GetSkillVisual(int templateId)
        {
            // [THÊM DÒNG NÀY] Nếu là ID -1 (ô trống) thì trả về null luôn, không báo lỗi
            if (templateId == -1) return null;

            if (_skillDict == null) Init();

            if (_skillDict.TryGetValue(templateId, out SkillVisualData visual))
                return visual;

            Debug.LogWarning($"[SkillDatabase] Không tìm thấy hình ảnh cho Skill ID: {templateId}");
            return null;
        }
    }
}