using System;
using UnityEngine;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// LỚP NỀN PHẦN HUD (thanh EXP + menu, minimap, khung chat, nút điện thoại) — giống GameWindow nhưng không có khung:
    ///   có prefab Resources/UI/Hud/&lt;Lớp&gt; → tạo từ prefab rồi Bind(); chưa có → Build() bằng code rồi Bind().
    ///   Build()  BỐ CỤC vào chính transform này (gốc mặc định phủ kín cha; tự Place lại nếu cần) — tool chạy để xuất prefab
    ///   Bind()   HÀNH VI: nối nút, khởi tạo đồ lúc chạy
    /// </summary>
    public abstract class HudPanel : MonoBehaviour
    {
        public static T Create<T>(Transform parent) where T : HudPanel
        {
            T p;
            var prefab = UIPrefabs.Hud(typeof(T));
            if (prefab != null)
            {
                var go = UIPrefabs.Spawn(prefab, parent);
                go.name = typeof(T).Name;
                p = go.GetComponent<T>();
            }
            else p = (T)CreateByCode(typeof(T), parent);
            p.Bind();
            return p;
        }

        /// <summary>Dựng bằng code (chưa Bind). Tool xuất prefab cũng gọi hàm này.</summary>
        public static HudPanel CreateByCode(Type type, Transform parent)
        {
            var rt = UIKit.Rect(type.Name, parent).Fill();
            var p = (HudPanel)rt.gameObject.AddComponent(type);
            p.Build();
            return p;
        }

        protected abstract void Build();
        protected virtual void Bind() { }
    }
}
