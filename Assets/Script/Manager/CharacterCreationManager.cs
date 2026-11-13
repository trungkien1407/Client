using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Assets.Script.Models;
using Assets.Script.Network;
using Assets.Script.Constants;
using Assets.Script.Manager;

namespace Assets.Script.Manager
{
    /// <summary>
    /// MÀN TẠO NHÂN VẬT: tải danh sách hệ phái (NinjaClassData, qua Addressables), sinh 1 nút cho mỗi hệ kèm hình Spine xem trước,
    /// bấm nút → hiện mô tả + hình của hệ đó; bấm [Tạo] → kiểm tên rồi gửi CREATE_CHARACTER(tên, hệ).
    /// Kết quả server trả do GameAuthManager.OnCreateCharResponse xử lý (vào game hoặc báo lỗi).
    /// </summary>
    public class CharacterCreationManager : MonoBehaviour
    {
        [Header("UI Tên & Mô tả")]
        public TMP_InputField inputName;
        public TextMeshProUGUI textClassDescription;

        [Header("Dynamic UI Settings")]
        public GameObject classButtonPrefab;
        public Transform buttonContainer;
        public Transform spineContainer;

        [Header("Ảnh trạng thái nút")]
        public Sprite normalSprite;
        public Sprite selectedSprite;

        public Button btnCreate;

        private List<NinjaClassData> availableClasses = new List<NinjaClassData>();
        private List<Image> generatedButtonImages = new List<Image>();
        private List<GameObject> generatedSpineObjects;

        private int currentClassId = -1;
        private int loadedSpineCount = 0;

        /// <summary>Gắn nút Tạo + phím Enter trong ô tên, rồi bắt đầu tải dữ liệu hệ phái.</summary>
        void Start()
        {
            if (btnCreate != null) btnCreate.onClick.AddListener(OnCreateButtonClicked);
            if (inputName != null)
            {
                inputName.characterLimit = 15;   // khớp server: tên 3–15 ký tự
                // Enter trong ô tên = bấm Tạo (trừ khi đang có hộp thông báo che màn hình)
                inputName.onSubmit.AddListener(_ => { if (PopupAndLoad.Instance == null || !PopupAndLoad.Instance.IsShowing) OnCreateButtonClicked(); });
            }
            // Bắt đầu quy trình khởi tạo và tải dữ liệu
            StartCoroutine(InitialSetupRoutine());
        }

        /// <summary>
        /// Coroutine (chạy qua nhiều khung hình, "yield return" = đợi việc đó xong rồi chạy tiếp):
        /// khởi tạo Addressables → nếu dữ liệu hệ phái chưa có trên máy thì tải về → nạp danh sách hệ.
        /// Mỗi "handle" của Addressables phải Release sau khi dùng để không rò bộ nhớ.
        /// </summary>
        private IEnumerator InitialSetupRoutine()
        {
            // BƯỚC 0: Khởi tạo hệ thống
            var initHandle = Addressables.InitializeAsync();
            yield return initHandle;

            // BƯỚC 1: Kiểm tra size (Cần cực kỳ cẩn thận đoạn này)
            var sizeHandle = Addressables.GetDownloadSizeAsync(Assets.Script.Core.AddressKeys.ClassDataLabel);
            yield return sizeHandle;

            // KIỂM TRA TÍNH HỢP LỆ TRƯỚC KHI TRUY CẬP STATUS
            if (sizeHandle.IsValid())
            {
                if (sizeHandle.Status == AsyncOperationStatus.Succeeded)
                {
                    long downloadSize = sizeHandle.Result;
                    Debug.Log($"[Addressables] Download Size: {downloadSize} bytes");

                    if (downloadSize > 0)
                    {
                        var downloadHandle = Addressables.DownloadDependenciesAsync(Assets.Script.Core.AddressKeys.ClassDataLabel);
                        yield return downloadHandle;

                        if (downloadHandle.IsValid() && downloadHandle.Status == AsyncOperationStatus.Failed)
                        {
                            Debug.LogError("[Addressables] Download lỗi: " + downloadHandle.OperationException);
                        }

                        if (downloadHandle.IsValid()) Addressables.Release(downloadHandle);
                    }
                }
                else
                {
                    Debug.LogWarning("[Addressables] GetDownloadSize thất bại.");
                }

                // Giải phóng handle sau khi dùng xong
                Addressables.Release(sizeHandle);
            }
            else
            {
                Debug.LogError("[Addressables] sizeHandle không hợp lệ (Invalid). Có thể do lỗi URI/Profile.");
            }

            // BƯỚC 2: Load dữ liệu (Cơ chế load này an toàn hơn)
            LoadAllNinjaClasses();
        }

        /// <summary>Nạp mọi NinjaClassData có nhãn ClassDataLabel; nạp xong thì xếp theo classId rồi dựng nút (GenerateUI).</summary>
        private void LoadAllNinjaClasses()
        {
            Addressables.LoadAssetsAsync<NinjaClassData>(Assets.Script.Core.AddressKeys.ClassDataLabel, (data) =>
            {
                // Hàm này chạy cho TỪNG asset vừa nạp; .Completed chạy 1 lần khi nạp xong tất cả
                if (data != null) availableClasses.Add(data);
            }).Completed += handle => {
                if (handle.Status == AsyncOperationStatus.Succeeded)
                {
                    availableClasses.Sort((a, b) => a.classId.CompareTo(b.classId));
                    GenerateUI();
                }
                else
                {
                    Debug.LogError("[Addressables] Không thể load ClassData: " + handle.OperationException);
                }
            };
        }

