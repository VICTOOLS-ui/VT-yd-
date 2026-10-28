//FurShell 1.1 完善基础fur特性
//FurShell 1.2 添加AlphaOffset优化毛发透明结构算法，优化毛发剔除效果BaseMapA颜色图A通道影响毛发长度
//FurShell 1.3 添加UseVerShadow选项，可以使用像素阴影，优化Fresnel暗部过暗问题
//FurShell 1.4 修改边缘光范围加大；修改变量_BaseColor；修改默认值
//FurShell 1.5 支持多点触摸
//FurShell 1.6 添加法线纹理控制毛发簇效果
//FurShell 1.7 修复“使用圆锥风力”选项开关控制无效问题
//FurShell 1.8 优化边缘光透光，减弱模型中间部分边缘光，添加边缘光相关参数。
//FurShell 1.9 完善边缘光算法，优化毛发逆光效果表现，优化模型底部边缘光过曝处理。
//FurShell 2.0 添加(明暗交界线软硬)、(阴影亮度)与(整体亮度)参数。
//FurShell 2.1 （阴影强度）参数要同时控制自身阴影，使明暗交界与阴影一致。

Shader "Custom/FurShell_Mobile_SingleC"
{
    Properties
    {
        [Toggle(_USEDISTANCEATTEN)] _UseDistanceAtten ("Use Distance Attenuation", Float) = 0.0
        [Toggle(_USEVERSHADOW)] _UseVerShadow ("Use Vertex Shadow", Float) = 1.0
        [Toggle(_USESELFSHADOW)] _UseSelfShadow ("Use Self Shadow", Float) = 1.0
        [Space]
        [MainColor] _BaseColor("Base Color", Color) = (1,1,1,1)
        [ToggleUI] _UseAlpha ("Use Alpha", Float) = 1.0
        _BaseMap("Base Map", 2D) = "white" {}
        _FurMap("Fur Map", 2D) = "white" {}
        _NoiseMap("Noise Map (Curl Bend)", 2D) = "gray" {}
        _NoiseBendStrength("Noise Bend Strength", Range(0.0, 30.0)) = 0.3
//        _NormalMap("Fur Map", 2D) = "white" {}
        [IntRange] _ShellAmount("Shell Amount", Range(1, 20)) = 8
        _FurLength("Fur Length", Range(0.0, 0.2)) = 0.004
        _AlphaOffset("Alpha Offset", Range(0.0, 1.01)) = 0.06
        _AlphaCutout("Alpha Cutout", Range(0.0, 1.0)) = 0.3
        _FurScale("Fur Density", Range(0.1, 50.0)) = 1.0
        _Occlusion("Occlusion", Range(0.0, 1.0)) = 0.23
        // 明暗交界线软度（toon shading 阈值化）：对 NdotL 做 smoothstep 阈值化
        //   1.0 = 渐变原版（向后兼容，等价 smoothstep(0,1,x) = x）
        //   0.0 = 硬色阶（等价 smoothstep(0.5,0.5,x) = step(0.5,x)）
        //   0~1 之间 = smoothstep 渐变带宽度可调，越小越像参考图的色阶感
        _LightStepSoftness("Light Step Softness (明暗交界线软度, 1=原版渐变, 0=硬色阶)", Range(0.0, 1.0)) = 1.0
        // 暗部亮度（**统一**作用于明暗交界与自身投影阴影）：
        //   0 = 暗部保持原样（最暗）；1 = 暗部抬满与亮区同等亮度；0~1 = 线性抬亮
        //   明暗交界(NdotL) 与 自身阴影(shadowAttenuation) 合并为单一遮蔽项后只 lift 一次，
        //   保证阴影区与交界暗侧落在同一亮度，不会出现"阴影比交界还黑"的断层。
        _ShadowBrightness ("Shadow Brightness (阴影强度: 明暗交界+自身阴影)", Range(0, 1)) = 0.4
        // 整体亮度（参考 PBR_Mobile_NEW.shader:71），finalColor *= _Brightness，
        _Brightness("Brightness", Range(0.5, 2)) = 1.0
        [Space]
        [ToggleUI] _UseTouch ("Use Touch", Float) = 0.0
        [Space]
        [ToggleUI] _UseWind ("Use Wind", Float) = 0.0
        // 基础移动参数：控制毛发的整体运动
        // x, y, z: 基础移动方向向量 (世界空间)
        // w: 移动因子指数，控制移动强度随毛发层数的变化 (建议值: 2.0-4.0)
        _BaseMove("Base Move", Vector) = (0.9, -1.0, 0.0, 2.6)
        
        // 风频率参数：控制风动画的速度和节奏
        // x, y, z: 风频率向量，分别对应三个轴向的动画频率
        // w: 预留参数，当前未使用
        // 值越大，风动画变化越快
        _WindFreq("Wind Freq", Vector) = (2.5, 0.7, 0.9, 1.0)

        // 风移动参数：控制风动画的幅度和空间变化
        // x, y, z: 风移动幅度向量，控制风在各个方向上的摆动强度
        // w: 空间频率，控制风动画随模型位置的变化 (值越大，空间变化越明显)
        _WindMove("Wind Move", Vector) = (1.2, 0.3, 0.2, 2.0)
         
        // 背面剔除阈值
        _FaceViewProdThresh("Direction Threshold", Range(0.0001, 1.0)) = 0.001
        // 背光边缘光（逆光轮廓模拟：相机与主光方向对角的幅度，最逆光时最强、偏离渐弱）
        // _RimLightIntensity：边缘光整体强度（0 = 关闭，1 = 默认，>1 增强）
        // _RimLightEdgeStart：外边界（轮廓）smoothstep 起始阈值，Fresnel(1-N·V) 小于此值无边缘光
        // _RimLightEdgeEnd：外边界（轮廓）smoothstep 终止阈值，大于此值满边缘光
        _RimLightIntensity("Rim Light Intensity", Range(0.0, 5.0)) = 1.0
        _RimLightEdgeStart("Rim Light Edge Start", Range(0.1, 1.0)) = 0.5
        _RimLightEdgeEnd("Rim Light Edge End", Range(0.7, 1.0)) = 0.95
        // _RimShadowStrength：阴影中边缘光的保留强度（0 = 被自身投影完全遮挡，1 = 阴影中不衰减）
        _RimShadowStrength("Rim Light Shadow Strength", Range(0.0, 1.0)) = 0.0
        // _RimBottomStrength：底部（法线朝下）边缘光强度。
        // 用世界空间法线 Y 分量识别朝下的面（如下巴），按此值衰减其边缘光。
        // 0 = 朝下面完全无边缘光（下巴彻底抑制）；1 = 朝下面边缘光不衰减（等同关闭此约束）。
        _RimBottomStrength("Rim Light Bottom Intensity", Range(0.0, 1.0)) = 0.0
        // ● 边缘光颜色：默认白色 → 颜色 = 主光颜色 × _RimLightColor（继承主光）
        //   不受 _BaseColor（毛发底色）影响，避免把透光染成毛发色。
        //   想特殊染色（黄/蓝/紫调）直接调此色即可。
        _RimLightColor("Rim Light Color (tints by main light)", Color) = (1, 1, 1, 1)
        // 触摸挤压参数（支持最多4个触摸点）
        _TouchPosition("Touch Position 1", Vector) = (0, -1000, 0, 1)  // xyz: 世界空间位置, w: 强度
        _TouchPosition2("Touch Position 2", Vector) = (0, -1000, 0, 1)
        _TouchPosition3("Touch Position 3", Vector) = (0, -1000, 0, 1)
        _TouchPosition4("Touch Position 4", Vector) = (0, -1000, 0, 1)
        _TouchRadius("Touch Radius", Float) = 1.0
        _MaxDepression("Max Depression", Range(0.0, 2.0)) = 1.3  // 最大凹陷度
        
        [Space(20)]
        // 圆锥形风力影响参数（模拟吹风机效果）
        [ToggleUI] _UseWindCone ("Use Wind Cone", Float) = 0.0
        _WindConePosition("Wind Cone Position", Vector) = (0, 0, 0, 1)  // xyz: 圆锥中心位置, w: 强度倍增
        _WindConeDirection("Wind Cone Direction", Vector) = (0, 1, 0, 0)  // xyz: 圆锥方向, w: 未使用
        _WindConeAngle("Wind Cone Angle", Range(0.0, 90.0)) = 30.0  // 圆锥角度（度）
        _WindConeRange("Wind Cone Range", Float) = 5.0  // 圆锥范围
        _WindConeFrequencyBoost("Wind Cone Freq Boost", Range(0.0, 10.0)) = 2.0  // 圆锥内风频率加大值
        
        // Alpha通道控制参数
//        _AlphaControlPower("Alpha Control Power", Range(0.0, 4.0)) = 0.8  // Alpha通道控制强度
//        _AlphaThreshold("Alpha Threshold", Range(0.0, 1.0)) = 0.8  // Alpha通道阈值：大于此值才允许变形
    }

    SubShader
    {
        Tags 
        {
            "RenderType" = "Opaque"
            // "Queue" = "Geometry"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline" 
            "IgnoreProjector" = "True"
        }

        LOD 100

        ZWrite Off
        Cull Back

        Pass
        {
            Name "UnlitLambertGeo"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            // #pragma exclude_renderers gles gles3 glcore
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            // #pragma multi_compile _ _SHADOWS_SOFT
            #pragma vertex vert
            #pragma require geometry
            #pragma geometry geom 
            #pragma fragment frag
            #pragma shader_feature_local _USESELFSHADOW
            #pragma shader_feature_local _USEVERSHADOW
            // #pragma shader_feature_local _USETOUCH
            // #pragma shader_feature_local _USEWINDCONE
            #pragma shader_feature_local _USEDISTANCEATTEN
            
            // 首先包含必要的URP头文件以定义TEXTURE2D和SAMPLER宏
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // 纹理和采样器（不在 CBUFFER 中）
            TEXTURE2D(_FurMap); 
            SAMPLER(sampler_FurMap);
            TEXTURE2D(_NoiseMap);
            SAMPLER(sampler_NoiseMap);
            // ============================================
            // 包含 Param.hlsl（SRP Batcher 兼容）
            // ============================================
            int _ShellAmount;
            float _FurLength;
            float _AlphaOffset;
            float _AlphaCutout;
            float _Occlusion;
            float _UseWind;
            float _UseTouch;
            float _FurScale;
            float _UseAlpha;
            float4 _BaseMove;
            float4 _WindFreq;
            float4 _WindMove;
            // float4 _BaseColor;  // 注释掉，由UnlitInput.hlsl定义
            float _Brightness;          // 整体亮度倍数（参考 PBR_Mobile_NEW.shader:71），最后乘到 finalColor
            float3 _AmbientColor;
            float _FaceViewProdThresh;
            // 边缘光参数
            float _RimLightIntensity;
            float _RimLightEdgeStart;
            float _RimLightEdgeEnd;
            float _RimShadowStrength;
            float _RimBottomStrength;
            half4 _RimLightColor;
            // 明暗交界线软度（控制 NdotL smoothstep 渐变带宽度）：1=原版渐变, 0=硬色阶
            float _LightStepSoftness;
            float _ShadowBrightness;

            float4 _FurMap_ST;
            float4 _NoiseMap_ST;
            float _NoiseBendStrength;
            
            // 触摸挤压参数（支持最多4个触摸点）
            float4 _TouchPosition;   // xyz: 世界空间位置, w: 强度
            float4 _TouchPosition2;
            float4 _TouchPosition3;
            float4 _TouchPosition4;
            float _TouchRadius;
            float _MaxDepression;   // 最大凹陷度
            
            // 圆锥形风力影响参数
            float _UseWindCone;
            float4 _WindConePosition;  // xyz: 圆锥中心位置, w: 强度倍增
            float4 _WindConeDirection; // xyz: 圆锥方向, w: 未使用
            float _WindConeAngle;      // 圆锥角度（度）
            float _WindConeRange;      // 圆锥范围
            float _WindConeFrequencyBoost; // 圆锥内风频率加大值
            
            float3 _LightDirection;
            float _ShadowExtraBias;
            // ============================================
            
            // ============================================
            // 开始嵌入 Common.hlsl 内容
            // ============================================
            // #ifndef FUR_COMMON_HLSL
            // #define FUR_COMMON_HLSL

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl"
            // #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            // 包含 CommonMaterial.hlsl 以提供 LerpWhiteTo 函数定义，使用Shadows.hlsl中的会报错
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            inline float3 GetViewDirectionOS(float3 posOS)
            {
                float3 cameraOS = TransformWorldToObject(GetCameraPositionWS());
                return normalize(posOS - cameraOS);
            }

            inline float3 CustomApplyShadowBias(float3 positionWS, float3 normalWS)
            {
                positionWS += _LightDirection * (_ShadowBias.x + _ShadowExtraBias);
                
                float invNdotL = 1.0 - saturate(dot(_LightDirection, normalWS));
                float scale = invNdotL * _ShadowBias.y;
                
                positionWS += normalWS * scale.xxx;

                return positionWS;
            }

            inline float4 GetShadowPositionHClip(float3 positionWS, float3 normalWS)
            {
                positionWS = CustomApplyShadowBias(positionWS, normalWS);
                
                float4 positionCS = TransformWorldToHClip(positionWS);
                
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            inline float rand(float2 seed)
            {
                return frac(sin(dot(seed.xy, float2(12.9898, 78.233))) * 43758.5453);
            }

            inline float3 rand3(float2 seed)
            {
                return 2.0 * (float3(rand(seed * 1), rand(seed * 2), rand(seed * 3)) - 0.5);
            }

            struct FurMoverData // 这个结构体存储了毛发动画点的数据
            {
                float3 posWS;      // 世界空间位置
                float3 dPosWS;     // 世界空间位置增量（用于动画）
                float3 velocityWS; // 世界空间速度
                float time;        // 时间参数
            };

            // RWStructuredBuffer<T>是HLSL中的一种缓冲区类型，表示一个可读写的结构化缓冲区。RW表示"Read-Write"
            // <FurMoverData>是缓冲区中存储的数据类型。从上面的定义可以看到存储毛发动画的数据类型
            // _Buffer：这是缓冲区的变量名
            // 这指定了缓冲区在哪个寄存器上绑定。u1表示无序访问视图(Unordered Access View)寄存器1。在HLSL中，u寄存器用于可读写资源（如RWStructuredBuffer），而t寄存器用于只读资源（如纹理）
            RWStructuredBuffer<FurMoverData> _Buffer : register(u1);
        // #endif
            // ============================================
            // 结束 Common.hlsl
            // ============================================
            
            // ============================================
            // 开始嵌入 UnlitLambert.hlsl 内容
            // ============================================
            // 包含UnlitInput.hlsl获取_BaseColor、_BaseMap等定义
            #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl"

            // 触摸挤压计算函数（改进版）- 支持多个触摸点
            // 返回触摸压扁因子 (0.0 = 完全压扁, 1.0 = 无影响)
            inline float CalculateTouchFlatten(float3 positionWS, float3 normalWS)
            {
                float minFlatten = 1.0; // 1.0表示无影响
                
                // 处理4个触摸点
                float4 touchPositions[4] = { _TouchPosition, _TouchPosition2, _TouchPosition3, _TouchPosition4 };
                
                for (int i = 0; i < 4; i++)
                {
                    float3 touchPos = touchPositions[i].xyz;
                    float touchStrength = touchPositions[i].w;
                    
                    // 如果触摸点在地下1000单位，表示无效，跳过
                    if (touchPos.y < -999.0)
                        continue;
                    
                    float distanceToTouch = length(positionWS - touchPos);
                    
                    // 如果距离大于半径，没有影响
                    if (distanceToTouch > _TouchRadius)
                        continue;
                    
                    // 计算衰减因子：使用平滑的衰减曲线
                    float falloff = 1.0 - saturate(distanceToTouch / _TouchRadius);
                    // 使用平方衰减使效果更自然，中心强边缘弱
                    falloff = falloff * falloff;
                    
                    // 计算压扁因子：falloff越大，压扁越多
                    // touchStrength控制压扁强度，_MaxDepression控制最大压扁程度
                    float flattenAmount = falloff * saturate(touchStrength * 0.5) * _MaxDepression;
                    
                    // 取最小值（最大压扁效果）
                    float flatten = 1.0 - saturate(flattenAmount);
                    minFlatten = min(minFlatten, flatten);
                }
                
                return minFlatten;
            }

            // 触摸变形函数：将顶点沿触摸方向压入，模拟按压凹陷
            // 参数：positionWS - 世界空间位置，normalWS - 世界空间法线，uv - 纹理坐标
            // 返回：变形后的世界空间位置
            inline float3 ApplyTouchDeformation(float3 positionWS, float3 normalWS, float2 uv)
            {
                float3 totalDeformation = float3(0, 0, 0);
                
                // 遍历4个触摸点，累加变形
                float4 touchPositions[4] = { _TouchPosition, _TouchPosition2, _TouchPosition3, _TouchPosition4 };
                
                for (int i = 0; i < 4; i++)
                {
                    float3 touchPos = touchPositions[i].xyz;
                    float touchStr = touchPositions[i].w;
                    
                    // 无效触摸点跳过
                    if (touchPos.y < -999.0)
                        continue;
                    
                    float3 toVertex = positionWS - touchPos;
                    float distanceToTouch = length(toVertex);
                    
                    // 超出半径无影响
                    if (distanceToTouch > _TouchRadius)
                        continue;
                    
                    // 平滑衰减：中心强边缘弱
                    float falloff = 1.0 - saturate(distanceToTouch / _TouchRadius);
                    falloff = falloff * falloff;
                    
                    // 压入方向：沿法线反方向（向模型内部压）
                    float3 pushDir = -normalWS;
                    
                    // 变形量：用 _TouchRadius * _MaxDepression 作为最大偏移距离
                    // touchStr 控制强度，falloff 控制空间衰减
                    // 最终 clamp 到毛发总厚度，确保最深只压到基础网格表面
                    float maxDeform = _FurLength * _ShellAmount;
                    float deformAmount = min(falloff * touchStr * _MaxDepression * _TouchRadius, maxDeform);
                    
                    totalDeformation += pushDir * deformAmount;
                }
                
                return positionWS + totalDeformation;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float  layer : TEXCOORD1;
                float3 lighting : TEXCOORD2;    // 预计算的灯光贡献 (diffuse + ambient + fresnel)
                float alphaFactor : TEXCOORD3;  // 预计算的alpha相关因子
                float3 viewDirWS : TEXCOORD4;   // 世界空间视角方向（用于可能的边缘光计算）
                #ifndef _USEVERSHADOW
                    // 像素阴影模式：需要传递位置和法线到片段着色器
                    float3 positionWS : TEXCOORD5;
                    float3 normalWS : TEXCOORD6;
                #endif
            };

            Attributes vert(Attributes input)
            {
                return input;
            }
            
            half fastPow(half x, half n) {
                return exp2(n * log2(x)); // 在某些GPU上更快
            }
            
            // 计算圆锥形风力影响（优化版）
            // 增强毛发飘动的方向感，使毛发更明显地沿着风吹的方向飘动
            // 优化进入风区的过渡效果，避免剧烈抖动
            // 返回：影响值，并输出调整后的风方向和BaseMove.w调整因子
            inline float CalculateWindConeInfluence(float3 positionWS, out float3 adjustedWindDirection, out float baseMoveWAdjustment)
            {
                // 初始化输出方向为原始风方向
                adjustedWindDirection = normalize(_WindMove.xyz);
                baseMoveWAdjustment = 1.0; // 默认不调整
                
                // 如果未启用圆锥风力影响，返回基础影响值1.0
                if (_UseWindCone < 0.5)
                    return 1.0;
                
                // 获取圆锥参数
                float3 conePos = _WindConePosition.xyz;
                float3 coneDir = normalize(_WindConeDirection.xyz);
                float coneAngleRad = radians(_WindConeAngle);
                float coneRange = _WindConeRange;
                float coneIntensity = _WindConePosition.w;
                
                // 计算顶点到圆锥中心的向量
                float3 toVertex = positionWS - conePos;
                float distanceToCone = length(toVertex);
                
                // 计算顶点方向与圆锥方向的夹角
                float3 vertexDir = normalize(toVertex);
                float cosAngle = dot(vertexDir, coneDir);
                float angle = acos(clamp(cosAngle, -1.0, 1.0));
                
                // 优化：使用平滑过渡函数，避免在边界处突然变化
                // 1. 距离过渡：使用smoothstep实现平滑的距离衰减
                float distanceFactor = 1.0 - smoothstep(coneRange * 0.8, coneRange, distanceToCone);
                
                // 2. 角度过渡：使用smoothstep实现平滑的角度衰减
                float angleFactor = 1.0 - smoothstep(coneAngleRad * 0.8, coneAngleRad, angle);
                
                // 如果完全在圆锥范围外，无影响
                if (distanceFactor <= 0.0 || angleFactor <= 0.0)
                    return 1.0;
                
                // 计算距离衰减：越靠近圆锥中心影响越强
                float distanceFalloff = 1.0 - saturate(distanceToCone / coneRange);
                distanceFalloff = fastPow(distanceFalloff, 2.0); // 二次衰减，中心更强
                
                // 计算角度衰减：越靠近圆锥中心轴影响越强
                float angleFalloff = 1.0 - saturate(angle / coneAngleRad);
                angleFalloff = fastPow(angleFalloff, 2.0); // 二次衰减，中心轴更强
                
                // 综合衰减因子，应用过渡因子使边界平滑
                float falloff = distanceFalloff * angleFalloff * distanceFactor * angleFactor;
                
                // 优化：增强方向感
                // 1. 计算从圆锥中心到顶点的方向（风从圆锥中心吹向顶点）
                float3 windFromConeDir = normalize(toVertex);
                
                // 2. 计算风的方向应该是圆锥方向（吹风机方向）与从圆锥中心到顶点方向的混合
                // 这样可以创建更自然的发散效果，毛发被风吹离风源
                float3 coneToVertexDir = -windFromConeDir; // 从顶点指向圆锥中心的方向
                
                // 3. 计算方向混合因子：基于角度和距离
                // 在圆锥中心轴附近，主要使用圆锥方向
                // 在圆锥边缘，混合更多从圆锥中心到顶点的方向，创建发散效果
                float directionBlend = 1.0 - angleFalloff * 0.7; // 在边缘增加发散效果
                
                // 4. 计算最终风方向
                // 基础方向是圆锥方向（风吹的方向）
                float3 baseWindDir = coneDir;
                
                // 在圆锥边缘添加发散效果：风从圆锥中心向外发散
                float3 divergentDir = normalize(coneDir + coneToVertexDir * 0.5);
                
                // 混合基础方向和发散方向，应用过渡因子使方向变化平滑
                float smoothDirectionBlend = lerp(1.0, 1.0 - directionBlend, distanceFactor * angleFactor);
                adjustedWindDirection = normalize(lerp(baseWindDir, divergentDir, smoothDirectionBlend));
                
                // 5. 确保风方向与圆锥方向大致一致（防止反向）
                float alignment = dot(adjustedWindDirection, coneDir);
                if (alignment < 0.3) // 如果方向偏差太大，强制使用圆锥方向
                {
                    adjustedWindDirection = coneDir;
                }
                
                // 6. 增强方向感：在圆锥中心轴附近，风方向更集中
                // 在边缘，风方向更发散
                float directionFocus = 1.0 + angleFalloff * 2.0; // 边缘增加发散度
                adjustedWindDirection = normalize(adjustedWindDirection * directionFocus);
                
                // 7. 计算BaseMove.w调整因子：在圆锥中心处过渡为0.1
                // 在圆锥中心轴附近，减少基础移动的影响，使毛发更明显地沿着风吹的方向飘动
                // 目标值：圆锥中心处为0.1，边缘处为1.0（不调整）
                float targetBaseMoveW = 0.5; // 圆锥中心处的目标值
                // 使用平滑过渡：在圆锥边界处逐渐过渡
                baseMoveWAdjustment = lerp(targetBaseMoveW, 1.0, saturate(falloff * 2.0));
                
                // 计算最终影响值：基础值 + 圆锥内增强
                // 在圆锥中心轴附近，风频率和强度都增加
                float influence = 1.0 + falloff * (coneIntensity - 1.0);
                
                // 优化：增强方向感的影响
                // 在圆锥中心轴附近，影响更强
                influence *= 1.0 + angleFalloff * 0.5;
                
                // 应用过渡因子，使影响值在边界处平滑变化
                influence = lerp(1.0, influence, distanceFactor * angleFactor);
                
                return influence;
            }
            
            void AppendShellVertex(inout TriangleStream<Varyings> stream, Attributes input, int index, float3 deformedBasePosWS, float3 originalPosWS, float3 normalWS, float2 uv)
            {
                Varyings output = (Varyings)0;

                float3 posOS = input.positionOS.xyz;
                
                float3 posWS = float3(0.0f, 0.0f, 0.0f);
                float3 windMove = float3(0, 0, 0);
                
                // ============================================
                // 风动计算逻辑详解
                // ============================================
                
                // 1. 移动因子计算：控制不同毛发层的移动强度
                // 公式：moveFactor = pow(abs(index / _ShellAmount), _BaseMove.w)
                // - index: 当前毛发层索引 (0为基础层，越大越靠近毛发尖端)
                // - _ShellAmount: 总毛发层数
                // - _BaseMove.w: 指数参数，控制移动强度随层数的变化曲线
                //   值越大，外层毛发移动强度衰减越快
                float moveFactor = fastPow(abs((float)index / _ShellAmount), _BaseMove.w);
                float winmoveW = _WindMove.w;
                
                // 2. 计算圆锥形风力影响（无论是否启用风，都需要计算BaseMove.w调整）
                float baseMoveWAdjustment = 1.0;
                float3 adjustedWindDirection = normalize(_WindMove.xyz);
                float coneInfluence = 1.0;
                
                if (_UseWindCone > 0.5)
                {
                    // 计算圆锥形风力影响，获取BaseMove.w调整因子
                    coneInfluence = CalculateWindConeInfluence(deformedBasePosWS, adjustedWindDirection, baseMoveWAdjustment);
                    
                    // 应用BaseMove.w调整：在圆锥中心处过渡为0.1
                    // 调整移动因子计算，使用调整后的BaseMove.w值
                    float adjustedBaseMoveW = _BaseMove.w * baseMoveWAdjustment;
                    moveFactor = fastPow(abs((float)index / _ShellAmount), adjustedBaseMoveW);
                }
                
                // 3. 风动画计算（当_UseWind启用时）
                if (_UseWind > 0.5)
                {
                    // 3.1 计算风角度：基于时间和频率参数
                    float3 windAngle = _Time.w * _WindFreq.xyz;
                    
                    // 3.2 风移动向量
                    float3 windMoveVector = _WindMove.xyz;
                    
                    // 仅在圆锥风启用时应用圆锥效果
                    if (_UseWindCone > 0.5)
                    {
                        // 应用圆锥频率增强
                        float frequencyBoost = 1.0 + (coneInfluence - 1.0) * _WindConeFrequencyBoost;
                        windAngle *= frequencyBoost;
                        
                        // 调整风方向以匹配圆锥方向
                        windMoveVector = adjustedWindDirection * length(_WindMove.xyz);
                        // 应用圆锥强度影响
                        windMoveVector *= coneInfluence;
                    }
                    
                    windMove = moveFactor * windMoveVector * sin(windAngle + posOS * _WindMove.w);
                }
                
                // 3. 基础移动计算
                // 公式：move = moveFactor * _BaseMove.xyz
                // - _BaseMove.xyz: 基础移动方向向量
                // 提供毛发的整体运动方向，如重力效果或基础摆动
                float3 move = moveFactor * _BaseMove.xyz;
                
                // 4. 触摸区域移动衰减：使用Lerp做渐变过渡，使效果更自然
                // 只有在启用触摸时才计算触摸衰减
                if (_UseTouch > 0.5)
                {
                    // 计算变形后的基础位置到所有触摸点的最小距离（世界空间）
                    // 使用原始位置计算距离，避免变形后位置偏移导致距离不准
                    float4 touchPositions[4] = { _TouchPosition, _TouchPosition2, _TouchPosition3, _TouchPosition4 };
                    float minDistanceToTouch = 999999.0;
                    
                    for (int i = 0; i < 4; i++)
                    {
                        float3 touchPos = touchPositions[i].xyz;
                        
                        // 如果触摸点在地下1000单位，表示无效，跳过
                        if (touchPos.y < -999.0)
                            continue;
                        
                        float distanceToTouch = length(originalPosWS - touchPos);
                        minDistanceToTouch = min(minDistanceToTouch, distanceToTouch);
                    }
                    
                    // 如果找到了有效的触摸点
                    if (minDistanceToTouch < 999999.0)
                    {
                        // 计算衰减因子：使用smoothstep实现平滑过渡
                        float falloff = 1.0 - smoothstep(_TouchRadius * 0.5, _TouchRadius, minDistanceToTouch);
                        
                        // 根据衰减因子减弱所有移动
                        windMove = lerp(float3(0, -0.9, 0), windMove, 1.0 - falloff);
                        _WindMove.w = lerp(0, winmoveW, 1.0 - falloff);
                    }
                }
                
                // 5. 最终毛发方向计算
                // 公式：shellDir = normalize(normalWS + move + windMove)
                // - normalWS: 世界空间法线方向
                // - move: 基础移动向量
                // - windMove: 风移动向量
                // 归一化得到最终的毛发生长方向
                float3 shellDir = normalize(normalWS + move + windMove);
                
                // 6. Noise弯曲偏移：采样noise纹理，对各层毛发施加横向偏移，模拟毛发弯曲
                // noise.rg 映射到 [-1,1]，乘以层因子使外层弯曲更明显
                // _NoiseMap_ST.zw 作为纹理游走速度，值为0时跳过游走计算
                float2 noiseScrollSpeed = _NoiseMap_ST.zw;
                float2 noiseUV = uv * _NoiseMap_ST.xy; // 只应用Tiling，Offset用于游走速度
                if (abs(noiseScrollSpeed.x) > 0.0001 || abs(noiseScrollSpeed.y) > 0.0001)
                    noiseUV += noiseScrollSpeed * _Time.y;
                float2 noiseSample = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, noiseUV, 1).rg;
                float2 bendOffset = (noiseSample * 2.0 - 1.0) * _NoiseBendStrength * moveFactor;
                // 将偏移量投影到切线平面（沿法线的垂直平面），避免穿入模型
                float3 tangentWS = normalize(cross(normalWS, float3(0, 1, 0.0001)));
                float3 bitangentWS = normalize(cross(normalWS, tangentWS));
                shellDir = normalize(shellDir + tangentWS * bendOffset.x + bitangentWS * bendOffset.y);
                float BaseMapA = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 1).a;
                if (index > 0)
                {
                    float3 viewDirOS = GetViewDirectionOS(posOS);
                    float eyeDotN = dot(viewDirOS, input.normalOS);
                    if (abs(eyeDotN) < _FaceViewProdThresh) return;
                    
                    // 基于变形后的基础位置计算上层毛发位置(BaseMapA颜色图A通道影响毛发长度)
                    posWS = deformedBasePosWS + shellDir * (_FurLength*BaseMapA * index);
                }
                else
                {
                    // index <= 0（包括基础层和第一层毛发）使用变形后的基础位置
                    posWS = deformedBasePosWS;
                }
                float4 posCS = TransformWorldToHClip(posWS);
                
                // 计算世界空间视角方向
                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(posWS);
                
                // 计算Fresnel效果（基础版本，1 - N·V）
                float Fresnel = 1.0h - max(0.0h, dot(normalWS, viewDirWS));
                
                // ============================================
                // 阴影计算：支持顶点阴影和像素阴影两种模式
                // ============================================
                float shadowAttenuation = 1.0;
                Light mainLight = GetMainLight();
                
                #ifdef _USEVERSHADOW
                    // 顶点阴影模式：在几何着色器中计算阴影（性能更好）
                    #ifdef _USESELFSHADOW
                        float4 shadowCoord = TransformWorldToShadowCoord(posWS);
                        shadowCoord.w = max(shadowCoord.w, 0.001);
                        mainLight = GetMainLight(shadowCoord);
                        shadowAttenuation = mainLight.shadowAttenuation;
                    #endif
                #else
                    // 像素阴影模式：传递位置和法线到片段着色器（质量更好）
                    // 阴影将在片段着色器中计算
                    shadowAttenuation = 1.0; // 顶点阶段不计算阴影
                #endif
                // ⚠ 不要在这里对 shadowAttenuation 单独做 lift，两个理由：
                //   1) 会与下方 NdotL 的 lift 相乘，导致阴影区(N·S)比明暗交界暗侧暗 SB 倍，交界线亮度断层；
                //   2) 改写 shadowAttenuation 会污染下方边缘光的 shadowRimAtten（那里要的是原始遮蔽值）。
                // 像素阴影模式此处 shadowAttenuation 恒为 1.0（下方合并时为 no-op），由 frag 内部处理。
                
                // ============================================
                // 团结引擎兼容性修复：distanceAttenuation 可能返回异常值
                // ============================================
                float distanceAtten = 1.0; // 默认无衰减
                
                #ifdef _USEDISTANCEATTEN
                    distanceAtten = mainLight.distanceAttenuation;
                    
                    // 修复异常值：如果小于0.001或为NaN，设置为1.0（无衰减）
                    if (distanceAtten < 0.001 || isnan(distanceAtten) || isinf(distanceAtten))
                    {
                        distanceAtten = 1.0;
                    }
                #endif
                
                // 计算NdotL和diffuse
                // 明暗交界线软度：smoothstep 阈值化 (1.0=原版渐变, 0.0=硬色阶)
                // 公式：smoothstep(0.5-w*0.5, 0.5+w*0.5, raw) 其中 w = _LightStepSoftness
                //   1.0 → smoothstep(0, 1, raw) = raw                       (向后兼容)
                //   0.0 → smoothstep(0.5, 0.5, raw) = step(0.5, raw)       (硬色阶)
                float NdotL_raw = saturate(dot(normalWS, mainLight.direction));
                float NdotL = smoothstep(0.5 - _LightStepSoftness * 0.5,
                                          0.5 + _LightStepSoftness * 0.5,
                                          NdotL_raw);
                // ============ 暗部亮度统一（_ShadowBrightness）============
                // 明暗交界(NdotL) 与自身投影阴影(shadowAttenuation) 必须**先合并、
                // 再 lift 一次**，不能各自独立 lift。独立 lift 时两项相乘：
                //     lerp(SB,1,N) * lerp(SB,1,S)
                // 阴影区(N=0,S=0) 落到 SB²，明暗交界暗侧(N=0,S=1) 落到 SB，
                // 两者差一个 SB 倍 → 交界处出现亮度断层，看上去"阴影比明暗交界还黑"。
                // 合并后全暗部（无论亮度来自交界还是阴影）下限都恰好是 _ShadowBrightness：
                //     directMask = lerp(SB, 1, saturate(N * S))
                // 逐项核对兼容性：当 N=1 或 S=1 时与旧公式**完全等价**
                // (lerp(SB,1,x)·1 = lerp(SB,1,x))，差异仅出现在两项同时 <1 的重叠区，
                // 而那正是需要被抹平的断层位置 → 已调好的 _ShadowBrightness 手感不变。
                //   0 = 暗部保持原样(最暗)；1 = 暗部抬满与亮区同等亮度；0~1 = 线性抬亮
                float directMask = lerp(_ShadowBrightness, 1.0, saturate(NdotL * shadowAttenuation));

                // 使用修复后的距离衰减值
                float3 diffuse = directMask * mainLight.color * distanceAtten;
                
                // ============ 背光边缘光（逆光轮廓模拟）============
                // 只在"逆光方向的外边界（轮廓）"出现边缘光，内部/顺光无：
                //   1) 外边界：Fresnel = 1 - N·V，法线与视线越垂直越靠近模型轮廓
                //   2) 逆光幅度：backlit = -dot(V, L)，相机方向与主光方向的对角程度
                //      相机、物体、主光成一直线（最逆光，V·L = -1）时最强，
                //      相机偏离时渐弱；顺光（V·L > 0）时 backlit = 0，彻底无边缘光
                //   3) 内部：正对相机表面（Fresnel 小）edgeMask ≈ 0，无边缘光
                //   4) 自身投影遮挡：阴影中边缘光按 _RimShadowStrength 保留（0 = 完全遮挡，1 = 阴影中不衰减）
                float backlit = saturate(-dot(viewDirWS, mainLight.direction));                        // 逆光幅度（相机与主光对角）
                float edgeMask = smoothstep(_RimLightEdgeStart, _RimLightEdgeEnd, saturate(Fresnel)); // 外边界
                // 底部方向约束：法线朝下（normalWS.y < 0）的面减弱边缘光，抑制下巴等朝下的倾斜面
                float downward = saturate(-normalWS.y); // 0 = 朝上/水平，1 = 纯朝下
                float bottomAtten = 1.0 - downward * (1.0 - _RimBottomStrength);
                float shadowRimAtten = lerp(_RimShadowStrength, 1.0, shadowAttenuation); // 阴影区域边缘光保留强度
                float rimMask = edgeMask * backlit * bottomAtten * _RimLightIntensity * shadowRimAtten;
                diffuse += rimMask; // 保留标量项（保持与原逻辑兼容），色彩项追加到 lighting 末尾
                // ● 边缘光颜色 = 主光颜色 × _RimLightColor（继承主光，默认白色 = 纯主光色）
                //   在 lighting 末尾独立累加，绕开 _BaseColor（毛发底色）染色，模拟"光穿透毛发"的物理感
                float3 rimColor = mainLight.color * _RimLightColor.rgb;
                float3 rimAdd = rimMask * rimColor;
                half baseAlpha = 1;
                // 计算环境光
                float3 ambient = SampleSH(normalWS);
                if (_UseAlpha > 0.5)    // 使用颜色贴图a通道排除无毛发处的MainColor影响
                {   // 即使物体很远，也强制使用固定LOD=0（原始分辨率）
                    baseAlpha = BaseMapA;
                }
                
                // 合并灯光贡献（包含Fresnel）
                float3 lighting = (diffuse + ambient) * lerp(float3(1,1,1), _BaseColor.rgb, baseAlpha);
                // 边缘光按"主光颜色"独立追加，不被 _BaseColor 染色（继承主光）
                lighting += rimAdd;
                
                // 预计算alpha相关因子（考虑基础遮挡）
                float alphaFactor = _Occlusion; // 将光影计算移至顶点，这里就不能反转 1.0 - _Occlusion
                
                output.vertex = posCS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.layer = (float)index / _ShellAmount;
                output.lighting = lighting;
                output.alphaFactor = alphaFactor;
                output.viewDirWS = viewDirWS;
                
                #ifndef _USEVERSHADOW
                    // 像素阴影模式：传递位置和法线到片段着色器
                    output.positionWS = posWS;
                    output.normalWS = normalWS;
                #endif

                stream.Append(output);
            }
            
            #define MAX_SHELL_LAYERS _ShellAmount
            
            // 根据是否启用像素阴影，动态调整最大顶点数
            // 像素阴影模式：每个顶点20个标量（增加了positionWS和normalWS）
            // 顶点阴影模式：每个顶点14个标量
            // 硬件限制：总标量数不能超过1024
            #ifndef _USEVERSHADOW
                // 像素阴影：1024 / 21 = 48.7，取48（16层毛发）
                [maxvertexcount(48)]
            #else
                // 顶点阴影：1024 / 15 = 68.2，取66（22层毛发）
                [maxvertexcount(66)]
            #endif
            void geom(triangle Attributes input[3], inout TriangleStream<Varyings> stream)
            {
                // int maxLayers = 72 / 3;
                // int layers = min(_ShellAmount, maxLayers);

                // 预先计算每个顶点的变形位置和法线
                float3 deformedBasePosWS[3];    //声明一个包含3个元素的数组，用于存储每个顶点变形后的世界空间位置
                float3 originalBasePosWS[3];    //存储原始世界空间位置（用于触摸区域风力衰减距离计算）
                float3 normalWS[3];     //用于存储每个顶点的世界空间法线
                
                if (_UseTouch>0.5){
                [unroll] for (int k = 0; k < 3; k++)
                {
                    VertexPositionInputs vertexInput = GetVertexPositionInputs(input[k].positionOS.xyz);
                    VertexNormalInputs normalInput = GetVertexNormalInputs(input[k].normalOS, input[k].tangentOS);
                    // 计算变形后的基础位置（触摸挤压效果）
                    float3 basePosWS = vertexInput.positionWS;
                    originalBasePosWS[k] = basePosWS;
                    // 获取UV坐标用于纹理采样
                    float2 uv = TRANSFORM_TEX(input[k].uv, _BaseMap);
                    deformedBasePosWS[k] = ApplyTouchDeformation(basePosWS, normalInput.normalWS, uv);
                    normalWS[k] = normalize(normalInput.normalWS);
                }
                }else{
                // 当_USETOUCH未定义时，仍然需要初始化数组
                [unroll] for (int k = 0; k < 3; k++)
                {
                    VertexPositionInputs vertexInput = GetVertexPositionInputs(input[k].positionOS.xyz);
                    VertexNormalInputs normalInput = GetVertexNormalInputs(input[k].normalOS, input[k].tangentOS);
                    // 没有触摸变形，直接使用原始位置
                    deformedBasePosWS[k] = vertexInput.positionWS;
                    originalBasePosWS[k] = vertexInput.positionWS;
                    normalWS[k] = normalize(normalInput.normalWS);
                }
                }

                // 边缘光的底部方向约束已在 AppendShellVertex 内按 normalWS.y 计算（朝下面减弱），
                // 因此几何着色器无需再按三角形 N·V 符号做轮廓检测。
// 推荐：移除 [loop]，让编译器自动展开（或显式 [unroll]）`[unroll]` 指令提示编译器展开循环，提高GPU执行效率
                for (int i = MAX_SHELL_LAYERS - 1; i >= -1; i--)
                {
                    [unroll] for (int j = 0; j < 3; j++)
                    {
                        float2 uv = TRANSFORM_TEX(input[j].uv, _BaseMap);
                        AppendShellVertex(stream, input[j], i, deformedBasePosWS[j], originalBasePosWS[j], normalWS[j], uv);
                    }
                    stream.RestartStrip();
                }
            }

            float4 frag(Varyings input) : SV_Target
            {
                // 像素级BaseMap颜色采样（保持像素级质量）
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);

                // 毛发层逻辑（贴图采样）
                float4 furColor = SAMPLE_TEXTURE2D(_FurMap, sampler_FurMap, input.uv * _FurScale);
                
                // 每层毛发宽度控制因子 - 优化版本
                // 1. 计算基础毛发密度：增强纹理对比度
                float furDensity = furColor.r * 2.0;
                
                // 2. 计算偏移量：简化数学表达式，提高可读性
                // 原公式：_AlphaOffset*_AlphaOffset + (_AlphaOffset*_AlphaCutout*2)
                // 优化为：_AlphaOffset * (_AlphaOffset + _AlphaCutout * 6.0)
                float offsetValue = _AlphaOffset * (_AlphaOffset + _AlphaCutout * 6.0);
                
                // 3. 计算基础alpha值：毛发密度减去偏移量
                float baseAlpha = furDensity - offsetValue;
                
                // 4. 应用层衰减：越靠近毛发尖端透明度越高
                float alpha = baseAlpha * (1.0 - input.layer);
                
                // 5. 使用BaseMap的alpha通道控制毛发区域并加强剔除范围
                // 加强baseColor.a的作用：不仅作为蒙版，还影响剔除阈值
                half baseAlphaMask = baseColor.a;
                
                // 应用baseColor.a作为毛发区域蒙版
                alpha *= baseAlphaMask;
                
                // Alpha裁剪 - 加强baseColor.a的剔除范围
                if (input.layer < 0.0)
                {   // 最底层：增强alpha确保基础层可见
                    // 当layer为负时，(1.0 - input.layer) > 1，增强alpha值
                    alpha = alpha * (1.0 - input.layer);
                    // 确保alpha在有效范围内
                    alpha = clamp(alpha, 0.0, 1.0);
                }
                else
                {   // 其他层：根据综合阈值剔除稀疏片段，加强baseColor.a的影响
                    // 优化剔除逻辑：加强baseColor.a在剔除中的作用
                    // 1. 基础剔除阈值
                    float baseDiscardThreshold = max(_AlphaCutout, _AlphaOffset * 0.5);
                    
                    // 2. 根据baseColor.a调整剔除阈值：baseColor.a越低，剔除越严格
                    // 使用指数函数加强baseColor.a的影响：baseColor.a^2
                    float alphaMaskFactor = baseAlphaMask * baseAlphaMask; // 平方加强影响
                    
                    // 3. 计算最终剔除阈值：baseColor.a越低，阈值越高（更容易剔除）
                    // 当baseColor.a接近1时，使用基础阈值；当baseColor.a接近0时，阈值增加
                    float adjustedThreshold = lerp(baseDiscardThreshold * 2.0, baseDiscardThreshold, alphaMaskFactor);
                    
                    // 4. 应用剔除：alpha小于调整后的阈值时剔除
                    if (alpha < adjustedThreshold) discard;
                    
                    // 确保alpha在有效范围内
                    alpha = saturate(alpha);
                }
                
                // 使用预计算的遮挡因子（考虑层间混合，使用baseColor.a排除最底层无毛发处AO影响）
                float occlusion = lerp(1.0 - input.alphaFactor * baseColor.a, 1.0, input.layer);
                float3 color = baseColor.xyz * occlusion;
                
                // ============================================
                // 像素阴影计算（仅在未启用顶点阴影时）
                // ============================================
                float3 lighting = input.lighting;
                
                #ifndef _USEVERSHADOW
                    // 像素阴影模式：在片段着色器中重新计算阴影（质量更好）
                    #ifdef _USESELFSHADOW
                        // 获取主光源和阴影
                        float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                        shadowCoord.w = max(shadowCoord.w, 0.001);
                        Light mainLight = GetMainLight(shadowCoord);
                        float pixelShadowAtten = mainLight.shadowAttenuation;
                        
                        // 重新计算光照（使用像素级阴影）
                        // 与 vertex 阶段一致：smoothstep 阈值化 (1=原版渐变, 0=硬色阶)
                        float NdotL_raw = saturate(dot(input.normalWS, mainLight.direction));
                        float NdotL = smoothstep(0.5 - _LightStepSoftness * 0.5,
                                                  0.5 + _LightStepSoftness * 0.5,
                                                  NdotL_raw);
                        
                        // 计算Fresnel（外边界 = 1 - N·V）
                        float fresnel = 1.0 - max(0.0, dot(input.normalWS, normalize(input.viewDirWS)));
                        
                        // 暗部亮度统一（_ShadowBrightness）：与 vertex 阶段**同公式**
                        // lerp(SB,1,saturate(N·S)) —— 合并后只 lift 一次，
                        // 使明暗交界与自身阴影的暗部下限一致（详见 AppendShellVertex 内注释）。
                        // 两个 stage 公式必须逐字一致，否则切换 _USEVERSHADOW 会改变明暗关系。
                        // 注意：pixelShadowAtten 保持原始遮蔽值不改写，
                        // 下方 shadowRimAttenPS（边缘光）需要的就是未 lift 的遮蔽。
                        float directMaskPS = lerp(_ShadowBrightness, 1.0, saturate(NdotL * pixelShadowAtten));
                        
                        // 重新计算diffuse（使用像素级阴影）
                        float3 pixelDiffuse = directMaskPS * mainLight.color;
                        // 背光边缘光：逆光幅度用相机与主光对角程度（-dot(V,L)），只在逆光外边界出现
                        float backlitPS = saturate(-dot(normalize(input.viewDirWS), mainLight.direction));
                        // 底部方向约束：法线朝下的面减弱边缘光（下巴抑制）
                        float downwardPS = saturate(-input.normalWS.y);
                        float bottomAttenPS = 1.0 - downwardPS * (1.0 - _RimBottomStrength);
                        float shadowRimAttenPS = lerp(_RimShadowStrength, 1.0, pixelShadowAtten); // 阴影区域边缘光保留强度
                        float rimMaskPS = smoothstep(_RimLightEdgeStart, _RimLightEdgeEnd, saturate(fresnel)) * backlitPS * bottomAttenPS * _RimLightIntensity * shadowRimAttenPS;
                        pixelDiffuse += rimMaskPS;
                        // ● 边缘光色彩 = 主光色 × _RimLightColor（继承主光，默认白色 = 纯主光色），不被 _BaseColor 染色
                        float3 rimColorPS = mainLight.color * _RimLightColor.rgb;
                        float3 rimAddPS = rimMaskPS * rimColorPS;

                        // 环境光
                        float3 ambient = SampleSH(input.normalWS);

                        // 重新计算lighting（使用baseColor.a作为alpha控制）
                        half colorAlpha = 1;
                        if (_UseAlpha > 0.5)
                        {
                            colorAlpha = baseColor.a;
                        }
                        lighting = (pixelDiffuse + ambient) * lerp(float3(1,1,1), _BaseColor.rgb, colorAlpha);
                        // 边缘光按"主光颜色"独立追加，绕过 _BaseColor 染色
                        lighting += rimAddPS;
                    #endif
                #endif
                
                // 应用光照
                color *= lighting;
                // 整体亮度（参考 PBR_Mobile_NEW.shader:819）：finalColor *= _Brightness
                // 位置选择：放在 vertex 阶段 output.lighting 之后、pixel 阶段末尾，避开 vertex 重复乘
                color *= _Brightness;

                return float4(color, alpha);
            }
            // #endif
            // ============================================
            // 结束 UnlitLambert.hlsl
            // ============================================
            
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
        
            ZWrite On
            ColorMask 0
        
            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma vertex vert
            #pragma fragment frag
            
            // ============================================
            // 开始嵌入 DepthSimple.hlsl 内容
            // ============================================
            // #ifndef FUR_SHELL_DEPTH_HLSL
            // 包含UnlitInput.hlsl获取_BaseColor、_BaseMap等定义
            #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.vertex = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(0, 0, 0, 0);
            }
            // #endif
            // ============================================
            // 结束 DepthSimple.hlsl
            // ============================================
            
            ENDHLSL
        }
        
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
        
            ZWrite On
            ZTest LEqual
            ColorMask 0
        
            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma vertex vert
            #pragma fragment frag
            
            // ============================================
            // 开始嵌入 ShadowSimple.hlsl 内容
            // ============================================
            // #ifndef FUR_SHELL_SHADOW_HLSL
            // 包含UnlitInput.hlsl获取_BaseColor、_BaseMap等定义
            #include "Packages/com.unity.render-pipelines.universal/Shaders/UnlitInput.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.vertex = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            // #endif
            // ============================================
            // 结束 ShadowSimple.hlsl
            // ============================================
            
            ENDHLSL
        }
    }
    CustomEditor "FurShell_MobileGUI"
}
