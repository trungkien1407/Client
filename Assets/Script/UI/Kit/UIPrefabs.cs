using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Assets.Script.UI.Kit
{
    /// <summary>
    /// NƠI LẤY PREFAB UI (thư mục Assets/Resources/UI/):
    ///   UI/UIRoot          canvas chung (lớp HUD + lớp cửa sổ)
    ///   UI/Kit/&lt;Tên&gt;      prefab mẫu: Window, Panel, Text, Button, Input, ScrollList, Bar
    ///   UI/Windows/&lt;Lớp&gt;  cửa sổ (vd UI/Windows/InventoryWindow)
    ///   UI/Hud/&lt;Lớp&gt;      phần HUD (GameHudView, MinimapHud, ChatOverlay, MobileControlsView)
    /// Không có prefab → dựng bằng code (giao diện cũ), game vẫn chạy.
    /// Tạo / cập nhật prefab: menu Tools → Naruto → UI (Assets/Editor/UIPrefabTool.cs).
    /// </summary>
    public static class UIPrefabs
    {
        public const string RootPath = "UI/UIRoot";
        public const string ThemePath = "UI/UITheme";
        public const string KitFolder = "UI/Kit/";
        public const string WindowFolder = "UI/Windows/";
        public const string HudFolder = "UI/Hud/";
        public static readonly string[] KitNames = { "Panel", "Text", "Button", "Input", "ScrollList", "Bar", "Window" };

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        /// <summary>Tắt khi tool đang dựng chính các prefab mẫu (để không lồng mẫu vào chính nó).</summary>
        public static bool UseKit = true;

        /// <summary>Tool Editor gán = PrefabUtility.InstantiatePrefab để prefab cửa sổ giữ liên kết với prefab mẫu.</summary>
        public static Func<GameObject, Transform, GameObject> InstantiateHook;

        public static GameObject Kit(string name) => UseKit ? Load(KitFolder + name) : null;
        public static GameObject Window(Type windowType) => Load(WindowFolder + windowType.Name);
        public static GameObject Hud(Type panelType) => Load(HudFolder + panelType.Name);

        public static GameObject Spawn(GameObject prefab, Transform parent)
        {
            if (InstantiateHook != null) return InstantiateHook(prefab, parent);
            return Object.Instantiate(prefab, parent, false);
        }

        /// <summary>Tool gọi sau khi tạo / xoá prefab.</summary>
        public static void ClearCache() => Cache.Clear();

        private static GameObject Load(string path)
        {
            if (!Cache.TryGetValue(path, out var p))
            {
                p = Resources.Load<GameObject>(path);
                Cache[path] = p;
            }
            return p;
        }
    }
}
