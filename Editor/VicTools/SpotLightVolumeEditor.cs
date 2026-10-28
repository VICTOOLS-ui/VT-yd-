/// SpotLightVolume v5.0 自定义Inspector面板
/// 存档/读档/预设按钮 + 参数面板 + 射线遮挡参数
/// SpotLightVolume v6.0 重构代码，改进重复的GetComponent调用，消除 UpdateGeometry 和 UpdateMaterial 中的重复计算
/// SpotLightVolume v6.4 增加"后处理优化"分组：曝光系数 + 软饱和开关；纳入存档系统
/// SpotLightVolume v7.4 切换为 VolumeOcclusionMode 枚举 + 自适应显隐（遮挡细分三档：Disabled/ZTestOnly/ZTestAndFade），
///   旧版 enableOcclusion 通过 SpotLightVolumeData 的 EnsureBackwardCompat 自动迁移到 ZTestAndFade。

using UnityEngine;
using UnityEditor;
using Vic.Runtime;

namespace VicTools
{
    [CustomEditor(typeof(SpotLightVolume))]
    [CanEditMultipleObjects]
    public class SpotLightVolumeEditor : Editor
    {
        // 距离
        private SerializedProperty lightSourceRadius;
        private SerializedProperty fallOffStart;
        private SerializedProperty maxDistance;
        // 羽化
        private SerializedProperty edgeFade;
        private SerializedProperty endFade;
        private SerializedProperty glareFrontal;
        private SerializedProperty glareBehind;
        // 外观
        private SerializedProperty intensity;
        private SerializedProperty startBoostIntensity;
        private SerializedProperty startBoostRange;
        private SerializedProperty centerFade;
        private SerializedProperty colorFromLight;
        private SerializedProperty volumeColor;
        private SerializedProperty blendMode;
        private SerializedProperty zTestMode;
        // Mesh
        private SerializedProperty coneSides;
        private SerializedProperty coneSegments;
        // 遮挡模式(v7.4:旧 enableOcclusion 已迁移为 _legacyEnableOcclusion,实际生效用 occlusionMode)
        private SerializedProperty occlusionMode;
        private SerializedProperty occlusionLayerMask;
        private SerializedProperty occlusionUpdateInterval;
        private SerializedProperty occlusionDetectTriggers;
        // 蒙版投影
        private SerializedProperty enableMask;
        private SerializedProperty maskTexture;
        private SerializedProperty maskIntensity;
        // 后处理优化
        private SerializedProperty volumeExposure;

        private const string SaveFolderBase = "Library/VicTools/SpotLightVolume";
        private const string PresetFolder = "Packages/com.youdoo.victools/Runtime/Presets/SpotLightVolume";

