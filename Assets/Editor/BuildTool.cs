using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// BUILD GAME CHO PC (Windows) VÀ ANDROID — 1 nút bấm hoặc 1 dòng lệnh.
///
/// Menu:  Tools/Naruto/Build/PC (Windows)   ·   Tools/Naruto/Build/Android (APK)
/// Dòng lệnh (Editor phải đóng):
///   unity run "D:\Game\Naruto" --editor-version 2022.3.62f3 -- -executeMethod BuildTool.BuildWindows -serverHost 1.2.3.4 -logFile build_pc.log
///   unity run "D:\Game\Naruto" --editor-version 2022.3.62f3 -- -executeMethod BuildTool.BuildAndroid -serverHost 1.2.3.4 -logFile build_android.log
///
/// Mỗi lần build: (1) ghi địa chỉ server vào Resources/server_config.json (nếu có -serverHost/-serverPort),
/// (2) build lại Addressables, (3) build game ra thư mục Builds/.
///
/// [CẦN ĐIỀN khi phát hành Android lên Store]
///   - Keystore riêng: Player Settings → Publishing Settings (đừng dùng debug key). Giữ file keystore + mật khẩu cẩn thận.
///   - Đổi AndroidPackageName dưới đây sang tên của bạn (vd com.tencongty.tengame) — đổi sau khi đã lên Store là KHÔNG được.
///   - Đổi tên/biểu tượng game (tên Naruto có bản quyền).
/// </summary>
public static class BuildTool
{
    private const string AndroidPackageName = "com.kien.ninjaonline"; // [CẦN ĐIỀN]
    private const string ServerConfigPath = "Assets/Resources/server_config.json";

    [MenuItem("Tools/Naruto/Build/PC (Windows)", priority = 20)]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/PC/NinjaOnline.exe");

    [MenuItem("Tools/Naruto/Build/Android (APK)", priority = 21)]
    public static void BuildAndroid()
    {
        // Thiết lập Android tối thiểu để chạy được + đúng yêu cầu Google Play (64-bit)
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, AndroidPackageName);
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
        PlayerSettings.Android.forceInternetPermission = true; // game dùng socket TCP
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel22;
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        EditorUserBuildSettings.buildAppBundle = false; // APK để cài thử; lên Store thì bật (AAB)
        Build(BuildTarget.Android, "Builds/Android/NinjaOnline.apk");
    }

    private static void Build(BuildTarget target, string output)
    {
        ApplyServerArgs();

        // Chuyển nền tảng nếu cần (lần đầu sang Android sẽ import lại texture — chậm)
        var group = BuildPipeline.GetBuildTargetGroup(target);
        if (EditorUserBuildSettings.activeBuildTarget != target)
            EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);

        // Addressables phải build cho đúng nền tảng trước khi build game
        AddressableAssetSettings.CleanPlayerContent();
        AddressableAssetSettings.BuildPlayerContent(out var aaResult);
        if (!string.IsNullOrEmpty(aaResult.Error))
        {
            Fail("Build Addressables lỗi: " + aaResult.Error);
            return;
        }

        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = target,
            options = BuildOptions.None,
        });

        var s = report.summary;
        if (s.result == BuildResult.Succeeded)
            Debug.Log($"[Build] ✅ {target} xong: {Path.GetFullPath(output)}  ({s.totalSize / (1024f * 1024f):F1} MB, {s.totalTime.TotalSeconds:F0}s)");
        else
            Fail($"{target} thất bại: {s.result}, {s.totalErrors} lỗi");
    }

    /// <summary>Đọc -serverHost / -serverPort / -tls / -noTls / -certSha256 từ dòng lệnh, ghi vào Resources/server_config.json.</summary>
    private static void ApplyServerArgs()
    {
        string host = Arg("-serverHost"), port = Arg("-serverPort"), pin = Arg("-certSha256");
        bool tlsFlag = Array.IndexOf(Environment.GetCommandLineArgs(), "-tls") >= 0, noTls = Array.IndexOf(Environment.GetCommandLineArgs(), "-noTls") >= 0;
        if (host == null && port == null && pin == null && !tlsFlag && !noTls) return;
        var json = File.Exists(ServerConfigPath) ? File.ReadAllText(ServerConfigPath) : "{\"host\":\"127.0.0.1\",\"port\":14444}";
        var cfg = JsonUtility.FromJson<Cfg>(json);
        if (host != null) cfg.host = host;
        if (port != null) cfg.port = int.Parse(port);
        if (tlsFlag) cfg.tls = true;
        if (noTls) cfg.tls = false;
        if (pin != null) cfg.certSha256 = pin;
        File.WriteAllText(ServerConfigPath, JsonUtility.ToJson(cfg, true));
        AssetDatabase.ImportAsset(ServerConfigPath);
        Debug.Log($"[Build] server_config = {cfg.host}:{cfg.port}{(cfg.tls ? " TLS" : "")}");
    }

    [Serializable] private class Cfg { public string host; public int port; public bool tls; public string certSha256 = ""; }

    private static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
    }

    private static void Fail(string msg)
    {
        Debug.LogError("[Build] ❌ " + msg);
        if (Application.isBatchMode) EditorApplication.Exit(1);
    }
}
