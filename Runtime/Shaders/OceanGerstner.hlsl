// 文件名: OceanGerstner.hlsl
// 版本: 1.0
// 日期: 2026-10-12
// 说明: Custom_Ocean 海面波涛顶点位移模块。
//      参考 NVIDIA GPU Gems Chapter 1 "Effective Water Simulation from Physical Models"
//      (Gerstner 波模型 + 解析法线 / 切线 叉积)。
//      通过 multi_compile 关键字 _GerstnerQuality_Low/Medium/High
//      在编译期选择叠加的 Gerstner 波数量，免去运行时分支发散开销。
// 管线: 通用渲染管线 (URP) - HLSL
// =============================================================================

#ifndef OCEAN_GERSTNER_INCLUDED
#define OCEAN_GERSTNER_INCLUDED

// -----------------------------------------------------------------------------
// 精度档位（编译期常量，由 Custom_Ocean.shader 的 keyword 选择）
// -----------------------------------------------------------------------------
// LOW    : 4  组波（远景 / 移动端）
// MEDIUM : 8  组波（中景）
// HIGH   : 16 组波（近景特写 / 影视级）

#if defined(_GerstnerQuality_High)
    #define OCEAN_GERSTNER_WAVE_COUNT 16
#elif defined(_GerstnerQuality_Medium)
    #define OCEAN_GERSTNER_WAVE_COUNT 8
#elif defined(_GerstnerQuality_Low)
    #define OCEAN_GERSTNER_WAVE_COUNT 4
#else
    // 未设置关键字时默认中等（与 [KeywordEnum(Low,Medium,High)] 默认索引一致）
    #define OCEAN_GERSTNER_WAVE_COUNT 8
#endif

// -----------------------------------------------------------------------------
// 预定义波参数（每组波占一个 float4）
//   .x = 方向 X（cos(角度)）
//   .y = 方向 Y（sin(角度)）
//   .z = 振幅 A（米）
//   .w = 波数 k = 2π / λ（弧度/米）
// 选取规则：
//   方向按 0~360° 均匀分布 + 轻微抖动，避免形成方向条带；
//   振幅 / 波长按 1/f 关系设置（长波振幅大），符合真实海浪频谱近似；
//   共用一个全局速度比例 _GerstnerSpeed 缩放相位，避免每波单独维护 _Time.y。
// -----------------------------------------------------------------------------
static const float4 _GerstnerWaveParams[OCEAN_GERSTNER_WAVE_COUNT] =
{
    // INDEX | 方向角(°) | 振幅 A | 波长 λ → k = 2π/λ
    float4( 0.9659,  0.2588, 0.420,  0.110 ),  // 0 : 15°
    float4(-0.2588,  0.9659, 0.350,  0.160 ),  // 1 : 105°
    float4( 0.7071, -0.7071, 0.270,  0.220 ),  // 2 : -45°
    float4(-0.8660, -0.5000, 0.200,  0.300 ),  // 3 : 210°
    #if OCEAN_GERSTNER_WAVE_COUNT >= 8
    float4( 0.5000,  0.8660, 0.150,  0.410 ),  // 4 : 60°
    float4(-0.9659, -0.2588, 0.110,  0.560 ),  // 5 : 195°
    float4( 0.2588, -0.9659, 0.080,  0.760 ),  // 6 : -75°
    float4(-0.5000,  0.8660, 0.060,  1.030 ),  // 7 : 120°
    #endif
    #if OCEAN_GERSTNER_WAVE_COUNT >= 16
    float4( 0.8660,  0.5000, 0.045,  1.320 ),  // 8 : 30°
    float4(-0.7071,  0.7071, 0.035,  1.700 ),  // 9 : 135°
    float4( 0.0000,  1.0000, 0.028,  2.180 ),  // 10: 90°
    float4( 1.0000,  0.0000, 0.022,  2.790 ),  // 11: 0°
    float4( 0.4226, -0.9063, 0.018,  3.420 ),  // 12: -65°
    float4(-0.9063, -0.4226, 0.014,  4.150 ),  // 13: 205°
    float4( 0.6428,  0.7660, 0.011,  5.010 ),  // 14: 50°
    float4(-0.1736,  0.9848, 0.009,  6.030 ),  // 15: 100°
    #endif
};