        /// <summary>
        /// Dọn nút / hình cũ rồi sinh mỗi hệ 1 nút + 1 hình Spine (tạo bất đồng bộ, ẩn sẵn).
        /// Đếm loadedSpineCount: khi hình cuối cùng tạo xong (kể cả hệ không có hình) thì tự chọn hệ đầu tiên.
        /// </summary>
        private void GenerateUI()
        {
            // Dọn dẹp nếu có dữ liệu cũ
            foreach (Transform child in buttonContainer) Destroy(child.gameObject);
            if (generatedSpineObjects != null)
            {
                foreach (var obj in generatedSpineObjects) if (obj != null) Addressables.ReleaseInstance(obj);
            }

            loadedSpineCount = 0;
            generatedButtonImages.Clear();
            // Tạo sẵn danh sách đủ chỗ (toàn null): hình tạo xong không theo thứ tự nên phải gán đúng vị trí currentIndex
            generatedSpineObjects = new List<GameObject>(new GameObject[availableClasses.Count]);

            for (int i = 0; i < availableClasses.Count; i++)
            {
                NinjaClassData data = availableClasses[i];
                // [C#] Chép i ra biến riêng: lambda bên dưới chạy SAU vòng lặp, dùng thẳng i sẽ luôn ra giá trị cuối
                int currentIndex = i;

                // 1. Tạo Nút bấm UI
                GameObject btnObj = Instantiate(classButtonPrefab, buttonContainer);
                generatedButtonImages.Add(btnObj.GetComponent<Image>());

                TextMeshProUGUI txtOnButton = btnObj.GetComponentInChildren<TextMeshProUGUI>();
                if (txtOnButton != null) txtOnButton.text = data.className;

                btnObj.GetComponent<Button>().onClick.AddListener(() => OnClassButtonClicked(currentIndex));

                // 2. Load Spine (Visual) bất đồng bộ
                if (data.spinePrefab != null && data.spinePrefab.RuntimeKeyIsValid())
                {
                    Addressables.InstantiateAsync(data.spinePrefab, spineContainer).Completed += (op) =>
                    {
                        if (op.Status == AsyncOperationStatus.Succeeded)
                        {
                            GameObject spineObj = op.Result;
                            spineObj.SetActive(false);
                            generatedSpineObjects[currentIndex] = spineObj;
                        }

                        loadedSpineCount++;
                        // Nếu load con cuối cùng xong thì mặc định chọn con đầu tiên
                        if (loadedSpineCount == availableClasses.Count) OnClassButtonClicked(0);
                    };
                }
                else
                {
                    loadedSpineCount++;
                    if (loadedSpineCount == availableClasses.Count) OnClassButtonClicked(0);
                }
            }
        }

        /// <summary>Chọn hệ thứ index: nhớ classId để gửi lúc tạo, đổi ảnh / chữ của nút, hiện mô tả và chỉ bật hình Spine của hệ đó.</summary>
        public void OnClassButtonClicked(int index)
        {
            if (index < 0 || index >= availableClasses.Count) return;

            currentClassId = availableClasses[index].classId;

            // Cập nhật Sprite nút
            for (int i = 0; i < generatedButtonImages.Count; i++)
            {
                if (generatedButtonImages[i] == null) continue;
                generatedButtonImages[i].sprite = (i == index) ? selectedSprite : normalSprite;
                var label = generatedButtonImages[i].GetComponentInChildren<TextMeshProUGUI>();   // nút đang chọn: chữ đậm, màu nổi
                if (label != null)
                {
                    label.fontStyle = i == index ? FontStyles.Bold : FontStyles.Normal;
                    label.color = i == index ? new Color(0.55f, 0.20f, 0.10f) : new Color(0.25f, 0.20f, 0.30f);
                }
            }

            // Mô tả: tên hệ (to, đậm) · vai trò · mô tả
            NinjaClassData selectedData = availableClasses[index];
            textClassDescription.text = $"<size=125%><b>{selectedData.className}</b></size>\n"
                + $"<color=#A0461E>{selectedData.element}</color>\n\n{selectedData.description}";

            // Bật/Tắt Spine tương ứng
            for (int i = 0; i < generatedSpineObjects.Count; i++)
            {
                if (generatedSpineObjects[i] != null)
                    generatedSpineObjects[i].SetActive(i == index);
            }
        }

        /// <summary>Bấm [Tạo]: kiểm tên 3–15 ký tự và đã chọn hệ, hiện "Đang tạo..." rồi gửi CREATE_CHARACTER. Kiểm kỹ (trùng tên, từ cấm) ở server.</summary>
        public void OnCreateButtonClicked()
        {
            string charName = inputName.text.Trim();
            // Khớp server (CreateCharacterHandler): 3–15 ký tự
            if (string.IsNullOrEmpty(charName) || charName.Length < 3 || charName.Length > 15)
            {
                PopupAndLoad.Instance.ShowPopup("Tên nhân vật phải từ 3 đến 15 ký tự!");
                return;
            }

            if (currentClassId == -1)
            {
                PopupAndLoad.Instance.ShowPopup("Đang tải hệ phái, vui lòng đợi giây lát...");
                return;
            }

            PopupAndLoad.Instance.ShowPopup("Đang tạo nhân vật...", null, false);

            Assets.Script.Data.GameActions.CreateCharacter(charName, currentClassId);
        }

        /// <summary>Rời màn: trả các hình Spine đã tạo qua Addressables (ReleaseInstance) để giải phóng bộ nhớ.</summary>
        void OnDestroy()
        {
            if (generatedSpineObjects != null)
            {
                foreach (var spineObj in generatedSpineObjects)
                {
                    if (spineObj != null) Addressables.ReleaseInstance(spineObj);
                }
            }
        }
    }
}
