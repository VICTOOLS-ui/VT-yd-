

（预弃用）

---

# ComputeBufferLight 自定义灯光系统 — 全量架构分析

## 一、文件清单总览

| 文件 | 平台 | 行数 | 职责 |
|------|------|------|------|
| `Runtime/Scripts/Shader/ComputeBuffer.cs` | Runtime | ~2200 | **核心**：`ComputeBufferLightManager` 管理器，CPU 端数据生产 |
| `Runtime/Shaders/PBR_Mobile_NEW.shader` | Runtime | ~1100 | 灯光计算的 GPU 载体（当前主用版本） |
| `Runtime/Shaders/PBR_Mobile.shader` | Runtime | - | 旧版 shader（兼容遗留场景） |
| `Runtime/Scripts/SpotLightVolume.cs` | Runtime | ~1200 | 聚光体积雾（独立功能，与 buffer 灯光并存） |
| `Editor/VicTools/ComputeBufferTool.cs` | Editor | ~1690 | 编辑器工具窗口（管理器 + 材质 + 灯光收集 UI） |
| `Editor/VicTools/ComputeBufferLightManagerEditor.cs` | Editor | - | 管理器的 Inspector 自定义绘制 |
| `Editor/VicTools/SpotLightVolumeEditor.cs` | Editor | - | 体积雾组件的 Inspector 绘制 |

> 说明：`SpotLightVolume.cs` 是「体积光柱雾」系统（`Hidden/VicTools/SpotLightVolume`），与 `ComputeBuffer` 的「逐像素点/聚光着色」是两套独立能力，只在「都属于自定义灯光」层面相关，下面重点分析前两者。

---

## 二、`ComputeBuffer.cs` — 核心管理器

### 2.1 类结构

```
ComputeBufferLightManager : MonoBehaviour
│
├── 数据结构（GPU buffer 元素格式，与 shader 一一对应）
│   ├── CustomPointLight   (line 37-48)
│   │     position(float3) / range(float) / color(float4) / intensity(float) / padding
│   └── CustomSpotLight    (line 50-58)
│         position / direction(float3) / color / range /
│         spotAngle(=cosOuter) / innerSpotAngle(=invCosDelta) / intensity
│
├── 序列化字段（Inspector 可见）
│   ├── 点光组：_usePointLight, _pointLightIntensity,
│   │           _pointLightRangeMultiplier, _pointLightFalloff, 回弹动画参数×4
│   ├── 聚光组：_useSpotLight, _spotLightIntensity, _spotLightRangeMultiplier,
│   │           _spotLightFalloff, _spotLightAmount, _useSpotTexture,
│   │           _spotTexture, _spotTextureContrast, _spotTextureIntensity, _spotTextureSize
│   └── 管理组：pointLights(List<Light>), spotLights(List<Light>),
│               targetMaterials(List<Material>), maxLights, autoFindMaterials
│
├── 私有运行态
│   ├── GPU 资源：_lightsBuffer, _spotLightsBuffer (GraphicsBuffer)
│   ├── 数据镜像：_lightsData[], _spotLightsData[], _currentLightCount, _currentSpotLightCount
│   ├── 参数缓存：_lastMaxLights, _lastUsePointLight...（脏检测用）
│   ├── 受控材质：_controlledMaterials(List<Material>)
│   └── 自适应状态：_lastCameraPos/Rotation, _cameraMotion, _lastUpdateTime, _frameCount
│
└── 方法分组
    ├── 生命周期：OnEnable / OnDisable / OnDestroy / OnValidate / InitializeComputeBuffer
    ├── 参数同步：CheckForParameterChanges / CacheCurrentParameters
    ├── 剔除收集：CollectVisibleLightIndices / IsLightInFrustum / CalculateViewportRadius
    ├── Buffer 更新：UpdateLightsBuffer / UpdateSpotLightsBuffer
    ├── 材质下发：SetMaterialParameters / UpdateAllMaterials / RemoveMaterial / ResetMaterialToDefaults
    ├── 场景收集：CollectScenePointLights / CollectSceneSpotLights / EditorFindPBRMobileMaterials
    ├── 自适应：ComputeCameraMotion / CalculateAdaptiveUpdateInterval / CalculateAdaptiveSpotLightUpdateInterval
    └── 资源释放：ReleaseBuffers / ReleaseControlledMaterials / CleanupUnmanagedSceneMaterials
```

