using UnityEngine;
using System;
using System.Collections;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Assets.Script.Manager;
using Assets.Script.Network;
using Assets.Script.Constants;

public class AppFlowManager : MonoBehaviour
{
    public static AppFlowManager Instance;

    // [MỚI] Biến tĩnh để ghi nhớ Game đã khởi động xong Addressables chưa
    private static bool _hasAppBooted = false;

    private bool _isServerConnected = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject); // Chống trùng lặp nếu có 2 cái trên Scene
    }

    void Start()
    {
        // [MỚI] Nếu game đã chạy qua luồng kiểm tra tài nguyên lần đầu rồi
        if (_hasAppBooted)
        {
            // Chỉ dọn popup "đang chờ..." còn sót và KHÔNG chạy luồng StartupFlow nữa.
            // Popup CÓ nút Đóng (VD "Mất kết nối...", "Máy chủ đang bảo trì...") vừa được GameDisconnectHandler hiện
            // ngay trước khi màn này được tạo → giữ lại cho người chơi đọc (trước đây bị ẩn mất ngay).
            if (PopupAndLoad.Instance != null && PopupAndLoad.Instance.IsWaitingPopup) PopupAndLoad.Instance.HidePopup();

            // Nếu đang mất kết nối, hệ thống GameDisconnectHandler/NetworkManager 
            // sẽ tự lo việc hiển thị thông báo. Ta chỉ cần dừng script này lại.
            return;
        }

        // Đánh dấu là đã boot game
        _hasAppBooted = true;
        StartCoroutine(StartupFlow());
    }

    IEnumerator StartupFlow()
    {
        // ==========================================
        // 1. KHỞI TẠO VÀ CẬP NHẬT ADDRESSABLES 
        // ==========================================
        PopupAndLoad.Instance.ShowPopup("Đang kiểm tra tài nguyên...", null, false);

        var initOp = Addressables.InitializeAsync();
        yield return initOp;

        // Kiểm tra xem có bản cập nhật Catalog nào trên Server/CDN không
        var checkUpdateOp = Addressables.CheckForCatalogUpdates(false);
        yield return checkUpdateOp;

        if (checkUpdateOp.Status == AsyncOperationStatus.Succeeded && checkUpdateOp.Result.Count > 0)
        {
            // Tải Catalog mới về
            PopupAndLoad.Instance.ShowPopup("Đang lấy thông tin bản cập nhật...", null, false);
            var updateCatalogOp = Addressables.UpdateCatalogs(checkUpdateOp.Result, false);
            yield return updateCatalogOp;

            // Tính toán tổng dung lượng MB cần tải
            var getSizeOp = Addressables.GetDownloadSizeAsync(updateCatalogOp.Result);
            yield return getSizeOp;

            long totalDownloadSize = getSizeOp.Result;
            if (totalDownloadSize > 0)
            {
                string sizeText = (totalDownloadSize / (1024f * 1024f)).ToString("F2") + " MB";

                // --- BẮT ĐẦU VÒNG LẶP TẢI & THỬ LẠI NẾU RỚT MẠNG ---
                bool isDownloadSuccess = false;
                int retryCount = 0;
                int maxRetry = 3;

                while (!isDownloadSuccess && retryCount < maxRetry)
                {
                    PopupAndLoad.Instance.ShowPopup($"Bắt đầu tải dữ liệu mới ({sizeText})...", null, false);
                    var downloadOp = Addressables.DownloadDependenciesAsync(updateCatalogOp.Result, false);

                    // Biến dùng để tính tốc độ mạng
                    float startTime = Time.time;
                    long lastDownloadedBytes = 0;

                    while (!downloadOp.IsDone)
                    {
                        // Cập nhật giao diện tốc độ mỗi 0.5 giây để tránh làm giật UI
                        if (Time.time - startTime >= 0.5f)
                        {
                            long currentBytes = downloadOp.GetDownloadStatus().DownloadedBytes;
                            long bytesPerSecond = (currentBytes - lastDownloadedBytes) * 2; // Tốc độ 1 giây
                            string speedText = (bytesPerSecond / (1024f * 1024f)).ToString("F1") + " MB/s";

                            float percent = downloadOp.PercentComplete * 100f;

                            // Hiển thị: Đang tải: 45% (2.5 MB/s)
                            PopupAndLoad.Instance.ShowPopup($"Đang cập nhật: {percent:F0}% ({speedText})", null, false);

                            lastDownloadedBytes = currentBytes;
                            startTime = Time.time;
                        }
                        yield return null;
                    }

                    // Kiểm tra kết quả tải
                    if (downloadOp.Status == AsyncOperationStatus.Succeeded)
                    {
                        isDownloadSuccess = true;
                        Addressables.Release(downloadOp); // Giải phóng RAM
                    }
                    else
                    {
                        retryCount++;
                        Addressables.Release(downloadOp); // Hủy tiến trình lỗi

                        if (retryCount < maxRetry)
                        {
                            PopupAndLoad.Instance.ShowPopup($"Mạng không ổn định! Đang thử lại lần {retryCount}/{maxRetry}...");
                            yield return new WaitForSeconds(2f); // Nghỉ 2 giây trước khi thử tải lại
                        }
                        else
                        {
                            PopupAndLoad.Instance.ShowPopup("Lỗi đường truyền! Vui lòng kiểm tra WiFi/4G và mở lại game.");

                            // Dọn dẹp handle trước khi break
                            Addressables.Release(getSizeOp);
                            Addressables.Release(updateCatalogOp);
                            yield break; // Dừng luôn game
                        }
                    }
                }
            }
            // Dọn dẹp sau khi tải thành công
            Addressables.Release(getSizeOp);
            Addressables.Release(updateCatalogOp);
        }
        Addressables.Release(checkUpdateOp);
        PopupAndLoad.Instance.HidePopup();

        // ==========================================
        // 2. KẾT NỐI TỚI SERVER SOCKET (TCP)
        // ==========================================
        if (!NetworkManager.Instance.IsConnected)
        {
            PopupAndLoad.Instance.ShowPopup("Đang kết nối máy chủ...", null, false);

            NetworkManager.Instance.OnConnectedSuccessfully += HandleServerConnected;
            NetworkManager.Instance.OnConnectionFailed += HandleConnectionFailed;

            NetworkManager.Instance.Connect(GameConfig.SocketHost, GameConfig.SocketPort);

            float timer = 0;
            while (!_isServerConnected && timer < GameConfig.ConnectionTimeout)
            {
                timer += Time.deltaTime;
                yield return null;
            }

            if (!_isServerConnected)
            {
                HandleConnectionFailed("Không thể kết nối tới máy chủ (Timeout)");
                yield break;
            }
        }

        // ==========================================
        // 3. KIỂM TRA PHIÊN BẢN VỚI SERVER GAME
        // ==========================================
        PopupAndLoad.Instance.ShowPopup("Đang đồng bộ dữ liệu...", null, false);
        NetworkEventDispatcher.Instance.AddHandler(Cmd.CHECK_VERSION, OnReceiveVersionFromServer);

        Assets.Script.Data.GameActions.CheckVersion(GameConfig.ClientVersion);
    }

    private void HandleServerConnected()
    {
        _isServerConnected = true;
        Debug.Log("[Network] Connected to Server successfully.");
    }

    private void HandleConnectionFailed(string error)
    {
        _isServerConnected = false;
        PopupAndLoad.Instance.HidePopup();
        PopupAndLoad.Instance.ShowPopup("Lỗi kết nối: " + error);
    }

    private void OnReceiveVersionFromServer(byte[] data)
    {
        NetworkEventDispatcher.Instance.RemoveHandler(Cmd.CHECK_VERSION, OnReceiveVersionFromServer);

        try
        {
            MessageReader reader = new MessageReader(data);
            short status = reader.ReadShort();
            reader.Cleanup();

            if (status == 0) // Hợp lệ
            {
                PopupAndLoad.Instance.HidePopup();
            }
            else if (status == 1) // Phiên bản khác server → BẮT BUỘC cập nhật (server cũng chặn đăng nhập, mã 11)
            {
                PopupAndLoad.Instance.HidePopup();
                PopupAndLoad.Instance.ShowPopup("Bản cài đặt đã cũ. Vui lòng cập nhật game lên phiên bản mới nhất!");
            }
            else // -1: server lỗi khi kiểm tra
            {
                PopupAndLoad.Instance.HidePopup();
                PopupAndLoad.Instance.ShowPopup("Không kiểm tra được phiên bản. Vui lòng thử lại sau.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Lỗi đọc dữ liệu Version: " + e.Message);
            PopupAndLoad.Instance.ShowPopup("Dữ liệu phiên bản không hợp lệ!");
        }
    }

    private void OnDestroy()
    {
        if (NetworkManager.Instance != null)
        {
            NetworkManager.Instance.OnConnectedSuccessfully -= HandleServerConnected;
            NetworkManager.Instance.OnConnectionFailed -= HandleConnectionFailed;
        }
        if (NetworkEventDispatcher.Instance != null)
        {
            NetworkEventDispatcher.Instance.RemoveHandler(Cmd.CHECK_VERSION, OnReceiveVersionFromServer);
        }
    }
}