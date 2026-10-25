using System.IO;
using Assets.Script.Manager;
using Assets.Script.Models;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MÀN ĐĂNG NHẬP (Assets/Prefabs/AuthCanvas.prefab) — dàn lại bố cục bằng code, rồi bạn chỉnh tay tiếp trong prefab.
///
/// Menu:  Tools/Naruto/UI/4. Dàn lại màn đăng nhập   — sửa prefab: đăng nhập · đăng ký · "Tiếp tục" · tạo nhân vật
///        Tools/Naruto/UI/5. Chụp thử màn đăng nhập   — xuất ảnh PNG từng khung ở 3 tỉ lệ màn hình (16:9, 20:9, 4:3) ra Builds/UIPreview/Auth
/// Dòng lệnh (Editor phải đóng):
///   Unity.exe -batchmode -quit -projectPath D:\Game\Naruto -executeMethod AuthScreenTool.Apply -logFile auth.log
///   Unity.exe -batchmode -quit -projectPath D:\Game\Naruto -executeMethod AuthScreenTool.Preview -logFile auth.log
///
/// Chạy lại nhiều lần không sao: object nào đã có thì dùng lại (tìm theo tên), KHÔNG tạo trùng.
/// Lưu ý: Apply GHI ĐÈ vị trí / cỡ / chữ của các object nó quản lý — chỉnh tay xong thì đừng chạy lại (hoặc sửa số ở đây).
/// Toạ độ theo màn chuẩn 1920×1080 (CanvasScaler), gốc (0,0) = giữa khung.
/// </summary>
public static class AuthScreenTool
{
    private const string PrefabPath = "Assets/Prefabs/AuthCanvas.prefab";
    private const string SpriteDir = "Assets/Sprite/UI/";

    // Màu chữ (khung nền sáng → chữ tối)
    private static readonly Color Ink = new Color(0.24f, 0.16f, 0.22f);          // chữ chính: tím nâu đậm
    private static readonly Color InkDim = new Color(0.45f, 0.38f, 0.42f, 0.85f); // gợi ý trong ô trống
    private static readonly Color BtnInk = new Color(0.08f, 0.18f, 0.20f);        // chữ trên nút xanh

    private static TMP_FontAsset _font;

    // =====================================================================
    // 1. DÀN LẠI PREFAB
    // =====================================================================
    [MenuItem("Tools/Naruto/UI/4. Dàn lại màn đăng nhập", priority = 23)]
    public static void Apply()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var auth = root.transform.Find("Auth");
            _font = root.GetComponentInChildren<TextMeshProUGUI>(true).font;   // giữ font đang dùng (có bảng chữ tiếng Việt dự phòng)

            // Cân giữa ngang/dọc (trước = 0: bám chiều ngang → máy 4:3 khung bé tí)
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            StyleBackground(auth);
            var mgr = root.GetComponent<GameAuthManager>();
            StyleLogin(auth.Find("LoginPanel"));
            var confirm = StyleRegister(auth.Find("RegisterPanel"));
            StyleQuick(auth.Find("QuickLoginPanel"));
            var (createBtn, backBtn) = StyleCreate(auth.Find("CreatcharPanel"));
            var version = StyleVersion(auth);

