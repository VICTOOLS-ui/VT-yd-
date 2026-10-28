/// SpotLightVolume v6.3 - 重构版（架构优化，无功能变更）
///
/// 重构要点：
/// A. 消除 Update 中的重复操作
///    1. 引入 ConeGeometryCache 结构体缓存派生计算结果（radiusEnd/slopeAngle/cosSlope/sinSlope），
///       避免 ApplyShaderProperties 中每帧重复调用 Tan/Atan2/Cos/Sin
///    2. ApplyBlendMode 仅在 blendMode 变化时执行，避免每帧重复 EnableKeyword/DisableKeyword/SetInt
///       （关键字切换会触发 Shader 变体重编译，这是真实的运行时开销）
///    3. _material.renderQueue 从 UpdateMaterial 移到 EnsureMaterial（仅创建时设置一次）
///    4. 拆分 Shader 属性推送：_ClipDistance 为高频参数（每帧），其余 17 个为低频（dirty 标志触发）
///    5. colorFromLight 模式下，单独跟踪 _light.color 变化、按需只更新颜色通道
/// B. 改进可读性
///    1. 拆分 ApplyShaderProperties 为职责单一的子方法（颜色/光照/几何/蒙版）
///    2. 拆分 GenerateNormalizedConeMesh 为 BuildSideVertices/BuildSideTriangles/BuildCap*
///    3. 引入 EnsureLight() 守卫函数，消除分散的 null/type 检查
///    4. 4 个独立 _cachedXxx 字段聚合为 ConeGeometryCache 结构体
///    5. ShaderIDs 静态类按用途分组
/// C. 不变项（保持向后兼容）
///    - 所有 public 字段、默认值、Header/Tooltip/Range/Min 标记
///    - Mesh 生成（顶点/UV/三角形索引顺序与原版 bit-identical）
///    - Material 关键字组合与最终 Shader 属性值
///    - 序列化兼容：Inspector 不变 → 现有 Scene/Prefab 不需要重新序列化
///
/// SpotLightVolume v6.2 - 蒙版投影：maskTexture 模拟窗格光柱投影
/// SpotLightVolume v6.1 - 射线遮挡支持角色碰撞：occlusionDetectTriggers 可检测 Trigger
/// SpotLightVolume v6.0 - 重构：改进重复 GetComponent 调用，消除 UpdateGeometry/UpdateMaterial 重复计算
///   - 射线遮挡截断：Physics.Raycast 沿光柱方向检测碰撞
///   - 参考 VLB 架构：归一化 Mesh + localScale + 双 Pass + Fresnel + 距离衰减
///   - 轻量探照灯体积雾（锥形 Mesh + 自定义 Shader）
///
/// SpotLightVolume v6.4 - 曝光系数防过曝：
///   - 新增 volumeExposure 字段（范围 0.1~2，默认 1）：曝光系数直接控制单灯总能量，
///     类似摄影曝光补偿，叠加场景建议调到 0.5~0.7
/// SpotLightVolume v7.0 优化工具性能，每帧 0 次 SetPropertyBlock（静止 dirty 命中）+ 0 次 UpdateOcclusion（守门跳过）+ 0 次 Native get- 稳定：6 个 shader变体编译无警告。
/// SpotLightVolume v7.1 修复 v7.0 引入的显示正确性 bug（用户报告：旧摆放的灯不显示 / 调节亮起后消失 / 不可控闪烁）：
///   - 移除 _needsPerFrameUpdate 静态守门（绑死了"显示正确性"和"OnValidate/OnEnable 时机"，时序错乱即 stale 显示）
///   - 移除 OnValidate 的延迟 lambda（Domain Reload / Scene 切换 / Prefab Override Apply 会丢失闭包）
///   - OnEnable 末尾强制 applyAllParams=true（保证 17 个参数首推完整）
///   - Update 走轻量流程：dirty 跟踪 + 高频参数单独推送路径保持 v7.0 优化
/// SpotLightVolume v7.1.1 根除 prefab 序列化 child 闪烁：
///   - CreateVolumeChild 用 HideAndDontSave 完全屏蔽 prefab 序列化
///   - EnsureVolumeChild 检测并销毁旧版(v6.x/v7.0) prefab 序列化的 child,升级后自动清理一次
///   - OnValidate 不再同步调用 UpdateGeometry/UpdateMaterial(避免 SendMessage 错误 + Apply 状态不一致)
/// SpotLightVolume v7.1.2 距离剔除 LOD(hysteresis 双阈值防闪烁)：
///   - 新增 updateCullDistance + updateCullHysteresis 字段
///   - Update 开头 ShouldCullByDistance() 守门：Camera.main 距离 > 阈值 → 跳 Update + 关闭 Renderer
///   - Camera.main 缓存 1 秒(避免每帧 Find 开销)
///   - 退出剔除时强制 dirty=true → 下次 Update 完整推参数(防 stale MPB)
/// SpotLightVolume v7.1.3 内核闪烁根因全面修复：
///   - Occlusion hysteresis:hit.distance 变化 > 0.2m 才更新 _clipDistance,miss 连续 2 次才退出截断
///     (防角色边缘穿过光柱时光柱长度高频抖动)
///   - EnsureMaterial/ApplyBlendMode 切 shared material 前 SetPropertyBlock(null) 清旧 MPB
///     (防跨实例 per-instance 参数残留导致闪烁)
///   - ShouldCullByDistance 加 OnEnable 首帧跳过(_skipDistanceCullOnce)+ Camera.main 销毁检测
///     (防 prefab 远位灯"闪一下就消失" / 跨 Scene 后 Distance 计算 NRE)
/// SpotLightVolume v7.1.4 距离剔除编辑器兼容 + 启动宽限期:
///   - ShouldCullByDistance 加 !Application.isPlaying 守门 → 编辑器模式不做距离剔除
///     (修复 prefab instance + 场景 MainCamera 远位时"灯不亮"问题,保护 prefab 编辑体验)
///   - 新增 _enableTime + CULL_GRACE_PERIOD(0.3s) 启动宽限期 → Play 启动后所有灯可见
///     (等 Camera.main 异步加载 + 玩家相机瞬移到初始位置稳定,避免启动瞬间误判)
///   - 时序稳定性:OnEnable 设 _enableTime,Update 中 unscaledTime 比较(不受 timeScale 影响)
/// SpotLightVolume v7.2 根除闪烁(尤其复制灯):放弃 shared material + MPB 架构,改为每盏灯独立 material 实例:
///   - 根因:shader 所有参数在 UnityPerMaterial CBUFFER(per-material),但旧代码用 MaterialPropertyBlock(per-instance)
///     设置它们 → SRP Batcher 下 MPB 覆盖 per-material 属性不可靠,多灯共享 shared material 时互相争抢 CBUFFER → 闪烁
///   - 修复:每盏灯 new Material(shader) 独立实例,参数直接 material.SetXxx 写入各自 CBUFFER,彻底隔离
///   - 复制灯(Instantiate/Ctrl+D)时 OnEnable 重新 new Material → 不共享任何状态
///   - keyword(_BLEND_*)改为 per-material 动态 Enable/Disable,不再拆 6 份 shared material
///   - 移除全部 MPB(_mpb/SetPropertyBlock/GetPropertyBlock/HasPropertyBlock)与 6 个静态 shared material 缓存
///   - 效果完全不变(shader 未改,参数值一致),仅牺牲 SRP Batcher 合批换取绝对稳定
/// SpotLightVolume v7.3 半透明渲染 overdraw 优化 + ZTest 可配置：
///   - Shader:ZTest Off → ZTest [_ZTest],新增 _ZTest 属性(Int,默认 4=LessEqual)
///   - 体积光柱是 Transparent queue 透明物体,渲染时场景不透明物已写入深度缓冲
///   - ZTest Off 导致被墙/角色遮挡的片段仍执行整套 fragment(深度采样+rayCone+raymarching)→ overdraw
///   - 默认 LessEqual:硬件在光栅化阶段直接剔除被遮挡片段,效果等价于 shader 内原有手动深度截断
///   - C#:新增 zTestMode 字段(UnityEngine.Rendering.CompareFunction,默认 LessEqual),
///     变化时写 _ZTest(与 blendMode 同级的"仅变化时写"缓存 _cachedZTestMode),材质属性 HideInInspector 由组件驱动
/// （LEqual 整体更轻量。虽然它比 Disabled 多了一步深度测试,但这一步几乎免费,却换来了对昂贵 fragment 计算的大量跳过。）
/// /// SpotLightVolume v7.4 - 遮挡模式分级 + 摄像机视锥剔除：
///   - 新增 VolumeOcclusionMode 枚举(Disabled / ZTestOnly / ZTestAndFade),旧版 enableOcclusion bool 通过
///     [FormerlySerializedAs] 吸收为隐藏字段 _legacyEnableOcclusion,OnEnable 首次激活时若为 true
///     自动升级到 ZTestAndFade 一次,然后清零标记。默认推荐 ZTestOnly(零 CPU 开销,仅 URP 深度硬件遮挡)。
///   - ShouldCullByDistance 增加 GameCamera 视锥剔除：摄像机外的灯直接关闭 MeshRenderer + 跳过
///     UpdateOcclusion / UpdateMaterial / PushHighFrequencyShaderParams,大幅降低大场景远镜时的 CPU/GPU 浪费。
///     视锥面缓存 1s 与 Camera.main 缓存共用,bounds 膨胀 0.5m 防边界闪烁。

