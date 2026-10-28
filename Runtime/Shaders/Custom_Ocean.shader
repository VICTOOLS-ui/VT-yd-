// Custom_Ocean 3.1 添加浪尖浪花效果相关参数，添加可控高光，分离岸边浪花使用文理Tiling值，添加接收阴影功能。
// 高光法线贴图 _BumpMap：XY/A 通道（由 UnpackNormalScale 解析）做高光 normal，R 通道被岸边波浪复用（裁出浪花）
// 岸边波浪使用 _WavePatternTilingX/Y 独立 Tiling，与 _BumpMap_ST 解耦
// Custom_Ocean 3.0 海面波涛：Gerstner 顶点位移（参考 NVIDIA GPU Gems Ch.1 "Effective Water Simulation"）
//   - 新增可开关 _GERSTNER_ON + 三档精度 KeywordEnum(Low/Medium/High)：编译期选 4/8/16 组波，零运行时分支
//   - 顶点位移：sin/cos 叠加求和后在 OS 空间叠加到 positionOS，再走 TransformObjectToHClip；默认关闭，渲染零变化
//   - 解析法线：用各波切线/副切线叉积构造，免去逐顶点 normalize(扰动后法线) 的额外成本
//   - 性能要点：位移只在顶点着色器算一次；法线同步传到 Varyings，可供未来 PBR 高光/反射复用而不重算
// Custom_Ocean 2.0 性能优化重构（渲染结果与原版保持一致，无可察觉差异）：
//   1. 片元中的 SampleSceneDepth 由 6 次降为 5 次 —— 中心点的水深已由 depthOffset 得到，直接复用，不再重复采样
//   2. 顶点不再走 GetVertexPositionInputs（会额外算 positionWS/positionVS/positionNDC），改用等价的 TransformObjectToHClip
//   3. 已 Tiling 的泡沫 UV（input.uv * _FoamDetailMap_ST.xy）只算一次，供水色斑驳/泡沫两层共用
//   4. _FoamDetailStrength = 0 时整段泡沫跳过（此时泡沫形状恒为 0），_FoamDistortStrength = 0 时跳过两处差分采样；
//      二者都是 Uniform 常量分支，所有像素走同一路径，无分支发散开销
//   5. 合并冗余运算：移除 saturate(step(...))、saturate+smoothstep 合一、提取公共子表达式
// Custom_Ocean 1.11 浪花颜色 _WaveColor 的 A 通道作为浪花不透明度：控制浪花混入水色的强度 + 浪花处不降低水面 alpha
// Custom_Ocean 1.10 泡沫扭曲的扭曲源由「波浪贴图」改为「水色斑驳的大尺度层输出」（低频、柔和、随水团流动）
// Custom_Ocean 1.9 波浪均匀化：条纹坐标改用「屏幕空间十字 5 点平均 + 线性」的水深（_WaveFalloff 只管强度），
//                 泡沫扭曲改为 U/V 双向梯度（两轴等权、均值 0），修复各方向拉扯不匀
// Custom_Ocean 1.7 ForwardLit Pass 接入 URP 雾，处理方式对齐 PBR_Mobile_NEW：multi_compile_fog + 顶点 ComputeFogFactor 存入 Varyings.fogFactor + 片元末尾 MixFog，不新增任何雾参数
// 适配 URP，使用 SampleSceneDepth 与 LinearEyeDepth，单 Pass 性能优化

