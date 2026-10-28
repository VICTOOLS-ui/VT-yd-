// Custom_Ocean.shader GUI控制脚本
// 参考 Glass_carWindowGUI.cs 的控制逻辑与样式实现
// CustomOceanGUIv1.0 存档/读档/预设/重置 + 深度渐变、风格化波浪、渲染设置分区绘制
// CustomOceanGUIv1.1 对照 Custom_Ocean.shader 现有参数，移除已删除的边缘浪花（_EdgeFoamWidth/_EdgeFoamStrength）分区
// CustomOceanGUIv1.2 新增海面泡沫（Surface Foam）分区，绘制 _FoamMap 及泡沫参数
// CustomOceanGUIv1.3 泡沫分区新增细节纹理 _FoamDetailMap，Offset 作为游走参数（Tiling/Offset 可编辑）
// CustomOceanGUIv1.4 深度分区新增水色斑驳参数；泡沫细节参数改名为「泡沫网强度/泡沫网裁切」
// CustomOceanGUIv1.5 移除无实际效果的 _FoamMap 分区（含泡沫强度/裁切/流速），泡沫统一由 _FoamDetailMap 驱动
// CustomOceanGUIv1.6 水色斑驳新增「斑驳扰动速度」，说明文案改为域扭曲双层噪声算法
// CustomOceanGUIv1.7 海面泡沫新增「波浪扭曲泡沫」（波浪贴图作为泡沫扭曲源）
// CustomOceanGUIv1.8 渲染设置补充 URP 雾效说明（雾由 Renderer Data 控制）
// CustomOceanGUIv1.9 波浪/泡沫扭曲说明更新：条纹位置用平滑线性水深，扭曲改为双向梯度
// CustomOceanGUIv1.10 泡沫扭曲源由波浪贴图改为水色斑驳输出
// CustomOceanGUIv1.11 浪花颜色标注 A = 不透明度
// CustomOceanGUIv1.12 新增海面波涛 (Gerstner 顶点变形) 分区：
//                  Toggle _GerstnerEnabled + KeywordEnum _GerstnerQuality 同步 shader keyword
// 注意：Custom_Ocean shader_feature toggle 在此 GUI 中显式同步

using UnityEngine;
using UnityEditor;
using VicTools;

public class CustomOceanGUI : ShaderGUI
{
    private MaterialEditor m_MaterialEditor;
    private MaterialProperty[] m_Properties;

    // 缓存属性：全局设置
    // 接收阴影：全局开关（控制是否计算主光阴影），与 shader 的 _RECEIVE_SHADOWS keyword 联动
    //   关闭 = 跳过 shadowMap 采样 + 整条 shadow path（零开销）
    //   启用 = 计算阴影，由 _ShadowStrength 控制最终强度（0=不暗 1=完全）
    private MaterialProperty receiveShadows;
    // 主光接收强度：0 = 水面不响应主光（自然色）；1 = 完全响应主光方向 + Directional Light Intensity
    private MaterialProperty mainLightStrength;

    // 缓存属性：深度渐变
    private MaterialProperty depthShallowColor;
    private MaterialProperty depthDeepColor;
    private MaterialProperty depthMaxDistance;
    private MaterialProperty waterColorVariation;
    private MaterialProperty waterVariationScale;
    private MaterialProperty waterVariationSpeed;

    // 简化 label/tooltip 构造：name + tooltip 分离，详细说明进 tooltip
    private static GUIContent L(string name, string tip) => new GUIContent(name, tip);

    // 缓存属性：风格化波浪
    // 岸边波浪图案：复用 _BumpMap.R 通道（与高光法线贴图共用一张纹理）
    //   Tiling 由 _WavePatternTilingX/Y 独立控制（与 _BumpMap_ST 解耦）
    private MaterialProperty wavePatternTilingX;
    private MaterialProperty wavePatternTilingY;
    private MaterialProperty waveColor;
    private MaterialProperty waveMaxDistance;
    private MaterialProperty waveFalloff;
    private MaterialProperty waveSpeed;
    private MaterialProperty waveStrength;
    private MaterialProperty waveCutOff;

    // 缓存属性：海面泡沫
    private MaterialProperty foamColor;
    private MaterialProperty foamDetailMap;
    private MaterialProperty foamDetailStrength;
    private MaterialProperty foamDetailCutOff;
    private MaterialProperty foamDistortStrength;

    // 缓存属性：渲染设置
    private MaterialProperty cullMode;

    // 缓存属性：海面波涛（Gerstner 顶点位移）
    private MaterialProperty gerstnerEnabled;
    private MaterialProperty gerstnerQuality;       // 0=Low / 1=Medium / 2=High
    private MaterialProperty gerstnerAmplitude;      // 波涛振幅（整体高低）
    private MaterialProperty gerstnerWaveSize;       // 波形大小（整体缩放，缩波数 k）
    private MaterialProperty gerstnerSpeed;
    private MaterialProperty gerstnerChoppy;         // 浪尖锐度 Q（陡度）
    private MaterialProperty crestMap;              // 浪尖浪花纹理
    private MaterialProperty crestColor;            // 浪尖浪花颜色（A=整体强度）
    private MaterialProperty crestSharpness;        // 浪花锐度 0~1（合并 sharpness+focus）
    private MaterialProperty crestDetail;           // 浪花飞沫细节 0~1（合并 noiseScale+density）
    private MaterialProperty crestLife;             // 浪花寿命 0~1（合并 delay+decay）
    private MaterialProperty crestDistortStrength;  // 海面泡沫扭曲浪花的强度

    // 缓存属性：高光（参考 PBR_Mobile_NEW.SimpleSpecular）
    private MaterialProperty bumpMap;             // 高光法线贴图（_BumpMap）：TS→WS 变换后参与高光 normal
    private MaterialProperty bumpStrength;        // 高光法线强度
    private MaterialProperty specularBumpFalloff;  // 高光法线聚拢度（衰减 BumpMap 对高光法线的影响）
    private MaterialProperty shadowStrength;       // 主光阴影接收强度（0=不接收 1=完全接收）
    private MaterialProperty specularColor;       // 高光颜色
    private MaterialProperty specularStrength;    // 高光强度（0 = 关闭，shader 整段短路）
    private MaterialProperty specularSmoothness;  // 高光锐度（0=粗糙, 1=光滑）

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        m_MaterialEditor = materialEditor;
        m_Properties = properties;

        // 查找所有属性
        FindProperties();

        // 绘制 GUI
        DrawGlobalSettings();