using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Vic.Runtime
{
    public enum VolumeBlendMode
    {
        Additive = 0,
        SoftAdditive = 1,
        Alpha = 2
    }

    /// 体积光柱遮挡模式(v7.4 新增,旧版 enableOcclusion bool 已迁移为 _legacyEnableOcclusion)。
    /// 控制遮挡的"强度档位":从纯硬件(零 CPU 开销)到完整 CPU Raycast 软淡出。
    public enum VolumeOcclusionMode
    {
        /// 完全关闭遮挡:`ZTest Always`,被遮挡部分也绘制。overdraw 最高,仅作调试用。
        Disabled = 0,
        /// 仅 URP 深度硬件遮挡:零 CPU 开销,遮挡面硬切(无软淡出)。推荐默认。
        ZTestOnly = 1,
        /// ZTest + CPU Raycast 软淡出(原 v7.3 行为):需要 occluder 上有 Collider,Raycast 命中
        /// 距离传入 shader 做遮挡面附近的 ~30% 范围软羽化。视觉更柔和,CPU 开销每盏每 interval 1 次 Raycast。
        ZTestAndFade = 2,
    }

    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class SpotLightVolume : MonoBehaviour
    {
        #region 公开参数

        [Header("距离控制")]
        [Tooltip("光源半径(光柱始端宽度): 0=尖锥, 越大始端越宽")]
        [Min(0f)]
        public float lightSourceRadius = 0f;

        [Tooltip("衰减起始距离")]
        [Min(0f)]
        public float fallOffStart = 0f;

        [Tooltip("衰减结束距离(最远距离)")]
        [Min(0.1f)]
        public float maxDistance = 3f;

        [Header("羽化控制")]
        [Tooltip("边缘羽化: 值越大边缘越软，越小越硬")]
        [Range(0.01f, 2f)]
        public float edgeFade = 0.3f;

        [Tooltip("末端羽化强度")]
        [Range(0f, 1f)]
        public float endFade = 0.5f;

        [Tooltip("正面眩光: 从光源方向看的亮度提升")]
        [Range(0f, 1f)]
        public float glareFrontal = 0.5f;

        [Tooltip("背面眩光: 从光束后方看的亮度提升")]
        [Range(0f, 1f)]
        public float glareBehind = 0.3f;

        [Header("外观")]
        [Tooltip("雾效整体强度")]
        [Range(0f, 5f)]
        public float intensity = 1f;

        [Tooltip("起始亮度增强幅度: 光柱起始处额外提亮")]
        [Range(0f, 8f)]
        public float startBoostIntensity = 1.5f;

        [Tooltip("起始亮度范围: 增亮区域的绝对距离，值越小亮区越短")]
        [Range(0.01f, 15f)]
        public float startBoostRange = 1f;

        [Tooltip("中心渐变距离: 控制中心高亮向外扩散的范围，值越小高亮越集中")]
        [Range(0.01f, 1f)]
        public float centerFade = 0.5f;

        [Tooltip("跟随灯光颜色")]
        public bool colorFromLight = true;

        [ColorUsage(false, true)]
        public Color volumeColor = Color.white;

        [Tooltip("混合方式")]
        public VolumeBlendMode blendMode = VolumeBlendMode.Additive;

        [Header("渲染")]
        [Tooltip("深度测试方式: 控制体积光柱被场景几何体遮挡时是否被剔除。\nLessEqual=默认(被遮挡部分剔除,性能最优,推荐);\nDisabled=不做深度测试(旧版行为,被遮挡也绘制,overdraw 高)")]
        public UnityEngine.Rendering.CompareFunction zTestMode = UnityEngine.Rendering.CompareFunction.LessEqual;

        [Header("Mesh质量")]
        [Tooltip("圆锥面数")]
        [Range(3, 32)]
        public int coneSides = 8;

        [Tooltip("圆锥分段数")]
        [Range(0, 10)]
        public int coneSegments = 1;

        [Header("遮挡")]
        [Tooltip("遮挡模式：\n• Disabled=完全关闭(ZTest Always,overdraw 最高,仅调试)\n• ZTestOnly=仅 URP 深度硬件遮挡(零 CPU 开销,推荐默认)\n• ZTestAndFade=ZTest + CPU Raycast 软淡出(原 v7.3 行为,需 Collider)")]
        public VolumeOcclusionMode occlusionMode = VolumeOcclusionMode.ZTestOnly;

        // [P1 迁移] 旧版 `enableOcclusion` bool 字段已替换为 occlusionMode 枚举。
        // 通过 [FormerlySerializedAs] 把 prefab/scene 里残留的旧字段值吸收进来,OnEnable 首次激活时
        // 若为 true 自动升级 occlusionMode = ZTestAndFade,然后清零标记,确保旧资产零迁移成本。
        [SerializeField, HideInInspector, FormerlySerializedAs("enableOcclusion")]
        private bool _legacyEnableOcclusion = false;

        // 下面三个字段在 ZTestAndFade 模式下生效,其他模式编辑器会按 occlusionMode 自动显隐,运行时无副作用。
        [Tooltip("射线遮挡检测的Layer Mask")]
        public LayerMask occlusionLayerMask = ~0;

        [Tooltip("射线遮挡更新间隔(秒), 0=每帧更新。默认 0.1(10Hz), 多盏场景兼顾性能; 单盏可调到 0.05(20Hz) 或 0")]
        [Range(0f, 0.5f)]
        public float occlusionUpdateInterval = 0.1f;

        [Tooltip("是否检测Trigger碰撞体（角色可能使用Trigger类型的Collider）")]
        public bool occlusionDetectTriggers = false;

        [Header("蒙版投影")]
        [Tooltip("启用蒙版纹理投影")]
        public bool enableMask = false;

        [Tooltip("蒙版纹理（模拟窗格光柱投影，黑色区域无光）")]
        public Texture2D maskTexture;

        [Tooltip("蒙版强度: 0=无蒙版效果, 1=完全按蒙版遮挡")]
        [Range(0f, 1f)]
        public float maskIntensity = 1f;

        [Header("后处理优化")]
        [Tooltip("曝光系数（类似摄影曝光补偿）: 控制单灯总能量。多盏体积光叠加 + Bloom 过曝时降低（如 0.5），默认值=1 = 与原版一致")]
        [Range(0.1f, 2f)]
        public float volumeExposure = 1f;

        [Header("性能剔除")]
        [Tooltip("距离剔除阈值(米):摄像机到灯距离超过此值时跳过Update且不渲染。0=禁用剔除")]
        [Min(0f)]
        public float updateCullDistance = 50f;

        [Tooltip("距离剔除滞回(米):防闪烁。进/出剔除阈值错开此值，避免灯在边界反复切换可见/不可见")]
        [Range(0f, 10f)]
        public float updateCullHysteresis = 5f;

        #endregion

        #region 内部状态

        private Light _light;
        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        // [v7.2] 每盏灯独立 material 实例:参数直接写入各自 UnityPerMaterial CBUFFER,彻底隔离。
        // 复制灯(Instantiate/Ctrl+D)时 OnEnable 会 new Material → 不共享任何材质状态。
        private Material _material;
        private GameObject _volumeChild;

        // 射线遮挡
        private float _clipDistance = -1f;
        private float _lastOcclusionCheckTime;
        private float _lastPushedClipDistance = float.PositiveInfinity;  // dirty 跟踪:_clipDistance 变化才写 material(避开每帧冗余 SetFloat)
        // [FIX v7.1.3] miss 连续计数器:必须连续 2 次 miss 才退出截断,防角色边缘瞬间离开导致闪烁
        private int _occlusionMissStreak;

        // 几何派生缓存（聚合：避免 4 个散落的 _cachedXxx 字段）
        // UpdateGeometry 中刷新，ApplyShaderProperties 读取，避免每帧重复三角函数
        private ConeGeometryCache _coneCache;

        // Material dirty 控制：低频参数在 dirty=true 时推送，避免每帧 17 次无意义 SetFloat
        private VolumeBlendMode _cachedBlendMode = (VolumeBlendMode)(-1); // 初始非法值，强制首次刷新
        private Color _lastAppliedLightColor;
        private bool _lastColorFromLight;
        private bool _materialParamsDirty = true;

        // ZTest 深度测试状态缓存（仅在 zTestMode 变化时才写 material,避免每帧冗余 SetInt）
        private UnityEngine.Rendering.CompareFunction _cachedZTestMode = (UnityEngine.Rendering.CompareFunction)(-1);

        // Animator 存在标记：动画可能驱动 Light 任意属性（spotAngle/color/intensity/...），
        // 有 Animator 时必须每帧全推 shader 参数，无 Animator 时无人工干预可走轻量路径。
        private bool _hasAnimator;

        // [FIX v7.1] 移除 v7.0 的 _needsPerFrameUpdate 静态守门。
        // 原守门会把"显示正确性"和"OnValidate/OnEnable 时机"绑死:
        //   - OnEnable 后 Update 永不跑 → Renderer/Scene/Prefab 任何后续状态变化都会导致 stale 显示
        //   - OnValidate 的 EditorApplication.delayCall 在 Domain Reload/Scene 切换/Prefab Override Apply
        //     时会丢失 lambda 引用 → "调节亮起后再次消失"
        //   - enableOcclusion/colorFromLight 在 Inspector 反复切换会让 _needsPerFrameUpdate 跳变 → Update 路径不稳 → 闪烁
        // 性能仍由 dirty 跟踪 + PushHighFrequencyShaderParams 跳过不变参数保证，
        // 每帧成本从"1 次 bool + return"提升到"3 次 dirty bool 比较 + 可能的 1 次轻量推送"，
        // 仍远低于 v6.x 全推开销。

        // 距离剔除状态:hysteresis 双阈值防边界闪烁(进/出剔除阈值错开 updateCullHysteresis 米)。
        // 进入剔除(远处 → 看不见):distance > updateCullDistance + hysteresis
        // 退出剔除(近处 → 看见):distance < updateCullDistance - hysteresis
        private bool _isCulledByDistance;
        // [FIX v7.1.3] OnEnable 后第一帧跳过距离剔除,保证灯至少有 1 帧可见
        // (否则 prefab 远位灯 OnEnable 推 MPB 后下一帧立即被剔除 → "闪一下就消失")
        // [FIX v7.1.4] 启动宽限期:OnEnable 后 CULL_GRACE_PERIOD 秒内不剔除,等 Camera.main 加载稳定
        // + 玩家相机瞬移完成,避免 Play 启动瞬间误判。
        // 优先级:_skipDistanceCullOnce(首帧保护)< 宽限期(时间窗口保护)< 距离判断
        private bool _skipDistanceCullOnce;
        private float _enableTime;  // OnEnable 时刻(unscaledTime 基准,与 ShouldCullByDistance 一致)
        private const float CULL_GRACE_PERIOD = 0.3f;  // 0.3 秒宽限期,够 Camera.main 加载 + 玩家初始位置稳定
        // Camera.main 缓存(每帧 Find 性能差),1 秒刷新一次。Camera.main 为 null 时跳过剔除(保守)。
        private Camera _cachedMainCamera;
        private float _lastMainCameraCacheTime;

        // [P0 视锥剔除] 摄像机视锥面缓存(GeometryUtility.CalculateFrustumPlanes 同样 1 秒刷新,
        // 与 _cachedMainCamera 缓存共用同一刷新窗口,避免重复调用)。
        private Plane[] _cachedFrustumPlanes;
        private float _lastFrustumCacheTime;
        // 视锥内/外状态独立于距离剔除,无 hysteresis(视锥是 sharp in/out,边沿处靠 bounds 膨胀兜底防 1 帧抖动)。
        private bool _isCulledByFrustum;
        // [FIX v7.1.3] 兜底:每 FULL_PUSH_INTERVAL_FRAMES 帧强制全推一次,修复任何路径未覆盖的
        // stale 材质状态(如外部脚本改 Renderer、Pipeline 切换、Domain Reload 后 _meshRenderer 状态丢失等)。
        // 每盏灯每 60 帧多一次 UpdateMaterial(applyAllParams=true),代价<0.1ms/盏,收益显著。
        private int _framesSinceFullPush;
        private const int FULL_PUSH_INTERVAL_FRAMES = 60;

        // 共享归一化Mesh缓存：按 (sides, segments) 配置缓存，每种配置一份、永久复用。
        // v6.3：之前用单个静态 _sharedMesh，配置变化时直接覆盖且不销毁旧 Mesh → 泄漏；
        // 多个不同配置的实例还会反复"抢占"重建。改为字典后每配置一份，不泄漏不抢占。
        private static readonly Dictionary<long, Mesh> _sharedMeshes = new Dictionary<long, Mesh>();

        /// 圆锥几何及其派生参数的缓存结构：
        /// 在 UpdateGeometry 时一次性刷新，ApplyShaderProperties 直接读取，避免每帧重复 Tan/Atan2/Cos/Sin
        private struct ConeGeometryCache
        {
            // 输入（用于脏检测）
            public float spotAngle;
            public float maxDistance;
            public float lightSourceRadius;
            public int sides;
            public int segments;

            // 派生输出
            public float radiusEnd;
            public float radiusStart;
            public float slopeAngle;
            public float cosSlope;
            public float sinSlope;

            public bool IsValid => sides > 0 && segments > 0;

            /// 当输入参数与缓存记录不一致时返回 false，调用方应 Refresh
            public bool Matches(Light light, float md, int s, int seg, float lsr)
            {
                return IsValid
                    && Mathf.Approximately(spotAngle, light.spotAngle)
                    && Mathf.Approximately(maxDistance, md)
                    && sides == s
                    && segments == seg
                    && Mathf.Approximately(lightSourceRadius, lsr);
            }

            /// 重新计算所有派生输出
            public void Refresh(Light light, float md, int s, int seg, float lsr)
            {
                spotAngle = light.spotAngle;
                maxDistance = md;
                lightSourceRadius = lsr;
                sides = s;
                segments = seg;

                radiusEnd = md * Mathf.Tan(spotAngle * 0.5f * Mathf.Deg2Rad);
                radiusStart = Mathf.Max(lsr, 0.001f);
                slopeAngle = Mathf.Atan2(radiusEnd - radiusStart, md);
                cosSlope = Mathf.Cos(slopeAngle);
                sinSlope = Mathf.Sin(slopeAngle);
            }
        }

        #endregion

        #region Shader属性ID（静态缓存，按用途分组）

        private static class ShaderIDs
        {
            // 颜色 / 强度
            public static readonly int VolumeColor         = Shader.PropertyToID("_VolumeColor");
            public static readonly int Intensity           = Shader.PropertyToID("_Intensity");
            public static readonly int VolumeExposure      = Shader.PropertyToID("_VolumeExposure");

            // 距离 / 衰减
            public static readonly int FallOffStart        = Shader.PropertyToID("_FallOffStart");
            public static readonly int FallOffEnd          = Shader.PropertyToID("_FallOffEnd");

            // 羽化 / 眩光
            public static readonly int EdgeFade            = Shader.PropertyToID("_EdgeFade");
            public static readonly int EndFade             = Shader.PropertyToID("_EndFade");
            public static readonly int GlareFrontal        = Shader.PropertyToID("_GlareFrontal");
            public static readonly int GlareBehind         = Shader.PropertyToID("_GlareBehind");

            // 锥体几何
            public static readonly int ConeRadiusStart     = Shader.PropertyToID("_ConeRadiusStart");
            public static readonly int ConeRadiusEnd       = Shader.PropertyToID("_ConeRadiusEnd");
            public static readonly int ConeSlopeCosSin     = Shader.PropertyToID("_ConeSlopeCosSin");

            // 起始增亮 / 中心渐变
            public static readonly int StartBoostIntensity = Shader.PropertyToID("_StartBoostIntensity");
            public static readonly int StartBoostRange     = Shader.PropertyToID("_StartBoostRange");
            public static readonly int CenterFade          = Shader.PropertyToID("_CenterFade");

            // 蒙版投影
            public static readonly int MaskTex             = Shader.PropertyToID("_MaskTex");
            public static readonly int MaskIntensity       = Shader.PropertyToID("_MaskIntensity");

            // 高频推送（每帧由 UpdateOcclusion 更新后单独 SetFloat）
            public static readonly int ClipDistance        = Shader.PropertyToID("_ClipDistance");

            // 渲染状态（深度测试方式）
            public static readonly int ZTest               = Shader.PropertyToID("_ZTest");
        }

        #endregion

        #region 生命周期

        void OnEnable()
        {
            // 重置所有非序列化运行时字段,防止 Instantiate/Ctrl+D 浅拷贝导致复制实例与原实例共享状态。
            // [v7.2] 浅拷贝保留引用 → 多盏灯共用同一 Material / Light / 子物体 → 参数互相覆盖 → 闪烁。
            // 这里置 null 后,EnsureMaterial 会为每盏灯 new 一份独立 material,EnsureVolumeChild 重新绑定本实例 child。
            // _coneCache / cached 字段(值类型或缓存)交给后续流程重建。
            _volumeChild = null;
            _meshFilter = null;
            _meshRenderer = null;
            _light = null;
            _material = null;
            _lastPushedClipDistance = float.PositiveInfinity;  // 强制复制灯第一次 Update 时一定推送 _ClipDistance 到 material

            // Animator 存在性只需在启用时检测一次即可，
            // 运行时切换 Animator 是罕见场景，如需要可在外部调用 GetComponent<Animator>() 后自行维护
            _hasAnimator = GetComponent<Animator>() != null;
            // 距离剔除状态重置:OnEnable 后让下次 Update 重新判断距离(防 stale culled=true)
            _isCulledByDistance = false;
            _cachedMainCamera = null;
            _skipDistanceCullOnce = true;  // [FIX v7.1.3] OnEnable 后第一帧不剔除,保证首帧可见
            _enableTime = Time.unscaledTime;  // [FIX v7.1.4] 启动宽限期基准时刻
            _framesSinceFullPush = 0;  // [FIX v7.1.3] 兜底计数器重置

            // [P0] 视锥剔除状态重置:OnEnable 后让下次 Update 重新判断视锥(防 stale culled=true)。
            _isCulledByFrustum = false;
            _cachedFrustumPlanes = null;
            _lastFrustumCacheTime = 0f;

            // [P1 迁移] 旧版 enableOcclusion=true 的资产,首次激活时自动升级到 ZTestAndFade。
            // 清零后下次保存序列化时只保留新字段,旧标志位不再重复保留(避免 inflation)。
            if (_legacyEnableOcclusion)
            {
                occlusionMode = VolumeOcclusionMode.ZTestAndFade;
                _legacyEnableOcclusion = false;
            }

            EnsureLight();
            EnsureVolumeChild();
            // [FIX v7.1] OnEnable 末尾强制完整推送(applyAllParams=true):
            //   - 旧版仅依赖 _materialParamsDirty=true 触发首推,在某些路径可能不推 → "场景摆放的灯不显示"
            //   - applyAllParams=true 保证 17 个参数 + 颜色 + 几何 + 蒙版全部走一遍
            // [v7.2] 此时 EnsureMaterial 已 new 独立 material,applyAllParams 把所有参数写入该 material 的 CBUFFER
            UpdateGeometry();
            UpdateMaterial(applyAllParams: true);
            // OnEnable 末尾确保 Renderer 启用(可能在 prefab 还原时为 false)
            if (_meshRenderer != null) _meshRenderer.enabled = true;

            if (Camera.main != null)
                Camera.main.depthTextureMode |= DepthTextureMode.Depth;
        }

        void OnDisable()
        {
            if (_volumeChild != null)
                _volumeChild.SetActive(false);
        }

        void Update()
        {
            // [FIX v7.1] 移除 v7.0 的 _needsPerFrameUpdate 静态守门(见字段定义处注释)。
            // [FIX v7.1.2] 增加距离剔除守门:摄像机距离 > 阈值 → 完全跳过 Update + 关闭 Renderer。
            //   - 性能收益:每盏远处灯省 EnsureLight + NeedsGeometryRebuild + UpdateOcclusion + 推送
            //   - 防闪烁:hysteresis 双阈值(进出错开 updateCullHysteresis 米),灯不会在边界反复切换
            //   - 退出剔除时强制完整推一次参数(防 stale 材质)
            if (ShouldCullByDistance()) return;

            if (!EnsureLight()) return;

            if (NeedsGeometryRebuild())
                UpdateGeometry();

            UpdateOcclusion();

            // 性能分支：
            //   有 Animator → 强制每帧全推（动画可能改变任意 Light 参数，必须保证 shader 完全同步）
            //   无 Animator 且参数 dirty → 推一次完整参数
            //   无 Animator 且无 dirty → 仅推高频参数（_ClipDistance），跳过 17 次冗余 SetFloat
            //
            // [FIX v7.1.3] 兜底:每 FULL_PUSH_INTERVAL_FRAMES(60)帧强制全推一次,修复任何未覆盖的
            //   stale 材质状态(Pipeline 切换、Domain Reload、外部脚本干扰等)。
            //   优先级最高——保证灯在长时间运行后仍保持正确显示。
            _framesSinceFullPush++;
            if (_framesSinceFullPush >= FULL_PUSH_INTERVAL_FRAMES)
            {
                UpdateMaterial(applyAllParams: true);
                _framesSinceFullPush = 0;
            }
            else if (_hasAnimator)
                UpdateMaterial(applyAllParams: true);
            else if (_materialParamsDirty || _cachedBlendMode != blendMode || _cachedZTestMode != zTestMode)
                UpdateMaterial();
            else
                PushHighFrequencyShaderParams();
        }

        void OnDestroy()
        {
            SafeDestroy(_volumeChild);
            // [v7.2] _material 是每盏灯独立实例,可安全销毁;HideAndDontSave 的运行时材质不会泄漏到资产。
            // 先断开 Renderer 引用再销毁,避免 Renderer 持空引用。
            if (_material != null)
            {
                SafeDestroy(_material);
                _material = null;
            }
            _meshRenderer = null;
        }

        void OnValidate()
        {
            maxDistance = Mathf.Max(0.1f, maxDistance);
            fallOffStart = Mathf.Clamp(fallOffStart, 0f, maxDistance - 0.01f);
            lightSourceRadius = Mathf.Max(0f, lightSourceRadius);

            // OnValidate 中修改参数 → 强制下次 UpdateMaterial 重新推送所有 shader 参数
            _materialParamsDirty = true;

            // [FIX v7.1.1] OnValidate 中**禁止**同步调用 UpdateGeometry/UpdateMaterial:
            //   - Prefab 编辑模式下,Inspector 改动会触发 __SpotLightVolumeMesh__ 的
            //     OnMeshFilterChanged / OnMeshRendererChanged 等 SendMessage 回调
            //   - OnValidate 上下文处于 Awake/CheckConsistency 阶段,SendMessage 被 Unity 拒绝
            //   - 同步调用还会因为修改子物体导致其他组件 OnEnable/OnDisable 链式触发 → 闪烁
            //   - 仅置 dirty 标志,Update() 会消费(ExecuteAlways 保证编辑器中 Update 每帧跑)
            //
            // 闪烁根因分析:
            //   旧方案(v7.1 同步调用)在 prefab 编辑模式下:
            //     1. 调 UpdateGeometry() → _meshFilter.sharedMesh = ... 触发 Unity 内部回调
            //     2. 调 UpdateMaterial() → _meshRenderer.sharedMaterial = ... 触发 OnMeshRendererChanged
            //     3. SendMessage 失败但部分状态被修改 → prefab 保存的状态不一致
            //     4. Apply 到 prefab → 实例化后 MPB/material 引用错位 → 闪烁
            //   新方案:OnValidate 仅置 dirty,Update 异步消费 → 无 SendMessage 风险
        }

        #endregion

        #region 核心逻辑

        /// [FIX v7.1] 已移除 UpdateNeedsPerFrameUpdate() —— v7.0 引入的静止守门被证实会引入
        /// 显示正确性 bug(OnValidate/OnEnable 时机错乱、Scene 切换丢状态、Inspector 切换
        /// enableOcclusion/colorFromLight 导致 _needsPerFrameUpdate 跳变)。
        /// Update 现在每帧跑轻量流程,dirty 跟踪 + 高频参数推送路径保持 v7.0 优化。

        /// Light 守卫：按需缓存 _light 引用，确保为 Spot 类型。
        /// 替代分散在多处的 `if (_light == null || _light.type != LightType.Spot)` 样板。
        private bool EnsureLight()
        {
            if (_light == null)
                _light = GetComponent<Light>();
            return _light != null && _light.type == LightType.Spot;
        }

        /// 距离剔除守门(v7.1.4 → v7.4 视锥剔除合并):按摄像机距离 + 视锥 LOD 跳过 Update。
        ///
        /// 三层防护(从外到内):
        ///   1. **编辑器模式跳过** —— [ExecuteAlways] 下编辑场景时不做剔除,
        ///      保护 prefab 编辑体验(prefab instance + 场景 MainCamera 距离 > 阈值时不再"消失")
        ///   2. **启动宽限期** —— OnEnable 后 CULL_GRACE_PERIOD(0.3s)秒内不剔除,
        ///      等 Camera.main 异步加载完成 + 玩家相机瞬移到初始位置稳定
        ///   3. **hysteresis 双阈值(仅距离轴)** —— 进入剔除比退出剔除门槛远一档 updateCullHysteresis,
        ///      灯在边界 ±hysteresis 米区间内状态稳定,不会因 1 帧抖动反复切显隐
        ///
        /// [P0 视锥剔除] 在距离剔除之上叠加视锥剔除:摄像机外的灯直接关闭 Renderer + 跳过 Update,
        ///   取消完整的 CPU/GPU 开销。视锥面与 Camera.main 共享 1s 缓存窗口,无额外开销。
        ///   视锥 cull 是 sharp in/out,通过 bounds 膨胀 0.5m 防 1 帧抖动闪烁。
        ///
        /// Camera.main 缓存 1 秒:避免每帧 Find 性能开销;null 时跳过剔除(保守)。
        /// 退出剔除时强制 dirty=true → 当帧 Update 完整推送 shader 参数(防 stale MPB)。
        private bool ShouldCullByDistance()
        {
            // [FIX v7.1.4] 第 1 层防护:编辑器模式跳过距离剔除
            //   - prefab 编辑器(Project 视图打开 prefab):Camera.main=null → 本来就不剔除 ✓
            //   - 场景中选中 prefab instance + 场景 MainCamera 存在:旧版会被距离剔除 → "灯不亮"
            //   - Scene 视图相机移动 / Game 视图相机切换 → 编辑器中不再闪烁
            //   - 运行时性能优化改由"每 60 帧兜底全推" + dirty 跟踪承担,无性能损失
            if (!Application.isPlaying) return false;

            // [FIX v7.1.4] 第 2 层防护:启动宽限期
            //   - OnEnable → 第 1 帧 _skipDistanceCullOnce 保护
            //   - 第 2~N 帧(width period 内)→ 全部不剔除,等玩家相机稳定 + Camera.main 加载完成
            //   - 宽限期结束后才进入正常 hysteresis 距离判断
            // 时间源用 Time.unscaledTime(与 _cachedMainCamera 一致),不受 timeScale=0 暂停影响
            if (Time.unscaledTime - _enableTime < CULL_GRACE_PERIOD) return false;

            // [FIX v7.1.3] 首帧保护(在 isPlaying + 宽限期内基本被上层吸收,但保留兜底)
            if (_skipDistanceCullOnce)
            {
                _skipDistanceCullOnce = false;
                // [P0] 退出任意 cull(距离 + 视锥):开启 Renderer,推完整参数防 stale
                if ((_isCulledByDistance || _isCulledByFrustum) && _meshRenderer != null)
                {
                    _meshRenderer.enabled = true;
                    _materialParamsDirty = true;
                }
                _isCulledByDistance = false;
                _isCulledByFrustum = false;
                return false;
            }

            // ===== 距离剔除计算 =====
            bool distanceCull = false;
            if (updateCullDistance > 0f)
            {
                // 缓存 Camera.main(1 秒刷新,避免每帧 Find 开销)
                // [FIX v7.1.3] 销毁检测:Unity 销毁 Camera 后引用变 FakeNull(== null 返回 true),
                // 但 transform 仍可访问但位置错 → 必须检测
                bool camCacheStale = _cachedMainCamera == null
                    || Time.unscaledTime - _lastMainCameraCacheTime > 1f;
                if (camCacheStale)
                {
                    _cachedMainCamera = Camera.main;
                    _lastMainCameraCacheTime = Time.unscaledTime;
                }

                Camera cam = _cachedMainCamera;
                if (cam != null && cam.transform != null)
                {
                    float dist = Vector3.Distance(cam.transform.position, transform.position);
                    if (_isCulledByDistance)
                    {
                        // 已在剔除 → 退出门槛(更近)才退出:dist < updateCullDistance - hysteresis
                        distanceCull = dist > updateCullDistance - updateCullHysteresis;
                    }
                    else
                    {
                        // 未在剔除 → 进入门槛(更远)才剔除:dist > updateCullDistance + hysteresis
                        distanceCull = dist > updateCullDistance + updateCullHysteresis;
                    }
                }
                // cam == null 时 distanceCull 保持 false(无主相机保守跳过距离剔除,与 v7.1.4 行为一致)
            }
            else
            {
                // updateCullDistance <= 0 → 距离剔除禁用
                if (_isCulledByDistance)
                {
                    _isCulledByDistance = false;
                    // 注意:_meshRenderer 在下方统一处理,这里只清状态
                }
            }

            // ===== [P0] 视锥剔除叠加 =====
            // 仅在 distanceCull 已 false 时检查视锥(优先按距离剔除跳过,免一次 frustum.planes 计算);
            // 距离无需剔除但视锥外时,仍关闭 Renderer 节省 GPU。
            // 视锥内时 _isCulledByFrustum 状态维持 false,不重置(避免远距离仍持续重算)。
            bool frustumCull = distanceCull ? false : ShouldCullByFrustum();

            // ===== 合并应用 =====
            bool prevAnyCull = _isCulledByDistance || _isCulledByFrustum;
            bool newAnyCull = distanceCull || frustumCull;

            _isCulledByDistance = distanceCull;
            _isCulledByFrustum = frustumCull;

            if (newAnyCull != prevAnyCull)
            {
                if (_meshRenderer != null)
                {
                    _meshRenderer.enabled = !newAnyCull;
                    if (!newAnyCull)
                    {
                        // 退出任意 cull → 强制 dirty 让下次 Update 推完整参数
                        _materialParamsDirty = true;
                    }
                }
            }

            return newAnyCull;
        }

        /// [P0] 视锥剔除辅助:用 _cachedMainCamera 渲染的视锥面测试 _meshRenderer.bounds。
        /// 1 秒缓存视锥面(与 Camera.main 缓存共用刷新窗口,无额外开销)。
        /// bounds 膨胀 0.5m 是为了避免锥体网格刚好穿过视锥平面时 TestPlanesAABB 在边界帧反复切显隐。
        /// 无主相机或 bounds 无效时保守返回 false(不剔除)。
        private bool ShouldCullByFrustum()
        {
            Camera cam = _cachedMainCamera;
            if (cam == null || cam.transform == null) return false;
            // _meshFilter.sharedMesh 是网格资产的真实来源(_meshRenderer.bounds 由它计算得出);
            // sharedMesh 不在 MeshRenderer 上,只在 MeshFilter 上。
            if (_meshRenderer == null || _meshFilter == null || _meshFilter.sharedMesh == null) return false;

            // 视锥面缓存(每 1 秒刷新一次,与 Camera.main 缓存同步)
            if (_cachedFrustumPlanes == null || Time.unscaledTime - _lastFrustumCacheTime > 1f)
            {
                _cachedFrustumPlanes = GeometryUtility.CalculateFrustumPlanes(cam);
                _lastFrustumCacheTime = Time.unscaledTime;
            }

            // bounds 微膨胀:锥体网格末端可能恰好压在视锥面上,膨胀 0.5m 让"边界"灯始终可见,
            // 避免 TestPlanesAABB 在边界帧因浮点误差切显隐(每盏每帧 0 额外开销,仅内存读 + 6 次平面求距离)。
            Bounds bounds = _meshRenderer.bounds;
            bounds.Expand(0.5f);

            // TestPlanesAABB 返回 true 表示 AABB 与视锥有交集(可见),所以"不可见"=!true
            return !GeometryUtility.TestPlanesAABB(_cachedFrustumPlanes, bounds);
        }

        private bool NeedsGeometryRebuild()
        {
            return !_coneCache.IsValid
                || !_coneCache.Matches(_light, maxDistance, coneSides, coneSegments, lightSourceRadius);
        }

        /// 更新几何体：使用归一化 Mesh + localScale 缩放；同时刷新派生缓存。
        private void UpdateGeometry()
        {
            if (!EnsureLight()) return;

            _coneCache.Refresh(_light, maxDistance, coneSides, coneSegments, lightSourceRadius);
            // 派生参数发生变化，下次 ApplyShaderProperties 需重新推送
            _materialParamsDirty = true;

            if (_meshFilter != null)
                _meshFilter.sharedMesh = GetSharedNormalizedMesh(coneSides, coneSegments);

            // localScale 控制锥体实际尺寸（归一化 Mesh: XY[-1,1], Z[0,1]）
            float maxRadius = Mathf.Max(_coneCache.radiusEnd, _coneCache.radiusStart);
            if (_volumeChild != null)
                _volumeChild.transform.localScale = new Vector3(maxRadius, maxRadius, maxDistance);
        }

        /// 材质刷新：
        /// 1. blendMode 变化时才切换 shader 关键字与 blend 状态（消除每帧 6 次无意义 Shader 调用）
        /// 2. 大部分参数走 dirty 推送（仅在 OnValidate/geometry 变更时才推）
        /// 3. _ClipDistance 是唯一高频参数（随射线检测更新），单独推送
        /// 4. _light.color 变化时仅刷新颜色通道
        /// <param name="applyAllParams">true 时跳过 dirty 检查强制全推（用于有 Animator 场景：
        /// 动画可能改变任何 Light 属性，必须每帧把全部 shader 参数同步一次）</param>
        private void UpdateMaterial(bool applyAllParams = false)
        {
            if (_meshRenderer == null) return;
            EnsureMaterial();
            if (_material == null) return;

            // 1. BlendMode keyword：仅在变化时切换(独立 material,直接 Enable/Disable)
            if (_cachedBlendMode != blendMode)
            {
                ApplyBlendMode();
                _cachedBlendMode = blendMode;
            }

            // 1b. ZTest 深度测试状态:仅在变化时写入(独立于 blend keyword)
            if (_cachedZTestMode != zTestMode)
            {
                _material.SetInt(ShaderIDs.ZTest, (int)zTestMode);
                _cachedZTestMode = zTestMode;
            }

            // 2. 整体参数：dirty 时或强制时全推
            if (applyAllParams || _materialParamsDirty)
            {
                ApplyShaderProperties();
                _materialParamsDirty = false;
                _lastColorFromLight = colorFromLight;
                _lastAppliedLightColor = colorFromLight ? _light.color : volumeColor;
            }
            // 3. colorFromLight 模式下，单通道跟踪灯光颜色变化
            else if (colorFromLight && _light.color != _lastAppliedLightColor)
            {
                _material.SetColor(ShaderIDs.VolumeColor, _light.color);
                _lastAppliedLightColor = _light.color;
            }

            // 4. 高频参数：每帧直接写各自独立 material,不再走 MPB
            _material.SetFloat(ShaderIDs.ClipDistance, _clipDistance);
        }

        /// 轻量更新：仅推送每帧/实时变化的参数。
        /// 用于无 Animator 且无 dirty 的场景，跳过 17 个静态参数的无意义 SetFloat 调用。
        /// dirty 跟踪 _clipDistance：仅在变化时写 material(独立实例,开销低且完全隔离)。
        /// 静态场景下 _clipDistance = -1f 不变,完全省去每帧的 SetFloat 开销。
        private void PushHighFrequencyShaderParams()
        {
            if (_clipDistance == _lastPushedClipDistance) return;
            _lastPushedClipDistance = _clipDistance;

            EnsureMaterial();
            if (_material == null) return;
            _material.SetFloat(ShaderIDs.ClipDistance, _clipDistance);
        }

        /// 强制完整刷新材质：让所有 dirty 缓存失效并立即推送所有 Shader 参数。
        /// 专用于外部代码（存档/读档、Undo/Redo）直接修改字段后调用。
        /// 正常 Inspector 调节字段会通过 OnValidate 自动标 dirty，无需调用此方法。
        public void RefreshMaterial()
        {
            // 失效所有 dirty 缓存，确保 UpdateMaterial 走"完整推送"分支
            _materialParamsDirty = true;
            _cachedBlendMode = (VolumeBlendMode)(-1);
            _cachedZTestMode = (UnityEngine.Rendering.CompareFunction)(-1);
            _lastColorFromLight = !colorFromLight;
            _lastAppliedLightColor = colorFromLight ? _light.color : volumeColor; // 后续 if 分支按当前状态记录，避免立即再次推

            // 立即推送完整参数（不等下次 Update tick）
            UpdateMaterial(applyAllParams: true);
        }

        /// 标记 dirty 并唤醒 Update：用于运行时通过代码修改字段后调用。
        /// 典型场景:
        ///   - 运行时修改 intensity/volumeColor 等 → 唤醒并推送一次
        ///   - 运行时切换 enableOcclusion/colorFromLight(任何方向)→ 由于 v7.1 已移除
        ///     静止守门,Update 始终每帧跑轻量流程,无需额外唤醒逻辑
        public void MarkDirty()
        {
            _materialParamsDirty = true;
            if (!isActiveAndEnabled) return;
            Update();
        }

        /// 射线检测：沿光柱方向发射单条射线，找到第一个碰撞物体并截断光柱。
        /// [FIX v7.1.3] 加 hysteresis 防 hit.distance 抖动:
        ///   角色边缘穿过光柱 → Raycast 可能在 hit/miss 或 hit 在两个相近距离间跳变 →
        ///   _clipDistance 高频变化 → shader 光柱长度视觉上"闪烁/抖"。
        ///   修复:仅当 hit.distance 显著变化(>0.2m)或 状态切换(hit↔miss)时更新 _clipDistance。
        ///   miss 时:必须连续 2 次 miss 才退出截断(防单次 miss 误判)。
        private void UpdateOcclusion()
        {
            // [P1] 仅 ZTestAndFade 模式才跑 CPU Raycast。其他模式(Disabled/ZTestOnly):
            //   - 不发射任何物理查询(零 CPU 开销)
            //   - _clipDistance 强制为 -1(让 shader 跳过 clipFade 计算)
            //   - 遮挡交由 URP 深度硬件在光栅化阶段处理(ZTest LEqual)
            if (occlusionMode != VolumeOcclusionMode.ZTestAndFade)
            {
                if (_clipDistance != -1f)
                {
                    _lastPushedClipDistance = float.PositiveInfinity;  // 强制下次推送 -1
                    _clipDistance = -1f;
                }
                _occlusionMissStreak = 0;
                return;
            }

            if (occlusionUpdateInterval > 0f && Time.time - _lastOcclusionCheckTime < occlusionUpdateInterval)
                return;
            _lastOcclusionCheckTime = Time.time;

            var triggerInteraction = occlusionDetectTriggers
                ? QueryTriggerInteraction.Collide
                : QueryTriggerInteraction.Ignore;

            bool rayHit = Physics.Raycast(transform.position, transform.forward, out RaycastHit hit,
                maxDistance, occlusionLayerMask, triggerInteraction);

            if (rayHit)
            {
                _occlusionMissStreak = 0;
                float newDist = hit.distance;
                if (_clipDistance < 0f)
                {
                    // 当前无截断 → 直接进入截断
                    _clipDistance = newDist;
                }
                else
                {
                    // 当前已截断 → 仅当距离变化显著(>0.2m)才更新,防抖动
                    if (Mathf.Abs(newDist - _clipDistance) > 0.2f)
                        _clipDistance = newDist;
                }
            }
            else
            {
                // miss:必须连续 miss 才退出截断(防单次 miss 误判,如角色边缘瞬间离开)
                _occlusionMissStreak++;
                if (_occlusionMissStreak >= 2 && _clipDistance >= 0f)
                {
                    _clipDistance = -1f;
                    _lastPushedClipDistance = float.PositiveInfinity;  // 强制下次推送 -1
                }
            }
        }

        #endregion

        #region 材质辅助

        private void EnsureMaterial()
        {
            // [v7.2] 每盏灯独立 material 实例:生命周期内稳定,参数直接写各自 CBUFFER,不与其他灯共享。
            if (_material != null) return;

            var shader = Shader.Find("Hidden/VicTools/SpotLightVolume");
            if (shader == null)
            {
                Debug.LogError("SpotLightVolume: 找不到Shader 'Hidden/VicTools/SpotLightVolume'");
                return;
            }

            _material = new Material(shader)
            {
                name = "SpotLightVolume_Instance",
                hideFlags = HideFlags.HideAndDontSave,
                renderQueue = 3000,
            };
            // 防御:_meshRenderer 正常由 OnEnable 的 EnsureVolumeChild 保证,但异常销毁路径下避免 NRE
            if (_meshRenderer != null)
                _meshRenderer.sharedMaterial = _material;
            // 新 material 的 keyword/blend/参数均未设置,强制下一次 UpdateMaterial 走完整推送
            _materialParamsDirty = true;
            _cachedBlendMode = (VolumeBlendMode)(-1);
            _cachedZTestMode = (UnityEngine.Rendering.CompareFunction)(-1);
            _lastColorFromLight = !colorFromLight;
        }

        /// [v7.2] 把 blendMode 对应的 keyword / blend state 应用到当前独立 material。
        /// shader 用 shader_feature_local,keyword 是 per-material,直接 Enable/Disable 即可。
        private void ApplyBlendMode()
        {
            if (_material == null) return;

            // 先清空三个互斥 blend keyword,再启用目标
            _material.DisableKeyword("_BLEND_ADDITIVE");
            _material.DisableKeyword("_BLEND_SOFTADD");
            _material.DisableKeyword("_BLEND_ALPHA");

            switch (blendMode)
            {
                case VolumeBlendMode.SoftAdditive:
                    _material.EnableKeyword("_BLEND_SOFTADD");
                    _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusDstColor);
                    _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    break;
                case VolumeBlendMode.Alpha:
                    _material.EnableKeyword("_BLEND_ALPHA");
                    _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    break;
                default: // Additive
                    _material.EnableKeyword("_BLEND_ADDITIVE");
                    _material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    _material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                    break;
            }
        }

        /// 推送所有非高频 shader 参数（颜色 / 光照 / 几何 / 蒙版）
        private void ApplyShaderProperties()
        {
            ApplyColorProperty();
            ApplyLightingProperties();
            ApplyGeometryProperties();
            ApplyMaskProperties();
        }

        private void ApplyColorProperty()
        {
            Color c = colorFromLight ? _light.color : volumeColor;
            _material.SetColor(ShaderIDs.VolumeColor, c);
            _lastAppliedLightColor = c;
            _lastColorFromLight = colorFromLight;
        }

        private void ApplyLightingProperties()
        {
            _material.SetFloat(ShaderIDs.Intensity, intensity);
            _material.SetFloat(ShaderIDs.VolumeExposure, volumeExposure);
            _material.SetFloat(ShaderIDs.StartBoostIntensity, startBoostIntensity);
            _material.SetFloat(ShaderIDs.StartBoostRange, startBoostRange);
            _material.SetFloat(ShaderIDs.CenterFade, centerFade);
            _material.SetFloat(ShaderIDs.GlareFrontal, glareFrontal);
            _material.SetFloat(ShaderIDs.GlareBehind, glareBehind);
        }

        private void ApplyGeometryProperties()
        {
            _material.SetFloat(ShaderIDs.FallOffStart, fallOffStart);
            _material.SetFloat(ShaderIDs.FallOffEnd, maxDistance);
            _material.SetFloat(ShaderIDs.EdgeFade, edgeFade);
            _material.SetFloat(ShaderIDs.EndFade, endFade);
            _material.SetFloat(ShaderIDs.ConeRadiusStart, _coneCache.radiusStart);
            _material.SetFloat(ShaderIDs.ConeRadiusEnd, _coneCache.radiusEnd);
            _material.SetVector(ShaderIDs.ConeSlopeCosSin,
                new Vector4(_coneCache.cosSlope, _coneCache.sinSlope, 0, 0));
        }

        private void ApplyMaskProperties()
        {
            if (enableMask && maskTexture != null)
            {
                _material.SetTexture(ShaderIDs.MaskTex, maskTexture);
                _material.SetFloat(ShaderIDs.MaskIntensity, maskIntensity);
            }
            else
            {
                _material.SetTexture(ShaderIDs.MaskTex, Texture2D.whiteTexture);
                _material.SetFloat(ShaderIDs.MaskIntensity, 0f);
            }
        }

        #endregion

        #region 几何体辅助

        /// 计算光锥末端半径：使用 ConeGeometryCache 缓存，避免每帧重复三角函数计算。
        /// 保持向后兼容（OnDrawGizmosSelected 仍调用此方法）。
        private float ComputeRadiusEnd()
        {
            if (_light == null) return 0f;
            if (!_coneCache.IsValid
                || !_coneCache.Matches(_light, maxDistance, coneSides, coneSegments, lightSourceRadius))
            {
                _coneCache.Refresh(_light, maxDistance, coneSides, coneSegments, lightSourceRadius);
            }
            return _coneCache.radiusEnd;
        }

        #endregion

        #region 子物体管理

        private void EnsureVolumeChild()
        {
            // 关键:验证 _volumeChild 是否真的是当前实例的子物体。
            // Unity 在 Instantiate/Ctrl+D 复制 GameObject 时会深拷贝子物体 hierarchy,但 _volumeChild 是
            // 运行时赋值的非序列化字段,Unity 不会自动重新绑定到新 hierarchy 中的子物体。
            // 如果直接信任 _volumeChild != null,可能导致多盏灯共用同一 MeshRenderer,
            // 互相写材质参数覆盖 → 中央区域渲染闪烁。
            if (_volumeChild != null && _volumeChild.transform.parent == transform)
            {
                _volumeChild.SetActive(true);
                CacheChildComponents();
                return;
            }

            // _volumeChild 字段可能是从其他灯浅拷贝过来的脏引用 → 丢弃,在当前 hierarchy 中查找或新建
            _volumeChild = null;

            Transform existing = transform.Find("__SpotLightVolumeMesh__");
            if (existing != null)
            {
                _volumeChild = existing.gameObject;
                // [FIX v7.1.1] 检测到旧版(v6.x / v7.0) prefab 序列化的 child(没有 HideAndDontSave 标记):
                //   - 旧版 child 被 prefab system 记录了 sharedMesh/sharedMaterial override
                //   - 复用会触发 OnMeshFilterChanged 路径,在某些时机会造成闪烁
                //   - 销毁并重建,彻底屏蔽 prefab 序列化影响
                if ((_volumeChild.hideFlags & HideFlags.HideAndDontSave) != HideFlags.HideAndDontSave)
                {
                    // 注意:SafeDestroy 用 DestroyImmediate 在编辑器中正确处理 prefab hierarchy
                    SafeDestroy(_volumeChild);
                    _volumeChild = null;
                    existing = null;
                }
                else
                {
                    _volumeChild.SetActive(true);
                    CacheChildComponents();
                    return;
                }
            }

            if (existing == null)
            {
                CreateVolumeChild();
            }
        }

        private void CacheChildComponents()
        {
            _meshFilter = _volumeChild.GetComponent<MeshFilter>();
            _meshRenderer = _volumeChild.GetComponent<MeshRenderer>();
        }

        private void CreateVolumeChild()
        {
            _volumeChild = new GameObject("__SpotLightVolumeMesh__");
            _volumeChild.transform.SetParent(transform, false);
            _volumeChild.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            _volumeChild.transform.localScale = Vector3.one;
            // [FIX v7.1.1] 关键修复:使用 HideAndDontSave 完全阻止 prefab 序列化 child GameObject。
            //   旧版 HideInHierarchy|NotEditable 仍会被 Unity prefab system 序列化 hierarchy:
            //     - MeshFilter.sharedMesh / MeshRenderer.sharedMaterial 被记录为 prefab override
            //     - Apply to Prefab 时把 mesh override 写回 prefab asset,污染共享资源
            //     - 加载其他 instance 时,Unity 还原 mesh override,OnEnable 期间我们的 UpdateGeometry
            //       又设了同样的 mesh,prefab system 检测到"override 应用"→ 触发 OnMeshFilterChanged
            //       → 内部 SendMessage 失败/状态错乱 → 闪烁
            //   HideAndDontSave 完全屏蔽 prefab 序列化 → child 是纯运行时对象 → 无 override → 无闪烁
            //   唯一副作用:Scene 视图/Inspector 看不到 child(本就该隐藏,符合预期)
            _volumeChild.hideFlags = HideFlags.HideAndDontSave;

            _meshFilter = _volumeChild.AddComponent<MeshFilter>();
            _meshRenderer = _volumeChild.AddComponent<MeshRenderer>();
            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
            // [FIX v7.1.1] child 上的组件也屏蔽 prefab 序列化(防止 MeshRenderer.material override 等被记录)
            _meshFilter.hideFlags = HideFlags.HideAndDontSave;
            _meshRenderer.hideFlags = HideFlags.HideAndDontSave;
        }

        #endregion

        #region Mesh生成（静态共享）

        private static Mesh GetSharedNormalizedMesh(int sides, int segments)
        {
            long key = ((long)sides << 32) | (uint)segments;
            if (_sharedMeshes.TryGetValue(key, out var mesh) && mesh != null)
                return mesh;

            mesh = GenerateNormalizedConeMesh(sides, segments);
            _sharedMeshes[key] = mesh;
            return mesh;
        }

        /// 生成归一化锥形 Mesh：XY 在 [-1,1]，Z 在 [0,1]
        /// 包含锥面 + 前 Cap(Z=0) + 后 Cap(Z=1)
        /// UV.x 标记：0=锥面, 1=cap
        private static Mesh GenerateNormalizedConeMesh(int sides, int segments)
        {
            int ringCount = segments + 2;
            int vertCountSides = sides * ringCount;
            int vertCountCap = sides + 1;
            int vertCountTotal = vertCountSides + vertCountCap * 2;

            var vertices = new Vector3[vertCountTotal];
            var uvs = new Vector2[vertCountTotal];

            // 锥面顶点 / UV
            BuildSideVertices(vertices, uvs, sides, ringCount, segments);

            // 前 Cap（Z=0）+ 后 Cap（Z=1）
            int frontCapStart = vertCountSides;
            BuildCapVertices(vertices, uvs, frontCapStart, sides, 0f);
            int backCapStart = frontCapStart + vertCountCap;
            BuildCapVertices(vertices, uvs, backCapStart, sides, 1f);

            // 三角形（保持与原版相同的索引序列以确保 Mesh 资产 bit-identical）
            var triangles = new int[sides * (segments + 1) * 6 + sides * 3 * 2];
            int tri = 0;
            tri = BuildSideTriangles(triangles, tri, sides, segments);
            tri = BuildCapTriangles(triangles, tri, frontCapStart, sides, forward: true);   // 前 Cap 正面朝 +Z
            tri = BuildCapTriangles(triangles, tri, backCapStart, sides, forward: false);   // 后 Cap 正面朝 -Z

            var mesh = new Mesh
            {
                name = "SpotLightVolume_SharedCone",
                vertices = vertices,
                uv = uvs,
                triangles = triangles
            };
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void BuildSideVertices(Vector3[] vertices, Vector2[] uvs, int sides, int ringCount, int segments)
        {
            for (int i = 0; i < sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                for (int seg = 0; seg < ringCount; seg++)
                {
                    float t = (float)seg / (segments + 1);
                    int idx = i + seg * sides;
                    vertices[idx] = new Vector3(cos, sin, t);
                    uvs[idx] = Vector2.zero;
                }
            }
        }

        private static int BuildSideTriangles(int[] triangles, int triStart, int sides, int segments)
        {
            int tri = triStart;
            for (int seg = 0; seg < segments + 1; seg++)
            {
                for (int i = 0; i < sides; i++)
                {
                    int current = seg * sides + i;
                    int next = seg * sides + (i + 1) % sides;
                    int currentUp = (seg + 1) * sides + i;
                    int nextUp = (seg + 1) * sides + (i + 1) % sides;

                    triangles[tri++] = current;
                    triangles[tri++] = next;
                    triangles[tri++] = currentUp;
                    triangles[tri++] = next;
                    triangles[tri++] = nextUp;
                    triangles[tri++] = currentUp;
                }
            }
            return tri;
        }

        private static void BuildCapVertices(Vector3[] vertices, Vector2[] uvs, int startIdx, int sides, float z)
        {
            vertices[startIdx] = new Vector3(0, 0, z);
            uvs[startIdx] = new Vector2(1, 0);
            for (int i = 0; i < sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides;
                vertices[startIdx + 1 + i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), z);
                uvs[startIdx + 1 + i] = new Vector2(1, 0);
            }
        }

        /// forward=true 朝 +Z（前 Cap），forward=false 朝 -Z（后 Cap）
        private static int BuildCapTriangles(int[] triangles, int triStart, int capStart, int sides, bool forward)
        {
            int tri = triStart;
            for (int i = 0; i < sides; i++)
            {
                if (forward)
                {
                    triangles[tri++] = capStart;
                    triangles[tri++] = capStart + 1 + i;
                    triangles[tri++] = capStart + 1 + (i + 1) % sides;
                }
                else
                {
                    triangles[tri++] = capStart;
                    triangles[tri++] = capStart + 1 + (i + 1) % sides;
                    triangles[tri++] = capStart + 1 + i;
                }
            }
            return tri;
        }

        #endregion

        #region 工具方法

        private static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(obj);
            else
#endif
                Destroy(obj);
        }

        #endregion

        #region Gizmos

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (_light == null || _light.type != LightType.Spot) return;

            Gizmos.matrix = transform.localToWorldMatrix;

            if (fallOffStart > 0)
            {
                Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
                float radiusStart = fallOffStart * Mathf.Tan(_light.spotAngle * 0.5f * Mathf.Deg2Rad);
                DrawWireCircle(Vector3.forward * fallOffStart, radiusStart, 16);
            }

            Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.4f);
            DrawWireCircle(Vector3.forward * maxDistance, ComputeRadiusEnd(), 16);
        }

        private static void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            for (int i = 0; i < segments; i++)
            {
                float a1 = 2f * Mathf.PI * i / segments;
                float a2 = 2f * Mathf.PI * (i + 1) / segments;
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1) * radius, 0);
                Vector3 p2 = center + new Vector3(Mathf.Cos(a2) * radius, Mathf.Sin(a2) * radius, 0);
                Gizmos.DrawLine(p1, p2);
            }
        }
#endif

        #endregion
    }
}
