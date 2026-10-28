# 渲染路径 Forward+ 分析

**`PBR_Mobile_NEW.shader` 当前不支持 Forward+。** 项目所用 URP 版本为 14.0.12（Unity 2022.3），对照其标准 shader 与库实现，该 shader 存在三处不兼容。

## 核心证据

**1. 缺少 `_FORWARD_PLUS` 关键字变体声明**

URP 所有标准 shader 都显式声明了该关键字，例如 `Lit.shader` 第 147 行：

```147:147:Library/PackageCache/com.unity.render-pipelines.universal@14.0.12/Shaders/Lit.shader
            #pragma multi_compile _ _FORWARD_PLUS
```

而 `PBR_Mobile_NEW.shader` 只声明了附加光关键字，没有 `_FORWARD_PLUS`：

```185:185:Runtime/Shaders/PBR_Mobile_NEW.shader
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
```

`Core.hlsl` 里 `USE_FORWARD_PLUS` 与 `_ADDITIONAL_LIGHTS` 都依赖 `_FORWARD_PLUS` 才生效：

```8:14:Library/PackageCache/com.unity.render-pipelines.universal@14.0.12/ShaderLibrary/Core.hlsl
#if defined(_FORWARD_PLUS)
#define _ADDITIONAL_LIGHTS 1
#undef _ADDITIONAL_LIGHTS_VERTEX
#define USE_FORWARD_PLUS 1
#else
#define USE_FORWARD_PLUS 0
#endif
```

缺少声明 → 该 shader 不会编译出 Forward+ 变体，`USE_FORWARD_PLUS` 恒为 0。

**2. 附加光循环写法不兼容 Forward+ 的 cluster 剔除**

当前附加光用 `GetAdditionalLightsCount()` + 手写 `for` 遍历：

```844:851:Runtime/Shaders/PBR_Mobile_NEW.shader
                {
                    int additionalLightCount = GetAdditionalLightsCount();
                    for (int lightIndex = 0; lightIndex < additionalLightCount; ++lightIndex)
                    {
                        Light additionalLight = GetAdditionalLight(lightIndex, input.positionWS_shadow.xyz);
                        finalColor += CalculateRealtimeLight(additionalLight, mat, viewDirWS);
                    }
                }
```

而 URP 库中 `GetAdditionalLightsCount()` 在 Forward+ 下**恒返回 0**：

```296:307:Library/PackageCache/com.unity.render-pipelines.universal@14.0.12/ShaderLibrary/RealtimeLights.hlsl
int GetAdditionalLightsCount()
{
#if USE_FORWARD_PLUS
    // Counting the number of lights in clustered requires traversing the bit list, and is not needed up front.
    return 0;
#else
    ...
    return int(min(_AdditionalLightsCount.x, unity_LightData.y));
#endif
}
```

Forward+ 必须走 `LIGHT_LOOP_BEGIN`/`LIGHT_LOOP_END` 宏（内部用 `ClusterInit`/`ClusterNext` 做 tile/cluster 光源遍历）：

```35:42:Library/PackageCache/com.unity.render-pipelines.universal@14.0.12/ShaderLibrary/RealtimeLights.hlsl
#if USE_FORWARD_PLUS
    #define LIGHT_LOOP_BEGIN(lightCount) { \
    uint lightIndex; \
    ClusterIterator _urp_internal_clusterIterator = ClusterInit(inputData.normalizedScreenSpaceUV, inputData.positionWS, 0); \
    [loop] while (ClusterNext(_urp_internal_clusterIterator, lightIndex)) { \
        lightIndex += URP_FP_DIRECTIONAL_LIGHTS_COUNT; \
```

**3. 缺少 `InputData` 与 `normalizedScreenSpaceUV` 构造**

上面的 `LIGHT_LOOP_BEGIN` 宏依赖 `inputData.normalizedScreenSpaceUV` 和 `inputData.positionWS`，而当前 shader 既没构造 `InputData`，也没调用 `GetNormalizedScreenSpaceUV(input.positionCS)`。`InputData` 结构体定义在：

```40:52:Library/PackageCache/com.unity.render-pipelines.universal@14.0.12/ShaderLibrary/Input.hlsl
struct InputData
{
    float3  positionWS;
    float4  positionCS;
    float3  normalWS;
    half3   viewDirectionWS;
    float4  shadowCoord;
    half    fogCoord;
    half3   vertexLighting;
    half3   bakedGI;
    float2  normalizedScreenSpaceUV;
    half4   shadowMask;
    half3x3 tangentToWorld;
```

## 实际表现

若直接把管线切到 Forward+：

- 主光、阴影、GI、烘焙、反射、自发光、雾等**仍正常**（这些用的是与路径无关的 `GetMainLight`/`SampleSH`/`SampleLightmap`/`MixFog` 等 API）。
- 实时附加光（点光/聚光）**失效或异常**：因没有 `_FORWARD_PLUS` 变体，shader 以非 Forward+ 路径运行，`GetAdditionalLightsCount()` 读 `_AdditionalLightsCount.x`，而 Forward+ 下 URP 不再填充 per-object 附加光数据，结果不可预期（大概率不亮）。

## 改造要点（如需支持）