            // Nối tham chiếu mới vào script (prefab trước đây thiếu nút Tạo → màn tạo lại sau mất mạng bấm Tạo không được)
            var so = new SerializedObject(mgr);
            so.FindProperty("inputRegConfirm").objectReferenceValue = confirm;
            so.FindProperty("btnBackFromCreate").objectReferenceValue = backBtn;
            so.FindProperty("txtVersion").objectReferenceValue = version;
            so.ApplyModifiedPropertiesWithoutUndo();
            var ccm = auth.GetComponentInChildren<CharacterCreationManager>(true);
            var so2 = new SerializedObject(ccm);
            so2.FindProperty("btnCreate").objectReferenceValue = createBtn;
            so2.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log("[AuthScreenTool] Đã dàn lại " + PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>Ảnh nền phủ kín mọi tỉ lệ màn hình (cắt bớt mép, không méo) + lớp tối nhẹ cho khung nổi lên.</summary>
    private static void StyleBackground(Transform auth)
    {
        // "Auth" là Transform thường (không phải RectTransform) → không neo giãn được; CanvasCover tự tính cỡ theo canvas
        var bg = (RectTransform)auth.Find("BG");
        Place(bg, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 1080));
        var oldFit = bg.GetComponent<AspectRatioFitter>();
        if (oldFit != null) Object.DestroyImmediate(oldFit, true);
        Cover(bg);
        bg.SetSiblingIndex(0);

        var dim = Img(auth, "Dim", null, new Color(0.05f, 0.03f, 0.10f, 0.30f));
        Place(dim.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1920, 1080));
        Cover(dim.rectTransform);
        dim.raycastTarget = false;
        dim.transform.SetSiblingIndex(1);
    }

    private static void Cover(RectTransform rt)
    {
        var c = rt.GetComponent<Assets.Script.UI.Kit.CanvasCover>();
        if (c == null) c = rt.gameObject.AddComponent<Assets.Script.UI.Kit.CanvasCover>();
        c.artSize = new Vector2(1920, 1080);
    }

    /// <summary>Chữ phiên bản: con của canvas gốc (có RectTransform) để neo được góc dưới phải.</summary>
    private static TextMeshProUGUI StyleVersion(Transform auth)
    {
        var stray = auth.Find("TxtVersion");   // bản chạy trước đặt nhầm dưới Auth
        if (stray != null) Object.DestroyImmediate(stray.gameObject, true);
        var t = Txt(auth.parent, "TxtVersion", "Phiên bản", 20, new Color(1, 1, 1, 0.75f), TextAlignmentOptions.BottomRight);
        Place(t.rectTransform, new Vector2(1, 0), new Vector2(-24, 14), new Vector2(400, 30), new Vector2(1, 0));
        t.transform.SetAsLastSibling();
        return t;
    }

    // ---------------- Đăng nhập ----------------
    private static void StyleLogin(Transform p)
    {
        Frame(p, new Vector2(560, 560), "ĐĂNG NHẬP");
        Field(p, "TxtUsername", "LblUsername", "Tài khoản", "Nhập tên tài khoản", 120, 32, TMP_InputField.ContentType.Standard);
        Field(p, "TxtPassword", "LblPassword", "Mật khẩu", "Nhập mật khẩu", 22, 64, TMP_InputField.ContentType.Password);
        // (nút "Ghi nhớ đăng nhập" do GameAuthManager tạo lúc chạy, nằm ngay dưới ô mật khẩu)
        Btn(p.Find("ButtonRegister"), "Button_3", "Đăng ký", new Vector2(-105, -150), new Vector2(190, 72), 28);
        Btn(p.Find("ButtonLogin"), "Button_4", "Đăng nhập", new Vector2(105, -150), new Vector2(190, 72), 28);
    }

    // ---------------- Đăng ký ----------------
    private static TMP_InputField StyleRegister(Transform p)
    {
        Frame(p, new Vector2(560, 680), "ĐĂNG KÝ");
        Field(p, "TxtUsername", "LblUsername", "Tài khoản", "4–32 ký tự: chữ không dấu, số, _", 188, 32, TMP_InputField.ContentType.Standard);
        Field(p, "TxtPassword", "LblPassword", "Mật khẩu", "6–64 ký tự", 100, 64, TMP_InputField.ContentType.Password);
        var confirm = Field(p, "TxtConfirm", "LblConfirm", "Nhập lại mật khẩu", "Gõ lại mật khẩu ở trên", 12, 64, TMP_InputField.ContentType.Password);
        Field(p, "TxtEmail", "LblEmail", "Email", "Dùng khi cần lấy lại tài khoản", -76, 100, TMP_InputField.ContentType.EmailAddress);
        Btn(p.Find("ButtonRegister"), "Button_4", "Tạo tài khoản", new Vector2(0, -175), new Vector2(280, 72), 28);
        CloseBtn(p.Find("ButtonClose"));
        return confirm;
    }

    // ---------------- "Tiếp tục" (đã lưu tài khoản) ----------------
    private static void StyleQuick(Transform p)
    {
        // Trước đây khung này neo giãn lệch (-8,-233) và trong suốt → nút trôi giữa ảnh nền. Giờ thành khung như các khung khác.
        var img = p.GetComponent<Image>();
        img.sprite = Spr("BG"); img.type = Image.Type.Sliced; img.color = Color.white;
        Frame(p, new Vector2(520, 360), "CHÀO MỪNG TRỞ LẠI");
        Btn(p.Find("ButtonQuick"), "Button_4", null, new Vector2(0, 30), new Vector2(380, 84), 30);
        Btn(p.Find("ButtonLogin"), "Button_3", "Đổi tài khoản", new Vector2(0, -78), new Vector2(380, 64), 26);
    }

    // ---------------- Tạo nhân vật ----------------
    private static (Button create, Button back) StyleCreate(Transform p)
    {
        var rt = (RectTransform)p;
        Place(rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 680));
        var frame = (RectTransform)p.Find("ImgBg");
        Stretch(frame, -6);
        Banner(p, "TẠO NHÂN VẬT");

        // Cột trái: khung nhân vật + ô tên
        var ch = (RectTransform)p.Find("Character");
        Place(ch, new Vector2(0.5f, 0.5f), new Vector2(-270, 45), new Vector2(320, 390));
        var holder = (RectTransform)ch.Find("CharacterContainer");
        Place(holder, new Vector2(0.5f, 0f), new Vector2(0, 48), new Vector2(100, 100));   // gốc hình Spine ở chân → đặt sát đáy khung
        holder.localScale = Vector3.one * 2.4f;   // hình nhân vật (Spine UI ~100px) phóng to cho vừa khung
        Field(p, "TxtName", "LblName", "Tên nhân vật", "3–15 ký tự", -207, 15, TMP_InputField.ContentType.Standard,
            x: -270, width: 320);
        var nameImg = p.Find("TxtName").GetComponent<Image>();
        nameImg.sprite = Spr("regbt");

        // Cột phải: chọn hệ · mô tả · nút Tạo
        var pick = Txt(p, "LblClass", "Chọn hệ phái", 24, Ink, TextAlignmentOptions.MidlineLeft);
        pick.fontStyle = FontStyles.Bold;
        Place(pick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(165, 222), new Vector2(500, 34));

        var row = (RectTransform)p.Find("ButtonContainer");
        Place(row, new Vector2(0.5f, 0.5f), new Vector2(165, 170), new Vector2(500, 64));
        var h = row.GetComponent<HorizontalLayoutGroup>();
        h.spacing = 12; h.childAlignment = TextAnchor.MiddleCenter;
        h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = true;

        var desc = (RectTransform)p.Find("Mota");
        Place(desc, new Vector2(0.5f, 0.5f), new Vector2(165, -12), new Vector2(500, 270));
        var descImg = desc.GetComponent<Image>();
        descImg.color = new Color(1, 1, 1, 0.9f);
        var descTxt = desc.GetComponentInChildren<TextMeshProUGUI>(true);
        Stretch(descTxt.rectTransform, 18);
        descTxt.font = _font; descTxt.fontSize = 24; descTxt.color = Ink;
        descTxt.alignment = TextAlignmentOptions.TopLeft; descTxt.enableWordWrapping = true;
        descTxt.enableAutoSizing = true; descTxt.fontSizeMin = 18; descTxt.fontSizeMax = 24;   // mô tả dài thì tự nhỏ chữ, không tràn khung
        descTxt.text = "";

        var create = p.Find("ButtonCreate").GetComponent<Button>();
        Btn(create.transform, "Button_4", "Tạo nhân vật", new Vector2(285, -210), new Vector2(240, 72), 28);
        var back = p.Find("ButtonClose").GetComponent<Button>();
        CloseBtn(back.transform);
        return (create, back);
    }

    // =====================================================================
    // KHỐI DÙNG CHUNG
    // =====================================================================

    /// <summary>Khung: nền trắng bo góc (ảnh BG) + viền tím (con "Image") + dải tiêu đề nằm vắt mép trên.</summary>
    private static void Frame(Transform p, Vector2 size, string title)
    {
        Place((RectTransform)p, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        var border = p.Find("Image") ?? p.Find("Frame");
        if (border == null)
        {
            var b = Img(p, "Frame", Spr("bagBG_0"), Color.white);
            b.type = Image.Type.Sliced; b.raycastTarget = false;
            border = b.transform;
        }
        border.SetSiblingIndex(0);
        Stretch((RectTransform)border, -6);
        Banner(p, title);
    }

    /// <summary>Dải tiêu đề (sprTabTitle0) giữa mép trên khung, chữ trắng đậm.</summary>
    private static void Banner(Transform p, string title)
    {
        var old = p.Find("Title");   // khung tạo nhân vật có sẵn Title/TxtTitle
        Image img;
        if (old != null) img = old.GetComponent<Image>();
        else img = Img(p, "Title", Spr("sprTabTitle0"), Color.white);
        img.sprite = Spr("sprTabTitle0"); img.type = Image.Type.Simple; img.raycastTarget = false;
        Place(img.rectTransform, new Vector2(0.5f, 1f), new Vector2(0, 2), new Vector2(440, 72));
        var t = Txt(img.transform, "TxtTitle", title, 32, Color.white, TextAlignmentOptions.Center);
        t.text = title; t.font = _font; t.fontSize = 32; t.fontStyle = FontStyles.Bold; t.color = Color.white;
        t.alignment = TextAlignmentOptions.Center; t.raycastTarget = false;
        t.enableAutoSizing = true; t.fontSizeMin = 20; t.fontSizeMax = 32;   // tiêu đề dài tự nhỏ lại, không tràn dải
        Stretch(t.rectTransform, 0);
        t.rectTransform.offsetMin = new Vector2(72, 8);    // chừa phần hoa văn 2 bên + bóng dưới
        t.rectTransform.offsetMax = new Vector2(-72, -2);
        img.transform.SetAsLastSibling();
    }

    /// <summary>Ô nhập 420×62 giữa khung (hoặc theo x/width), nhãn nhỏ ngay phía trên, chữ gợi ý mờ, giới hạn ký tự.</summary>
    private static TMP_InputField Field(Transform p, string name, string labelName, string label, string hint, float y,
        int limit, TMP_InputField.ContentType type, float x = 0, float width = 420)
    {
        var input = p.Find(name).GetComponent<TMP_InputField>();
        var rt = (RectTransform)input.transform;
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(width, 62));
        var img = input.GetComponent<Image>();
        img.sprite = Spr("regbt"); img.type = Image.Type.Sliced; img.color = Color.white;
        input.contentType = type;
        input.characterLimit = limit;
        var area = (RectTransform)input.textViewport;
        area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
        area.offsetMin = new Vector2(16, 6); area.offsetMax = new Vector2(-16, -6);
        if (input.textComponent is TextMeshProUGUI tc)
        {
            tc.font = _font; tc.fontSize = 24; tc.color = Ink; tc.alignment = TextAlignmentOptions.MidlineLeft;
        }
        if (input.placeholder is TextMeshProUGUI ph)
        {
            ph.text = hint; ph.font = _font; ph.fontSize = 21; ph.fontStyle = FontStyles.Italic; ph.color = InkDim;
            ph.alignment = TextAlignmentOptions.MidlineLeft; ph.enableWordWrapping = false;
            ph.overflowMode = TextOverflowModes.Ellipsis;
        }
        input.pointSize = 24;

        var lbl = Txt(p, labelName, label, 22, Ink, TextAlignmentOptions.BottomLeft);
        lbl.fontStyle = FontStyles.Bold;
        Place(lbl.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y + 46), new Vector2(width, 30));
        return input;
    }

    /// <summary>Nút có nền sprite (Sliced) + chữ đậm. label null = giữ chữ (vd "Tiếp tục: tên" do code điền).</summary>
    private static void Btn(Transform b, string sprite, string label, Vector2 pos, Vector2 size, float fontSize)
    {
        Place((RectTransform)b, new Vector2(0.5f, 0.5f), pos, size);
        var img = b.GetComponent<Image>();
        img.sprite = Spr(sprite); img.type = Image.Type.Sliced; img.color = Color.white;
        var t = b.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t == null) return;
        if (label != null) t.text = label;
        t.font = _font; t.fontSize = fontSize; t.fontStyle = FontStyles.Bold; t.color = BtnInk;
        t.alignment = TextAlignmentOptions.Center; t.enableWordWrapping = false;
        t.enableAutoSizing = true; t.fontSizeMin = 18; t.fontSizeMax = fontSize;   // chữ dài (tên tài khoản) tự nhỏ lại
        t.raycastTarget = false;
        Stretch(t.rectTransform, 0);
        t.rectTransform.offsetMin = new Vector2(12, 4); t.rectTransform.offsetMax = new Vector2(-12, -4);
    }

    /// <summary>Nút X: góc trên phải, đè lên viền.</summary>
    private static void CloseBtn(Transform b)
    {
        Place((RectTransform)b, new Vector2(1, 1), new Vector2(-14, -14), new Vector2(64, 64));
        var t = b.GetComponentInChildren<TextMeshProUGUI>(true);
        if (t != null) t.text = "";
        b.SetAsLastSibling();
    }

    private static Image Img(Transform parent, string name, Sprite sprite, Color color)
    {
        var tr = parent.Find(name);
        if (tr == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            tr = go.transform;
        }
        var img = tr.GetComponent<Image>();
        img.sprite = sprite; img.color = color;
        return img;
    }

    private static TextMeshProUGUI Txt(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align)
    {
        var tr = parent.Find(name);
        if (tr == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            tr = go.transform;
        }
        var t = tr.GetComponent<TextMeshProUGUI>();
        t.font = _font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.enableWordWrapping = false; t.raycastTarget = false;
        return t;
    }

    private static Sprite Spr(string name)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + name + ".png");
        if (s == null) Debug.LogWarning("[AuthScreenTool] Không thấy hình " + SpriteDir + name + ".png");
        return s;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    /// <summary>Giãn kín cha, chừa lề {pad} (âm = tràn ra ngoài).</summary>
    private static void Stretch(RectTransform rt, float pad)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
    }

    /// <summary>Dòng lệnh: dàn lại rồi chụp thử luôn (1 lần mở Unity).</summary>
    public static void ApplyAndPreview() { Apply(); Preview(); }

    // =====================================================================
    // 2. CHỤP THỬ (không cần Play, không cần server)
    // =====================================================================
    [MenuItem("Tools/Naruto/UI/5. Chụp thử màn đăng nhập", priority = 24)]
    public static void Preview()
    {
        string outDir = Path.GetFullPath("Builds/UIPreview/Auth");
        Directory.CreateDirectory(outDir);
        // Mở scene trống để chụp, xong mở lại scene đang làm (hỏi lưu nếu có thay đổi chưa lưu)
        string prevScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        try
        {
            var camGo = new GameObject("PreviewCam");
            EditorSceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.transform.position = new Vector3(0, 0, -10);

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            var canvas = inst.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cam; canvas.planeDistance = 5;
            var auth = inst.transform.Find("Auth");
            FillCreatePreview(auth.Find("CreatcharPanel"));
            var quick = auth.Find("QuickLoginPanel/ButtonQuick").GetComponentInChildren<TextMeshProUGUI>(true);
            if (quick != null) quick.text = "Tiếp tục: clientbot";
            var verT = inst.transform.Find("TxtVersion");
            var ver = verT != null ? verT.GetComponent<TextMeshProUGUI>() : null;
            if (ver != null) ver.text = "Phiên bản " + Application.version;

            string[] panels = { "LoginPanel", "RegisterPanel", "QuickLoginPanel", "CreatcharPanel" };
            (int w, int h, string tag)[] sizes = { (1920, 1080, "16x9"), (2400, 1080, "20x9"), (1440, 1080, "4x3") };
            foreach (var (w, h, tag) in sizes)
            {
                var rtex = new RenderTexture(w, h, 24);
                cam.targetTexture = rtex;
                // batchmode không có vòng Update → tự kích CanvasScaler + AspectRatioFitter tính lại theo cỡ mới
                var scaler = inst.GetComponent<CanvasScaler>();
                scaler.enabled = false; scaler.enabled = true;
                Canvas.ForceUpdateCanvases();
                foreach (var cov in inst.GetComponentsInChildren<Assets.Script.UI.Kit.CanvasCover>(true)) cov.Fit();
                foreach (var name in panels)
                {
                    foreach (var other in panels) auth.Find(other).gameObject.SetActive(other == name);
                    Canvas.ForceUpdateCanvases();
                    foreach (var lg in inst.GetComponentsInChildren<LayoutGroup>()) LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)lg.transform);
                    Canvas.ForceUpdateCanvases();
                    cam.Render();
                    RenderTexture.active = rtex;
                    var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, $"{name}_{tag}.png"), tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                }
                cam.targetTexture = null; RenderTexture.active = null;
                Object.DestroyImmediate(rtex);
            }
            Debug.Log("[AuthScreenTool] Ảnh chụp thử: " + outDir);
        }
        finally
        {
            if (!string.IsNullOrEmpty(prevScene)) EditorSceneManager.OpenScene(prevScene);
            else EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        }
    }

    /// <summary>Lúc chạy thật, nút hệ phái + hình nhân vật do CharacterCreationManager tải qua Addressables. Chụp thử: dựng tay.</summary>
    private static void FillCreatePreview(Transform p)
    {
        var ccm = p.GetComponent<CharacterCreationManager>();
        var guids = AssetDatabase.FindAssets("t:NinjaClassData");
        var list = new System.Collections.Generic.List<NinjaClassData>();
        foreach (var g in guids) list.Add(AssetDatabase.LoadAssetAtPath<NinjaClassData>(AssetDatabase.GUIDToAssetPath(g)));
        list.Sort((a, b) => a.classId.CompareTo(b.classId));
        for (int i = 0; i < list.Count; i++)
        {
            var btn = (GameObject)PrefabUtility.InstantiatePrefab(ccm.classButtonPrefab, ccm.buttonContainer);
            var t = btn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (t != null)
            {
                t.text = list[i].className;
                t.fontStyle = i == 0 ? FontStyles.Bold : FontStyles.Normal;
                t.color = i == 0 ? new Color(0.55f, 0.20f, 0.10f) : new Color(0.25f, 0.20f, 0.30f);
            }
            btn.GetComponent<Image>().sprite = i == 0 ? ccm.selectedSprite : ccm.normalSprite;
        }
        if (list.Count > 0)
        {
            ccm.textClassDescription.text = $"<size=125%><b>{list[0].className}</b></size>\n<color=#A0461E>{list[0].element}</color>\n\n{list[0].description}";
            var spine = list[0].spinePrefab.editorAsset as GameObject;
            if (spine != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(spine, ccm.spineContainer);
                var sg = go.GetComponent<Spine.Unity.SkeletonGraphic>();
                if (sg != null) { sg.Initialize(true); sg.Update(0); sg.LateUpdate(); }
            }
        }
    }
}
