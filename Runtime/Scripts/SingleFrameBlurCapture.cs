using UnityEngine;

/// 单帧场景颜色捕获组件（运行时自动执行）。
/// 启动时抓取一帧场景颜色，在 GPU 端预模糊后赋给材质的 _CapturedSceneTex。
/// shader 在 _SINGLE_FRAME 模式下只需 1 次采样，性能极高。
///
/// 性能要点：
///   - 捕获完成后会 Destroy(this) 彻底移除组件，**杜绝任何后续 MonoBehaviour/LateUpdate 开销**
///   - 不订阅 sceneLoaded 事件（首次启动时场景早已就绪，事件不会重发，徒增复杂度）
///   - 不在 Awake/Start 中做重活，所有捕获工作集中到一次 LateUpdate 调用
[RequireComponent(typeof(Renderer))]
public class SingleFrameBlurCapture : MonoBehaviour
{
    [Tooltip("目标相机，为空则优先使用 Camera.main，再回退到场景中第一个 enabled 的相机（\"激活的相机\"）")]
    public Camera targetCamera;

    [Tooltip("模糊 Shader（必须指定，否则打包后无法使用）")]
    [SerializeField] private Shader blurShader;

    [Tooltip("捕获延迟帧数")]
    [Range(1, 10)]
    public int captureDelayFrames = 3;

    [Tooltip("RT 分辨率缩放")]
    [Range(0.25f, 1f)]
    public float resolutionScale = 0.5f;

    [Tooltip("模糊大小（对应材质 _BlurSize）")]
    [Range(0f, 11f)]
    public float blurSize = 4.0f;

    [Tooltip("像素大小（对应材质 _PixelSize）")]
    [Range(0.25f, 4f)]
    public float pixelSize = 1.0f;

    [Tooltip("Sigma 高斯权重（对应材质 _Sigma）")]
    [Range(0.1f, 5f)]
    public float sigma = 2.7f;

    [Tooltip("采样数量（对应材质 _SampleCount）")]
    [Range(1, 12)]
    public int sampleCount = 6;

    [Tooltip("使用性能模式（对应材质 Performance Mode）")]
    public bool usePerformanceMode = false;

    private RenderTexture _capturedRT;
    private Material _material;
    private Material _blurMaterial;
    private int _frameCount;
    private bool _captureStarted; // ★ 一次性锁定：捕获已开始/完成后，LateUpdate 早返回

    private static readonly int CapturedSceneTex = Shader.PropertyToID("_CapturedSceneTex");
    private static readonly int BlurSizeID = Shader.PropertyToID("_BlurSize");
    private static readonly int PixelSizeID = Shader.PropertyToID("_PixelSize");
    private static readonly int SigmaID = Shader.PropertyToID("_Sigma");
    private static readonly int SampleCountID = Shader.PropertyToID("_SampleCount");
    private static readonly int UsePerformanceModeID = Shader.PropertyToID("_UsePerformanceMode");
    private static readonly int ResolutionScaleID = Shader.PropertyToID("_ResolutionScale");

    private void Awake()
    {
        var renderer = GetComponent<Renderer>();
        if (renderer != null)
            _material = renderer.material;
    }

    private void Start()
    {
        // ● 相机解析顺序：
        //   1. 用户手动指定的 targetCamera
        //   2. Camera.main（Tag 为 MainCamera）
        //   3. Camera.allCameras 中第一个 enabled 的相机（"激活的相机"）
        if (targetCamera == null)
            targetCamera = ResolveActiveCamera();

        _frameCount = 0;

        // 初始化模糊材质
        if (blurShader == null)
            blurShader = Shader.Find("Hidden/SingleFrameBlur");

        if (blurShader != null && blurShader.isSupported)
        {
            _blurMaterial = new Material(blurShader);
            _blurMaterial.hideFlags = HideFlags.HideAndDontSave;
        }
        else
        {
            Debug.LogError("SingleFrameBlurCapture: Blur shader 无效或不支持，单帧捕获功能不可用。");
            Destroy(this); // 立即销毁组件，避免无效 LateUpdate
        }
    }

    /// 解析"激活的相机"：优先 Camera.main，再回退 Camera.allCameras 第一个 enabled 的。
    private static Camera ResolveActiveCamera()
    {
        var cam = Camera.main;
        if (cam != null && cam.enabled && cam.gameObject.activeInHierarchy)
            return cam;

#if UNITY_2023_1_OR_NEWER
        var all = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<Camera>();
#endif
        for (int i = 0; i < all.Length; i++)
        {
            var c = all[i];
            if (c != null && c.enabled && c.gameObject.activeInHierarchy)
                return c;
        }
        return null;
    }