        // 当前编辑的材质（多个目标共用同一个 ShaderGUI 实例时，遍历所有 targets 同步 keyword）
        var editorTargets = m_MaterialEditor != null ? m_MaterialEditor.targets : null;

        // 海面波涛：放在最前面，便于一眼看到是否开启；keyword 在此同步
        using (new GUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawVertexWave();
        }
        SyncGerstnerKeywords(editorTargets);

        using (new GUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawDepthGradient();
        }
        using (new GUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawStylizedWave();
        }
        using (new GUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawSurfaceFoam();
        }
        using (new GUILayout.VerticalScope(EditorStyles.helpBox))
        {
            DrawSpecular();
        }
        DrawRenderSettings();
    }

    private void FindProperties()
    {
        receiveShadows       = FindProperty("_ReceiveShadows", m_Properties);
        mainLightStrength    = FindProperty("_MainLightStrength", m_Properties, false);

        depthShallowColor = FindProperty("_DepthShallowColor", m_Properties);
        depthDeepColor    = FindProperty("_DepthDeepColor", m_Properties);
        depthMaxDistance  = FindProperty("_DepthMaxDistance", m_Properties);
        waterColorVariation  = FindProperty("_WaterColorVariation", m_Properties);
        waterVariationScale  = FindProperty("_WaterVariationScale", m_Properties);
        waterVariationSpeed  = FindProperty("_WaterVariationSpeed", m_Properties);

        wavePatternTilingX    = FindProperty("_WavePatternTilingX", m_Properties);
        wavePatternTilingY    = FindProperty("_WavePatternTilingY", m_Properties);
        waveColor             = FindProperty("_WaveColor", m_Properties);
        waveMaxDistance  = FindProperty("_WaveMaxDistance", m_Properties);
        waveFalloff      = FindProperty("_WaveFalloff", m_Properties);
        waveSpeed        = FindProperty("_WaveSpeed", m_Properties);
        waveStrength     = FindProperty("_WaveStrength", m_Properties);
        waveCutOff       = FindProperty("_WaveCutOff", m_Properties);

        foamColor           = FindProperty("_FoamColor", m_Properties);
        foamDetailMap       = FindProperty("_FoamDetailMap", m_Properties);
        foamDetailStrength  = FindProperty("_FoamDetailStrength", m_Properties);
        foamDetailCutOff    = FindProperty("_FoamDetailCutOff", m_Properties);
        foamDistortStrength = FindProperty("_FoamDistortStrength", m_Properties);

        cullMode = FindProperty("_Cull", m_Properties);

        // 海面波涛 (Gerstner)
        gerstnerEnabled        = FindProperty("_GerstnerEnabled",        m_Properties, false);
        gerstnerQuality        = FindProperty("_GerstnerQuality",        m_Properties, false);
        gerstnerAmplitude      = FindProperty("_GerstnerAmplitude",      m_Properties, false);
        gerstnerWaveSize       = FindProperty("_GerstnerWaveSize",       m_Properties, false);
        gerstnerSpeed          = FindProperty("_GerstnerSpeed",          m_Properties, false);
        gerstnerChoppy         = FindProperty("_GerstnerChoppy",         m_Properties, false);
        crestMap               = FindProperty("_CrestMap",               m_Properties, false);
        crestColor             = FindProperty("_CrestColor",             m_Properties, false);
        crestSharpness         = FindProperty("_CrestSharpness",         m_Properties, false);
        crestDetail            = FindProperty("_CrestDetail",            m_Properties, false);
        crestLife              = FindProperty("_CrestLife",              m_Properties, false);
        crestDistortStrength   = FindProperty("_CrestDistortStrength",   m_Properties, false);
        // 高光（参考 PBR_Mobile_NEW.SimpleSpecular）
        bumpMap                = FindProperty("_BumpMap",                m_Properties, false);
        bumpStrength           = FindProperty("_BumpStrength",           m_Properties, false);
        specularBumpFalloff    = FindProperty("_SpecularBumpFalloff",    m_Properties, false);
        shadowStrength         = FindProperty("_ShadowStrength",         m_Properties, false);
        specularColor          = FindProperty("_SpecularColor",          m_Properties, false);
        specularStrength       = FindProperty("_SpecularStrength",       m_Properties, false);
        specularSmoothness     = FindProperty("_SpecularSmoothness",     m_Properties, false);
    }

    private void DrawGlobalSettings()
    {
        // 顶部工具栏：标题 + 存档/读档/重置/预设 4 个 utility 按钮
        //   布局参考 PBR_MobileGUI.DrawGlobalSettings：utility 按钮独占顶部一行，
        //   全局参数 _ReceiveShadows 放独立下一行，避免挤在按钮行里造成 checkbox 可点击区域被压缩
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(HeaderStyle.Rich("全局设置", HeaderStyle.HeaderTitle), EditorStyle.Get.BoldLabelRichStyle);

        // 添加存档按钮
        GUI.backgroundColor = new Color(0.3f, 0.8f, 1.0f); // 蓝色背景
        if (GUILayout.Button("存档", GUILayout.Width(50)))
        {
            EditorApplication.delayCall += SaveMaterialParameters;
        }

        // 添加读档按钮
        GUI.backgroundColor = new Color(0.5f, 1.0f, 0.5f); // 绿色背景
        if (GUILayout.Button("读档 ▾", GUILayout.Width(55)))
        {
            ShowLoadDropdown();
        }

        // 添加重置按钮
        GUI.backgroundColor = new Color(1.0f, 0.8f, 0.3f); // 黄色背景
        if (GUILayout.Button("重置参数", GUILayout.Width(60)))
        {
            EditorApplication.delayCall += ResetMaterialParameters;
        }

        // 预设下拉菜单
        GUI.backgroundColor = new Color(0.9f, 0.7f, 1.0f); // 紫色背景
        if (GUILayout.Button("预设 ▾", GUILayout.Width(55)))
        {
            ShowPresetDropdown();
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        // 全局参数：_ReceiveShadows（与 shader _RECEIVE_SHADOWS keyword 联动）
        //   独立一行：参考 PBR_MobileGUI.useSoftShadow 的全局参数布局，
        //   避免挤在顶部按钮行造成 checkbox 难点击。
        //   关联参数 _ShadowStrength 在「高光 (Specular)」分区中按本开关条件显示。
        EditorGUILayout.BeginHorizontal();
        m_MaterialEditor.ShaderProperty(receiveShadows,
            L("接收阴影",
              "全局开关：是否计算主光阴影（与 shader _RECEIVE_SHADOWS keyword 联动）。\n" +
              "关闭 = 跳过 shadowMap 采样（零开销）\n" +
              "启用 = 计算阴影，最终暗度由 _ShadowStrength 控制（0=不暗 1=完全）"));
        EditorGUILayout.EndHorizontal();

        // 主光接收强度：与 _ReceiveShadows 解耦——接收主光 ≠ 接收阴影
        //   紧跟「接收阴影」下方独立一行（不放同行右侧，避免 checkbox / slider 互相挤压可点击区）
        //   0 = 水面不响应主光（自然 unlit 色，与旧版完全一致）
        //   1 = 水面随主光方向 + Directional Light Intensity 连续变化
        //   用 Rec.709 luminance 取主光亮度，避免给水面强加太阳色调
        if (mainLightStrength != null)
        {
            EditorGUILayout.BeginHorizontal();
            m_MaterialEditor.ShaderProperty(mainLightStrength,
                L("主光接收强度",
                  "水面随主光方向 + 主光 Intensity 变化的强度。\n" +
                  "0 = 不接收（unlit，与旧版一致）\n" +
                  "1 = 完全接收（Directional Light Intensity = 0 时整片黑；= 1 时 half-lambert）\n" +
                  "与「接收阴影」独立——可单独控制。"));
            EditorGUILayout.EndHorizontal();
        }
    }

    /// 显示读档下拉菜单（用户存档，Library 目录）
    private void ShowLoadDropdown()
    {
        Material material = m_MaterialEditor.target as Material;
        if (material == null || material.shader == null) return;

        string shaderName = material.shader.name.Replace("/", "_");
        string folderPath = "Library/VicTools/Ocean/" + shaderName;

        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }

        string[] files = System.IO.Directory.GetFiles(folderPath, "*.json");

        GenericMenu menu = new GenericMenu();

        if (files.Length == 0)
        {
            menu.AddDisabledItem(new GUIContent("（无预设存档）"));
        }
        else
        {
            foreach (string file in files)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                string filePath = file; // 捕获到局部变量供lambda使用
                menu.AddItem(new GUIContent(fileName), false, () =>
                {
                    EditorApplication.delayCall += () => LoadPresetFile(filePath);
                });
            }
        }

        menu.ShowAsContext();
    }

    /// 显示预设下拉菜单（随包分发的预设，Packages 目录）
    private void ShowPresetDropdown()
    {
        Material material = m_MaterialEditor.target as Material;
        if (material == null || material.shader == null) return;

        string shaderName = material.shader.name.Replace("/", "_");
        string folderPath = "Packages/com.youdoo.victools/Runtime/Shaders~/" + shaderName;
        // v3.4 迁移：旧 Runtime/Shaders/<shaderName>/ → Runtime/Shaders~/<shaderName>/（AssetDatabase.MoveAsset 同步 .meta 保 GUID）
        string oldFolderPath = "Packages/com.youdoo.victools/Runtime/Shaders/" + shaderName;
        if (System.IO.Directory.Exists(oldFolderPath) && !System.IO.Directory.Exists(folderPath))
            AssetDatabase.MoveAsset(oldFolderPath, folderPath);

        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }

        string[] files = System.IO.Directory.GetFiles(folderPath, "*.json");

        GenericMenu menu = new GenericMenu();

        if (files.Length == 0)
        {
            menu.AddDisabledItem(new GUIContent("（无预设存档）"));
        }
        else
        {
            foreach (string file in files)
            {
                string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                string filePath = file; // 捕获到局部变量供lambda使用
                menu.AddItem(new GUIContent(fileName), false, () =>
                {
                    EditorApplication.delayCall += () => LoadPresetFile(filePath);
                });
            }
        }

        menu.ShowAsContext();
    }

    /// 从预设文件加载
    private void LoadPresetFile(string filePath)
    {
        if (!System.IO.File.Exists(filePath)) return;
        LoadMaterialParametersFromFile(filePath);
    }

    private void DrawDepthGradient()
    {
        GUILayout.Label(HeaderStyle.Rich("2 ▌深度渐变 (Depth Gradient)", HeaderStyle.Base), EditorStyle.Get.BoldLabelRichStyle);
        EditorGUILayout.HelpBox(
            "基于场景深度：越浅越接近浅水色，越深越接近深水色。\n" +
            "需要 URP 开启 Depth Texture（深度纹理）才能正确采样。",
            MessageType.Info);
        m_MaterialEditor.ColorProperty(depthShallowColor, "浅水颜色");
        m_MaterialEditor.ColorProperty(depthDeepColor, "深水颜色");
        m_MaterialEditor.FloatProperty(depthMaxDistance, "深度最大距离");
        m_MaterialEditor.RangeProperty(waterColorVariation, "水色斑驳强度");
        m_MaterialEditor.FloatProperty(waterVariationScale, "斑驳密度 (相对泡沫 Tiling)");
        m_MaterialEditor.FloatProperty(waterVariationSpeed, "斑驳扰动速度");

        // EditorGUILayout.HelpBox(
        //     "水色斑驳：用「海面泡沫」分区的泡沫纹理做低频采样，让浅/深水色交织出青色亮斑与深蓝暗块（参考实拍海面）。\n" +
        //     "采样频率 = 泡沫纹理 Tiling × 斑驳密度，取 0.3~0.6 得到大块斑驳；斑驳强度 0 时关闭该效果。\n" +
        //     "算法：一层噪声做域扭曲打散平铺感，另两层一大一小尺度缓慢反向漂移混合成大小不一的斑块；\n" +
        //     "斑驳值直接偏移深度渐变因子（亮斑趋浅水色、暗斑趋深水色），再叠加同色相轻微明暗起伏。",
        //     MessageType.Info);
    }

    private void DrawStylizedWave()
    {
        GUILayout.Label(HeaderStyle.Rich("3 ▌岸边波浪 (Shore Wave)", HeaderStyle.Sparkle), EditorStyle.Get.BoldLabelRichStyle);

        // 岸边波浪图案复用 _BumpMap.R 通道（与高光法线贴图共用一张纹理，节省一张采样）
        //   Tiling 独立控制（_WavePatternTilingX/Y），与 _BumpMap_ST 完全解耦
        EditorGUILayout.HelpBox(
            "岸边波浪图案复用『高光法线贴图 _BumpMap』的 R 通道：\n" +
            "  - 共享纹理 → 节省一张采样、减少内存\n" +
            "  - 独立 Tiling X/Y → 岸边波浪密度可独立调整，不影响高光法线 Tiling",
            MessageType.Info);
        m_MaterialEditor.FloatProperty(wavePatternTilingX, "图案 Tiling X");
        m_MaterialEditor.FloatProperty(wavePatternTilingY, "图案 Tiling Y");

        m_MaterialEditor.ColorProperty(waveColor, "岸边浪花颜色 (A = 不透明度)");
        m_MaterialEditor.FloatProperty(waveMaxDistance, "浪花最大距离");
        m_MaterialEditor.RangeProperty(waveFalloff, "岸边波浪外扩 (越小越宽)");
        m_MaterialEditor.FloatProperty(waveSpeed, "岸边波浪速度");
        m_MaterialEditor.FloatProperty(waveStrength, "岸边波浪强度");
        m_MaterialEditor.RangeProperty(waveCutOff, "岸边波浪裁切");

        // EditorGUILayout.HelpBox(
        //     "算法：贴图 R 通道 × 浪花衰减 × 强度，超过裁切值时二值化为条纹。\n" +
        //     "外扩 < 1 时衰减曲线被压平，浪花带向远海延伸更宽（只作用于强度，不改变条纹间距）；\n" +
        //     "条纹位置用的是「屏幕空间十字 5 点平均 + 线性」的水深，沿纵深方向间距均匀、\n" +
        //     "不会被海底起伏按各方向拉扯得疏密不匀；\n" +
        //     "波浪密度与贴图 Tiling Y 共同控制条纹数量；\n" +
        //     "浪花颜色的 A 通道 = 浪花不透明度：A 越小浪花越淡、越透出水色（A = 1 为完全覆盖），\n" +
        //     "浪花处的水面 alpha 取「水面与浪花 A 取大」，只增不减，不会把水面挖透明。",
        //     MessageType.Info);
    }

    private void DrawVertexWave()
    {
        GUILayout.Label(HeaderStyle.Rich("1 ▌海面波涛 (Vertex Gerstner)", HeaderStyle.Sparkle), EditorStyle.Get.BoldLabelRichStyle);

        // 若材质不带 Gerstner 属性（旧版 shader 升级残留 / 第三方），给一个清晰提示而不是抛异常
        if (gerstnerEnabled == null)
        {
            EditorGUILayout.HelpBox(
                "当前 Shader 缺少 _GerstnerEnabled 属性。\n请在 Project 中将使用的 Ocean 材质切换到带 Gerstner 顶点变形支持的 Custom_Ocean.shader。",
                MessageType.Warning);
            return;
        }

        // EditorGUILayout.HelpBox(
        //     "Gerstner 顶点位移：参考 NVIDIA GPU Gems Ch.1 多组波叠加求和，\n" +
        //     "在顶点阶段解析出位移与新法线（避免 fragment 重算）。\n" +
        //     "Toggle 关闭时走原始平面海面，零渲染变化；开启后选「模型精度」决定叠加的波组数量：\n" +
        //     "  Low = 4 组（远景/移动端）｜Medium = 8 组（默认中景）｜High = 16 组（特写/影视级）。\n" +
        //     "模型精度对 Mesh 同样有效 —— 海面平面建议按精度选用合适分段：\n" +
        //     "  Low ≈ 64×64 ｜ Medium ≈ 128×128 ｜ High ≈ 256×256。\n" +
        //     "  波涛振幅 ＝ 整体高低（米）\n" +
        //     "  波形大小 ＝ 整体缩放，直接缩波数 k：>1 让波形变小、变碎、变密；<1 让波形变大、变宽、变缓\n" +
        //     "  浪尖锐度 Q ＝ 横向位移系数（0 = 平缓余弦，0.35 = 经典 Gerstner，1 ≈ 破碎波）。\n" +
        //     "  浪尖浪花 ＝ 顶点解析 Jacobian 给出 mask 通道 → 采样 _CrestMap → 现有海面泡沫作扭曲源，\n" +
        //     "             浪尖位置随波形迁移（生命周期由「_CrestLife 控制的 τ 秒前 Gerstner 状态」与现在取 max 提供 →\n" +
        //     "             浪花延迟消散，模拟真实白色浪花，无 RenderTexture / 无 per-frame state）。",
        //     MessageType.Info);

        m_MaterialEditor.ShaderProperty(gerstnerEnabled,   "开启海面波涛");
        using (new EditorGUI.DisabledScope(gerstnerEnabled != null && gerstnerEnabled.floatValue < 0.5f))
        {
            m_MaterialEditor.ShaderProperty(gerstnerQuality,   "海面模型精度");
            m_MaterialEditor.ShaderProperty(gerstnerAmplitude,  L("波涛振幅",   "整体高低（0=无波, 1=默认, 2=极大）"));
            m_MaterialEditor.ShaderProperty(gerstnerWaveSize,   L("波形大小",   "整体缩放（0.25=大浪, 1=默认, 4=碎波）"));
            m_MaterialEditor.ShaderProperty(gerstnerSpeed,      L("波涛速度",   "时间倍率（0=静止, 1=实时, 5=5 倍速）"));
            m_MaterialEditor.ShaderProperty(gerstnerChoppy,     L("浪尖锐度",   "Gerstner 横移系数 Q（steepness）；0=平缓余弦, 0.35=默认, 1=碎浪可能自交"));

            // 浪尖浪花：mask 作通道 → 采样纹理 → 复用现有泡沫扭曲
            // 浪尖浪花纹理：单行显示贴图选择 + 下方展开 Tiling/Offset 控件
            //   Tiling/Offset 直接写 _CrestMap_ST，frag 里用 crestUV*ST.xy + ST.zw 采样
            m_MaterialEditor.TexturePropertySingleLine(
                new GUIContent("浪尖浪花纹理"), crestMap);
            EditorGUI.indentLevel++;
            m_MaterialEditor.TextureScaleOffsetProperty(crestMap);
            EditorGUI.indentLevel--;
            m_MaterialEditor.ShaderProperty(crestColor,            L("浪尖浪花颜色",   "RGBA 颜色；Alpha 通道 = 整体强度（0=关闭浪花）"));
            m_MaterialEditor.ShaderProperty(crestSharpness,        L("浪花锐度",       "0 = 广覆盖（弱锐度）；1 = 仅最高峰（强锐度）；合并了旧 sharpness+focus"));
            m_MaterialEditor.ShaderProperty(crestDetail,           L("浪花飞沫细节",   "0 = 粗大稀疏（块状）；1 = 细密飞沫；合并了旧 noiseScale+density"));
            m_MaterialEditor.ShaderProperty(crestLife,             L("浪花寿命",       "0 = 瞬时（无残留）；1 = 1.5s 强延迟；合并了旧 delay+decay"));
            m_MaterialEditor.ShaderProperty(crestDistortStrength,  L("泡沫扭曲浪花",   "0 = 不扭曲；1 = 与海面泡沫完全同向（推荐 0.3）"));
        }
    }

    private void DrawSpecular()
    {
        GUILayout.Label(HeaderStyle.Rich("5 ▌高光 (Specular)", HeaderStyle.Sparkle), EditorStyle.Get.BoldLabelRichStyle);

        // 旧版 shader 没声明高光属性（升级残留 / 第三方）→ 给清晰提示而不是抛异常
        if (specularColor == null || specularStrength == null || specularSmoothness == null)
        {
            EditorGUILayout.HelpBox(
                "当前 Shader 缺少 _SpecularColor / _SpecularStrength / _SpecularSmoothness 属性。\n请将材质切换到带高光支持的 Custom_Ocean.shader。",
                MessageType.Warning);
            return;
        }

        // 高光法线贴图：法线贴图解码为 TS→WS 法线参与 GGX
        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("高光法线贴图"), bumpMap);
        if (bumpMap.textureValue != null)
        {
            EditorGUI.indentLevel++;
            m_MaterialEditor.TextureScaleOffsetProperty(bumpMap);
            // MaterialEditor.RangeProperty 的两个重载 (prop, label) / (Rect, prop, label) 都只接 string
            // 想保留 tooltip 必须改走 ShaderProperty(prop, GUIContent) —— 对 Range-typed property 同样画 slider，行为完全一致
            m_MaterialEditor.ShaderProperty(bumpStrength, L("高光法线强度", "BumpMap 法线扰动 0=无 1=标准"));
            EditorGUI.indentLevel--;
        }

        m_MaterialEditor.ShaderProperty(specularColor,        L("高光颜色",       "RGBA 颜色，rgb = 高光色"));
        m_MaterialEditor.ShaderProperty(specularStrength,     L("高光强度",       "0 = 关闭高光（shader 整段短路）；1 = 标准；>1 = 加强"));
        m_MaterialEditor.ShaderProperty(specularSmoothness,   L("高光锐度",       "0 = 粗糙（roughness=1，高光散开）；1 = 光滑（roughness=0，高光集中尖锐）"));
        m_MaterialEditor.ShaderProperty(specularBumpFalloff,  L("高光法线聚拢度", "0=完全用基础法线（最聚拢，参考真实海面窄光带）；1=完全用 BumpMap 扰动法线（最分散）"));

        // 主光阴影相关参数：仅在 _ReceiveShadows 勾选时才显示
        //   关闭时连同说明 HelpBox 一起隐藏——避免出现"无意义参数/说明错位"
        if (receiveShadows != null && receiveShadows.floatValue > 0.5f)
        {
            // 主光阴影接收：影响所有色相（深度渐变/浪花/泡沫/浪尖）+ 高光
            EditorGUILayout.HelpBox(
                "主光阴影：场景中投射阴影的物体（船、桥、岸上建筑）会在水面产生阴影。\n" +
                "依赖：URP Renderer 启用 Main Light Shadows + Cascade，且场景中有投射阴影的光源。\n" +
                "_ShadowStrength = 0 时完全不接收；= 1 时与 shadowMap 采样一致。",
                MessageType.Info);
            if (shadowStrength != null)
                m_MaterialEditor.ShaderProperty(shadowStrength, L("阴影接收强度", "0=不接收主光阴影；1=完全接收（受 shadowMap 控制）"));
        }
    }

    /// 同步 Gerstner 相关 keyword 到 materials
    /// _GerstnerEnabled (0/1) → _GERSTNER_ON
    /// _GerstnerQuality (0/1/2) → _GerstnerQuality_Low/Medium/High
    /// 旧版 SyncShaderKeywords 字典已登记的 _UseBladeOverlay 等保留不动
    private void SyncGerstnerKeywords(UnityEngine.Object[] targets)
    {
        if (targets == null || targets.Length == 0) return;

        const string KW_GERSTNER_ON        = "_GERSTNER_ON";
        const string KW_QUALITY_LOW        = "_GerstnerQuality_Low";
        const string KW_QUALITY_MEDIUM     = "_GerstnerQuality_Medium";
        const string KW_QUALITY_HIGH       = "_GerstnerQuality_High";

        foreach (var obj in targets)
        {
            Material mat = obj as Material;
            if (mat == null || mat.shader == null) continue;
            if (!mat.HasProperty("_GerstnerEnabled")) continue;

            // 1) Toggle keyword
            bool on = mat.GetFloat("_GerstnerEnabled") > 0.5f;
            if (on) mat.EnableKeyword(KW_GERSTNER_ON);
            else    mat.DisableKeyword(KW_GERSTNER_ON);

            // 2) 三选一 keyword：disable 全部再 enable 选定
            mat.DisableKeyword(KW_QUALITY_LOW);
            mat.DisableKeyword(KW_QUALITY_MEDIUM);
            mat.DisableKeyword(KW_QUALITY_HIGH);
            if (on && mat.HasProperty("_GerstnerQuality"))
            {
                int q = Mathf.RoundToInt(mat.GetFloat("_GerstnerQuality"));
                switch (q)
                {
                    case 0:  mat.EnableKeyword(KW_QUALITY_LOW);    break;
                    case 2:  mat.EnableKeyword(KW_QUALITY_HIGH);   break;
                    default: mat.EnableKeyword(KW_QUALITY_MEDIUM); break;
                }
            }
        }
    }

    private void DrawSurfaceFoam()
    {
        GUILayout.Label(HeaderStyle.Rich("4 ▌海面泡沫 (Surface Foam)", HeaderStyle.Sparkle), EditorStyle.Get.BoldLabelRichStyle);

        m_MaterialEditor.TexturePropertySingleLine(new GUIContent("泡沫纹理 (R 通道 / Offset = 游走)"), foamDetailMap);
        if (foamDetailMap.textureValue != null)
        {
            DrawTextureTilingOffset("_FoamDetailMap", "Change Foam Texture Tiling/Offset");
        }

        m_MaterialEditor.ColorProperty(foamColor, "泡沫颜色");
        m_MaterialEditor.RangeProperty(foamDetailStrength, "泡沫网强度");
        m_MaterialEditor.RangeProperty(foamDetailCutOff, "泡沫网裁切 (越高网线越细)");
        m_MaterialEditor.RangeProperty(foamDistortStrength, "波浪扭曲泡沫 (用波浪贴图扭动泡沫)");

        // EditorGUILayout.HelpBox(
        //     "海面泡沫：单张纹理双层错位采样，覆盖全海面（不依赖岸边深度）。\n" +
        //     "1）泡沫网：两层取 1-|a-b| 得到交织成网的等值轮廓，裁切越高网线越细 —— 对应参考图的全海面泡沫网；\n" +
        //     "2）片状白沫：取纹理高亮区域，裁切成块白沫。\n" +
        //     "Offset 即游走参数（X/Y 为漂移方向与速度，受时间驱动），Tiling 控制泡沫疏密；\n" +
        //     "该纹理同时用于「深度渐变」分区的水色斑驳低频采样。\n" +
        //     "波浪扭曲：扭曲源是「水色斑驳」的大尺度层输出，取其在 U/V 两个方向的前向差分构成二维扭曲向量\n" +
        //     "（两轴等权、均值为 0），偏移两层泡沫采样坐标，使泡沫网随水团被撕扯扭动；\n" +
        //     "斑驳是低频大块噪声，扭曲柔和、方向随水团流动，泡沫网不会被条纹切成一条条；\n" +
        //     "扭曲随「斑驳速度」漂移，强度 0 时关闭。",
        //     MessageType.Info);
    }

    /// 绘制纹理的 Tiling / Offset 编辑（写回材质，支持 Undo）
    private void DrawTextureTilingOffset(string propertyName, string undoLabel)
    {
        Material material = m_MaterialEditor.target as Material;
        if (material == null) return;

        EditorGUI.indentLevel++;

        Vector2 tiling = material.GetTextureScale(propertyName);
        Vector2 offset = material.GetTextureOffset(propertyName);

        EditorGUI.BeginChangeCheck();
        tiling = EditorGUILayout.Vector2Field("Tiling", tiling);
        offset = EditorGUILayout.Vector2Field("Offset", offset);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(material, undoLabel);
            material.SetTextureScale(propertyName, tiling);
            material.SetTextureOffset(propertyName, offset);
            EditorUtility.SetDirty(material);
        }

        EditorGUI.indentLevel--;
    }

    private void DrawRenderSettings()
    {
        GUILayout.Label(HeaderStyle.Rich("5 ▌渲染设置 (Render Settings)", HeaderStyle.Render), EditorStyle.Get.BoldLabelRichStyle);

        m_MaterialEditor.ShaderProperty(cullMode, "剔除模式");

        m_MaterialEditor.RenderQueueField();
        m_MaterialEditor.EnableInstancingField();
        m_MaterialEditor.DoubleSidedGIField();

        // EditorGUILayout.HelpBox(
        //     "雾：本 Shader 的主 Pass 已接入 URP 雾（multi_compile_fog + MixFog），\n" +
        //     "雾的颜色/模式/起止距离在「Universal Renderer Data → Lighting → Fog」中设置，此面板无独立开关。\n" +
        //     "关闭 Fog 时自动使用无雾变体，混合时只影响 RGB，alpha 与泡沫透明度不受雾影响。",
        //     MessageType.Info);
    }

    /// 获取材质参数存档路径（Library中，用户存读档）
    private string GetPresetPath(string presetName)
    {
        Material material = m_MaterialEditor.target as Material;
        if (material == null || material.shader == null) return null;

        string shaderName = material.shader.name.Replace("/", "_");
        string folderPath = "Library/VicTools/Ocean/" + shaderName;

        if (!System.IO.Directory.Exists(folderPath))
        {
            System.IO.Directory.CreateDirectory(folderPath);
        }

        return folderPath + "/" + presetName + ".json";
    }

    /// 存档材质参数（含纹理引用）
    private void SaveMaterialParameters()
    {
        Material material = m_MaterialEditor.target as Material;
        if (material == null || material.shader == null) return;

        string shaderName = material.shader.name.Replace("/", "_");
        string defaultPath = "Library/VicTools/Ocean/" + shaderName;

        // 确保目录存在
        if (!System.IO.Directory.Exists(defaultPath))
        {
            System.IO.Directory.CreateDirectory(defaultPath);
        }

        // 弹出输入框让用户输入存档名称
        string presetName = EditorUtility.SaveFilePanel(
            "保存海洋材质参数存档",
            defaultPath,
            "OceanPreset",
            "json"
        );

        if (string.IsNullOrEmpty(presetName)) return;

        // 提取文件名（不含扩展名）
        string fileName = System.IO.Path.GetFileNameWithoutExtension(presetName);

        SaveMaterialParametersToFile(fileName);
    }

    /// 保存材质参数到指定文件
    private void SaveMaterialParametersToFile(string presetName)
    {
        Material material = m_MaterialEditor.target as Material;
        if (material == null || material.shader == null) return;

        // 从 MaterialProperty 列表构建类型对照表，修正 ShaderUtil 对 Vector 类型的误判
        var propTypeMap = new System.Collections.Generic.Dictionary<string, MaterialProperty.PropType>();
        if (m_Properties != null)
        {
            foreach (var mp in m_Properties)
            {
                if (mp != null)
                    propTypeMap[mp.name] = mp.type;
            }
        }

        Shader shader = material.shader;
        int propertyCount = ShaderUtil.GetPropertyCount(shader);

        // 手动构建JSON
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("{");
        bool first = true;

        for (int i = 0; i < propertyCount; i++)
        {
            string propertyName = ShaderUtil.GetPropertyName(shader, i);
            ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.GetPropertyType(shader, i);

            if (!material.HasProperty(propertyName)) continue;

            // 优先用 MaterialProperty.type 修正类型（某些 Unity 版本 Vector → Float 误判）
            MaterialProperty.PropType mpropType;
            if (propTypeMap.TryGetValue(propertyName, out mpropType)
                && mpropType == MaterialProperty.PropType.Vector)
            {
                propertyType = ShaderUtil.ShaderPropertyType.Vector;
            }

            if (!first) sb.AppendLine(",");
            first = false;

            sb.Append("  \"" + propertyName + "\": ");

            switch (propertyType)
            {
                case ShaderUtil.ShaderPropertyType.Color:
                    Color color = material.GetColor(propertyName);
                    sb.Append($"[{color.r}, {color.g}, {color.b}, {color.a}]");
                    break;

                case ShaderUtil.ShaderPropertyType.Vector:
                    Vector4 vector = material.GetVector(propertyName);
                    sb.Append($"[{vector.x}, {vector.y}, {vector.z}, {vector.w}]");
                    break;

                case ShaderUtil.ShaderPropertyType.Float:
                case ShaderUtil.ShaderPropertyType.Range:
                    sb.Append(material.GetFloat(propertyName).ToString());
                    break;

                case ShaderUtil.ShaderPropertyType.TexEnv:
                    Texture tex = material.GetTexture(propertyName);
                    string texPath = "";
                    if (tex != null)
                    {
                        texPath = AssetDatabase.GetAssetPath(tex);
                    }
                    Vector2 tiling = material.GetTextureScale(propertyName);
                    Vector2 offset = material.GetTextureOffset(propertyName);
                    sb.Append("{");
                    sb.Append($"\"path\": \"{EscapeJsonString(texPath)}\", ");
                    sb.Append($"\"tiling\": [{tiling.x}, {tiling.y}], ");
                    sb.Append($"\"offset\": [{offset.x}, {offset.y}]");
                    sb.Append("}");
                    break;
            }
        }

        sb.AppendLine();
        sb.AppendLine("}");

        // 保存到文件
        string path = GetPresetPath(presetName);
        System.IO.File.WriteAllText(path, sb.ToString());

        Debug.Log($"海洋材质参数已保存到: {path}（含纹理引用）");
    }

    /// JSON字符串转义（处理路径中的反斜杠）
    private static string EscapeJsonString(string str)
    {
        if (string.IsNullOrEmpty(str)) return "";
        return str.Replace("\\", "/");
    }

    /// 从文件加载材质参数
    private void LoadMaterialParametersFromFile(string filePath, bool isReset = false)
    {
        Material material = m_MaterialEditor.target as Material;
        if (material == null || material.shader == null) return;

        if (!System.IO.File.Exists(filePath))
        {
            Debug.LogWarning($"存档文件不存在: {filePath}");
            return;
        }

        // 读取JSON
        string json = System.IO.File.ReadAllText(filePath);

        // 检测存档中是否包含纹理数据
        bool hasTextureData = json.Contains("\"path\":");
        bool loadTextures = false;

        if (hasTextureData && !isReset)
        {
            loadTextures = EditorUtility.DisplayDialog("读取纹理",
                "存档中包含纹理贴图引用，是否同时读取纹理？\n\n选择「是」将还原纹理贴图及Tiling/Offset\n选择「否」仅读取数值参数",
                "是，读取纹理", "否，仅参数");
        }

        // 记录撤销操作
        Undo.RecordObject(material, "Load Ocean Material Parameters");

        // 构建 MaterialProperty 类型对照表，用于区分配 Color/Vector
        var propTypeMap = new System.Collections.Generic.Dictionary<string, MaterialProperty.PropType>();
        if (m_Properties != null)
        {
            foreach (var mp in m_Properties)
            {
                if (mp != null)
                    propTypeMap[mp.name] = mp.type;
            }
        }

        // 手动解析JSON
        var lines = json.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);

        // 收集缺失纹理的警告信息
        System.Collections.Generic.List<string> missingTextures = new System.Collections.Generic.List<string>();

        for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
        {
            string line = lines[lineIdx];
            if (!line.Contains(":")) continue;

            string trimmed = line.Trim().TrimEnd(',');
            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex < 0) continue;

            string propertyName = trimmed.Substring(0, colonIndex).Trim().Trim('"');
            string valueStr = trimmed.Substring(colonIndex + 1).Trim();

            if (!material.HasProperty(propertyName)) continue;

            // 纹理类型：值以 { 开头
            if (valueStr.StartsWith("{"))
            {
                // 拼接多行直到找到 }
                string texJson = valueStr;
                while (!texJson.Contains("}") && lineIdx + 1 < lines.Length)
                {
                    lineIdx++;
                    texJson += lines[lineIdx];
                }

                // 纹理贴图本身：仅在用户选择加载纹理时还原
                if (loadTextures)
                {
                    // 主贴图已有时不替换
                    bool isMainTex = (propertyName == "_BumpMap" || propertyName == "_BaseMap" || propertyName == "_MainTex");
                    if (isMainTex && material.GetTexture(propertyName) != null)
                    {
                        // 跳过主贴图替换，但仍读取 Tiling/Offset
                    }
                    else
                    {
                        // 解析纹理路径
                        string texPath = ExtractJsonStringValue(texJson, "path");

                        if (!string.IsNullOrEmpty(texPath))
                        {
                            Texture tex = AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                            if (tex != null)
                            {
                                material.SetTexture(propertyName, tex);
                            }
                            else
                            {
                                missingTextures.Add($"  {propertyName}: {texPath}");
                            }
                        }
                        else
                        {
                            // 路径为空，清除纹理
                            material.SetTexture(propertyName, null);
                        }
                    }
                }

                // Tiling/Offset 属于数值参数，无论是否加载纹理都应还原
                float[] tilingValues = ExtractJsonFloatArray(texJson, "tiling");
                if (tilingValues != null && tilingValues.Length == 2)
                {
                    material.SetTextureScale(propertyName, new Vector2(tilingValues[0], tilingValues[1]));
                }

                float[] offsetValues = ExtractJsonFloatArray(texJson, "offset");
                if (offsetValues != null && offsetValues.Length == 2)
                {
                    material.SetTextureOffset(propertyName, new Vector2(offsetValues[0], offsetValues[1]));
                }

                continue;
            }

            // 数组类型（Color或Vector）
            if (valueStr.StartsWith("["))
            {
                valueStr = valueStr.Trim('[', ']');
                string[] parts = valueStr.Split(',');
                if (parts.Length == 4)
                {
                    float[] values = new float[4];
                    for (int i = 0; i < 4; i++)
                    {
                        float.TryParse(parts[i].Trim(), out values[i]);
                    }

                    // 根据 MaterialProperty.type 区分 Vector 和 Color，避免误用 SetColor
                    MaterialProperty.PropType loadPropType;
                    bool isVector = propTypeMap.TryGetValue(propertyName, out loadPropType)
                                 && loadPropType == MaterialProperty.PropType.Vector;

                    if (isVector)
                    {
                        material.SetVector(propertyName, new Vector4(values[0], values[1], values[2], values[3]));
                    }
                    else
                    {
                        try
                        {
                            material.SetColor(propertyName, new Color(values[0], values[1], values[2], values[3]));
                        }
                        catch
                        {
                            material.SetVector(propertyName, new Vector4(values[0], values[1], values[2], values[3]));
                        }
                    }
                }
            }
            else
            {
                // Float类型
                if (float.TryParse(valueStr, out float floatValue))
                {
                    material.SetFloat(propertyName, floatValue);
                }
            }
        }

        // 显示缺失纹理警告
        if (missingTextures.Count > 0)
        {
            string msg = "以下纹理资源不存在，已跳过：\n\n" + string.Join("\n", missingTextures);
            EditorUtility.DisplayDialog("纹理缺失警告", msg, "确定");
            Debug.LogWarning("[CustomOceanGUI] 读档时部分纹理资源不存在:\n" + string.Join("\n", missingTextures));
        }

        // 同步 shader_feature toggle 对应的 keyword
        //   路径：Material.SetFloat 不会触发 keyword 同步，必须显式 Enable/Disable
        //   当前 Custom_Ocean toggle：_ReceiveShadows ↔ _RECEIVE_SHADOWS
        SyncShaderKeywords(material);

        EditorUtility.SetDirty(material);
        if (m_MaterialEditor != null) m_MaterialEditor.Repaint();
        SceneView.RepaintAll();

        string texInfo = loadTextures ? "（含纹理）" : "（仅参数）";
        Debug.Log($"海洋材质参数已从存档加载{texInfo}: {filePath}");
    }

    /// 从简易JSON中提取字符串值
    private static string ExtractJsonStringValue(string json, string key)
    {
        string search = "\"" + key + "\":";
        int idx = json.IndexOf(search);
        if (idx < 0) return null;

        int start = json.IndexOf('"', idx + search.Length);
        if (start < 0) return null;
        int end = json.IndexOf('"', start + 1);
        if (end < 0) return null;

        return json.Substring(start + 1, end - start - 1);
    }

    /// 从简易JSON中提取浮点数组
    private static float[] ExtractJsonFloatArray(string json, string key)
    {
        string search = "\"" + key + "\":";
        int idx = json.IndexOf(search);
        if (idx < 0) return null;

        int start = json.IndexOf('[', idx);
        if (start < 0) return null;
        int end = json.IndexOf(']', start);
        if (end < 0) return null;

        string arrayStr = json.Substring(start + 1, end - start - 1);
        string[] parts = arrayStr.Split(',');
        float[] result = new float[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            float.TryParse(parts[i].Trim(), out result[i]);
        }
        return result;
    }

    /// 同步所有 shader_feature toggle 对应的 keyword
    /// 读档 / 重置 / 外部 SetFloat 不会触发 keyword 同步，必须在此手动 Enable/Disable
    /// 登记规则：Key = toggle 浮点字段名（与 [Toggle(KEYWORD)] 一致），Value = keyword 字符串
    ///   Unity 的 MaterialEditor.ShaderProperty 写入时 Unity 会自动处理；
    ///   但 Material.SetFloat 路径完全不会触发 keyword 切换，依赖本表兜底
    private void SyncShaderKeywords(Material material)
    {
        var toggleKeywords = new System.Collections.Generic.Dictionary<string, string>
        {
            // 接收阴影（全局开关）：float = 0/1 ↔ keyword _RECEIVE_SHADOWS
            { "_ReceiveShadows", "_RECEIVE_SHADOWS" },
            // 后续如新增 toggle 字段（如 _UseXxx），按 { "_UseXxx", "_USEXXX" } 格式登记
        };

        foreach (var pair in toggleKeywords)
        {
            if (!material.HasProperty(pair.Key)) continue;

            bool enabled = material.GetFloat(pair.Key) > 0.5f;
            if (enabled)
                material.EnableKeyword(pair.Value);
            else
                material.DisableKeyword(pair.Value);
        }
    }

    /// 重置材质参数为默认值（使用Default存档或shader默认值）
    private void ResetMaterialParameters()
    {
        Material material = m_MaterialEditor.target as Material;
        if (material == null || material.shader == null) return;

        string defaultPresetPath = GetPresetPath("Default");

        // 检查Default存档是否存在
        if (System.IO.File.Exists(defaultPresetPath))
        {
            // 使用Default存档
            if (EditorUtility.DisplayDialog("重置参数",
                "将使用Default存档重置参数。\n\n注意：纹理不会被重置。",
                "确定", "取消"))
            {
                LoadMaterialParametersFromFile(defaultPresetPath, true);
            }
        }
        else
        {
            // Default存档不存在，创建它
            if (EditorUtility.DisplayDialog("创建Default存档",
                "Default存档不存在，将使用Shader默认值创建Default存档。",
                "确定", "取消"))
            {
                // 创建临时材质以获取shader默认值
                Material tempMaterial = new Material(material.shader);

                Shader shader = material.shader;
                int propertyCount = ShaderUtil.GetPropertyCount(shader);

                // 手动构建JSON（与SaveMaterialParametersToFile一致的格式）
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("{");
                bool first = true;

                for (int i = 0; i < propertyCount; i++)
                {
                    string propertyName = ShaderUtil.GetPropertyName(shader, i);
                    ShaderUtil.ShaderPropertyType propertyType = ShaderUtil.GetPropertyType(shader, i);

                    if (!tempMaterial.HasProperty(propertyName)) continue;

                    if (!first) sb.AppendLine(",");
                    first = false;

                    sb.Append("  \"" + propertyName + "\": ");

                    switch (propertyType)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            Color c = tempMaterial.GetColor(propertyName);
                            sb.Append($"[{c.r}, {c.g}, {c.b}, {c.a}]");
                            break;
                        case ShaderUtil.ShaderPropertyType.Vector:
                            Vector4 v = tempMaterial.GetVector(propertyName);
                            sb.Append($"[{v.x}, {v.y}, {v.z}, {v.w}]");
                            break;
                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            sb.Append(tempMaterial.GetFloat(propertyName).ToString());
                            break;
                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            sb.Append("{\"path\": \"\", \"tiling\": [1, 1], \"offset\": [0, 0]}");
                            break;
                    }
                }

                sb.AppendLine();
                sb.AppendLine("}");

                System.IO.File.WriteAllText(defaultPresetPath, sb.ToString());

                Object.DestroyImmediate(tempMaterial);

                Debug.Log($"海洋材质Default存档已创建: {defaultPresetPath}");

                // 加载Default存档（重置模式，不弹纹理提示）
                LoadMaterialParametersFromFile(defaultPresetPath, true);
            }
        }
    }
}