### 2.2 各方法用途要点

| 方法 | 用途 / 技术点 |
|------|------|
| `InitializeComputeBuffer` | 按 `maxLights`/`_spotLightAmount` 创建 `GraphicsBuffer`（`GraphicsBuffer.Target.Structured`） |
| `CleanupUnmanagedSceneMaterials` | 启动期「自愈」：把不在受控列表却残留 `_USEPOINTLIGHT` 关键字的材质清理掉，防止变黑 |
| `CheckForParameterChanges` | 每 5 帧做一次参数脏检测，命中才触发 `UpdateAllMaterials`，避免每帧重复 `SetFloat`/`EnableKeyword` |
| `CollectVisibleLightIndices` | 视锥剔除 + 距离剔除 + **按距离升序排序**（近灯优先进入上限） |
| `IsLightInFrustum` | 灯位置的 6 平面视锥测试 |
| `CalculateViewportRadius` | 按灯光 range 换算屏幕半径，用于距离 LOD 剔除 |
| `UpdateLightsBuffer` | 核心：剔除→排序→截断 `maxLights`→填充 `_lightsData`（**预计算 cosOuter/invCosDelta**）→`SetData`→`Shader.SetGlobalBuffer`→`SetGlobalInt` |
| `UpdateSpotLightsBuffer` | 同上，聚光上限固定 `_spotLightAmount` |
| `ComputeCameraMotion` | 累计相机位移/旋转，驱动自适应刷新 |
| `CalculateAdaptiveUpdateInterval` | 相机静止时拉长更新间隔（省 CPU），移动时立即刷新 |
| `SetMaterialParameters` | 单材质全量下发：3 个关键字 + 强度/范围/衰减/数量/纹理/对比等全部数值 |
| `UpdateAllMaterials` | 遍历受控材质调用 `SetMaterialParameters` |
| `StartBounceAnimation` | 协程驱动点光回弹动画（运行时动态效果） |

---

## 三、`PBR_Mobile_NEW.shader` — GPU 端

### 3.1 结构

```
Shader "Custom/PBR_Mobile_NEW"
├── Properties（材质属性：_PointLightAmount, _SpotTexture, _UsePointlight ...）
├── 宏与常量
│   ├── SHADER_API_MOBILE 下  MAX_POINT_LIGHTS = _PointLightAmount（运行时材质属性）
│   └── 非移动端             MAX_POINT_LIGHTS = 8（编译期常量）
├── 全局缓冲（与 CPU 结构体字节对齐）
│   ├── StructuredBuffer<CustomPointLight> _CustomPointLights;
│   ├── int _CustomPointLightCount;
│   ├── StructuredBuffer<CustomSpotLight> _CustomSpotLights;
│   ├── int _CustomSpotLightCount;
│   └── float _CustomLightSystemActive;   // 安全位
├── 工具函数：fastPow / GGX 高光
├── 光照函数
│   ├── CalculateCustomPointLights(...)   // 点光漫反射 + 高光
│   └── CalculateCustomSpotLights(...)    // 聚光 + 光斑纹理投影
├── vert：输出 positionWS / normalWS / UV
└── frag：主光照 + 自定义点光 + 自定义聚光 叠加
```

### 3.2 关键技术点（shader 侧）

1. **循环上限双重钳制**：`lightCount = min(_CustomPointLightCount, MAX_POINT_LIGHTS)`，CPU 端 `maxLights` 和 shader 端 `_PointLightAmount` 双重卡死。

2. **用「饱和距离比」替代 `continue` 提前退出**（`623-630`）：
   ```hlsl
   half distanceRatio = saturate(distance / effectiveRange);
   half attenuation = saturate(1.0 - fastPow(max(distanceRatio, 0.01), _PointLightFalloff));
   ```
   超范围时 `distanceRatio=1 → attenuation=0`，消除动态分支，规避移动端某些 GPU 驱动对 `[loop]+continue` 组合的兼容问题。

3. **CPU 预计算三角函数**：聚光的 `cosOuter`、`invCosDelta` 在 C# 端算好存入 buffer，shader 里免去每像素 `cos`/除法。

