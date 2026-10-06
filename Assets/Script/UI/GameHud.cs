using System.Collections.Generic;
using Assets.Script.Constants;
using Assets.Script.Data;
using Assets.Script.Entities;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.Player;
using Assets.Script.UI.Kit;
using Assets.Script.UI.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Assets.Script.UI
{
    /// <summary>
    /// HUD BỔ SUNG (dựng bằng code, nằm trên HUD cũ của scene):
    ///  - Thanh EXP dưới đáy màn hình + chữ "Cấp N  x%".
    ///  - Nút menu góc phải trên: Nhân vật / Túi / Kỹ năng / Nhiệm vụ (để chơi trên điện thoại).
    ///  - Phím F (hoặc nút "Nói") : nói chuyện với NPC gần nhất.
    ///  - Nhận NPC_MENU → mở hội thoại; SHOP_DATA → mở cửa hàng.
    /// Tạo tự động trong GameplayBootstrap.
    /// </summary>
    public class GameHud : NetworkListener
    {
        private const float TalkRange = 3.5f;

        private GameObject _root;
        private Image _expFill;
        private TextMeshProUGUI _expText;

        protected override void RegisterHandlers()
        {
            Listen(Cmd.NPC_MENU, OnNpcMenu);
            Listen(Cmd.SHOP_DATA, OnShopData);
        }

        protected override void Update()
        {
            base.Update();
            bool inGame = LocalPlayerState.Id >= 0;
            if (inGame && _root == null) BuildHud();
            if (_root != null && _root.activeSelf != inGame) _root.SetActive(inGame);
            if (!inGame) return;

            var kb = Keyboard.current;
            if (kb != null && !Combat.ChatBox.IsTyping && kb.fKey.wasPressedThisFrame) TalkToNearestNpc();
        }

        private void BuildHud()
        {
            var hud = UIRoot.Instance.HudLayer;
            _root = UIKit.Rect("GameHud", hud).Fill().gameObject;

            // ---- Thanh EXP ----
            _expFill = UIKit.Bar("ExpBar", _root.transform, new Color(0.95f, 0.75f, 0.15f));
            ((RectTransform)_expFill.transform.parent).Place(new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), new Vector2(-4, 14), new Vector2(0.5f, 0));
            _expText = UIKit.Text("ExpText", _expFill.transform.parent, "", 12, TextAlignmentOptions.Center);
            _expText.rectTransform.Fill();

            // ---- Nút menu ----
            string[] labels = { "Nhân vật", "Túi", "Kỹ năng", "Nhiệm vụ", "Nói (F)" };
            System.Action[] actions =
            {
                () => GameWindow.Get<CharacterWindow>().Toggle(),
                () => GameWindow.Get<InventoryWindow>().Toggle(),
                () => GameWindow.Get<SkillWindow>().Toggle(),
                () => GameWindow.Get<QuestWindow>().Toggle(),
                TalkToNearestNpc,
            };
            for (int i = 0; i < labels.Length; i++)
            {
                var b = UIKit.Button("Menu" + i, _root.transform, labels[i], actions[i], 15);
                ((RectTransform)b.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-8 - i * 92, -8), new Vector2(86, 34), new Vector2(1, 1));
            }

            // Tạo sẵn cửa sổ để phím tắt C/I/K/L dùng được ngay
            GameWindow.Preload<CharacterWindow>();
            GameWindow.Preload<InventoryWindow>();
            GameWindow.Preload<SkillWindow>();
            GameWindow.Preload<QuestWindow>();

            GameData.OnChanged += k => { if (k == DataKind.Character) UpdateExp(); };
            UpdateExp();
        }

        private void UpdateExp()
        {
            if (_expFill == null) return;
            var c = GameData.Me;
            float pct = c.expToNext > 0 ? Mathf.Clamp01((float)c.exp / c.expToNext) : 0;
            _expFill.fillAmount = pct;
            _expText.text = $"Cấp {c.level}   EXP {pct * 100:F1}%   ({c.exp:N0}/{c.expToNext:N0})" +
                            (c.potential > 0 ? $"   <color=#fd5>+{c.potential} điểm tiềm năng (C)</color>" : "") +
                            (c.skillPoints > 0 ? $"   <color=#7cf>+{c.skillPoints} điểm kỹ năng (K)</color>" : "");
        }

        /// <summary>Nói chuyện với NPC gần nhân vật nhất (trong tầm). Server kiểm tra lại tầm.</summary>
        public static void TalkToNearestNpc()
        {
            var me = NetworkPlayerManager.Instance != null ? NetworkPlayerManager.Instance.localPlayer : null;
            if (me == null) return;
            NpcEntity best = null;
            float bestSqr = TalkRange * TalkRange;
            foreach (var npc in FindObjectsByType<NpcEntity>(FindObjectsSortMode.None))
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

        /// <summary>SHOP_DATA: int npcId, short count, [int itemId, int price] x count</summary>
        private void OnShopData(byte[] data)
        {
            var r = new MessageReader(data);
            int npcId = r.ReadInt();
            int n = r.ReadShort();
            var goods = new List<KeyValuePair<int, int>>();
            for (int i = 0; i < n; i++) goods.Add(new KeyValuePair<int, int>(r.ReadInt(), r.ReadInt()));
            r.Cleanup();
            GameWindow.Get<ShopWindow>().ShowShop(npcId, goods);
        }
    }
}
