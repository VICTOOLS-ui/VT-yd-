# SpotLightVolume 体积光工具 分析

## 一、文件结构清单

```
Packages/com.youdoo.victools/
├── Runtime/
│   ├── Scripts/
│   │   └── SpotLightVolume.cs          主组件 MonoBehaviour(1203 行,59.6 KB)
│   ├── Resources/Shaders/
│   │   ├── SpotLightVolume.shader      Shader 封装入口(单 Pass,73 行)
│   │   └── SpotLightVolumeCore.hlsl    核心着色逻辑(vert + frag + Ray-Cone,322 行)
│   └── Presets/SpotLightVolume/
│       ├── 激光.json / 激光-2.json / 路灯.json / 绿色光束.json / 阳光模拟.json   内置预设
│       └── *.meta                      (5 份)
└── Editor/
    └── VicTools/
        ├── SpotLightVolumeEditor.cs    自定义 Inspector + 存档/读档/预设系统
        └── SpotLightVolume.png         组件图标
```

> 注:`SpotLightVolumeMigrationTool.cs` 已不存在(历史遗留,当前工程中已删除)。

---

## 二、各文件结构、用途与技术要点

### 1. `SpotLightVolume.cs` — 主组件

**职责**:锥形体积光柱的完整 CPU 端驱动(几何生成、材质参数管理、性能剔除、遮挡检测)。

结构按 `#region` 划分:

| 区域 | 内容 |
|------|------|
| 公开参数 | 距离控制、羽化、外观、混合方式、**ZTest 深度测试**、Mesh 质量、射线遮挡、蒙版投影、后处理优化、性能剔除(约 20 个序列化字段) |
| 内部状态 | `_light/_material/_meshFilter/_meshRenderer/_volumeChild`、`ConeGeometryCache`、各类 dirty 缓存、距离剔除状态 |
| ShaderIDs | 静态 `PropertyToID` 缓存(避免每帧字符串查找) |
| 生命周期 | OnEnable/OnDisable/Update/OnDestroy/OnValidate |
| 核心逻辑 | `EnsureLight`、`ShouldCullByDistance`、`UpdateGeometry`、`UpdateMaterial`、`PushHighFrequencyShaderParams`、`UpdateOcclusion` |
| 材质辅助 | `EnsureMaterial`、`ApplyBlendMode`、`ApplyShaderProperties`(按颜色/光照/几何/蒙版分组) |
| 子物体管理 | `EnsureVolumeChild`、`CreateVolumeChild`(运行时动态挂 MeshFilter+MeshRenderer 子物体) |
| Mesh 生成 | 静态共享字典 `_sharedMeshes`,`GenerateNormalizedConeMesh` + BuildSide/BuildCap* |
| 工具/Gizmos | `SafeDestroy`、`OnDrawGizmosSelected` |

**关键技术要点**:
- `[ExecuteAlways]` + `[RequireComponent(typeof(Light))]` + `[DisallowMultipleComponent]`
- **每灯独立 material**(`new Material` + `HideAndDontSave`),参数直接写各自 `UnityPerMaterial` CBUFFER,与 SRP Batcher 兼容,根除共享材质闪烁
- **dirty 跟踪 + 高低频参数分离**:17 个静态参数仅在 dirty 时推送;`_ClipDistance` 为高频参数单独推送;`colorFromLight` 模式下单独跟踪灯光颜色
- **距离剔除 LOD**:hysteresis 双阈值 + 0.3s 启动宽限期 + 编辑器模式跳过,`Camera.main` 缓存 1s
- **射线遮挡**:`Physics.Raycast` 沿光柱 forward 检测,hit 距离变化 >0.2m 才更新、miss 连续 2 次才退出(hysteresis 防闪烁)
- **60 帧兜底全推**:防 stale 材质状态

### 2. `SpotLightVolume.shader` — Shader 入口

**用途**:定义渲染状态与关键字变体,承载 HLSL 代码。