    private void LateUpdate()
    {
        // ● 一次性锁定：捕获已开始或已完成，立刻早返回，避免任何后续帧开销
        //   （Destroy(this) 是延迟到帧末执行的，此处双重保险）
        if (_captureStarted) return;

        // ● 相机解析**仅在 targetCamera 为 null 时尝试一次**，不再每帧调用 ResolveActiveCamera
        //   老版本每帧都做 Camera.main + FindObjectsByType<Camera>()，在大场景中开销显著
        if (targetCamera == null)
        {
            targetCamera = ResolveActiveCamera();
            if (targetCamera == null)
            {
                _frameCount++;
                if (_frameCount > captureDelayFrames + 5)
                {
                    Debug.LogError($"SingleFrameBlurCapture [{name}] 在 {captureDelayFrames + 5} 帧内未找到可用相机，单帧捕获已放弃。" +
                                   "请手动指定 targetCamera，或检查场景是否有 enabled 的 Camera。", this);
                    Destroy(this);
                }
                return;
            }
        }

        // 相机被禁用（仅 O(1) 属性访问，无 FindObjects）
        if (!targetCamera.isActiveAndEnabled)
        {
            _frameCount++;
            if (_frameCount > captureDelayFrames + 5)
            {
                Debug.LogError($"SingleFrameBlurCapture [{name}] 目标相机 {targetCamera.name} 未激活，单帧捕获已放弃。", this);
                Destroy(this);
            }
            return;
        }

        _frameCount++;
        if (_frameCount >= captureDelayFrames)
        {
            _captureStarted = true; // ★ 锁定，确保即使 Destroy 延迟也只捕获一次

            bool ok = CaptureAndBlur();
            if (ok)
                Debug.Log($"SingleFrameBlurCapture [{name}] 已捕获一帧到 _CapturedSceneTex (相机: {targetCamera.name})", this);

            // ★ 关键优化：捕获完成立即销毁组件，杜绝后续每帧 LateUpdate 调用
            Destroy(this);
        }
    }

    private bool CaptureAndBlur()
    {
        if (targetCamera == null || _material == null || _blurMaterial == null) return false;

        int width = Mathf.Max(64, Mathf.RoundToInt(targetCamera.pixelWidth * resolutionScale));
        int height = Mathf.Max(64, Mathf.RoundToInt(targetCamera.pixelHeight * resolutionScale));

        // 捕获场景（排除自身）
        var selfRenderer = GetComponent<Renderer>();
        bool wasEnabled = selfRenderer != null && selfRenderer.enabled;
        if (selfRenderer != null)
            selfRenderer.enabled = false;

        RenderTexture sceneRT = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        sceneRT.name = "SceneCapture_Temp";

        var originalRT = targetCamera.targetTexture;

        targetCamera.targetTexture = sceneRT;
        targetCamera.Render();
        targetCamera.targetTexture = originalRT;

        if (selfRenderer != null)
            selfRenderer.enabled = wasEnabled;

        // GPU 端多 Pass 模糊
        _capturedRT = ApplyBlur(sceneRT, width, height);
        RenderTexture.ReleaseTemporary(sceneRT);

        // 赋给材质
        _material.SetTexture(CapturedSceneTex, _capturedRT);

        return true;
    }

    private RenderTexture ApplyBlur(RenderTexture source, int width, int height)
    {
        // 设置与主材质一致的模糊参数
        _blurMaterial.SetFloat(BlurSizeID, blurSize);
        _blurMaterial.SetFloat(PixelSizeID, pixelSize);
        _blurMaterial.SetFloat(SigmaID, sigma);
        _blurMaterial.SetFloat(SampleCountID, sampleCount);
        _blurMaterial.SetFloat(UsePerformanceModeID, usePerformanceMode ? 1f : 0f);
        _blurMaterial.SetFloat(ResolutionScaleID, resolutionScale);

        // 单次 Blit 完成全部模糊（shader 内部完整计算高斯核或 Kawase）
        RenderTexture result = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
        result.name = "SingleFrameBlurred";
        // ● 关键：HideAndDontSave 让 RT 独立于组件存活
        //   组件会在捕获完成后 Destroy(this)，但材质的 _CapturedSceneTex 仍要长期引用这个 RT，
        //   若不标记，OnDestroy 释放 RT 后 shader 采样会变成全黑（模糊区域消失）
        result.hideFlags = HideFlags.HideAndDontSave;
        Graphics.Blit(source, result, _blurMaterial);

        return result;
    }

    private void OnDestroy()
    {
        // ★ _capturedRT 不再释放：它已被材质 _CapturedSceneTex 引用，必须让它独立存活，
        //   否则组件销毁后模糊区域会立即变全黑。
        //   HideAndDontSave 标记后 RT 不会被 SceneManager 等场景清理机制误删，
        //   它会在游戏结束时随进程退出自然释放（占用仅为一帧 RT，可忽略）。
        if (_blurMaterial != null)
            Destroy(_blurMaterial);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (blurShader == null)
        {
            blurShader = Shader.Find("Hidden/SingleFrameBlur");
        }
    }

    private void Reset()
    {
        blurShader = Shader.Find("Hidden/SingleFrameBlur");
    }
#endif
}