// -----------------------------------------------------------------------------
// 单组 Gerstner 波的位移 / 切线 / 副切线分量
//   参考 GPU Gems Ch.1 公式 (1)~(9)：
//     x = x0 + Q·A·Dx·cos(k·D·P − ω·t)
//     z = z0 + Q·A·Dz·cos(k·D·P − ω·t)
//     y =       A  ·sin(k·D·P − ω·t)
//     T.x = 1 − Q·A·k·Dx·Dx·sin(φ)            ← 切线（dP/dx）
//     T.y = −A·k·Dx·cos(φ)
//     T.z = −Q·A·k·Dx·Dy·sin(φ)
//     B.x = −Q·A·k·Dx·Dy·sin(φ)              ← 副切线（dP/dz）
//     B.y = −A·k·Dy·cos(φ)
//     B.z = 1 − Q·A·k·Dy·Dy·sin(φ)
//   同时输出 Jacobian 的两个对角线元素（T.x 与 B.z），后续主入口累加用于浪尖泡沫。
//   重力加速度 g = 9.81，色散相位 ω = sqrt(g·k)。
// -----------------------------------------------------------------------------
void GerstnerWave_Sample(
    float2 positionXZ,        // 静止 XZ 坐标（顶点）
    float  time,              // _Time.y × _GerstnerSpeed
    float  Q,                 // 陡度（0 = 纯垂直，1 = 极限陡峭）
    float4 wave,              // (Dx, Dy, A, k)
    out float3 offset,        // 位移（叠加到 vertex.positionOS）
    out float3 tangentDir,    // 切线 T = dP/dx
    out float3 binormalDir)   // 副切线 B = dP/dz
{
    float2 D    = wave.xy;                 // 方向
    float  A    = wave.z;                  // 振幅
    float  k    = wave.w;                  // 波数
    float  wlen = dot(D, positionXZ);      // k·(D·P)
    float  phi  = time * sqrt(9.81 * k);   // ω·t,  ω = sqrt(g·k)
    float  c    = cos(wlen - phi);
    float  s    = sin(wlen - phi);

    // 位移（GPU Gems Eq. 1~3）
    offset = float3(Q * A * D.x * c,
                    A * s,
                    Q * A * D.y * c);

    // 切线 / 副切线（GPU Gems Eq. 8 / 9）
    // 缩放因子：WA = Q·A·k；DxDx = D.x·D.x 等
    float WA   = Q * A * k;
    float DxDx = D.x * D.x;
    float DxDy = D.x * D.y;
    float DyDy = D.y * D.y;
    float sWA  = WA * s;       // 公共的 sin·Q·A·k

    tangentDir  = float3(1.0 - sWA * DxDx,    // T.x = 1 - Q·A·k·Dx·Dx·sin
                        -A * k * D.x * c,     // T.y = -A·k·Dx·cos
                        -sWA * DxDy);          // T.z = -Q·A·k·Dx·Dy·sin
    binormalDir = float3(-sWA * DxDy,         // B.x = -Q·A·k·Dx·Dy·sin
                        -A * k * D.y * c,     // B.y = -A·k·Dy·cos
                         1.0 - sWA * DyDy);    // B.z = 1 - Q·A·k·Dy·Dy·sin
}

// -----------------------------------------------------------------------------
// Gerstner 波叠加核心循环（一次采样）
//   给定 time，输出一帧完整的波场状态：
//     offset : 累加位移（x,y,z）
//     T / B  : 切线 / 副切线（已经扣掉 (1,0,0) / (0,0,1) 的基础项，只剩"扰动"）
//     yPos   : 累加高度（用于 heightMask 过滤浪尖）
//   这样写是为了让主入口能跑两次（当前 + 延迟）→ 浪花"残留 / 延迟消失"。
// -----------------------------------------------------------------------------
void GerstnerWave_Accumulate(
    float2 positionXZ,
    float  time,
    float  Q,
    float  amplitudeScale,
    float  waveSize,
    out float3 offset,
    out float3 T,
    out float3 B,
    out float  yPos)
{
    // 累加器：T、B 用一组基础切线起步
    // 关键：每波 t.x 包含「基础 1」（GPU Gems Ch.1 Eq. 8 第一个分量），
    //   若直接 T += t，T.x = 1 + N - Σ(扰动) → 量级 ~N（8 波时 ~8），
    //   算出的 det(J) = (1 + N)² ≈ 80 → (1 − detJ)·crestSharpness = −160 → saturate = 0，泡沫全黑。
    // 正确做法：T += t - (1,0,0)，B += b - (0,0,1)，只累加「扰动项」，让 T/B 围绕单位基微调。
    T = float3(1.0, 0.0, 0.0);
    B = float3(0.0, 0.0, 1.0);
    offset = 0.0;
    yPos   = 0.0;

    UNITY_UNROLL
    for (int i = 0; i < OCEAN_GERSTNER_WAVE_COUNT; ++i)
    {
        float4 wave = _GerstnerWaveParams[i];
        wave.z *= amplitudeScale;   // 全局振幅缩放
        wave.w *= waveSize;         // 全局波形大小缩放

        float3 off, t, b;
        GerstnerWave_Sample(positionXZ, time, Q, wave, off, t, b);
        offset += off;
        yPos   += off.y;            // 高度 = 位移的 y 分量

        // 累加切线 / 副切线的「扰动项」→ 用于解析法线 + Jacobian
        T += float3(t.x - 1.0, t.y, t.z);
        B += float3(b.x,       b.y, b.z - 1.0);
    }
}

