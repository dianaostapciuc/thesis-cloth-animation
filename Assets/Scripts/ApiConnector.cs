using System.Collections;
using System.IO;
using System.IO.Compression;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

public class ApiConnector : MonoBehaviour
{
    [Header("Common UI Elements (Body Betas and Gender)")]
    public Slider bodyBetaSlider1;  
    public Slider bodyBetaSlider2; 
    public TMP_Dropdown bodyGenderDropdown;

    [Header("Garment Generation UI Elements")]
    public Slider garmentGammaSlider;
    public Button garmentUpdateButton;
    public TMP_Dropdown garmentTypeDropdown;

    [Header("Preset Body Size Buttons")]
    public Button smallSizeButton;
    public Button mediumSizeButton;
    public Button largeSizeButton;


    [Header("Button for Body Mesh Update (optional)")]
    public Button bodyUpdateButton;

    [Header("Runtime OBJ Import Settings")]
    public Material defaultMaterial; 
    public Material garmentMaterial;     

    [Header("Imported Meshes Parent")]
    public Transform importedMeshesParent;

    [Header("Spawn Transform")]
    public Transform spawnPoint;

    [Header("Camera Zoom")]
    public Camera targetCamera;
    public float zoomedInFOV = 30f;
    public float zoomedOutFOV = 60f;
    public float zoomSpeed = 5f;
    private bool isZoomedIn = false;
    private float targetFOV;

    public string bodyApiUrl = "http://127.0.0.1:8000/body/compute_obj";
    public string garmentApiUrl = "http://127.0.0.1:8000/garment/generate";

    private string bodyObjFilePath;
    private Coroutine pendingBodyDebounce;
    private bool isBodyRequestRunning;
    private bool refreshBodyQueued;

    void Start()
    {
        smallSizeButton.onClick.AddListener(() => SetPresetBodySize("S"));
        mediumSizeButton.onClick.AddListener(() => SetPresetBodySize("M"));
        largeSizeButton.onClick.AddListener(() => SetPresetBodySize("L"));

        if (bodyUpdateButton != null)
            bodyUpdateButton.onClick.AddListener(StartBodyRequestManually);

        if (garmentUpdateButton != null)
            garmentUpdateButton.onClick.AddListener(() => StartCoroutine(RequestGarmentAndBodyMesh()));

        bodyBetaSlider1.onValueChanged.AddListener(OnBodySliderChanged);
        bodyBetaSlider2.onValueChanged.AddListener(OnBodySliderChanged);

        if (targetCamera != null)
            targetFOV = targetCamera.fieldOfView;
    }

    void Update()
    {
        if (targetCamera != null && Mathf.Abs(targetCamera.fieldOfView - targetFOV) > 0.01f)
        {
            targetCamera.fieldOfView = Mathf.Lerp(targetCamera.fieldOfView, targetFOV, Time.deltaTime * zoomSpeed);
        }

        if (Input.GetKeyDown(KeyCode.Z))
        {
            ToggleZoom();
        }
    }

    private void ToggleZoom()
    {
        if (targetCamera == null) return;

        isZoomedIn = !isZoomedIn;
        targetFOV = isZoomedIn ? zoomedInFOV : zoomedOutFOV;
    }

    void SetPresetBodySize(string sizeLabel)
    {
        float beta1 = 0.0f; // height
        float beta2 = 0.0f; // fatness

        string gender = bodyGenderDropdown.options[bodyGenderDropdown.value].text.ToLower();

        if (gender == "female")
        {
            switch (sizeLabel)
            {
                case "S":
                    beta1 = -1.613364f;
                    beta2 = 1.466258f;
                    break;
                case "M":
                    beta1 = -1.163548f;
                    beta2 = 0.7431093f;
                    break;
                case "L":
                    beta1 = -0.6623814f;
                    beta2 = -0.2166621f;
                    break;
            }
        }
        else if (gender == "male")
        {
            switch (sizeLabel)
            {
                case "S":
                    beta1 = -2f;
                    beta2 = 1.671358f;
                    break;
                case "M":
                    beta1 = -1.510837f;
                    beta2 = 0.7419249f;
                    break;
                case "L":
                    beta1 = -0.6442611f;
                    beta2 = -0.6590072f;
                    break;
            }
        }

        bodyBetaSlider1.SetValueWithoutNotify(beta1);
        bodyBetaSlider2.SetValueWithoutNotify(beta2);

        StartBodyRequestManually();
    }

