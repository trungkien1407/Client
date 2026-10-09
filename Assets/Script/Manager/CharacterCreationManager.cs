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

    void Start()
    {
        if (btnCreate != null) btnCreate.onClick.AddListener(OnCreateButtonClicked);
        // Bắt đầu quy trình khởi tạo và tải dữ liệu
        StartCoroutine(InitialSetupRoutine());
    }

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

    private void LoadAllNinjaClasses()
    {
        Addressables.LoadAssetsAsync<NinjaClassData>(Assets.Script.Core.AddressKeys.ClassDataLabel, (data) =>
        {
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
        generatedSpineObjects = new List<GameObject>(new GameObject[availableClasses.Count]);

        for (int i = 0; i < availableClasses.Count; i++)
        {
            NinjaClassData data = availableClasses[i];
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

    public void OnClassButtonClicked(int index)
    {
        if (index < 0 || index >= availableClasses.Count) return;

        currentClassId = availableClasses[index].classId;

        // Cập nhật Sprite nút
        for (int i = 0; i < generatedButtonImages.Count; i++)
        {
            generatedButtonImages[i].sprite = (i == index) ? selectedSprite : normalSprite;
        }

        // Cập nhật text mô tả
        NinjaClassData selectedData = availableClasses[index];
        textClassDescription.text = $"{selectedData.description}\n\n<color=#ff0000>Hệ:</color> {selectedData.element}";

        // Bật/Tắt Spine tương ứng
        for (int i = 0; i < generatedSpineObjects.Count; i++)
        {
            if (generatedSpineObjects[i] != null)
                generatedSpineObjects[i].SetActive(i == index);
        }
    }

    public void OnCreateButtonClicked()
    {
        string charName = inputName.text.Trim();
        // Khớp server (CreateCharacterHandler): 3–15 ký tự
        if (string.IsNullOrEmpty(charName) || charName.Length < 3 || charName.Length > 15)
        {
            PopupAndLoad.Instance.ShowPopup("Tên nhân vật phải từ 3 đến 15 ký tự!");
            return;
        }

        if (currentClassId == -1) return;

        PopupAndLoad.Instance.ShowPopup("Đang tạo nhân vật...", null, false);

        Assets.Script.Data.GameActions.CreateCharacter(charName, currentClassId);
    }

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