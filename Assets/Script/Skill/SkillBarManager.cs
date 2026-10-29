using UnityEngine;
using Assets.Script.Database;
using System.Collections.Generic;
using Assets.Script.Models;
namespace Assets.Script.Skill
{
    public class SkillBarManager : MonoBehaviour
    {
        public static SkillBarManager Instance;

        /// <summary>Số ô chiêu (phím 1 → 9) — khớp server SkillService.SHORTCUT_SLOTS.</summary>
        public const int SlotCount = 9;

        [Header("Danh sách các ô Skill (Kéo thả từ UI vào)")]
        [Tooltip("Kéo các ô SkillSlotUI trên HUD theo thứ tự phím 1→5 — thiếu tới 9 ô thì lúc chạy tự nhân bản ô cuối (EnsureSlots)")]
        public SkillSlotUI[] skillSlots;

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
            else { Destroy(gameObject); return; }
            EnsureSlots();
        }

        /// <summary>
        /// 9 Ô CHIÊU (2026-10-29, mỗi hệ giờ có tới 9 chiêu chủ động): scene chỉ kéo sẵn 5 ô → nhân bản ô cuối cho đủ SlotCount,
        /// rồi THU NHỎ CẢ THANH cho vừa bề rộng cũ (ô 120 → ~75 px trên màn hình). Không co cellSize của lưới: các lớp con trong ô (khung chọn,
        /// icon) vẽ theo cỡ 120 → co lưới thì ô 1 (khung chọn) và icon phình ra đè ô bên cạnh / nút Auto. Mỗi ô bấm / chạm được (trước chỉ phím 1–5).
        /// </summary>
        private void EnsureSlots()
        {
            if (skillSlots == null || skillSlots.Length == 0) return;
            var list = new List<SkillSlotUI>(skillSlots);
            var last = list[list.Count - 1];
            while (list.Count < SlotCount && last != null)
            {
                var copy = Instantiate(last, last.transform.parent);
                copy.name = "Slot" + (list.Count + 1);
                copy.assignedSkillId = -1;
                list.Add(copy);
            }
            skillSlots = list.ToArray();
            for (int i = 0; i < skillSlots.Length; i++)
            {
                var s = skillSlots[i];
                if (s == null) continue;
                s.slotIndex = i;
                s.SetSelected(false);
                if (!s.TryGetComponent(out UnityEngine.UI.Button btn)) btn = s.gameObject.AddComponent<UnityEngine.UI.Button>();
                btn.transition = UnityEngine.UI.Selectable.Transition.None;
                int index = i;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => SelectSlotAndUse(index));
            }
            var parent = skillSlots[0].transform.parent;
            if (parent != null && parent.TryGetComponent(out UnityEngine.UI.GridLayoutGroup grid))
            {
                var rt = (RectTransform)parent;
                float width = rt.sizeDelta.x;                                           // bề rộng thanh cũ (700)
                int n = skillSlots.Length;
                float need = n * grid.cellSize.x + (n - 1) * grid.spacing.x;           // 9 ô 120 px + khe
                if (need > width)
                {
                    rt.sizeDelta = new Vector2(need, rt.sizeDelta.y);
                    rt.localScale = Vector3.one * (width / need);                       // pivot giữa → vẫn nằm đúng chỗ cũ
                }
            }
        }

        private void Start()
        {
            // Khởi tạo giao diện ban đầu (Tắt hết highlight, chỉ bật ô đang chọn)
            UpdateHighlight();
            Data.GameData.OnChanged += OnDataChanged;
        }

        private void OnDestroy() => Data.GameData.OnChanged -= OnDataChanged;

        // Thanh chiêu gán lúc vào game có thể TRƯỚC khi GAME_DATA_SKILLS tới → ô lùi về icon cũ trong SkillDatabase.asset.
        // Dữ liệu mẫu tới (Templates) → vẽ lại icon theo skill_template.icon_id của server.
        private void OnDataChanged(Data.DataKind kind)
        {
            if (kind != Data.DataKind.Templates || skillSlots == null) return;
            foreach (var slot in skillSlots)
                if (slot != null && slot.assignedSkillId != -1) slot.AssignSkill(slot.assignedSkillId, skillDatabase);
        }

        private void Update()
        {
            // Phím 1 → 9 (GameInput: đang gõ chữ thì bỏ qua)
            for (int i = 0; i < skillSlots.Length && i < SlotCount; i++) if (Core.GameInput.SkillSlot(i)) SelectSlotAndUse(i);
        }

        // Hàm đổi vùng chọn sang ô mới và dùng luôn
        public void SelectSlotAndUse(int index)
        {
            if (index < 0 || index >= skillSlots.Length) return;
            if (skillSlots[index].assignedSkillId == -1) return; // Ô trống thì không làm gì
            if (Assets.Script.UI.Kit.UIRoot.GameplayBlocked) return;   // đang mở cửa sổ → không ra chiêu

            int skillId = skillSlots[index].assignedSkillId;
            // 1. Chiêu đánh → thành chiêu đang chọn (J / Enter đánh tiếp bằng chiêu này). Chiêu hỗ trợ / buff (Hồi Phục, Thiết Thể...)
            //    chỉ dùng 1 lần, GIỮ chiêu đánh đang chọn — không thì J / Enter sau đó lại tung chiêu buff đang hồi thay vì đánh
            bool support = Data.GameData.Skills.TryGetValue(skillId, out var tpl) && tpl.type == (int)Constants.SkillType.Support;
            if (!support)
            {
                currentSelectedIndex = index;
                UpdateHighlight();
            }

            // 2. Tung chiêu (SkillCaster tự chọn mục tiêu / kiểm tầm)
            Assets.Script.Combat.SkillCaster.TryCast(skillId);
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