// -----------------------------------------------------------------------------
// 从 (T, B, yPos) 反推浪尖泡沫 mask
//   浪尖泡沫算法（GPU Gems Ch.1 P.20 Eq. 16~18 的精简版，性价比最高）：
//   detJ = T.x·B.z − T.z·B.x   ← 高度场在 (x,z) 平面上的 Jacobian 行列式
//   detJ → 1 : 海面平坦、面积守恒
//   detJ → 0 : 波峰被「卷起来」、面积压缩 → 该处产生白浪
//   detJ < 0 : 折叠（cusp），物理上不会真发生（Q 过大才出现）
//   所以：rawFoam = saturate((1 − detJ) · sharpness)
//   再乘 heightMask = smoothstep(0, threshold, yPos) 滤掉波谷（detJ 也接近 0，但 yPos < 0 → 不是浪尖）
//   最后用便宜的 hash 让浪尖边缘破碎 → 视觉上更像「飞溅的浪花」而不是一块均匀白斑
// -----------------------------------------------------------------------------
// 廉价 spatial-temporal hash（无 sin/cos 库依赖，2D 输入 → 0~1 输出）
//   frac(sin(dot(p, k)) * 43758.5453) 是 ShaderToy / GPU Gems 都用的经典 1-line hash
//   输入 cell 坐标（已经 floor 过），输出该 cell 的伪随机值
float Gerstner_Hash21(float2 p)
{
    return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
}