    private void OnBodySliderChanged(float _)
    {
        if (pendingBodyDebounce != null) StopCoroutine(pendingBodyDebounce);
        pendingBodyDebounce = StartCoroutine(BodyDebounce());
    }

    private IEnumerator BodyDebounce()
    {
        yield return new WaitForSeconds(0.3f);
        pendingBodyDebounce = null;
        StartBodyRequestManually();
    }

    private void StartBodyRequestManually()
    {
        if (isBodyRequestRunning)
        {
            refreshBodyQueued = true;
            return;
        }
        StartCoroutine(RequestBodyMesh());
    }

    IEnumerator RequestBodyMesh()
    {
        isBodyRequestRunning = true;

        string gender = bodyGenderDropdown.options[bodyGenderDropdown.value].text.ToLower();
        float min = bodyBetaSlider1.minValue;
        float max = bodyBetaSlider1.maxValue;
        float raw = bodyBetaSlider1.value;
        float beta1 = gender == "male" ? (min + max - raw) : raw;
        float beta2 = bodyBetaSlider2.value;

        string url = $"{bodyApiUrl}?gender={gender}";
        string jsonPayload = $"[ {beta1}, {beta2} ]";

        using UnityWebRequest req = new UnityWebRequest(url, "POST")
        {
            uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonPayload)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Body mesh API error: {req.error} (code {req.responseCode})");
            isBodyRequestRunning = false;
            yield break;
        }

        bodyObjFilePath = Path.Combine(Application.persistentDataPath, $"body_{gender}.obj");
        File.WriteAllBytes(bodyObjFilePath, req.downloadHandler.data);

        ClearExistingMeshes();
        LoadMesh(bodyObjFilePath, $"BodyMesh_{gender}");

        isBodyRequestRunning = false;

