using Assets.Script.Data;
using Assets.Script.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Script.Combat
{
    /// <summary>
    /// UỐNG BÌNH NHANH — phím Q (máu) / E (chakra) và 2 icon bình "HP" / "MP" trên HUD scene (trước 2026-10-22 chỉ là hình, bấm không ăn;
    /// phím Q / E chỉ dùng được Bình Nhỏ id 1 / 2 → mang Bình Vừa / Lớn / Cực Đại thì phím không làm gì).
    /// Tự chọn bình trong túi:
    ///   1. bình hồi NGAY (type 0 máu / 1 chakra) đủ cấp dùng: bình NHỎ NHẤT mà đủ bù phần đang thiếu; không bình nào đủ → bình TO NHẤT;
    ///   2. hết bình hồi ngay → đan hồi DẦN (type 2 / 3);
    ///   3. không có gì → báo trong khung chat.
    /// Server vẫn tự kiểm cấp, hồi chiêu, số lượng (ItemService.useItem) — ở đây chỉ chọn món.
    /// </summary>
    public static class QuickPotion
    {
        /// <summary>Mẫu bình nên dùng cho máu ({hp} = true) hoặc chakra; -1 = túi không có bình loại đó.</summary>
        public static int Pick(bool hp)
        {
            int instant = hp ? 0 : 1, overTime = hp ? 2 : 3;
            int missing = hp ? LocalPlayerState.MaxHp - LocalPlayerState.Hp : LocalPlayerState.MaxMp - LocalPlayerState.Mp;
            int bestEnough = -1, enoughAmt = int.MaxValue, biggest = -1, bigAmt = -1, slow = -1;
            foreach (var s in GameData.Inventory)
            {
                if (s.qty <= 0 || !GameData.Items.TryGetValue(s.tpl, out var t) || t.levelRequire > GameData.Me.level) continue;
                if (t.type == overTime) { if (slow < 0) slow = t.id; continue; }
                if (t.type != instant) continue;
                int amt = hp ? t.hpRestore : t.mpRestore;
                if (amt >= missing && amt < enoughAmt) { bestEnough = t.id; enoughAmt = amt; }
                if (amt > bigAmt) { biggest = t.id; bigAmt = amt; }
            }
            return bestEnough >= 0 ? bestEnough : biggest >= 0 ? biggest : slow;
        }

        /// <summary>Uống bình máu / chakra hợp nhất.</summary>
        public static void Use(bool hp)
        {
            if (LocalPlayerState.IsDead) return;
            int id = Pick(hp);
            if (id < 0) { ChatBox.AddSystem(hp ? "Túi không còn bình máu." : "Túi không còn bình chakra."); return; }
            GameActions.UseItem(id);
        }

        /// <summary>Tổng số bình máu (type 0, 2) / chakra (type 1, 3) trong túi — hiện trên icon HUD.</summary>
        public static int Count(bool hp)
        {
            int n = 0;
            foreach (var s in GameData.Inventory)
                if (GameData.Items.TryGetValue(s.tpl, out var t) && (hp ? t.type == 0 || t.type == 2 : t.type == 1 || t.type == 3)) n += s.qty;
            return n;
        }

        /// <summary>
        /// Biến hình "HP" / "MP" của HUD scene thành nút uống bình + số lượng ở góc. Trả false nếu chưa tìm thấy (HUD bật sau đăng nhập).
        /// </summary>
        public static bool HookHudIcons()
        {
            var hpGo = GameObject.Find("HUDCanvas/HP") ?? FindUnder("HP");
            var mpGo = GameObject.Find("HUDCanvas/MP") ?? FindUnder("MP");
            if (hpGo == null || mpGo == null) return false;
            Hook(hpGo, true);
            Hook(mpGo, false);
            return true;
        }

        private static GameObject FindUnder(string name)
        {
            var hud = GameObject.Find("HUDCanvas");
            if (hud == null) return null;
            foreach (var t in hud.GetComponentsInChildren<Transform>(true)) if (t.name == name && t.GetComponent<Image>() != null) return t.gameObject;
            return null;
        }

        private static void Hook(GameObject go, bool hp)
        {
            if (go.GetComponent<Button>() != null) return;   // đã nối
            var img = go.GetComponent<Image>();
            if (img != null) img.raycastTarget = true;
            go.AddComponent<Button>().onClick.AddListener(() => Use(hp));
            var label = new GameObject("Count", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(go.transform, false);
            var rt = label.rectTransform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-2f, 2f);
            rt.sizeDelta = new Vector2(60f, 24f);
            label.alignment = TextAlignmentOptions.BottomRight;
            label.fontSize = 18;
            label.fontStyle = FontStyles.Bold;
            label.outlineWidth = 0.25f;
            label.raycastTarget = false;
            go.AddComponent<QuickPotionCount>().Init(label, hp);
        }
    }

    /// <summary>Số bình còn lại trên icon HUD — vẽ lại khi túi đổi.</summary>
    public class QuickPotionCount : MonoBehaviour
    {
        private TextMeshProUGUI _label;
        private bool _hp;

        public void Init(TextMeshProUGUI label, bool hp)
        {
            _label = label;
            _hp = hp;
            GameData.OnChanged += OnData;
            Refresh();
        }

        private void OnData(DataKind k) { if (k == DataKind.Inventory) Refresh(); }

        private void Refresh()
        {
            if (_label == null) return;
            int n = QuickPotion.Count(_hp);
            _label.text = n > 0 ? n.ToString() : "<color=#f66>0</color>";
        }

        private void OnDestroy() => GameData.OnChanged -= OnData;
    }
}
