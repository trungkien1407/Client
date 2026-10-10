using UnityEngine;
using Assets.Script.Database;
using System.Collections.Generic;
using Assets.Script.Models;
namespace Assets.Script.Skill
{
    public class SkillBarManager : MonoBehaviour
    {
        public static SkillBarManager Instance;

        [Header("Danh sách các ô Skill (Kéo thả từ UI vào)")]
        [Tooltip("[CẦN ĐIỀN] Kéo 5 ô SkillSlotUI trên HUD theo thứ tự phím 1→5")]
        public SkillSlotUI[] skillSlots; // Mảng 5 ô skill

        [Tooltip("[CẦN ĐIỀN] Kéo Assets/SO/SkillDatabase.asset. Thêm skill mới: mở asset đó, thêm dòng templateId (khớp skill_template.id bên server) + iconId + atlas icon")]
        public SkillDatabaseSO skillDatabase;

        private int currentSelectedIndex = 0; // Mặc định chọn ô đầu tiên (index 0)

        // Thêm tham số mảng shortcuts (có thể null)
        public void InitPlayerSkills(List<PlayerSkillData> mySkills, int[] shortcuts = null)
        {
            // 1. Dọn dẹp/Xóa hết skill cũ trên các ô (Nếu có)
            for (int i = 0; i < skillSlots.Length; i++)
            {
                skillSlots[i].AssignSkill(-1, skillDatabase);
            }

            if (mySkills == null || mySkills.Count == 0) return;

            // 2. Rải skill theo mảng cài đặt từ Server
            if (shortcuts != null && shortcuts.Length > 0)
            {
                for (int i = 0; i < shortcuts.Length; i++)
                {
                    // Tránh lỗi nếu Server gửi mảng dài hơn số lượng ô UI thực tế
                    if (i >= skillSlots.Length) break;

                    int shortcutSkillId = shortcuts[i];

                    if (shortcutSkillId != -1)
                    {
                        // Kiểm tra xem người chơi có thực sự đã học/sở hữu skill này không
                        bool isOwningSkill = mySkills.Exists(s => s.templateId == shortcutSkillId);

                        if (isOwningSkill)
                        {
                            skillSlots[i].AssignSkill(shortcutSkillId, skillDatabase);
                        }
                        else
                        {
                            Debug.LogWarning($"[Skill] Server yêu cầu gán skill {shortcutSkillId} vào ô {i} nhưng Player chưa có skill này!");
                        }
                    }
                }
            }
            else
            {
                // 2.1 Fallback: Nếu không có mảng settings (hoặc mới tạo nhân vật), rải tuần tự cho đỡ trống
                int maxSlots = Mathf.Min(mySkills.Count, skillSlots.Length);
                for (int i = 0; i < maxSlots; i++)
                {
                    skillSlots[i].AssignSkill(mySkills[i].templateId, skillDatabase);
                }
            }

            // 3. Tự động chọn (Highlight) ô skill đầu tiên (chỉ chọn, không tung chiêu)
            currentSelectedIndex = 0;
            UpdateHighlight();
        }
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void Start()
        {
            // Khởi tạo giao diện ban đầu (Tắt hết highlight, chỉ bật ô đang chọn)
            UpdateHighlight();
        }

        private void Update()
        {
            // Phím 1 → 5 (GameInput: đang gõ chữ thì bỏ qua)
            for (int i = 0; i < 5; i++) if (Core.GameInput.SkillSlot(i)) SelectSlotAndUse(i);
        }

        // Hàm đổi vùng chọn sang ô mới và dùng luôn
        public void SelectSlotAndUse(int index)
        {
            if (index < 0 || index >= skillSlots.Length) return;
            if (skillSlots[index].assignedSkillId == -1) return; // Ô trống thì không làm gì

            // 1. Cập nhật UI
            currentSelectedIndex = index;
            UpdateHighlight();

            // 2. Tung chiêu (SkillCaster tự chọn mục tiêu / kiểm tầm)
            Assets.Script.Combat.SkillCaster.TryCast(skillSlots[index].assignedSkillId);
        }

        /// <summary>Id skill (templateId) đang được chọn trên thanh phím tắt, -1 nếu ô trống.</summary>
        public int GetSelectedSkillId()
        {
            if (skillSlots == null || currentSelectedIndex < 0 || currentSelectedIndex >= skillSlots.Length) return -1;
            return skillSlots[currentSelectedIndex].assignedSkillId;
        }

        // Hàm cập nhật viền sáng
        private void UpdateHighlight()
        {
            for (int i = 0; i < skillSlots.Length; i++)
            {
                skillSlots[i].SetSelected(i == currentSelectedIndex);
            }
        }

        // Xử lý gửi lệnh đánh lên Server

        // (Dành cho việc Load Data) Gán skill vào ô cụ thể
        public void AssignSkillToSlot(int slotIndex, int skillId)
        {
            if (slotIndex >= 0 && slotIndex < skillSlots.Length)
            {
                skillSlots[slotIndex].AssignSkill(skillId, skillDatabase);

                // Nếu vừa gán vào ô đang được select, thì update lại UI
                if (slotIndex == currentSelectedIndex) UpdateHighlight();
            }
        }

        // ==========================================
        // DỌN DẸP DỮ LIỆU (DÙNG KHI ĐĂNG XUẤT)
        // ==========================================
        public void ClearSkills()
        {
            if (skillSlots == null) return;

            // 1. Xóa data trên tất cả các ô skill (gán về -1)
            for (int i = 0; i < skillSlots.Length; i++)
            {
                if (skillSlots[i] != null)
                {
                    skillSlots[i].AssignSkill(-1, skillDatabase);
                }
            }

            // 2. Reset lại vị trí đang chọn về ô đầu tiên
            currentSelectedIndex = 0;

            // 3. Cập nhật lại viền sáng trên UI
            UpdateHighlight();

            Debug.Log("[Client] Đã dọn dẹp toàn bộ Skill trên thanh phím tắt.");
        }
    }
}