1. 在 Forward Pass 增加 `#pragma multi_compile _ _FORWARD_PLUS`。
2. 在 `frag` 内构造 `InputData`（重点填 `positionWS`、`positionCS`、`normalWS`、`viewDirectionWS`、`normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS)`）。
3. 把附加光循环改为：

```hlsl
LIGHT_LOOP_BEGIN(additionalLightCount)
    Light additionalLight = GetAdditionalLight(lightIndex, inputData.positionWS);
    finalColor += CalculateRealtimeLight(additionalLight, mat, viewDirWS);
LIGHT_LOOP_END
```

（若还想接收 Forward+ 下的附加方向光，需照 `Lighting.hlsl` 第 306-322 行的写法，先单独循环 `min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS)` 处理方向光。）





## Forward 与 Forward+ 双方机制对比

结合这个 shader 的定位（`PBR_Mobile`，移动端为主）与 URP 14.0.12 的实现，**在典型移动端场景下（1 主光 + 少量点光/聚光，通常 ≤4 个），当前 Forward（per-object 附加光）性能更优**；Forward+ 的优势只在高光源数量（几十~几百）且桌面/主机平台上才会体现，而这两点与本 shader 的定位相反。

| 维度            | 当前版本（Forward / per-object 附加光）                      | Forward+                                                |
| --------------- | ------------------------------------------------------------ | ------------------------------------------------------- |
| 附加光遍历上限  | `min(_AdditionalLightsCount.x, unity_LightData.y)`，`_AdditionalLightsCount.x` = Per Object Limit（默认 **4**） | 桌面最多 **256**，移动端仅 **16~32**（见下）            |
| 光源循环方式    | 固定 `for` 循环，循环次数已知且短                            | `ClusterInit`/`ClusterNext` 遍历 tile bit-list          |
| 额外预处理 Pass | **无**                                                       | **有**：构建 ZBin + Tile 数据（额外 compute/渲染 pass） |
| 额外显存/带宽   | 仅 per-object uniform 数组（小）                             | ZBin 16KB + Tile 16~64KB，且有额外读写带宽              |
| 屏幕空间剔除    | 无（靠 CPU 侧 per-object 排序）                              | 有（tile/cluster 剔除）                                 |
| CPU 开销        | `SetupPerObjectLightIndices` 逐物体排序取最近 4 盏           | 一次性构建 cluster，per-object 开销小                   |

关键常量（本项目 URP 14 配置）：

```10:12:Library/PackageCache/com.unity.render-pipelines.universal-config@14.0.10/Runtime/ShaderConfig.cs.hlsl
#define MAX_VISIBLE_LIGHT_COUNT_LOW_END_MOBILE (16)
#define MAX_VISIBLE_LIGHT_COUNT_MOBILE (32)
#define MAX_VISIBLE_LIGHT_COUNT_DESKTOP (256)
```

```24:37:Library/PackageCache/com.unity.render-pipelines.universal@14.0.12/ShaderLibrary/Input.hlsl
#define MAX_ZBIN_VEC4S 1024
#if MAX_VISIBLE_LIGHTS <= 16
    #define MAX_LIGHTS_PER_TILE 32
    #define MAX_TILE_VEC4S 1024
    ...
#else
    #define MAX_LIGHTS_PER_TILE MAX_VISIBLE_LIGHTS
    #define MAX_TILE_VEC4S 4096
    ...
```

## 分场景分析

**1. 移动端 + 少光源（本 shader 的目标场景）→ 当前版本更优**
- 当前版本每个像素只循环 ≤4 盏点光/聚光，循环体短、无剔除前置开销。
- Forward+ 在移动端 `MAX_VISIBLE_LIGHTS` 仅 16~32，本身上限就不高；却要额外付出一整套 cluster 构建 pass + 16KB ZBin + 16KB Tile 的显存与带宽，属于纯增量开销。
- 更关键：移动端 GPU 多为 **tile-based 架构**（Adreno/Mali/Apple GPU），硬件本身已具备 tile 级光源剔除能力，Forward+ 的软件 cluster 剔除与之冗余，收益被稀释。

**2. 桌面/主机 + 大量光源 → Forward+ 更优**
- 光源数到几十~几百时，per-object 4 盏的硬上限会让当前版本丢失大量光照；Forward+ 的 256 上限 + 屏幕空间剔除能明显降低逐像素循环量，此时 cluster 预处理的固定开销可被覆盖。

**3. 每光源计算量两者基本相当**
- 当前版本的 `CalculateRealtimeLight` 与 Forward+ 下的单光源 GGX 计算量接近，差异不在"单个光源算得快慢"，而在"遍历多少盏灯 + 是否有多余的剔除前置开销"。

## 补充两点

- 当前 shader 的附加光循环是无条件执行的（未用 `#ifdef _ADDITIONAL_LIGHTS` 包裹），实际对应 URP 的 **Per Pixel 附加光** 模式；在 `_ADDITIONAL_LIGHTS_VERTEX` 模式下 `_AdditionalLightsCount.x` 为 0，循环空转且顶点光也未计算。所以当前版本本质上只完整支持 per-pixel 附加光这一条路径。
- 若项目确实以移动端为主，**不建议**为了 Forward+ 而改造；上一轮提到的"补 `_FORWARD_PLUS` + 改 `LIGHT_LOOP_BEGIN`"更多是为了保证在桌面切换 Forward+ 时点光/聚光不失效（正确性），而非为了移动端性能提升。