half GerstnerWave_CrestMaskFrom(
    float3 T, float3 B, float yPos,
    float2 positionXZ, float time,
    half  crestSharpness,        // 0~1, 内部展开 sharpness(1~8) + focus(1~3) 为单旋钮
    half  crestDetail)           // 0~1, 内部展开 noiseScale(1~8) + sprayDensity(0.2~0.8) 为单旋钮
{
    // T / B 已在 Accumulate 阶段累加"扰动项"，基础项 (1,0,0)/(0,0,1) 隐含在外层的 T.x / B.z
    float detJ = T.x * B.z - T.z * B.x;

    // 锐度展开：sharpness 控制 detJ 阈值（斜率），focus 控制 pow 形状（塑形）
    //   旧版需要 2 个独立参数；合并为 0~1 单一旋钮
    //   内部: sharp = 1 + 7*s (1~8), focus = 1 + 2*s (1~3)
    //   s=0 → 弱锐度（浪花广覆盖）    / s=1 → 强锐度（仅最高峰）
    float sharp = 1.0 + 7.0 * (float)crestSharpness;
    float focus = 1.0 + 2.0 * (float)crestSharpness;

    // 物理意义：1 - detJ ≈ 面积压缩量；>0 → 产生白浪
    // sharp 较小（≤ 3）时，rawFoam 在 detJ 中等位置保持低值 → 只有"卷得最厉害"的浪尖才到 1
    float rawFoam = saturate((1.0 - detJ) * sharp);

    // 形状塑形：pow 重新塑形曲线
    //   focus > 1.0 → 曲线在低值区压扁、高值区陡峭 → mask 集中在浪尖，远处快速消失
    //   例: focus=2, rawFoam=0.3 → 0.09（远低于阈值，颗粒门控直接过滤）
    //   max(0.25, ...) 防止 0 幂次把曲线压成常数 1
    rawFoam = pow(rawFoam, max(0.25, focus));

    // 高度 mask：固定 smoothstep(0, 0.15) 软渐变（无色阶硬度参数）
    half heightMask = smoothstep((half)0.0, (half)0.15, (half)yPos);

    // ============================================================
    // 细节展开：noiseScale 控制颗粒大小，sprayDensity 控制颗粒概率
    //   旧版需要 2 个独立参数；合并为 0~1 单一旋钮
    //   内部: noiseScale = 1 + 7*d (1~8, cell 物理大小 1~0.125m), density = 0.2 + 0.6*d (0.2~0.8)
    //   d=0 → 粗大稀疏颗粒（块状）   / d=1 → 细密颗粒（飞沫感）
    // ============================================================
    float noiseScale = 1.0 + 7.0 * (float)crestDetail;
    half  sprayDensity = (half)lerp(0.2, 0.8, (float)crestDetail);

    // 飞沫颗粒门控（解决"面积大、不像浪花、闪烁"的核心）
    //   在浪尖区域内用 spatial-temporal hash 把 mask 切成不规则颗粒，
    //   只有 hash > (1 - density) 的 cell 才有 mask → 浪花变成"颗粒集合"
    //   （GPU Gems Ch.1 / UE4 Ocean / Unity Crest 的标准做法）
    //   时间漂移：cell 坐标 = pos*scale + time*drift，drift 在两个轴不同 → 颗粒缓慢斜向漂移
    //   → 用 smoothstep(1-d, 1, hash) 让 cell 边缘有渐入渐出（缓解闪烁）
    float2 cellId = floor(positionXZ * noiseScale + time * float2(0.45, 0.30));
    float  cellHash = Gerstner_Hash21(cellId);
    half   sprayMask = (half)smoothstep(
        (half)1.0 - sprayDensity,
        (half)1.0,
        (half)cellHash);

    // 飞沫自身的"内部纹理"：cell 内仍用 hash 做小幅调制，避免颗粒"完全实心"
    //   比例 0.7~1.0，让颗粒中心亮、边缘暗（更像泡沫气泡的高光感）
    float innerHash = Gerstner_Hash21(cellId + float2(13.7, 41.3));
    half innerMod  = (half)lerp(0.7, 1.0, innerHash);

    // ============================================================
    // 颗粒生命周期调制（解决"闪现"）
    //   问题：cellId 用 floor 离散，cell 边界瞬时跨越 → 颗粒"啪"地出现/消失
    //   当前 smoothstep(1-d, 1, hash) 只软化"哪些 cell 入选"，不软化"颗粒本身的显隐时机"
    //   解决：给每个 cell 独立的"生命相位" cellPhase，在 [0, 1] 周期内淡入淡出
    //   cellAge = frac(time * 0.4 + cellPhase)  → 周期 2.5s，每个 cell 错相循环
    //   软门：cellAge ∈ [0, 0.25] 淡入；[0.75, 1.0] 淡出；中间 0.5 区段满强度
    //   → 颗粒永远从 0 渐变到 1 再渐变到 0，肉眼看不到"闪现"
    //   副作用：颗粒永远不会完全稳定（一直在漂移亮度），但周期 2.5s 视觉上等同真实浪花
    //   不用新增 Property：cycleSpeed=0.4 / fadeIn=0.25 / fadeOut=0.25 写死为常量
    //   pass-1 / pass-2（crestLife 残留）独立 cellId → 残留也自动获得淡入淡出
    // ============================================================
    float cellPhase = Gerstner_Hash21(cellId + float2(31.3, 7.7));
    float cellAge   = frac(time * 0.4 + cellPhase);
    half  lifeFade  = (half)(smoothstep(0.0, 0.25, cellAge) * smoothstep(1.0, 0.75, cellAge));

    return (half)(rawFoam * heightMask * sprayMask * innerMod * lifeFade);
}