| 项 | 值 |
|----|----|
| 名称 | `Hidden/VicTools/SpotLightVolume` |
| Queue | `Transparent`(renderQueue=3000) |
| Blend | `[_SrcBlend] [_DstBlend]`(由 C# 动态写入) |
| 深度 | `ZWrite Off`、`Cull Off`、`ZTest [_ZTest]`(v7.3 可配置) |
| 关键字 | `_BLEND_ADDITIVE/_BLEND_SOFTADD/_BLEND_ALPHA`(互斥),均为 `shader_feature_local` |

属性除 `_VolumeColor/_Intensity` 等核心参数外,还有 3 个 `[HideInInspector]` 渲染状态属性(`_SrcBlend/_DstBlend/_ZTest`),由组件驱动。

### 3. `SpotLightVolumeCore.hlsl` — 核心着色

**用途**:顶点变换 + 片段级 Ray-Cone 体积光积分。

- `CBUFFER_START(UnityPerMaterial)`:所有 per-material 属性(SRP Batcher 兼容)
- `rayConeIntersect()`:解析求交(起始 cap / 末端 cap / 侧面,Inigo Quilez 算法)
- `vert()`:归一化 Mesh → 实际尺寸还原
- `frag()`:深度采样 + 单步积分 + 多层衰减

### 4. `SpotLightVolumeEditor.cs` — 自定义 Inspector

**用途**:逐字段手动绘制面板(`DrawSection` 分组),提供存档/读档/预设按钮,内置 `SpotLightVolumeData` 序列化类做 JSON 存取(含向后兼容兜底)。

### 5. 预设 JSON

`SpotLightVolumeData` 的 `JsonUtility` 输出,含光柱参数 + 灯光 `spotAngle/innerSpotAngle/lightColor`,存于 `Packages/.../Presets/`(只读),存档写到 `Library/VicTools/SpotLightVolume/`。

---

## 三、渲染过程(完整链路)

### CPU 端:初始化 → 每帧更新

```
OnEnable:
  EnsureLight()                    // 校验 Spot Light
  EnsureVolumeChild()              // 找/建 "__SpotLightVolumeMesh__" 子物体(HideAndDontSave)
    └─ 挂 MeshFilter + MeshRenderer(关闭阴影/探针)
  UpdateGeometry()                 // 计算半径/斜率 → 分配共享 Mesh → 设 localScale
  UpdateMaterial(applyAllParams)   // EnsureMaterial(new 独立 Material) + 全量推送参数
  Camera.main.depthTextureMode |= Depth   // 开启深度纹理

Update (每帧):
  ShouldCullByDistance()?  return  // 距离 LOD 剔除(跳过一切)
  EnsureLight()
  NeedsGeometryRebuild()? UpdateGeometry()
  UpdateOcclusion()                // Raycast → 更新 _ClipDistance
  分支:
    有 Animator / dirty / 60帧兜底 → UpdateMaterial(全推或 dirty 推)
    否则 → PushHighFrequencyShaderParams()(仅推 _ClipDistance)
```

### GPU 端:vert → frag

**顶点阶段** `vert()`:
1. 归一化顶点(XY∈[-1,1], Z∈[0,1])按 cap/侧面分支缩放,乘以 `scaleOS=(radiusEnd, radiusEnd, fallOffEnd)` 还原实际尺寸
2. 计算世界坐标 → 裁剪坐标、物体空间相机位置、屏幕坐标、眼空间深度 `eyeDepth`

**片段阶段** `frag()`:
1. **深度采样**:`SampleSceneDepth` → `LinearEyeDepth` 得到 `sceneEyeDepth`
2. **Ray-Cone 求交**:`rayConeIntersect` 得射线进入光柱的 `tIn`
3. **硬件深度测试**(v7.3):`ZTest LessEqual` 在光栅化阶段提前剔除被墙/角色遮挡的片段(减少 overdraw)
4. **软件深度截断兜底**:若 `sceneEyeDepth < eyeDepth`,按比例缩短 `tOut`
5. **`clip(tOut - tIn)`** 丢弃无效区域
6. **单步积分**(STEPS=1):在光柱中心采样一次,累乘多层衰减:
   - 距离衰减(`attenLinear²`)、末端羽化、径向羽化(`radialFade`)
   - 中心高亮渐变、蒙版纹理采样(可选)、射线截断羽化(可选)
7. **起始增亮** + **曝光系数** → `finalIntensity`
8. **正面/背面眩光**(`facingLight/facingAway` 加权)
9. **软饱和**(可选):`x/(1+x)` Reinhard 压缩
10. **输出**:按混合模式(Additive/SoftAdditive/Alpha)计算 RGB/Alpha

### 混合与合批

- 三种混合模式通过 **shader keyword + 动态 blend state**(`_SrcBlend/_DstBlend`)实现,在 `ApplyBlendMode` 中仅变化时切换
- 所有材质属性在 `UnityPerMaterial` CBUFFER,SRP Batcher 正常启用;每灯独立 material 保证参数完全隔离、绝对稳定

---

## 四、核心性能策略小结

| 策略 | 位置 | 收益 |
|------|------|------|
| 归一化 Mesh 共享缓存 | `_sharedMeshes` 字典 | 同配置灯只建一次 Mesh |
| 高低频参数分离 + dirty 跟踪 | `UpdateMaterial` / `PushHighFrequencyShaderParams` | 静止时每帧几乎 0 次 `SetFloat` |
| `PropertyToID` 静态缓存 | `ShaderIDs` | 免字符串查找 |
| 每灯独立 material | `EnsureMaterial` | SRP Batcher 兼容 + 绝对稳定 |
| ZTest LessEqual | shader + `zTestMode` | 硬件提前剔除被遮挡片段,降 overdraw |
| 距离 LOD + 遮挡节流 + Camera 缓存 | `ShouldCullByDistance` / `UpdateOcclusion` | 远处灯跳过全部 Update |
| 三角函数缓存 | `ConeGeometryCache` | 免每帧 Tan/Atan2/Cos/Sin |

需要我针对其中某一部分(如 Ray-Cone 求交的数学推导、或 dirty 推送的完整调用链)再展开说明吗?