Shader "Custom/Custom_Ocean"
{
    Properties
    {
        // [Header(Global)]
        // 全局开关：是否计算主光阴影
        //   关闭 = 跳过 shadowMap 采样 / GetMainLight / 整条 shadow 应用路径（性能优先 / 调试期）
        //   启用 = 计算阴影，最终暗度由 _ShadowStrength 控制（0=不暗 1=完全）
        //   与 _ShadowStrength 的区别：开关控"算不算"，强度控"算完后暗多少"
        [Toggle(_RECEIVE_SHADOWS)] _ReceiveShadows("接收阴影", Float) = 0
        // 主光接收强度：让水面颜色随主光方向（NoL）+ 主光 Intensity 连续变化
        //   0 = 完全不接收（与原版一致，水面纯 unlit）
        //   1 = 完全接收（Int=0 时整片黑；Int=1 时为 half-lambert；Int>1 时按比例提亮）
        //   用 Rec.709 luminance 取 mainLight.color 的亮度而非 RGB——避免给水面强加太阳色调
        //   与 _ReceiveShadows 解耦：接收主光 ≠ 接收主光阴影
        _MainLightStrength("主光接收强度 (0=无 1=完全)", Range(0.0, 1.0)) = 1.0

        [Header(Depth Gradient)]
        _DepthShallowColor("Shallow Color (浅水颜色)", Color) = (0.32, 0.72, 0.88, 0.80)
        _DepthDeepColor("Deep Color (深水颜色)", Color) = (0.05, 0.25, 0.45, 0.95)
        _DepthMaxDistance("Depth Max Distance (深度最大距离)", Float) = 1.0
        _WaterColorVariation("Water Color Variation (水色斑驳强度)", Range(0.0, 1.0)) = 0.4
        _WaterVariationScale("Water Variation Scale (斑驳密度，相对泡沫 Tiling)", Float) = 0.5
        _WaterVariationSpeed("Water Variation Speed (斑驳扰动速度)", Float) = 0.02

        [Header(Stylized Wave)]
        // 岸边波浪图案：复用 _BumpMap 的 R 通道（与高光法线贴图共用一张纹理，节省一张采样）
        //   R 通道值与 _WaveCutOff 比较裁出浪花
        //   Tiling 由 _WavePatternTilingX/Y 独立控制，与 _BumpMap_ST 完全解耦
        _WavePatternTilingX("Wave Pattern Tiling X (岸边波浪 X 密度)", Float) = 1.0
        _WavePatternTilingY("Wave Pattern Tiling Y (岸边波浪 Y 密度)", Float) = 1.0
        _WaveColor("Wave Color (浪花颜色)", Color) = (1.0, 1.0, 1.0, 0.90)
        _WaveMaxDistance("Wave Max Distance (浪花最大距离)", Float) = 1.0
        _WaveFalloff("Wave Falloff (波浪外扩，越小越宽)", Range(0.2, 4.0)) = 1.6
        _WaveSpeed("Wave Speed (波浪速度)", Float) = 0.4
        _WaveStrength("Wave Strength (波浪强度)", Float) = 13
        _WaveCutOff("Wave Cutoff (波浪裁切)", Range(0.0, 1.0)) = 0.5

        // [Header(Specular)]
        // 主光阴影接收强度：水面像素被场景物体（船、桥、岸上建筑等）遮挡时整体变暗
        //   影响：所有色相（深度渐变 / 浪花 / 泡沫 / 浪尖）+ 高光 同步衰减
        //   0 = 完全不接收阴影；1 = 完全接收（与 shadowMap 采样一致）
        //   依赖：URP Renderer 启用 Main Light Shadow + 至少一个投射阴影的光源
        _ShadowStrength("Shadow Strength (主光阴影接收 0=无 1=完全)", Range(0.0, 1.0)) = 0.4
        // 高光法线贴图：TS→WS 变换后参与高光 normal（XY 通道由 UnpackNormalScale 解析）
        //   R 通道同时被岸边波浪复用（裁出浪花，共用一张纹理节省采样）
        [MainTexture] _BumpMap("Wave Bump (高光法线贴图，R 通道供岸边波浪复用)", 2D) = "bump" {}
        _BumpStrength("Bump Strength (高光法线强度)", Range(0.0, 2.0)) = 1.0
        _SpecularColor("Specular Color (高光颜色)", Color) = (1.0, 1.0, 1.0, 1.0)
        _SpecularStrength("Specular Strength (高光强度)", Range(0.0, 1.0)) = 0.2
        _SpecularSmoothness("Specular Smoothness (高光锐度 0=粗糙 1=光滑)", Range(0.05, 0.8)) = 0.7
        // 高光法线聚拢度：衰减 BumpMap 对高光法线的影响
        //   0 = 完全用基础法线（最聚拢，参考真实海面窄光带）
        //   1 = 完全用 BumpMap 扰动后的法线（最分散）
        //   BumpMap 仍参与水面着色（高频波纹质感），但高光主体由大尺度 Gerstner 决定
        _SpecularBumpFalloff("Specular Bump Falloff (高光法线聚拢度 0=聚拢 1=分散)", Range(0.0, 1.0)) = 0.25

        [Header(Surface Foam)]
        _FoamDetailMap("Foam Texture (泡沫纹理，R通道，Offset=游走参数)", 2D) = "white" {}
        _FoamColor("Foam Color (泡沫颜色)", Color) = (1.0, 1.0, 1.0, 0.95)
        _FoamDetailStrength("Foam Strength (泡沫网强度)", Range(0.0, 4.0)) = 1.5
        _FoamDetailCutOff("Foam Cutoff (泡沫网裁切，越高网线越细)", Range(0.0, 1.0)) = 0.55
        _FoamDistortStrength("Foam Distort (波浪扭曲泡沫)", Range(0.0, 0.2)) = 0.01

        // [Header(Vertex Wave (Gerstner))]
        // Toggle：开启后 Shader 才会编译 Gerstner 计算路径，默认关闭 → 既有材质零变化
        [Toggle(_GERSTNER_ON)] _GerstnerEnabled("Enable Gerstner Wave (开启海面波涛顶点变形)", Float) = 0
        // 三档模型精度（编译期选 4/8/16 组波）：
        //   Low    : 4  组 — 远景、移动端
        //   Medium : 8  组 — 默认中景
        //   High   : 16 组 — 近景特写/影视级
        [KeywordEnum(Low, Medium, High)] _GerstnerQuality("Model Precision (海面模型精度)", Float) = 1
        _GerstnerAmplitude("Wave Amplitude (波涛振幅，整体高低)", Range(0.0, 2.0)) = 0.4
        // 全局波形大小缩放：直接乘到波数 k 上，>1 波形变小/变碎，<1 波形变大/变缓
        _GerstnerWaveSize("Wave Size (波形大小，整体缩放)", Range(0.25, 4.0)) = 1.0
        _GerstnerSpeed("Wave Speed (波涛速度，时间倍率)", Range(0.0, 5.0)) = 1.0
        _GerstnerChoppy("Wave Sharpness Q (浪尖锐度，0=平缓余弦 1=碎浪)", Range(0.0, 1.8)) = 0.95
        // 浪尖浪花：基于 Gerstner 解析 Jacobian 的「面积压缩量」作 mask 通道
        //   纹理 R 通道作为浪花细节（推荐给一张带白色羽化边的纹理）
        //   现有海面泡沫的扭曲场直接作为浪花的扭曲源 → 浪花与海面泡沫「同向漂移」
        _CrestMap("Crest Texture (浪尖浪花纹理，R通道)", 2D) = "white" {}
        _CrestColor("Crest Color (浪尖浪花颜色, A=整体强度)", Color) = (0.95, 0.97, 1.0, 1.0)
        // 锐度：合并「Sharpness 阈值」+「Focus 形状塑形」为单一 0~1 旋钮
        //   内部展开: sharp=1+7s (detJ 斜率 1~8), focus=1+2s (pow 指数 1~3)
        //   0 = 弱锐度（浪花覆盖广，向外扩散）/ 1 = 强锐度（仅浪尖最卷处）
        _CrestSharpness("Crest Sharpness (浪花锐度 0=广覆盖, 1=仅最高峰)", Range(0.0, 1.0)) = 0.4
        // 细节：合并「SprayDensity 颗粒概率」+「NoiseScale 颗粒大小」为单一 0~1 旋钮
        //   内部展开: noiseScale=1+7d (cell 大小 1~8m), density=0.2+0.6d (概率 20%~80%)
        //   0 = 粗大稀疏颗粒（块状）/ 1 = 细密颗粒（飞沫感）
        _CrestDetail("Crest Detail (浪花飞沫细节 0=粗大稀疏, 1=细密飞沫)", Range(0.0, 1.0)) = 0.3
        // 寿命：合并「Delay 残留时间」+「Decay 残留强度」为单一 0~1 旋钮
        //   内部展开: delay=1.5*l (0~1.5s), decay=1-0.6*l (1.0~0.4)
        //   0 = 瞬时（无残留）/ 1 = 1.5s 长延迟（残留较强）
        //   残留算法：跑两次 Gerstner（当前 + τ 秒前）取 max，τ 秒后浪花挂在原地逐渐消失
        _CrestLife("Crest Life (浪花寿命 0=瞬时, 1=1.5s 强延迟)", Range(0.0, 1.0)) = 0.3
        _CrestDistortStrength("Crest Distort (海面泡沫扭曲浪花强度)", Range(0.0, 1.0)) = 0.3

        // [Header(Render Settings)]
        // Opaque 模式：无 Src/Dst Blend 暴露（默认 One/Zero，由 SubShader Tags 控制）
        [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull Mode", Float) = 2
    }

    SubShader
    {
        // 海面是"看上去不透明、但必须在 Transparent 队列"的典型水着色器
        //   - Queue = "Transparent" 3000：让海面在所有 opaque 之后画，
        //     此时 _CameraDepthTexture 已包含地形/水下物体深度，depthOffset 计算正确
        //   - 无 Blend 语句：默认 One/Zero = 不透明覆盖（视觉上 = Opaque）
        //   - ZWrite On 保留：海面是深度来源（接收阴影 / 截断后续透明物体）
        //   - RenderType = "Opaque"：让 Unity 编辑器/工具把它当不透明材质处理
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
        }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            // Opaque 模式：无 Blend（默认 One/Zero）
            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // 雾：展开 FOG_LINEAR / FOG_EXP / FOG_EXP2 变体，由 Renderer Data 的 Fog 开关自动选择
            #pragma multi_compile_fog

            // 主光阴影：展开 _MAIN_LIGHT_SHADOWS / _MAIN_LIGHT_SHADOWS_CASCADE / _MAIN_LIGHT_SHADOWS_SCREEN 三个变体
            //   变体关闭时 shadowAttenuation 恒为 1.0，shader 仍可编译（URP Lighting.hlsl 内部有 #ifdef guard）
            //   启用条件：URP Renderer Data 勾选 Main Light Shadows + Cascade 模式
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            // 全局开关：控制是否计算主光阴影（与 _ShadowStrength 配合：开关控"算不算"，强度控"暗多少"）
            #pragma shader_feature_local _RECEIVE_SHADOWS
            // 软阴影：PCF 软采样（变体 _SHADOWS_SOFT 关闭时走硬阴影）
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            // 海面波涛：Gerstner 顶点位移开关 + 三档模型精度
            //  _GERSTNER_ON      关 → 走原始 vert 路径，渲染零变化；开 → 进入顶点位移
            //  _GerstnerQuality_* 在编译期决定叠加 4 / 8 / 16 组波，免运行时分支发散
            #pragma shader_feature_local _GERSTNER_ON
            #pragma shader_feature_local _GerstnerQuality_Low _GerstnerQuality_Medium _GerstnerQuality_High

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // SampleSceneDepth / LoadSceneDepth 定义在此文件中，Core.hlsl 不包含
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            // GetMainLight 定义在 Lighting.hlsl（Core.hlsl 不提供）
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // Gerstner 波位移模块（提供 GerstnerWave_Displace 接口）
            #include "Packages/com.youdoo.victools/Runtime/Shaders/OceanGerstner.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 screenPos  : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;  // 雾因子，按 PBR_Mobile_NEW 的规范在顶点计算
                float3 positionWS : TEXCOORD3;  // 世界空间位置（高光 viewDir 计算用）
                half3  normalWS   : TEXCOORD4;  // 永远输出：开 Gerstner = 解析法线；不开 = 顶点法线（高光 normal 源）
                #if defined(_GERSTNER_ON)
                half   crestMask  : TEXCOORD5;  // 浪尖浪花 mask（来自 GerstnerWave_Displace 解析 Jacobian）
                float2 crestUV    : TEXCOORD6;  // 浪尖 UV（来自 positionOS.xz → 浪花纹理"贴"在浪尖位置）
                #endif
                // 阴影坐标：故意不在 Varyings 里输出。
                //   大平面（64~256 格）上顶点间插值会让 TransformWorldToShadowCoord 内部的
                //   透视除 + cascade 索引选择失真 → cascade 边界处出现整条直线断带。
                //   改为在 frag 阶段用插值后的 positionWS 重新计算（见 frag 阴影块注释）。
            };

            // 高光法线贴图（R 通道同时被岸边波浪复用为图案）
            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);
            TEXTURE2D(_FoamDetailMap);
            SAMPLER(sampler_FoamDetailMap);
            // 浪尖浪花纹理：以浪尖 mask 作通道，现存海面泡沫扭曲作为扭曲源
            TEXTURE2D(_CrestMap);
            SAMPLER(sampler_CrestMap);

            CBUFFER_START(UnityPerMaterial)
                half4  _DepthShallowColor;
                half4  _DepthDeepColor;
                half4  _WaveColor;
                half4  _FoamColor;
                float4 _BumpMap_ST;
                float4 _FoamDetailMap_ST;
                half   _BumpStrength;            // 法线强度（参与高光 normal）
                half   _DepthMaxDistance;
                half   _WaterColorVariation;
                half   _WaterVariationScale;
                half   _WaterVariationSpeed;
                half   _WaveMaxDistance;
                half   _WaveFalloff;
                half   _WaveSpeed;
                half   _WaveStrength;
                half   _WaveCutOff;
                half   _WavePatternTilingX;     // 岸边波浪图案 Tiling X（独立于 _BumpMap_ST）
                half   _WavePatternTilingY;     // 岸边波浪图案 Tiling Y
                half   _FoamDetailStrength;
                half   _FoamDetailCutOff;
                half   _FoamDistortStrength;
                // 高光（参考 PBR_Mobile_NEW.SimpleSpecular）：GGX + 颜色 + 强度 + 锐度
                half4  _SpecularColor;           // 高光颜色
                half   _SpecularStrength;        // 高光强度
                half   _SpecularSmoothness;      // 高光锐度（roughness = 1 - smoothness）
                half   _SpecularBumpFalloff;     // 高光法线聚拢度（衰减 BumpMap 对高光法线的影响）
                half   _ShadowStrength;          // 主光阴影接收强度（0=不接收 1=完全接收）
                half   _MainLightStrength;       // 主光接收强度（0=不接收 1=完全）—— 整片水面随主光方向 + Intensity 变化
                // 海面波涛（Gerstner）：开关单独走 keyword，运行时只读 uniform
                half   _GerstnerAmplitude;
                half   _GerstnerWaveSize;
                half   _GerstnerSpeed;
                half   _GerstnerChoppy;
                // 浪尖浪花：mask 通道由 vert 给出（Gerstner Jacobian），fragment 再采样 _CrestMap × 现有泡沫扭曲
                float4 _CrestMap_ST;
                half4  _CrestColor;             // rgb = 浪花颜色, a = 整体强度（=0 时跳过采样与混合）
                half   _CrestSharpness;         // 0~1, 内部展开 sharpness(1~8) + focus(1~3)
                half   _CrestDetail;            // 0~1, 内部展开 noiseScale(1~8) + sprayDensity(0.2~0.8)
                half   _CrestLife;              // 0~1, 内部展开 delay(0~1.5s) + decay(1.0~0.4)
                half   _CrestDistortStrength;   // 来自现有海面泡沫的扭曲场强度
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                // 海面波涛（Gerstner 顶点位移）：仅在编译期开启 _GERSTNER_ON 时执行
                //   1) 用 positionOS.xz 作静止 XZ 平面位置，求和 N 组 sin/cos 得 OS 偏移
                //   2) 把 (1) 的解析法线传到 Varyings（未来 PBR 高光/反射可立即复用，零成本）
                //   3) 未开启时整个 ifdef 被 stripper 裁掉，渲染结果与原版完全一致
                #if defined(_GERSTNER_ON)
                    float3 _gwOffset;
                    float3 _gwNormal;
                    half  _gwCrest;
                    GerstnerWave_Displace(
                        input.positionOS.xz,
                        _Time.y * _GerstnerSpeed,
                        _GerstnerAmplitude,
                        _GerstnerChoppy,
                        _GerstnerWaveSize,
                        _CrestSharpness,
                        _CrestDetail,
                        _CrestLife,
                        _gwOffset,
                        _gwNormal,
                        _gwCrest);

                    float3 _posOS = input.positionOS.xyz + _gwOffset;
                    // 开启 Gerstner：用解析法线（已经按 Jacobian 求出正确法线，包含波形信息）
                    output.normalWS = _gwNormal;
                    output.crestMask = _gwCrest;
                    // 浪尖 UV：海面是 OS XZ 平面，positionOS.xz 直接作为浪花纹理 UV
                    //   → 浪花纹理「贴」在浪尖所在的水平位置，视觉上与波形同位置
                    output.crestUV = input.positionOS.xz;
                #else
                    float3 _posOS = input.positionOS.xyz;
                    // 关闭 Gerstner：退化为平面顶点法线（=模型自带的向上法线），
                    // 高光仍能正常显示（且不产生额外分支）
                    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                #endif

                // 本 Pass 只需要裁剪空间坐标，用 TransformObjectToHClip 替代 GetVertexPositionInputs：
                // 后者额外计算 positionWS / positionVS / positionNDC 但都没用到（等价运算，结果完全一致）
                output.positionCS = TransformObjectToHClip(_posOS);
                // 世界空间位置：仅高光 viewDir 用到；不开启 Gerstner 时仍要算（面是平的但 viewDir 仍是必要）
                output.positionWS = TransformObjectToWorld(_posOS);
                // 注意：阴影坐标不在顶点计算，详见 Varyings struct 注释与 frag 阴影块。
                output.uv = input.uv;
                output.screenPos = ComputeScreenPos(output.positionCS);
                // 雾因子：与 PBR_Mobile_NEW(viewDirWS_fog.w) 同一套处理，顶点计算 + 片元 MixFog
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            // 周边点水深：海底视深度 - 水面片元视深度（水面之下为正）
            float GetWaterDepth(float2 screenUV, float waterEyeDepth)
            {
                return max(0.0, LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams) - waterEyeDepth);
            }

            half4 frag(Varyings input) : SV_Target
            {
                // ==========================================================
                // 场景深度（全片元只采样一次，后续全部复用）
                // ==========================================================
                float2 screenUV      = input.screenPos.xy / input.screenPos.w;
                float  sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float  waterEyeDepth = input.positionCS.w;
                float  depthOffset   = max(0.0, sceneEyeDepth - waterEyeDepth);

                // 深度渐变因子（近岸浅、远海深）
                half depthOffset01 = saturate(depthOffset / max(_DepthMaxDistance, 0.001));

                // ==========================================================
                // 波带专用水深：屏幕空间十字 5 点（自身 + 上下左右）取平均
                // 直接用单点原始深度时，海底每一点起伏/锯齿都会按各自方向把条纹拉扯得疏密不匀；
                // 先 saturate 再平均：天空等未写入深度的像素恒为 1，不会把均值拉爆
                // 优化：中心点复用上面已算出的 depthOffset（等价于原先的 GetWaterDepth(screenUV, waterEyeDepth)），省一次深度采样
                // ==========================================================
                half waveInvMax = 1.0 / max(_WaveMaxDistance, 0.001);
                half waveDepth01 = saturate(depthOffset * waveInvMax);

                float2 blurStep = (_ScreenParams.zw - 1.0) * 3.0; // 约 3 像素
                waveDepth01 += saturate(GetWaterDepth(screenUV + float2( blurStep.x, 0.0), waterEyeDepth) * waveInvMax);
                waveDepth01 += saturate(GetWaterDepth(screenUV + float2(-blurStep.x, 0.0), waterEyeDepth) * waveInvMax);
                waveDepth01 += saturate(GetWaterDepth(screenUV + float2(0.0,  blurStep.y), waterEyeDepth) * waveInvMax);
                waveDepth01 += saturate(GetWaterDepth(screenUV + float2(0.0, -blurStep.y), waterEyeDepth) * waveInvMax);
                waveDepth01 *= 0.2;

                // 本帧常量：多次用到的时间值 / UV 只算一次
                float  time = _Time.y;
                // 泡沫与斑驳都以泡沫纹理 Tiling 为基准UV，这里统一乘一次（input.uv * _FoamDetailMap_ST.xy）
                float2 foamTiledUV = input.uv * _FoamDetailMap_ST.xy;

                // ==========================================================
                // 水色斑驳：双层低频噪声 + 域扭曲 + 缓慢漂移，得到自然的深浅交替（参考实拍海面）
                // 频率以泡沫纹理 Tiling 为基准再乘 _WaterVariationScale（<1 即比泡沫更疏、更大块）
                // ==========================================================
                float2 variationUV   = foamTiledUV * _WaterVariationScale;
                float  variationTime = time * _WaterVariationSpeed;

                // 第一层：低频噪声扰动采样坐标（域扭曲），打散规则平铺感
                half variationWarp = SAMPLE_TEXTURE2D(_FoamDetailMap, sampler_FoamDetailMap,
                                                      variationUV * 0.53 + 0.29
                                                      + variationTime * float2(0.008, -0.012)).r;
                float2 warpedUV = variationUV + (variationWarp - 0.5) * 0.35;

                // 第二/三层：一大一小两个尺度的水团缓慢反向漂移，混合出大小不一的斑块
                // 大尺度层的采样坐标单独留出来：泡沫扭曲直接复用它求差分，与水色斑驳是同一份噪声场
                float2 variationBigUV = warpedUV + variationTime * float2(0.011, 0.007);
                half variationBig   = SAMPLE_TEXTURE2D(_FoamDetailMap, sampler_FoamDetailMap, variationBigUV).r;
                half variationSmall = SAMPLE_TEXTURE2D(_FoamDetailMap, sampler_FoamDetailMap,
                                                       warpedUV * 2.1 + 0.43
                                                       - variationTime * float2(0.009, 0.006)).r;
                half variation = smoothstep(0.2, 0.8, saturate(variationBig * 0.85 + variationSmall * 0.4));

                // 斑驳值偏移深浅因子：亮斑靠浅水色、暗斑靠深水色，形成自然的深浅交替
                // 优化：把公共子表达式 (variation - 0.5) * _WaterColorVariation 提出来复用
                half variationMod = (variation - 0.5) * _WaterColorVariation;
                half depthVariant = saturate(depthOffset01 + variationMod * 1.6);
                half4 waterColor = lerp(_DepthShallowColor, _DepthDeepColor, depthVariant);

                // 再叠加同一色相的轻微明暗起伏，让斑驳更有层次
                waterColor.rgb *= 1.0 + variationMod;

                // ==========================================================
                // 风格化波浪：2D 横向条纹，深度越浅浪花越明显
                // _WaveFalloff 只作用于「强度」：越小衰减越平缓，浪花带向外扩得更宽
                // 条纹坐标改用「线性 + 平滑」的水深：沿纵深方向间距均匀，
                // 不会出现近岸挤成一片、远岸被无限拉长（pow 曲线叠加原始深度会各方向疏密不一）
                // ==========================================================
                half waveBand01 = 1.0 - waveDepth01;
                half waveFade = pow(waveBand01, _WaveFalloff);
                // 岸边波浪 UV：使用独立的 TilingX/Y（与 _BumpMap_ST 解耦，让岸边波浪密度独立于高光法线贴图 Tiling）
                float2 waveUV = float2(
                    input.uv.x * _WavePatternTilingX,
                    (waveBand01 + _WaveSpeed * time) * _WavePatternTilingY
                );

                // 波浪图案原始值（平滑，未裁切）：用于裁出浪花
                // 复用 _BumpMap.R 通道（与高光法线共用一张纹理，节省一张采样）
                //   Tiling 仍由 _WavePatternTilingX/Y 独立控制，与 _BumpMap_ST 无关
                half waveRaw = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, waveUV).r;
                half waveMask = step(_WaveCutOff, waveRaw * waveFade * _WaveStrength);

                // 深水颜色渐变（waterColor）保持不变，仅叠加浪花
                // _WaveColor.a 在 Opaque 模式下作为「浪花强度」权重（不是透明度的概念）
                //   值 = 1 时：mask 为 1 处完全采用浪花颜色；值 < 1 时让浪花透出水色
                //   不再写 finalColor.a —— Opaque 模式下写入 alpha 不影响渲染，省 lerp + max
                half waveOpacity = waveMask * _WaveColor.a;
                half4 finalColor = waterColor;
                finalColor.rgb = lerp(waterColor.rgb, _WaveColor.rgb, waveOpacity);

                // ==========================================================
                // 海面泡沫纹理：Offset(_FoamDetailMap_ST.zw) 作为游走参数（方向 + 速度），随时间漂移
                // 两层错位缩放采样（第二层反向游走），用于构建交织泡沫网
                // 泡沫扭曲：扭曲源改用「水色斑驳」的大尺度层输出，不再用波浪条纹
                // 斑驳是大块低频噪声，扭曲柔和、方向随水团流动，泡沫网不会被条纹切成一条条
                // 取斑驳场在 U/V 两个方向的前向差分构成二维向量：两轴等权、均值为 0，不会只沿单方向拉扯
                // 差分步长固定 0.03：斑驳噪声在该坐标空间下周期约为 1，幅度不受「斑驳密度」缩放影响
                // 两层泡沫共用同一扭曲向量 —— 泡沫网整体随水团被撕扯扭动，同时网线不会互相错断
                // ==========================================================
                // 现有海面泡沫的扭曲向量 —— 提前声明 + 默认 0（_FoamDetailStrength = 0 时不计算）
                // 浪尖浪花分支（下方）会复用这个扭曲场作为浪花的扭曲源，让浪花与海面泡沫同向漂移
                float2 foamDistort = 0.0;
                // _FoamDetailStrength = 0 时，foamWebLine 与 foamPatch 两项都乘 0，泡沫形状恒为 0、对结果无任何贡献，
                // 因此这里（以及内部的扭曲分支）可以直接跳过，最多省下 7 次纹理采样。
                // 分支条件是材质 uniform，同一 draw call 内所有像素走向一致，不产生分支发散代价。
                if (_FoamDetailStrength > 0.0)
                {
                    // _FoamDistortStrength = 0 时扭曲向量恒为 0，差分的两处采样纯属浪费
                    if (_FoamDistortStrength > 0.0)
                    {
                        half varDx = SAMPLE_TEXTURE2D(_FoamDetailMap, sampler_FoamDetailMap,
                                                      variationBigUV + float2(0.03, 0.0)).r;
                        half varDy = SAMPLE_TEXTURE2D(_FoamDetailMap, sampler_FoamDetailMap,
                                                      variationBigUV + float2(0.0, 0.03)).r;
                        foamDistort = float2(varDx - variationBig, varDy - variationBig) * (_FoamDistortStrength * 2.0);
                    }

                    float2 foamDetailUV1 = foamTiledUV + _FoamDetailMap_ST.zw * time
                                         + foamDistort;
                    float2 foamDetailUV2 = foamTiledUV * 1.37 + 0.41
                                         + float2(-_FoamDetailMap_ST.zw.y, _FoamDetailMap_ST.zw.x) * time * 0.83
                                         + foamDistort;
                    half foamDetail1 = SAMPLE_TEXTURE2D(_FoamDetailMap, sampler_FoamDetailMap, foamDetailUV1).r;
                    half foamDetail2 = SAMPLE_TEXTURE2D(_FoamDetailMap, sampler_FoamDetailMap, foamDetailUV2).r;

                    // 泡沫网：两层差值越接近（1-|a-b| 越大）越靠近等值轮廓，裁切成细丝后交织成网
                    half foamWeb = 1.0 - abs(foamDetail1 - foamDetail2);
                    half foamWebLine = saturate((foamWeb - _FoamDetailCutOff)
                                              / max(1.0 - _FoamDetailCutOff, 0.001) * _FoamDetailStrength);

                    // 片状白沫：直接用泡沫纹理的高亮区域扣出成块浪花
                    half foamPatch = saturate((foamDetail1 - _FoamDetailCutOff) * _FoamDetailStrength);

                    // 合并：全海面细丝泡沫网 + 成块白沫
                    half foamShape = saturate(max(foamWebLine, foamPatch));

                    finalColor.rgb = lerp(finalColor.rgb, _FoamColor.rgb, foamShape);
                }

                // ==========================================================
                // 海水高光：参考 PBR_Mobile_NEW.SimpleSpecular (500-512)
                //   放在泡沫扭曲之后计算：foamDistort 已算好，直接应用到 _BumpMap 法线采样
                //   normalWS  → 解析 Gerstner 法线（开 _GERSTNER_ON）/ 顶点法线
                //   BumpMap   → TS 法线贴图，TS→WS 变换后参与高光 normal；UV 叠加 foamDistort 扭曲
                //   viewDir   → _WorldSpaceCameraPos - positionWS
                //   lightDir  → GetMainLight().direction
                //   GGX       → 自写（与 PBR_Mobile_NEW 同一公式）
                // 整段在 _SpecularStrength = 0 时短路，省 GGX + 法线采样
                // ==========================================================
                // 主光 + 阴影：提前到 spec 块外，让 _ShadowStrength 也可复用（即使 _SpecularStrength = 0 也要算）
                //   两路强度都 = 0 时整段短路，省 GetMainLight(shadowCoord) 的 shadow sample 开销
                //   _RECEIVE_SHADOWS 关闭时用无参 GetMainLight() 兜底（不采 shadowMap，shadowAttenuation 恒为 1），
                //   既保证 mainLight 始终被初始化（D3D11 严格语义），又保留 spec 块读 direction/color 的正确性
                Light mainLight = GetMainLight();
                half  mainLightShadow = 1.0h;
                #if defined(_RECEIVE_SHADOWS)
                    if (_SpecularStrength > 0.0 || _ShadowStrength > 0.0)
                    {
                        // ★关键修复：shadowCoord 必须在 frag 阶段用插值后的 positionWS 重新计算。
                        //   顶点阶段算的话，cascade 索引选择 + 透视除会被大三角形插值破坏，
                        //   整片水面上 cascade 边界处会出现一条清晰的直线断带。
                        //   改到 frag 后：每个片元用自己的世界坐标挑 cascade，cascade 选择正确，断带消失。
                        float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                        mainLight        = GetMainLight(shadowCoord);
                        // 阴影应用：0=完全不接收 1=完全接收（中间值平滑过渡）
                        mainLightShadow  = lerp(1.0h, mainLight.shadowAttenuation, _ShadowStrength);
                    }
                #endif

                // 主光漫反射：让水面颜色随主光方向 + Intensity 连续变化
                //   NoL * 0.5 + 0.5 (half-lambert) 保证背光面保留可见性（不会黑透）
                //   mainLight.color 提取 Rec.709 luminance 后参与（保留亮度，忽略太阳色调，避免给水面强加偏色）
                //   独立于 _RECEIVE_SHADOWS：接收主光 ≠ 接收主光阴影
                //   三个独立项 = 方向衰减 × 光源亮度 × 用户强度；任一为 0 都能短路
                //   注：spec 块内部还会用 Nperturbed 算一次自己的 NoL（不共用，避免 BumpMap/TS 法线在 spec / 漫反射中漂移）
                half NoL_base          = saturate(dot(input.normalWS, mainLight.direction));
                half mainLightDiff     = NoL_base * 0.5h + 0.5h; // [0.5, 1.0]，方向性 half-lambert[0, ∞)
                half mainLightContrib  = mainLight.color * mainLightDiff; // [0, ∞) * [0.5, 1.0]

                half3 specular = (half3)0.0;
                if (_SpecularStrength > 0.0)
                {
                    // 1) MainLight + viewDir
                    half3 lightDir   = mainLight.direction;
                    half3 lightColor = mainLight.color;
                    half3 viewDir    = normalize(_WorldSpaceCameraPos - input.positionWS);

                    // 2) TS 法线扰动：无需 mesh tangent，用 ddx/ddy(positionWS) 构造基向量
                    //   水面是大平面，此法等价真实 TS 空间，零额外属性需求
                    float3 sigmaX = ddx(input.positionWS);
                    float3 sigmaY = ddy(input.positionWS);
                    float3 N      = normalize(input.normalWS);
                    float3 T      = normalize(sigmaX);
                    float3 B      = normalize(cross(N, T));
                    float3x3 TBN  = float3x3(T, B, N);

                    // 3) 解码 BumpMap 为 TS 法线，采样坐标叠加水面最终扭曲 foamDistort
                    //    → 法线扰动随泡沫扭曲场一起流动，高光细节与泡沫/浪花同向漂移
                    //    用 UnpackNormalScale 自动处理三种编码（与 PBR_Mobile_NEW 一致）：
                    //      - DXT5nm（桌面）      : X 在 a、Y 在 g
                    //      - UNITY_NO_DXT5nm（移动）: X 在 r、Y 在 g
                    //      - ASTC               : 由 Packing.hlsl 内部分支
                    float2 bumpUV = input.uv * _BumpMap_ST.xy + _BumpMap_ST.zw + foamDistort;
                    half4 normalSample = SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, bumpUV);
                    half3 normalTS = UnpackNormalScale(normalSample, _BumpStrength);
                    normalTS = normalize(normalTS);

                    // 4) 高光法线：WS 空间混合（基础法线 ↔ BumpMap 扰动法线）
                    //    _SpecularBumpFalloff = 0 → 完全用基础 Gerstner 法线，高光由大尺度波浪决定 → 聚拢成窄光带（参考真实海面）
                    //    _SpecularBumpFalloff = 1 → 完全用 BumpMap 扰动后的法线，高频噪声打散成满海面亮点
                    //    BumpMap 仍参与水面着色（体现小尺度波纹质感），但高光法线主体由大尺度波浪决定
                    half3 NperturbedWS = normalize(mul(normalTS, TBN));
                    half3 Nbase        = normalize(input.normalWS);
                    half3 Nperturbed   = normalize(lerp(Nbase, NperturbedWS, _SpecularBumpFalloff));

                    // 5) SimpleSpecular 核心（与 PBR_Mobile_NEW 一致）
                    //   halfDir / NoH / LoH / NoL → GGX specular term
                    half3 halfDir = normalize(lightDir + viewDir);
                    half  NoH     = saturate(dot(Nperturbed, halfDir));
                    half  LoH     = saturate(dot(lightDir, halfDir));
                    half  NoL     = saturate(dot(Nperturbed, lightDir));
                    half  perceptualRoughness = saturate(1.0 - _SpecularSmoothness);
                    // 对齐 PBR_Mobile_NEW / URP InitializeBRDFData 的双重钳制：
                    //   alphaRoughness 钳到 HALF_MIN_SQRT ≈ 7.81e-3 — 防止 GGX D 项在 roughness=0 退化为 delta
                    //   alpha2        钳到 HALF_MIN      ≈ 6.10e-5 — 防止 fp16 下溢到 0
                    half  alphaRoughness = max(perceptualRoughness * perceptualRoughness, 7.8125e-3h);
                    half  alpha2         = max(alphaRoughness * alphaRoughness, 6.1035e-5h);
                    half  alpha2MinusOne = alpha2 - 1.0;

                    // GGX 高光项：ARM Siggraph 2015 移动优化版（D*V*F 合并近似，与 PBR_Mobile_NEW 一致）
                    //   相比纯 D 项，多了 LoH 遮蔽项 + 能量归一化项，高光范围随 smoothness 收敛、不再整片海面发亮
                    half d   = NoH * NoH * alpha2MinusOne + 1.00001h;
                    half ggx = alpha2 / ((d * d) * max(0.1h, LoH * LoH) * (alphaRoughness * 4.0h + 2.0h));
                    // 高光在阴影下也被压暗（mainLightShadow 在外层 if 已算好，_ShadowStrength = 0 时恒为 1）
                    specular = _SpecularColor.rgb * (half3)ggx * NoL * lightColor * _SpecularStrength * mainLightShadow;
                }

                // ==========================================================
                // 浪尖浪花：以 vert 传来的 crestMask 作通道，采样 _CrestMap 作为浪花
                //   扭曲源 = 上面算好的 foamDistort（来自 _FoamDetailMap 的低频水色斑驳场）
                //   浪尖 UV = positionOS.xz × Tiling + Offset + foamDistort × 强度
                //   浪花 mask = crestMask × sampled.r × _CrestColor.a
                //   关闭 _GERSTNER_ON 或 _CrestColor.a = 0 时：Varyings 不存在 / 整段 ifdef 裁掉 → 零成本
                //   即使用户关掉 _FoamDetailStrength，foamDistort = 0，浪花仍能正常显示（无扭曲）
                // ==========================================================
                #if defined(_GERSTNER_ON)
                if (_CrestColor.a > 0.0 && input.crestMask > 0.0)
                {
                    // 浪花 UV：positionOS.xz × Tiling + Offset + 现有泡沫扭曲
                    float2 crestSampleUV = input.crestUV * _CrestMap_ST.xy + _CrestMap_ST.zw
                                         + foamDistort * _CrestDistortStrength;
                    half crestSample = SAMPLE_TEXTURE2D(_CrestMap, sampler_CrestMap, crestSampleUV).r;

                    // 通道混合：浪尖 mask × 浪花纹理 × 整体强度
                    //   crestMask 在外、crestSample 在内：只有浪尖区域能看见浪花纹理
                    //   _CrestColor.a 在 Opaque 模式下作为「浪花整体强度」权重
                    //   0 时整段跳过（已由外层 if 保证），不再写 finalColor.a
                    half crestAmount = input.crestMask * crestSample * _CrestColor.a;
                    finalColor.rgb = lerp(finalColor.rgb, _CrestColor.rgb, crestAmount);
                }
                #endif

                // ==========================================================
                // 主光漫反射 + 阴影应用：在所有色相（深度渐变/浪花/泡沫/浪尖）合成完成后统一处理
                //   mainLightContrib: half-lambert × mainLight.color 亮度
                //     _MainLightStrength = 0 → lerp(1, x, 0) = 1 → 水面自然色（关闭主光）
                //     _MainLightStrength = 1 → lerp(1, x, 1) = x → 完整受主光方向 + Intensity 影响
                //   mainLightShadow: 仅在 _RECEIVE_SHADOWS 开启 + _ShadowStrength > 0 时叠乘
                //   顺序：先漫反射再阴影 → 阴影区域在半 lit 基础上压暗
                //   specular 走内部 Nperturbed 算的 NoL（已含漫反射衰减），不再 * mainLightContrib 避免双重压暗
                // ==========================================================
                finalColor.rgb *= lerp(1.0h, mainLightContrib, _MainLightStrength);
                #if defined(_RECEIVE_SHADOWS)
                    if (_ShadowStrength > 0.0)
                    {
                        finalColor.rgb *= mainLightShadow;
                        specular       *= mainLightShadow;
                    }
                #endif

                // ==========================================================
                // 高光叠加：Opaque 模式下直接加 specular 到 rgb（alpha 不参与混合）
                // ==========================================================
                finalColor.rgb += specular;

                // 雾：与 PBR_Mobile_NEW 完全一致的处理方式（无独立参数，跟随 Renderer Data 的 Fog 设置）
                // Opaque 模式下 MixFog 只作用于 rgb；alpha 不参与混合
                finalColor.rgb = MixFog(finalColor.rgb, input.fogFactor);
                return finalColor;
            }
            ENDHLSL
        }
    }

    CustomEditor "CustomOceanGUI"
}