        void OnEnable()
        {
            lightSourceRadius = serializedObject.FindProperty("lightSourceRadius");
            fallOffStart = serializedObject.FindProperty("fallOffStart");
            maxDistance = serializedObject.FindProperty("maxDistance");
            edgeFade = serializedObject.FindProperty("edgeFade");
            endFade = serializedObject.FindProperty("endFade");
            glareFrontal = serializedObject.FindProperty("glareFrontal");
            glareBehind = serializedObject.FindProperty("glareBehind");
            intensity = serializedObject.FindProperty("intensity");
            startBoostIntensity = serializedObject.FindProperty("startBoostIntensity");
            startBoostRange = serializedObject.FindProperty("startBoostRange");
            centerFade = serializedObject.FindProperty("centerFade");
            colorFromLight = serializedObject.FindProperty("colorFromLight");
            volumeColor = serializedObject.FindProperty("volumeColor");
            blendMode = serializedObject.FindProperty("blendMode");
            zTestMode = serializedObject.FindProperty("zTestMode");
            coneSides = serializedObject.FindProperty("coneSides");
            coneSegments = serializedObject.FindProperty("coneSegments");
            occlusionMode = serializedObject.FindProperty("occlusionMode");
            occlusionLayerMask = serializedObject.FindProperty("occlusionLayerMask");
            occlusionUpdateInterval = serializedObject.FindProperty("occlusionUpdateInterval");
            occlusionDetectTriggers = serializedObject.FindProperty("occlusionDetectTriggers");
            maskTexture = serializedObject.FindProperty("maskTexture");
            maskIntensity = serializedObject.FindProperty("maskIntensity");
            enableMask = serializedObject.FindProperty("enableMask");
            volumeExposure = serializedObject.FindProperty("volumeExposure");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            DrawHeader();
            EditorGUILayout.Space(4);

            DrawSection("距离控制", () =>
            {
                EditorGUILayout.PropertyField(lightSourceRadius, new GUIContent("光源半径(始端宽度)"));
                EditorGUILayout.PropertyField(fallOffStart, new GUIContent("衰减起始"));
                EditorGUILayout.PropertyField(maxDistance, new GUIContent("最远距离"));
            });

            DrawSection("羽化控制", () =>
            {
                EditorGUILayout.PropertyField(edgeFade, new GUIContent("边缘羽化"));
                EditorGUILayout.PropertyField(endFade, new GUIContent("末端羽化"));
                EditorGUILayout.PropertyField(glareFrontal, new GUIContent("正面眩光"));
                EditorGUILayout.PropertyField(glareBehind, new GUIContent("背面眩光"));
            });

            DrawSection("外观", () =>
            {
                EditorGUILayout.PropertyField(intensity, new GUIContent("强度"));
                EditorGUILayout.PropertyField(startBoostIntensity, new GUIContent("起始亮度"));
                EditorGUILayout.PropertyField(startBoostRange, new GUIContent("起始亮度范围"));
                EditorGUILayout.PropertyField(centerFade, new GUIContent("中心渐变距离"));
                EditorGUILayout.PropertyField(blendMode, new GUIContent("混合方式"));
                EditorGUILayout.PropertyField(zTestMode, new GUIContent("深度测试(ZTest)", "LessEqual=被遮挡部分剔除(推荐,性能优); Disabled=不做深度测试(旧行为,被遮挡也绘制,overdraw 高)"));
                EditorGUILayout.PropertyField(colorFromLight, new GUIContent("跟随灯光颜色"));
                if (!colorFromLight.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(volumeColor, new GUIContent("自定义颜色"));
                    EditorGUI.indentLevel--;
                }
            });

            DrawSection("Mesh 质量", () =>
            {
                EditorGUILayout.PropertyField(coneSides, new GUIContent("圆锥面数"));
                EditorGUILayout.PropertyField(coneSegments, new GUIContent("圆锥分段"));
            });

            DrawSection("遮挡", () =>
            {
                EditorGUILayout.PropertyField(occlusionMode, new GUIContent("遮挡模式",
                    "Disabled=完全关闭(overdraw 最高,仅调试)\nZTestOnly=仅 URP 深度硬件遮挡(零 CPU 开销,推荐默认)\nZTestAndFade=ZTest + CPU Raycast 软淡出(原 v7.3 行为,需 Collider)"));
                // 仅 ZTestAndFade 模式才需要显式配置 Raycast 参数,其他模式隐藏
                if (occlusionMode.intValue == (int)VolumeOcclusionMode.ZTestAndFade)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.PropertyField(occlusionLayerMask, new GUIContent("检测层"));
                    EditorGUILayout.PropertyField(occlusionUpdateInterval, new GUIContent("检测间隔(秒)"));
                    EditorGUILayout.PropertyField(occlusionDetectTriggers, new GUIContent("检测Trigger"));
                    EditorGUI.indentLevel--;
                }
            });

            DrawSection("蒙版投影", () =>
            {
                EditorGUILayout.PropertyField(enableMask, new GUIContent("启用蒙版"));
                if (enableMask.boolValue)
                {
                    EditorGUILayout.PropertyField(maskTexture, new GUIContent("蒙版纹理", "黑白纹理模拟窗格投影，白色透光黑色遮光"));
                    EditorGUILayout.PropertyField(maskIntensity, new GUIContent("蒙版强度"));
                }
            });

            DrawSection("后处理优化(抗过曝)", () =>
            {
                EditorGUILayout.PropertyField(volumeExposure, new GUIContent("曝光系数", "类似摄影曝光补偿。多灯+Bloom 过曝时建议 0.5~0.7，单灯或无 Bloom 时保持 1"));
            });