        if (refreshBodyQueued)
        {
            refreshBodyQueued = false;
            StartBodyRequestManually();
        }
    }

    IEnumerator RequestGarmentAndBodyMesh()
    {
        string gender = bodyGenderDropdown.options[bodyGenderDropdown.value].text.ToLower();
        string garment = garmentTypeDropdown.options[garmentTypeDropdown.value].text.ToLower();

        float min = bodyBetaSlider1.minValue;
        float max = bodyBetaSlider1.maxValue;
        float raw = bodyBetaSlider1.value;
        float beta1 = gender == "male" ? (min + max - raw) : raw;
        float beta2 = bodyBetaSlider2.value;
        float gamma = garmentGammaSlider.value;

        string jsonPayload =
            $"{{\"gender\":\"{gender}\"," +
            $"\"garment\":\"{garment}\"," +
            $"\"betas\":[{beta1},{beta2}],\"gammas\":[{gamma}]}}";

        using UnityWebRequest req = new UnityWebRequest(garmentApiUrl, "POST")
        {
            uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonPayload)),
            downloadHandler = new DownloadHandlerBuffer()
        };
        req.SetRequestHeader("Content-Type", "application/json");

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"Garment API error: {req.error} (code {req.responseCode})");
            yield break;
        }

        string zipPath = Path.Combine(Application.persistentDataPath, $"garment_{gender}_meshes.zip");
        File.WriteAllBytes(zipPath, req.downloadHandler.data);

        string extractPath = Path.Combine(Application.persistentDataPath, $"GarmentMeshes_{gender}");
        if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
        Directory.CreateDirectory(extractPath);
        ZipFile.ExtractToDirectory(zipPath, extractPath);

        ClearExistingMeshes();

        string bodyPath = Path.Combine(extractPath, $"body_{gender}.obj");
        string garmentPath = Path.Combine(extractPath, $"garment_{garment}_{gender}.obj");

        LoadMesh(bodyPath, $"BodyMesh_{gender}");
        LoadMesh(garmentPath, $"GarmentMesh_{garment}_{gender}");
        MergeBodyAndGarment();
    }

    void ClearExistingMeshes()
    {
        if (importedMeshesParent == null) return;

        foreach (Transform child in importedMeshesParent)
        {
#if UNITY_EDITOR
            DestroyImmediate(child.gameObject);
#else
            Destroy(child.gameObject);
#endif
        }
    }

    void LoadMesh(string filePath, string objectName)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError("OBJ not found: " + filePath);
            return;
        }

        Mesh m = SimpleOBJImporter.Import(filePath);
        if (m == null)
        {
            Debug.LogError("Import failed: " + filePath);
            return;
        }

        GameObject go = new GameObject(objectName);
        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mf.mesh = m;

        mr.material = (objectName.StartsWith("GarmentMesh_") && garmentMaterial != null)
                      ? garmentMaterial
                      : defaultMaterial;

        PositionAndParentMesh(go, objectName);
    }

    void PositionAndParentMesh(GameObject go, string objectName)
    {
        go.transform.SetParent(importedMeshesParent, false);
        go.transform.position = spawnPoint.position;
        go.transform.rotation = spawnPoint.rotation;

        if (objectName.StartsWith("BodyMesh_"))
        {
            MeshRenderer r = go.GetComponent<MeshRenderer>();
            if (r != null)
            {
                float lowestY = r.bounds.min.y * go.transform.localScale.y;
                go.transform.position += Vector3.up * (-lowestY + 0.03f);
            }
        }
        else if (objectName.StartsWith("GarmentMesh_"))
        {
            string bodyName = "BodyMesh_" + bodyGenderDropdown.options[bodyGenderDropdown.value].text.ToLower();
            Transform body = importedMeshesParent.Find(bodyName);

            if (body != null)
            {
                go.transform.SetParent(body, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
            }
        }
    }

    Transform DeepFind(Transform root, string targetName)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == targetName) return t;
        return null;
    }

    void MergeBodyAndGarment()
    {
        string gender = bodyGenderDropdown.options[bodyGenderDropdown.value].text.ToLower();
        string garment = garmentTypeDropdown.options[garmentTypeDropdown.value].text.ToLower();

        string bodyName = $"BodyMesh_{gender}";
        string garName = $"GarmentMesh_{garment}_{gender}";

        Transform bodyTf = DeepFind(importedMeshesParent, bodyName);
        Transform garmentTf = DeepFind(importedMeshesParent, garName);

        if (!bodyTf || !garmentTf)
        {
            Debug.LogWarning("Cannot merge – one or both meshes are missing.");
            return;
        }

        GameObject bodyGO = bodyTf.gameObject;
        GameObject garmentGO = garmentTf.gameObject;

        MeshFilter[] src = {
        bodyGO.GetComponent<MeshFilter>(),
        garmentGO.GetComponent<MeshFilter>()
    };

        Matrix4x4 parentSpace = importedMeshesParent.worldToLocalMatrix;

        var combine = new CombineInstance[src.Length];
        for (int i = 0; i < src.Length; ++i)
        {
            combine[i].mesh = src[i].sharedMesh;
            combine[i].transform = parentSpace * src[i].transform.localToWorldMatrix;
        }

        var merged = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        merged.CombineMeshes(combine, mergeSubMeshes: false, useMatrices: true);
        merged.RecalculateBounds();
        merged.RecalculateNormals();

        var mergedGO = new GameObject("MergedCharacter");
        mergedGO.transform.SetParent(importedMeshesParent, false);
        mergedGO.transform.localPosition = Vector3.zero;
        mergedGO.transform.localRotation = Quaternion.identity;
        mergedGO.transform.localScale = Vector3.one;

        mergedGO.AddComponent<MeshFilter>().sharedMesh = merged;
        mergedGO.AddComponent<MeshRenderer>().sharedMaterials =
            new[] { defaultMaterial, garmentMaterial };

#if UNITY_EDITOR
        if (bodyGO) DestroyImmediate(bodyGO);
        if (garmentGO) DestroyImmediate(garmentGO);
#else
    if (bodyGO)    Destroy(bodyGO);
    if (garmentGO) Destroy(garmentGO);
#endif
    }

}