// -----------------------------------------------------------------------------
// 主入口：把多组 Gerstner 波叠加为顶点位移、法线、浪尖泡沫（带延迟消散）
//   positionOffset : 累加位移 → vert 里叠加到 positionOS
//   normalWS       : 解析法线（B × T，已归一化）→ 供 PBR 高光 / 反射复用
//   crestMask      : 浪尖白色泡沫 mask（0=无泡沫, 1=满泡沫）
//   waveSize       : 全局波形大小缩放（直接乘 k，<1 大波, >1 小波）
//   amplitudeScale : 全局振幅缩放
//   choppy         : 横向位移系数 Q（尖锐度）
//   crestSharpness : 浪花锐度 0~1（合并了旧 sharpness 阈值 + focus 形状塑形为单旋钮）
//                    0 = 广覆盖、弱锐度；1 = 仅最高峰、强锐度
//                    内部: sharp=1+7s, focus=1+2s
//   crestDetail    : 浪花飞沫细节 0~1（合并了旧 sprayDensity 颗粒概率 + noiseScale 颗粒大小为单旋钮）
//                    0 = 粗大稀疏（块状）；1 = 细密（飞沫感）
//                    内部: noiseScale=1+7d, density=0.2+0.6d
//   crestLife      : 浪花寿命 0~1（合并了旧 delay 残留时间 + decay 残留强度为单旋钮）
//                    0 = 瞬时（无残留）；1 = 1.5s 强延迟
//                    内部: delay=1.5*l, decay=1-0.6*l
//
// 浪花"延迟消失"算法（无 RenderTexture / 无 per-pixel state / 完全纯 ALU）：
//   跑两次 Gerstner 循环：
//     Pass-1 (现在): 用于 positionOffset / normalWS / crestNow
//     Pass-2 (τ 秒前): 同样跑 Gerstner，但 time 减 τ → 模拟"τ 秒前"的波形；
//                       在该时刻触发浪花的位置（=当前 τ 秒后会到达这里）会再出一份 crestPast
//   合成：crestMask = max(crestNow, crestPast × decay)
//     → 浪花"挂在原地"约 τ 秒后逐渐消失；
//     → 没有 per-frame state、没有 RT、没有 temporal AA 依赖；
//     → 唯一代价：vert 多跑一次循环（4~16 次 sin/cos，开销远低于一次 mipmap fetch）。
// -----------------------------------------------------------------------------
void GerstnerWave_Displace(
    float2 positionXZ,
    float  time,
    float  amplitudeScale,
    float  choppy,
    float  waveSize,
    half   crestSharpness,    // 0~1, 单旋钮（内部展开 sharpness+focus）
    half   crestDetail,       // 0~1, 单旋钮（内部展开 noiseScale+density）
    half   crestLife,         // 0~1, 单旋钮（内部展开 delay+decay）
    out float3 positionOffset,
    out float3 normalWS,
    out half   crestMask)
{
    // 寿命展开：delay 决定残留时间，decay 决定残留强度（自然反向相关）
    //   旧版 delay/decay 独立调参心智负担大（注释也写"τ 越大应越小"）
    //   合并为 0~1 单一旋钮：delay=1.5*l, decay=1-0.6*l
    //   l=0 → delay=0, decay=1（瞬时）
    //   l=1 → delay=1.5s, decay=0.4（强残留）
    float delay = 1.5 * (float)crestLife;
    half  decay = (half)lerp(1.0, 0.4, (float)crestLife);

    // Pass 1: 当前时刻 — 决定 positionOffset / normalWS / 当前浪尖
    float3 T, B, off;
    float  yPos;
    GerstnerWave_Accumulate(positionXZ, time, choppy, amplitudeScale, waveSize, off, T, B, yPos);
    positionOffset = off;
    normalWS = normalize(cross(B, T));
    half crestNow = GerstnerWave_CrestMaskFrom(T, B, yPos, positionXZ, time, crestSharpness, crestDetail);

    // Pass 2: τ 秒前 — 浪花"残留 / 延迟消散"
    //   同样跑 Gerstner 但 time 减 τ → 相当于采样"τ 秒前"的波形
    //   那一时刻的浪花所在位置（=现在的当前位置，τ 秒后浪已经传走）会再出一次 mask
    //   注意：delay ≤ 0 时直接跳过二次循环（life=0 自动满足）
    half crestPast = (half)0.0;
    if (delay > 0.001)
    {
        float3 Tp, Bp, offP;
        float  yPosP;
        GerstnerWave_Accumulate(positionXZ, time - delay, choppy, amplitudeScale, waveSize, offP, Tp, Bp, yPosP);
        crestPast = GerstnerWave_CrestMaskFrom(Tp, Bp, yPosP, positionXZ, time - delay, crestSharpness, crestDetail);
    }

    // max(now, past × decay) → 残留叠加，但残留不会比当前更强
    crestMask = max(crestNow, crestPast * decay);
}

#endif // OCEAN_GERSTNER_INCLUDED