4. **光斑纹理投影**（聚光）：用 `normalize/cross` 建立灯光局部正交基 `(lightRight, lightUp)`，把表面方向投影到 2D UV 采样 `_SpotTexture`，模拟窗格/遮光板光斑。

5. **关键字变体**：`_USEPOINTLIGHT` / `_USESPOTLIGHT` / `_USESPOTTEXTURE` 三个 `shader_feature`，未启用的变体整段被裁剪。

---

## 四、渲染过程（完整数据流）

### 4.1 CPU 端每帧流水线（`Update` → `UpdateLightsBuffer`）

```
Update()
 ├─ 每 5 帧：CheckForParameterChanges()  → 参数变了才 UpdateAllMaterials()
 ├─ 自适应：ComputeCameraMotion() → 决定本帧是否刷新 buffer
 └─ 命中刷新时：
      UpdateLightsBuffer()
        ├─ CollectVisibleLightIndices()       // 视锥剔除 + 距离剔除
        ├─ 按相机距离升序排序                  // 近灯优先
        ├─ 截断到 maxLights                    // 上限截断
        ├─ 填充 _lightsData[]（含预计算 cosOuter/invCosDelta）
        ├─ _lightsBuffer.SetData(_lightsData, 0, 0, count)
        ├─ Shader.SetGlobalBuffer("_CustomPointLights", _lightsBuffer)
        └─ Shader.SetGlobalInt("_CustomPointLightCount", count)

      UpdateSpotLightsBuffer()   // 同流程，上限 _spotLightAmount
```

### 4.2 GPU 端渲染（fragment）

```
frag(input)
 ├─ if (_CustomLightSystemActive > 0.5)      // 全局安全位
 ├─ 计算主光 / 环境光 / GI
 ├─ #ifdef _USEPOINTLIGHT
 │    for i in 0..min(_CustomPointLightCount, _PointLightAmount):
 │        CalculateCustomPointLights → 漫反射(NdotL) + GGX 高光
 ├─ #ifdef _USESPOTLIGHT
 │    for i in 0..min(_CustomSpotLightCount, 2):
 │        CalculateCustomSpotLights → 锥形衰减 + (#ifdef _USESPOTTEXTURE) 光斑纹理
 └─ 输出最终颜色
```

### 4.3 端到端数据契约

```
C# CustomPointLight 结构体  ←字节对齐→  Shader CustomPointLight 结构体
    ↓ SetData                              ↑ StructuredBuffer
GraphicsBuffer  ──SetGlobalBuffer──→  全局 Shader 缓冲
    ↓ SetGlobalInt                       ↑ _CustomPointLightCount
C# maxLights / 剔除结果  ──→  Shader 循环次数
    ↓ SetMaterialParameters              ↑ 材质属性 _PointLightAmount 等
C# 管理器参数  ──关键字+数值──→  材质变体 + 强度/衰减系数
```

---

## 五、技术要点汇总（架构亮点与代价）

**亮点**
1. **CPU 侧多级裁剪**：视锥剔除 → 距离剔除 → 距离排序 → 上限截断，最小化传给 GPU 的灯数。
2. **CPU 预计算**：把 `cos`/除法等移到 C#，GPU 每像素省三角函数。
3. **参数脏检测 + 自适应刷新**：静止相机不重复 `SetData`/`SetFloat`，省 CPU。
4. **`_CustomLightSystemActive` 安全位**：防止材质被移出管理后残留关键字导致画面变黑。
5. **饱和衰减替代分支**：对移动端 GPU 分支不友好的规避。

**代价（即前几轮定位的性能问题）**
1. **逐像素 `float` 精度 ALU**——灯光函数全程 fp32，移动端 fp16 吞吐减半（已在前几轮做 half 化）。
2. **`[loop]` 动态循环**：上限是运行时材质属性，编译器无法展开，移动端动态循环效率低。
3. **聚光光斑纹理逐像素采样**：带宽开销大。
4. **CPU 端虽然做了剔除，但若未开启视锥/距离剔除，所有灯始终 active**，`_CustomPointLightCount` 恒为上限值。

---

需要我针对其中某一部分（比如 `ComputeBufferLightManagerEditor.cs` 的 Inspector 布局、或 `UpdateLightsBuffer` 的剔除细节、或自适应更新算法）再深入展开吗？