            serializedObject.ApplyModifiedProperties();
        }

        #region UI辅助

        private static void DrawSection(string label, System.Action drawContent)
        {
            drawContent();
            EditorGUILayout.Space(4);
        }

        private new void DrawHeader()
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("探照灯体积雾", EditorStyles.boldLabel);

            DrawHeaderButton("存档", new Color(0.3f, 0.8f, 1.0f), "保存当前体积光参数和灯光角度/颜色到本地JSON文件", 50,
                () => EditorApplication.delayCall += SaveParameters);

            DrawHeaderButton("读档 ▾", new Color(0.5f, 1.0f, 0.5f), "从已保存的本地存档中加载参数", 55,
                () => ShowFileDropdown(SaveFolderBase));

            DrawHeaderButton("预设 ▾", new Color(0.9f, 0.7f, 1.0f), "加载内置预设参数（包内只读）", 55,
                () => ShowFileDropdown(PresetFolder));

            DrawHeaderButton("禁用光照", new Color(1.0f, 0.6f, 0.4f), "将SpotLight设为不产生实时照明", 60,
                DisableLightIllumination);

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawHeaderButton(string text, Color bgColor, string tooltip, float width, System.Action onClick)
        {
            GUI.backgroundColor = bgColor;
            if (GUILayout.Button(new GUIContent(text, tooltip), GUILayout.Width(width)))
                onClick();
        }

        #endregion

        #region 功能操作

        private void DisableLightIllumination()
        {
            foreach (var t in targets)
            {
                var vol = t as SpotLightVolume;
                if (vol == null) continue;

                var light = vol.GetComponent<Light>();
                if (light == null) continue;

                Undo.RecordObject(light, "Disable Light Illumination");
                light.renderMode = LightRenderMode.ForceVertex;
                light.intensity = 0f;
                light.bounceIntensity = 0f;
                light.shadows = LightShadows.None;
                EditorUtility.SetDirty(light);
            }
            Debug.Log("[SpotLightVolume] 已禁用灯光实时照明");
        }

        #endregion

        #region 存档/读档

        private void SaveParameters()
        {
            EnsureDirectory(SaveFolderBase);

            string path = EditorUtility.SaveFilePanel("保存体积光参数", SaveFolderBase, "VolumePreset", "json");
            if (string.IsNullOrEmpty(path)) return;

            SaveToFile(path);
            Debug.Log("[SpotLightVolume] 参数已保存: " + path);
        }

        /// 通用文件下拉菜单（存档和预设共用）
        private void ShowFileDropdown(string folder)
        {
            EnsureDirectory(folder);

            string[] files = System.IO.Directory.GetFiles(folder, "*.json");
            var menu = new GenericMenu();

            if (files.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("（无文件）"));
            }
            else
            {
                foreach (string file in files)
                {
                    string fileName = System.IO.Path.GetFileNameWithoutExtension(file);
                    string filePath = file;
                    menu.AddItem(new GUIContent(fileName), false, () =>
                    {
                        EditorApplication.delayCall += () => LoadFromFile(filePath);
                    });
                }
            }
            menu.ShowAsContext();
        }

        private void SaveToFile(string path)
        {
            var vol = target as SpotLightVolume;
            if (vol == null) return;

            var light = vol.GetComponent<Light>();
            var data = new SpotLightVolumeData
            {
                lightSourceRadius = vol.lightSourceRadius,
                fallOffStart = vol.fallOffStart,
                maxDistance = vol.maxDistance,
                edgeFade = vol.edgeFade,
                endFade = vol.endFade,
                glareFrontal = vol.glareFrontal,
                glareBehind = vol.glareBehind,
                intensity = vol.intensity,
                startBoostIntensity = vol.startBoostIntensity,
                startBoostRange = vol.startBoostRange,
                centerFade = vol.centerFade,
                colorFromLight = vol.colorFromLight,
                volumeColor = ColorToArray(vol.volumeColor),
                blendMode = (int)vol.blendMode,
                zTestMode = (int)vol.zTestMode,
                coneSides = vol.coneSides,
                coneSegments = vol.coneSegments,
                enableOcclusion = vol.occlusionMode != VolumeOcclusionMode.Disabled, // 仅供读旧档时参考,新档优先看 occlusionMode
                occlusionMode = (int)vol.occlusionMode,
                occlusionLayerMask = (int)vol.occlusionLayerMask,
                occlusionUpdateInterval = vol.occlusionUpdateInterval,
                occlusionDetectTriggers = vol.occlusionDetectTriggers,
                spotAngle = light != null ? light.spotAngle : 30f,
                innerSpotAngle = light != null ? light.innerSpotAngle : 0f,
                lightColor = light != null ? ColorToArray(light.color) : new float[] { 1, 1, 1, 1 },
                enableMask = vol.enableMask,
                maskIntensity = vol.maskIntensity,
                maskTexturePath = vol.maskTexture != null ? AssetDatabase.GetAssetPath(vol.maskTexture) : "",
                volumeExposure = vol.volumeExposure
            };

            System.IO.File.WriteAllText(path, JsonUtility.ToJson(data, true));
        }

        private void LoadFromFile(string path)
        {
            if (!System.IO.File.Exists(path)) return;

            string rawJson = System.IO.File.ReadAllText(path);
            var data = JsonUtility.FromJson<SpotLightVolumeData>(rawJson);
            if (data == null) return;

            // === 向后兼容：旧版存档（v6.4 之前）的 JSON 不含后续新增字段，
            // JsonUtility.FromJson 对缺失字段保留类型默认值（float=0, bool=false），
            // 这会导致 volumeExposure=0 → 灯光完全不亮。
            // 用 rawJson 字段存在性检测 + 兜底默认值修复：
            EnsureBackwardCompatibility(rawJson, data);

            foreach (var t in targets)
            {
                var vol = t as SpotLightVolume;
                if (vol == null) continue;

                Undo.RecordObject(vol, "Load SpotLightVolume Preset");
                data.ApplyTo(vol);

                var light = vol.GetComponent<Light>();
                if (light != null)
                {
                    Undo.RecordObject(light, "Load SpotLightVolume Light Preset");
                    data.ApplyLightTo(light);
                    EditorUtility.SetDirty(light);
                }
                EditorUtility.SetDirty(vol);

                // 存档/读档直接赋值字段会绕过 OnValidate，导致 _materialParamsDirty 不会被置位、
                // dirty 缓存（_cachedBlendMode / _lastAppliedLightColor）不会失效，
                // 结果新参数（如 volumeExposure / blendMode）调节后不会立刻推到 GPU。
                // 强制立即刷新材质：失效所有缓存并立刻完整推送一次，让新参数立即生效。
                vol.RefreshMaterial();
            }

            Debug.Log("[SpotLightVolume] 参数已加载: " + System.IO.Path.GetFileNameWithoutExtension(path));
        }

        /// 对缺失字段做兜底默认值（仅修复 float=0 这种破坏性默认）。
        /// 新增字段时在此追加：if (!rawJson.Contains("\"xxx\"")) data.xxx = 兜底值;
        /// bool 字段默认 false 通常无害，无需处理。
        private static void EnsureBackwardCompatibility(string rawJson, SpotLightVolumeData data)
        {
            // v6.4 新增字段：
            if (!rawJson.Contains("\"volumeExposure\"")) data.volumeExposure = 1f;
            // v7.3 新增字段：默认 LessEqual(4)，旧档缺失时若保留 int 默认 0=Disabled 会破坏效果
            if (!rawJson.Contains("\"zTestMode\"")) data.zTestMode = (int)UnityEngine.Rendering.CompareFunction.LessEqual;
            // v7.4 迁移：旧档没有 occlusionMode 字段时(JsonUtility 默认值 0=Disabled 与原行为冲突),
            //   根据旧版 enableOcclusion bool 决定：true → ZTestAndFade(2),false → ZTestOnly(1)(新默认值,与 GPU 遮挡兼容)
            if (!rawJson.Contains("\"occlusionMode\""))
            {
                data.occlusionMode = data.enableOcclusion
                    ? (int)VolumeOcclusionMode.ZTestAndFade
                    : (int)VolumeOcclusionMode.ZTestOnly;
            }
        }

        #endregion

        #region 工具方法

        private static void EnsureDirectory(string path)
        {
            if (!System.IO.Directory.Exists(path))
                System.IO.Directory.CreateDirectory(path);
        }

        private static float[] ColorToArray(Color c) => new[] { c.r, c.g, c.b, c.a };

        private static Color ArrayToColor(float[] arr)
        {
            return arr != null && arr.Length == 4 ? new Color(arr[0], arr[1], arr[2], arr[3]) : Color.white;
        }

        #endregion

        #region 序列化数据

        [System.Serializable]
        private class SpotLightVolumeData
        {
            public float lightSourceRadius;
            public float fallOffStart;
            public float maxDistance;
            public float edgeFade;
            public float endFade;
            public float glareFrontal;
            public float glareBehind;
            public float intensity;
            public float startBoostIntensity;
            public float startBoostRange;
            public float centerFade;
            public bool colorFromLight;
            public float[] volumeColor;
            public int blendMode;
            public int zTestMode;
            public int coneSides;
            public int coneSegments;
            // [v7.4] 遮挡模式：枚举值(0=Disabled, 1=ZTestOnly, 2=ZTestAndFade)。
            // 旧版 enableOcclusion bool 已废弃,新存档只写 occlusionMode。EnsureBackwardCompat
            // 会把缺失 occlusionMode 但 enableOcclusion=true 的旧档升级为 ZTestAndFade(2)。
            public int occlusionMode;
            // 旧版字段保留仅用于解析旧 JSON(v7.4 之前)，写新存档时不再输出(JsonUtility 自动忽略零值 bool)。
            public bool enableOcclusion;
            public int occlusionLayerMask;
            public float occlusionUpdateInterval;
            public bool occlusionDetectTriggers;
            public float spotAngle;
            public float innerSpotAngle;
            public float[] lightColor;
            public bool enableMask;
            public float maskIntensity;
            public string maskTexturePath; // AssetDatabase 路径
            // 后处理优化
            public float volumeExposure;

            public void ApplyTo(SpotLightVolume vol)
            {
                vol.lightSourceRadius = lightSourceRadius;
                vol.fallOffStart = fallOffStart;
                vol.maxDistance = maxDistance;
                vol.edgeFade = edgeFade;
                vol.endFade = endFade;
                vol.glareFrontal = glareFrontal;
                vol.glareBehind = glareBehind;
                vol.intensity = intensity;
                vol.startBoostIntensity = startBoostIntensity;
                vol.startBoostRange = startBoostRange;
                vol.centerFade = centerFade;
                vol.colorFromLight = colorFromLight;
                if (volumeColor != null && volumeColor.Length == 4)
                    vol.volumeColor = new Color(volumeColor[0], volumeColor[1], volumeColor[2], volumeColor[3]);
                vol.blendMode = (VolumeBlendMode)blendMode;
                vol.zTestMode = (UnityEngine.Rendering.CompareFunction)zTestMode;
                vol.coneSides = coneSides;
                vol.coneSegments = coneSegments;
                vol.occlusionMode = (VolumeOcclusionMode)occlusionMode;
                vol.occlusionLayerMask = occlusionLayerMask;
                vol.occlusionUpdateInterval = occlusionUpdateInterval;
                vol.occlusionDetectTriggers = occlusionDetectTriggers;
                vol.enableMask = enableMask;
                vol.maskIntensity = maskIntensity;
                if (!string.IsNullOrEmpty(maskTexturePath))
                    vol.maskTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(maskTexturePath);
                else
                    vol.maskTexture = null;
                vol.volumeExposure = volumeExposure;
            }

            public void ApplyLightTo(Light light)
            {
                light.spotAngle = spotAngle;
                light.innerSpotAngle = innerSpotAngle;
                if (lightColor != null && lightColor.Length == 4)
                    light.color = new Color(lightColor[0], lightColor[1], lightColor[2], lightColor[3]);
            }
        }

        #endregion
    }
}
