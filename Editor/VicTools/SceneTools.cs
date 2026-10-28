using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace VicTools
{
    /// 场景工具类 - 提供场景操作相关的公共静态方法

    public static class SceneTools
    {
        private static Light FindMainDirectionalLight()
        {
            Light[] allLights = Object.FindObjectsByType<Light>(FindObjectsSortMode.None);
            List<Light> directionalLights = new List<Light>();
                
            // 收集所有方向灯
            foreach (Light light in allLights)
            {
                if (light.type == LightType.Directional)
                {
                    directionalLights.Add(light);
                }
            }
            if (directionalLights.Count == 0)
            {
                return null;
            }
            // 优先返回启用的方向灯
            foreach (Light light in directionalLights)
            {
                if (light.enabled)
                {
                    return light;
                }
            }
            // 如果没有启用的，返回第一个方向灯
            return directionalLights[0];
        }
        public static void ApplyLightDirectionToMaterials()
        {
            Vector3 lightDirection = Vector3.forward;
            int materialCount = 0;
            HashSet<Material> processedMaterials = new HashSet<Material>();
            Light light = FindMainDirectionalLight();
            if (light != null)
            {
                lightDirection = -light.transform.forward;
                // 只处理当前场景中用到的材质
                Renderer[] allRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                foreach (Renderer renderer in allRenderers)
                {
                    foreach (Material mat in renderer.sharedMaterials)
                    {
                        if (mat != null && !processedMaterials.Contains(mat) && mat.shader)
                        {
                            // 支持 PBR_Mobile_NEW 和 PBR_Mobile_NEW_Trans
                            bool isPBRMobile = mat.shader.name == "Custom/PBR_Mobile_NEW";
                            bool isPBRMobileTrans = mat.shader.name == "Custom/PBR_Mobile_NEW_Trans";
                            
                            if ((isPBRMobile || isPBRMobileTrans) && mat.HasProperty("_BakedSpecularDirection"))
                            {
                                mat.SetVector("_BakedSpecularDirection", lightDirection);
                                EditorUtility.SetDirty(mat);
                                processedMaterials.Add(mat);
                                materialCount++;
                            }
                        }
                    }
                }
            }

            // 保存更改
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
            // 显示结果
            if (materialCount > 0)
            {
                EditorUtility.DisplayDialog("成功", 
                    $"已成功将灯光方向应用到 {materialCount} 个材质\n方向: {lightDirection}", 
                    "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("警告", 
                    "未在场景中找到使用 Custom/PBR_Mobile_NEW 的材质", 
                    "确定");
            }
        }

        /// 切换选中对象的材质着色器在 PBR_Mobile_NEW 和 PBR_Lighting 之间
        /// 公共函数调用方法：SceneTools.SwitchPBRLightingShader();
        /// <param name="useStaticObjects">true: 处理场景中所有静态对象; false: 只处理选中的对象</param>
        public static void SwitchPBRLightingShader(bool useStaticObjects = false)
        {
            GameObject[] targetObjects;
            
            if (useStaticObjects)
            {
                // 获取场景中所有带 Renderer 的静态对象
                Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                List<GameObject> staticObjects = new List<GameObject>();
                
                foreach (Renderer renderer in allRenderers)
                {
                    GameObject obj = renderer.gameObject;
                    // 检查对象是否有任何静态标志
                    StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(obj);
                    if (flags != 0 && !staticObjects.Contains(obj))
                    {
                        staticObjects.Add(obj);
                    }
                }
                
                targetObjects = staticObjects.ToArray();
                
                if (targetObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("提示", "场景中没有找到静态对象", "确定");
                    return;
                }
            }
            else
            {
                // 使用选中的对象
                targetObjects = Selection.gameObjects;
                
                if (targetObjects.Length == 0)
                {
                    EditorUtility.DisplayDialog("提示", "请先选择场景中的对象", "确定");
                    return;
                }
            }

            Shader pbrMobileShader = Shader.Find("Custom/PBR_Mobile_NEW");
            Shader pbrLightingShader = Shader.Find("Custom/PBR_Lighting");

            if (pbrMobileShader == null || pbrLightingShader == null)
            {
                EditorUtility.DisplayDialog("错误", 
                    "未找到着色器:\n" +
                    (pbrMobileShader == null ? "- Custom/PBR_Mobile_NEW\n" : "") +
                    (pbrLightingShader == null ? "- Custom/PBR_Lighting\n" : ""),
                    "确定");
                return;
            }

            int switchedCount = 0;
            int pbrMobileToLighting = 0;
            int pbrLightingToMobile = 0;
            HashSet<Material> processedMaterials = new HashSet<Material>();

            // 记录 Undo 操作
            string undoName = useStaticObjects ? "Switch PBR Lighting Shader (Static Objects)" : "Switch PBR Lighting Shader";
            Undo.RecordObjects(targetObjects, undoName);

            // 如果是静态对象模式，先检查材质一致性
            bool forceSetToLighting = false;
            if (useStaticObjects)
            {
                HashSet<string> shaderTypes = new HashSet<string>();
                
                // 收集所有材质的着色器类型
                foreach (GameObject obj in targetObjects)
                {
                    Renderer renderer = obj.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        foreach (Material mat in renderer.sharedMaterials)
                        {
                            if (mat != null)
                            {
                                string shaderName = mat.shader.name;
                                if (shaderName == "Custom/PBR_Mobile_NEW" || shaderName == "Custom/PBR_Lighting")
                                {
                                    shaderTypes.Add(shaderName);
                                }
                            }
                        }
                    }
                }
                
                // 如果材质不一致（同时存在两种着色器），强制设置为 PBR_Lighting
                if (shaderTypes.Count > 1)
                {
                    forceSetToLighting = true;
                }
            }

            foreach (GameObject obj in targetObjects)
            {
                Renderer[] renderers;
                
                if (useStaticObjects)
                {
                    // 静态对象模式：只处理对象本身的 Renderer，不包含子对象
                    Renderer renderer = obj.GetComponent<Renderer>();
                    renderers = renderer != null ? new Renderer[] { renderer } : new Renderer[0];
                }
                else
                {
                    // 选中对象模式：处理对象及其所有子对象
                    renderers = obj.GetComponentsInChildren<Renderer>(true);
                }
                
                foreach (Renderer renderer in renderers)
                {
                    // 记录 Renderer 的 Undo
                    Undo.RecordObject(renderer, undoName);
                    
                    foreach (Material mat in renderer.sharedMaterials)
                    {
                        if (mat == null || processedMaterials.Contains(mat))
                            continue;

                        string shaderName = mat.shader.name;
                        
                        if (forceSetToLighting)
                        {
                            // 强制设置为 PBR_Lighting（材质不一致时）
                            if (shaderName == "Custom/PBR_Mobile_NEW" || shaderName == "Custom/PBR_Lighting")
                            {
                                Undo.RecordObject(mat, "Force Set to PBR_Lighting");
                                mat.shader = pbrLightingShader;
                                EditorUtility.SetDirty(mat);
                                processedMaterials.Add(mat);
                                switchedCount++;
                                if (shaderName == "Custom/PBR_Mobile_NEW")
                                {
                                    pbrMobileToLighting++;
                                }
                            }
                        }
                        else
                        {
                            // 正常切换逻辑（材质一致时）
                            if (shaderName == "Custom/PBR_Mobile_NEW")
                            {
                                // 记录材质的 Undo
                                Undo.RecordObject(mat, "Switch to PBR_Lighting");
                                mat.shader = pbrLightingShader;
                                EditorUtility.SetDirty(mat);
                                processedMaterials.Add(mat);
                                switchedCount++;
                                pbrMobileToLighting++;
                            }
                            else if (shaderName == "Custom/PBR_Lighting")
                            {
                                // 记录材质的 Undo
                                Undo.RecordObject(mat, "Switch to PBR_Mobile_NEW");
                                mat.shader = pbrMobileShader;
                                EditorUtility.SetDirty(mat);
                                processedMaterials.Add(mat);
                                switchedCount++;
                                pbrLightingToMobile++;
                            }
                        }
                    }
                }
            }

            if (switchedCount > 0)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                // 只有在选中多个对象或处理静态对象时才显示提示窗口
                if (useStaticObjects || targetObjects.Length > 1)
                {
                    string objectType = useStaticObjects ? "静态对象" : "选中对象";
                    string message;
                    
                    if (forceSetToLighting)
                    {
                        message = $"检测到材质不一致，已统一设置为 PBR_Lighting ({objectType}):\n";
                        message += $"- 共处理 {switchedCount} 个材质";
                    }
                    else
                    {
                        message = $"成功切换 {switchedCount} 个材质的着色器 ({objectType}):\n";
                        if (pbrMobileToLighting > 0)
                            message += $"- PBR_Mobile_NEW → PBR_Lighting: {pbrMobileToLighting} 个\n";
                        if (pbrLightingToMobile > 0)
                            message += $"- PBR_Lighting → PBR_Mobile_NEW: {pbrLightingToMobile} 个";
                    }
                    
                    EditorUtility.DisplayDialog("成功", message.TrimEnd('\n'), "确定");
                }
            }
            else
            {
                string objectType = useStaticObjects ? "静态对象" : "选中的对象";
                EditorUtility.DisplayDialog("提示", 
                    $"{objectType}中没有使用 Custom/PBR_Mobile_NEW 或 Custom/PBR_Lighting 着色器的材质", 
                    "确定");
            }
        }
        
        /// 选择场景中所有非Prefab的模型对象
        /// 公共函数调用方法：SceneTools.SelectAllNoPrefab();
        public static void SelectAllNoPrefab()
        {
            // 调用新的通用选择函数，默认选择非Prefab的Mesh对象
            SelectObjectsByType(true, false, false);
        }
        
        /// 根据选项选择场景中的物体
        /// <param name="selectMesh">是否选择非Prefab的Mesh对象</param>
        /// <param name="selectPrefab">是否选择Prefab对象</param>
        /// <param name="selectLODGroup">是否选择带LODGroup的对象</param>
        /// <param name="selectMissMat">是否选择丢失材质球的模型对象</param>
        /// <param name="selectMissScript">是否选择丢失脚本的对象</param>
        /// <param name="selAct">勾选时只挑选激活的对象，取消时只挑选未激活的对象</param>
        /// <param name="selMeshObj">是否挑选Mesh对象</param>
        /// <param name="selParticleObj">是否挑选粒子对象</param>
        public static void SelectObjectsByType(bool selectMesh = true, bool selectPrefab = false, bool selectLODGroup = false, bool selectMissMat = false, bool selectMissMesh = false, bool selectMissScript = false, bool selAct = false, bool selMeshObj = false, bool selParticleObj = false, bool selParent = false, bool localScope = false)
        {
            // 辅助方法：检查对象是否符合激活状态条件
            bool CheckActivationState(GameObject obj)
            {
                if (selAct)
                {
                    // 勾选时只挑选激活的对象
                    return obj.activeSelf;
                }
                else
                {
                    // 取消时只挑选未激活的对象
                    return !obj.activeSelf;
                }
            }
            
            // 辅助方法：根据selParent获取应该选中的对象
            GameObject GetTargetObject(GameObject obj)
            {
                if (!selParent)
                {
                    return obj;
                }
                
                // 如果勾选了selectPrefab，找到上级Prefab对象
                if (selectPrefab && PrefabUtility.IsPartOfPrefabInstance(obj))
                {
                    // 获取Prefab的最外层对象（Prefab根对象）
                    var prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(obj);
                    if (prefabRoot != null && prefabRoot != obj)
                    {
                        // 如果找到的Prefab根对象不是当前对象，说明当前对象在Prefab内部
                        return prefabRoot;
                    }
                    else if (obj.transform.parent != null)
                    {
                        // 如果已经是Prefab根对象，检查父对象是否也是Prefab
                        var parent = obj.transform.parent.gameObject;
                        if (PrefabUtility.IsPartOfPrefabInstance(parent))
                        {
                            // 父对象也是Prefab，选择父对象
                            return parent;
                        }
                        else
                        {
                            // 父对象不是Prefab，保持选择当前Prefab对象
                            return obj;
                        }
                    }
                    else
                    {
                        // 如果没有父对象，保持原对象
                        return obj;
                    }
                }
                else
                {
                    // 其他情况，找到最顶级的父对象（根对象）
                    return obj.transform.root.gameObject;
                }
            }
            
            // 判断是否为排除模式（所有5个选项都为false时）
            bool isExcludeMode = !selectMesh && !selectPrefab && !selectLODGroup && !selectMissMat && !selectMissMesh && !selectMissScript;
            
            // 获取场景中的游戏对象
            GameObject[] allGameObjects;
            
            if (localScope)
            {
                // 局部范围：只在当前选中对象的父级（或选中对象自身）的子层级中搜索
                var currentSelection = Selection.gameObjects;
                if (currentSelection == null || currentSelection.Length == 0)
                {
                    EditorUtility.DisplayDialog("提示", "Ctrl+挑选需要先选中对象，将在其父级范围内搜索", "确定");
                    return;
                }
                var scopeObjects = new List<GameObject>();
                foreach (var sel in currentSelection)
                {
                    // 使用选中对象的父对象作为搜索根；如果没有父则用自身
                    Transform searchRoot = sel.transform.parent != null ? sel.transform.parent : sel.transform;
                    var transforms = searchRoot.GetComponentsInChildren<Transform>(true);
                    foreach (var t in transforms)
                    {
                        if (!scopeObjects.Contains(t.gameObject))
                            scopeObjects.Add(t.gameObject);
                    }
                }
                allGameObjects = scopeObjects.ToArray();
            }
            else
            {
                // 全场景范围
                var roots = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                var allObjects = new List<GameObject>();
                foreach (var root in roots)
                {
                    var transforms = root.GetComponentsInChildren<Transform>(true);
                    foreach (var t in transforms)
                    {
                        allObjects.Add(t.gameObject);
                    }
                }
                allGameObjects = allObjects.ToArray();
            }
            
            var selectedObjects = new HashSet<GameObject>();
            
            foreach (var gameObject in allGameObjects)
            {
                bool shouldSelect = false;
                
                if (isExcludeMode)
                {
                    // 排除模式：排除Prefab、Mesh、带LOD的对象、丢失脚本的对象
                    shouldSelect = true; // 默认选择
                    
                    // 首先检查激活状态
                    if (!CheckActivationState(gameObject))
                    {
                        shouldSelect = false;
                    }
                    
                    // 排除Prefab对象
                    if (shouldSelect && PrefabUtility.IsPartOfPrefabInstance(gameObject))
                    {
                        shouldSelect = false;
                    }
                    
                    // 排除Mesh对象
                    if (shouldSelect)
                    {
                        var renderer = gameObject.GetComponent<Renderer>();
                        if (renderer != null && renderer is MeshRenderer)
                        {
                            shouldSelect = false;
                        }
                    }
                    
                    // 排除带LODGroup的对象
                    if (shouldSelect)
                    {
                        var lodGroup = gameObject.GetComponent<LODGroup>();
                        if (lodGroup != null)
                        {
                            shouldSelect = false;
                        }
                    }
                    
                    // 排除丢失脚本的对象
                    if (shouldSelect)
                    {
                        var components = gameObject.GetComponents<Component>();
                        foreach (var component in components)
                        {
                            if (component == null)
                            {
                                shouldSelect = false;
                                break;
                            }
                        }
                    }
                }
                else
                {
                    // 辅助方法：检查对象类型是否符合二级选项筛选条件（除了selParent）
                    bool PassesSecondaryTypeFilter(GameObject obj)
                    {
                        // 如果两个选项都不勾选，则不进行类型筛选（全部通过）
                        if (!selMeshObj && !selParticleObj)
                        {
                            return true;
                        }
                        
                        bool isMeshObject = false;
                        bool isParticleObject = false;
                        
                        // 检查是否是模型对象（有MeshRenderer组件）
                        var renderer = obj.GetComponent<Renderer>();
                        if (renderer != null && renderer is MeshRenderer)
                        {
                            isMeshObject = true;
                        }
                        
                        // 检查是否是粒子对象（有ParticleSystem组件）
                        var particleSystem = obj.GetComponent<ParticleSystem>();
                        if (particleSystem != null)
                        {
                            isParticleObject = true;
                        }
                        
                        // 根据selMeshObj、selParticleObj筛选
                        if (selMeshObj && isMeshObject) return true;
                        if (selParticleObj && isParticleObject) return true;
                        
                        return false;
                    }
                    
                    // 正常选择模式
                    // 检查是否选择Prefab对象
                    if (selectPrefab)
                    {
                        // 检查对象是否是Prefab实例且符合激活状态条件和二级类型筛选
                        if (PrefabUtility.IsPartOfPrefabInstance(gameObject) && 
                            CheckActivationState(gameObject) && 
                            PassesSecondaryTypeFilter(gameObject))
                        {
                            shouldSelect = true;
                        }
                    }
                    
                    // 检查是否选择非Prefab的Mesh对象
                    if (selectMesh && !shouldSelect)
                    {
                        // 检查对象是否是Prefab实例
                        if (!PrefabUtility.IsPartOfPrefabInstance(gameObject))
                        {
                            // 检查对象是否有Renderer组件（模型对象通常有Renderer）
                            var renderer = gameObject.GetComponent<Renderer>();
                            if (renderer != null)
                            {
                                // 进一步检查是否是MeshRenderer且符合激活状态条件和二级类型筛选
                                if (renderer is MeshRenderer && 
                                    CheckActivationState(gameObject) && 
                                    PassesSecondaryTypeFilter(gameObject))
                                {
                                    shouldSelect = true;
                                }
                            }
                        }
                    }
                    
                    // 检查是否选择带LODGroup的对象
                    if (selectLODGroup && !shouldSelect)
                    {
                        // 检查对象是否有LODGroup组件且符合激活状态条件和二级类型筛选
                        var lodGroup = gameObject.GetComponent<LODGroup>();
                        if (lodGroup != null && 
                            CheckActivationState(gameObject) && 
                            PassesSecondaryTypeFilter(gameObject))
                        {
                            shouldSelect = true;
                        }
                    }
                    
                    // 检查是否选择丢失材质球的模型对象
                    if (selectMissMat && !shouldSelect)
                    {
                        // 检查对象是否有Renderer组件
                        var renderer = gameObject.GetComponent<Renderer>();
                        if (renderer != null)
                        {
                            // 检查材质球是否丢失（材质为null）
                            var materials = renderer.sharedMaterials;
                            bool hasMissingMaterial = false;
                            
                            if (materials == null || materials.Length == 0)
                            {
                                hasMissingMaterial = true;
                            }
                            else
                            {
                                // 对于ParticleSystemRenderer，只检查主材质（index 0），
                                // 跳过Trail Material槽位（index 1），因为未启用Trail时该槽位为null是正常的
                                int materialCountToCheck = materials.Length;
                                if (renderer is ParticleSystemRenderer)
                                {
                                    materialCountToCheck = System.Math.Min(1, materials.Length);
                                }
                                
                                for (int i = 0; i < materialCountToCheck; i++)
                                {
                                    if (materials[i] == null)
                                    {
                                        hasMissingMaterial = true;
                                        break;
                                    }
                                }
                            }
                            
                            if (hasMissingMaterial && 
                                CheckActivationState(gameObject) && 
                                PassesSecondaryTypeFilter(gameObject))
                            {
                                shouldSelect = true;
                            }
                        }
                    }
                    
                    // 检查是否选择丢失Mesh的对象
                    if (selectMissMesh && !shouldSelect)
                    {
                        bool hasMissingMesh = false;
                        
                        // 检查 MeshFilter 的 sharedMesh 是否为空
                        var meshFilter = gameObject.GetComponent<MeshFilter>();
                        if (meshFilter != null && meshFilter.sharedMesh == null)
                        {
                            hasMissingMesh = true;
                        }
                        
                        // 检查 SkinnedMeshRenderer 的 sharedMesh 是否为空
                        if (!hasMissingMesh)
                        {
                            var skinnedRenderer = gameObject.GetComponent<SkinnedMeshRenderer>();
                            if (skinnedRenderer != null && skinnedRenderer.sharedMesh == null)
                            {
                                hasMissingMesh = true;
                            }
                        }
                        
                        if (hasMissingMesh && 
                            CheckActivationState(gameObject) && 
                            PassesSecondaryTypeFilter(gameObject))
                        {
                            shouldSelect = true;
                        }
                    }

                    // 检查是否选择丢失脚本的对象
                    if (selectMissScript && !shouldSelect)
                    {
                        // 只检查当前对象本身是否有丢失的脚本
                        var components = gameObject.GetComponents<Component>();
                        bool hasMissingScript = false;
                        
                        foreach (var component in components)
                        {
                            // 如果组件为null，说明脚本丢失了
                            if (component == null)
                            {
                                hasMissingScript = true;
                                break;
                            }
                        }
                        
                        if (hasMissingScript && 
                            CheckActivationState(gameObject) && 
                            PassesSecondaryTypeFilter(gameObject))
                        {
                            shouldSelect = true;
                        }
                    }
                }
                
                // 最终决定是否选择
                if (shouldSelect)
                {
                    var targetObject = GetTargetObject(gameObject);
                    // 如果使用了selParent，需要重新检查目标对象是否符合激活状态条件
                    if (selParent && targetObject != gameObject)
                    {
                        if (CheckActivationState(targetObject))
                        {
                            selectedObjects.Add(targetObject);
                        }
                    }
                    else
                    {
                        selectedObjects.Add(targetObject);
                    }
                }
            }
            
            if (selectedObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "场景中没有找到符合条件的对象", "确定");
                return;
            }
            
            // 选择所有符合条件的对象
            Selection.objects = selectedObjects.ToArray();
            
            // 构建选择类型描述
            var typeDescriptions = new List<string>();
            if (isExcludeMode)
            {
                typeDescriptions.Add("排除Prefab、Mesh、带LOD的对象、丢失脚本的其他对象");
            }
            else
            {
                if (selectMesh) typeDescriptions.Add("非Prefab的Mesh对象");
                if (selectPrefab) typeDescriptions.Add("Prefab对象");
                if (selectLODGroup) typeDescriptions.Add("带LODGroup的对象");
                if (selectMissMat) typeDescriptions.Add("丢失材质球的模型对象");
                if (selectMissMesh) typeDescriptions.Add("丢失Mesh的对象");
                if (selectMissScript) typeDescriptions.Add("丢失脚本的对象");
            }
            
            // 添加筛选条件描述
            var filterDescriptions = new List<string>();
            if (selAct) filterDescriptions.Add("激活");
            else filterDescriptions.Add("未激活");
            
            var typeFilters = new List<string>();
            if (selMeshObj) typeFilters.Add("模型");
            if (selParticleObj) typeFilters.Add("粒子");
            if (selParent) typeFilters.Add("顶级父物体");
            
            if (typeFilters.Count > 0)
            {
                filterDescriptions.Add(string.Join("或", typeFilters));
            }
            
            var typeDescription = string.Join("、", typeDescriptions);
            var filterDescription = string.Join("、", filterDescriptions);
            
            Debug.Log($"已选择 {selectedObjects.Count} 个{typeDescription}（筛选：{filterDescription}）");
        }
        
        /// 尝试为选中的丢失 Mesh 对象自动找回 Mesh
        /// 策略：根据对象名称在项目中搜索匹配的 Mesh 资产
        /// _OL 后缀对象优先匹配 _SmoothNormal 后缀的 Mesh
        /// 公共函数调用方法：SceneTools.FindMissMeshs();
        public static void FindMissMeshs()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先选中丢失 Mesh 的对象（可先用 Miss Mesh 挑选）", "确定");
                return;
            }

            int fixedCount = 0;
            int failedCount = 0;

            foreach (var go in selectedObjects)
            {
                // 检查是否确实丢失 Mesh
                MeshFilter mf = go.GetComponent<MeshFilter>();
                SkinnedMeshRenderer smr = go.GetComponent<SkinnedMeshRenderer>();
                bool isMeshFilter = mf != null && mf.sharedMesh == null;
                bool isSkinned = smr != null && smr.sharedMesh == null;

                if (!isMeshFilter && !isSkinned)
                    continue;

                string objName = go.name;
                bool isOL = objName.EndsWith("_OL", System.StringComparison.OrdinalIgnoreCase);

                // 构建搜索关键词
                string baseName = isOL ? objName.Substring(0, objName.Length - 3) : objName;

                Mesh foundMesh = null;

                // 【最高优先级】在相似文件名的 FBX 资产内查找匹配的 Mesh
                // 优先级高于独立 .mesh 资产的原因：项目历史中丢失的 Mesh 常源于同名 FBX 中被提取出来的子 mesh，
                //       工程中存在与对象同名的 .mesh 资产时往往属于"陈旧/无关"版本，实际想要的 Mesh 大概率仍嵌在 FBX 内。
                // 行为：先反向定位文件名相似的 FBX，再从中挑出最匹配的 mesh 子资产；
                //       若无相似 FBX 命中（foundMesh 仍为 null），按既定优先级继续走精确匹配与模糊兜底。
                if (foundMesh == null)
                {
                    Mesh fbxInner = SearchMeshInSimilarFBX(objName, baseName, isOL);
                    if (fbxInner != null)
                    {
                        foundMesh = fbxInner;
                        Debug.Log($"[FindMissMeshs] 通过 FBX 内匹配找回 Mesh：{objName} → {fbxInner.name}");
                    }
                }

                // 【第二优先级】精确名称匹配：仅在 FBX 内匹配未命中时才执行
                //       必须用 if 守护，否则会直接覆盖 FBX 内已找回的 mesh，使 FBX 内匹配形同虚设。
                if (foundMesh == null)
                {
                    if (isOL)
                    {
                        // _OL 对象：最优先查找 objName + _SmoothNormal（如 xxx_OL_SmoothNormal）
                        foundMesh = SearchMeshAsset(objName + "_SmoothNormal");
                        // 备选：查找 baseName + _SmoothNormal（如 xxx_SmoothNormal）
                        if (foundMesh == null)
                            foundMesh = SearchMeshAsset(baseName + "_SmoothNormal");
                        // 备选：查找 baseName + _OL 的 Mesh
                        if (foundMesh == null)
                            foundMesh = SearchMeshAsset(baseName + "_OL");
                        // 再备选：查找 objName（完整名）的 Mesh
                        if (foundMesh == null)
                            foundMesh = SearchMeshAsset(objName);
                    }
                    else
                    {
                        // 普通对象：按对象名称直接查找
                        foundMesh = SearchMeshAsset(objName);
                        // 备选：去掉常见后缀再搜索
                        if (foundMesh == null)
                            foundMesh = SearchMeshAsset(baseName);
                    }
                }

                if (foundMesh != null)
                {
                    if (isMeshFilter)
                    {
                        Undo.RecordObject(mf, "找回 Mesh");
                        mf.sharedMesh = foundMesh;
                        EditorUtility.SetDirty(mf);
                    }
                    else if (isSkinned)
                    {
                        Undo.RecordObject(smr, "找回 Mesh");
                        smr.sharedMesh = foundMesh;
                        EditorUtility.SetDirty(smr);
                    }
                    fixedCount++;
                }
                else
                {
                    // 兜底：使用对象名称进行模糊相似匹配
                    // _OL 对象优先查找带 _SmoothNormal 后缀的 Mesh
                    if (isOL)
                    {
                        foundMesh = SearchMeshAssetFuzzy(objName + "_SmoothNormal");
                        if (foundMesh == null)
                            foundMesh = SearchMeshAssetFuzzy(baseName + "_SmoothNormal");
                        if (foundMesh == null)
                            foundMesh = SearchMeshAssetFuzzy(baseName);
                    }
                    else
                    {
                        foundMesh = SearchMeshAssetFuzzy(objName);
                    }

                    if (foundMesh != null)
                    {
                        if (isMeshFilter)
                        {
                            Undo.RecordObject(mf, "找回 Mesh（模糊匹配）");
                            mf.sharedMesh = foundMesh;
                            EditorUtility.SetDirty(mf);
                        }
                        else if (isSkinned)
                        {
                            Undo.RecordObject(smr, "找回 Mesh（模糊匹配）");
                            smr.sharedMesh = foundMesh;
                            EditorUtility.SetDirty(smr);
                        }
                        fixedCount++;
                        Debug.Log($"[FindMissMeshs] 通过模糊匹配找回 Mesh：{objName} → {foundMesh.name}");
                    }
                    else
                    {
                        failedCount++;
                        Debug.LogWarning($"[FindMissMeshs] 未找到匹配的 Mesh：{objName}（搜索关键词：{baseName}）");
                    }
                }
            }

            string msg = $"已找回 {fixedCount} 个 Mesh";
            if (failedCount > 0)
                msg += $"，{failedCount} 个未找到（详见 Console）";
            EditorUtility.DisplayDialog("找回 Mesh 结果", msg, "确定");
        }

        /// 在项目资产中按名称搜索 Mesh（支持 .asset 和模型文件中的子 Mesh）
        private static Mesh SearchMeshAsset(string meshName)
        {
            // 方式1：搜索 .asset 文件（独立 Mesh 资产）
            string[] guids = AssetDatabase.FindAssets(meshName + " t:Mesh");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                // 加载路径下的所有资产（模型文件可能包含多个子 Mesh）
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in assets)
                {
                    if (asset is Mesh mesh && mesh.name == meshName)
                        return mesh;
                }
            }

            // 方式2：模糊匹配（名称包含搜索词）
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in assets)
                {
                    if (asset is Mesh mesh && mesh.name.Contains(meshName))
                        return mesh;
                }
            }

            return null;
        }

        /// 基于对象名称的模糊相似匹配查找 Mesh
        /// 策略：将对象名拆分为单词片段，在项目中搜索包含最多匹配片段的 Mesh
        private static Mesh SearchMeshAssetFuzzy(string objectName)
        {
            // 清理名称：去掉常见后缀和数字编号，拆分为有意义的片段
            string cleanName = objectName
                .Replace("_OL", "")
                .Replace("_ol", "")
                .Replace("(Clone)", "")
                .Trim();

            // 按 _、空格、大小写变化拆分为片段
            var segments = System.Text.RegularExpressions.Regex.Split(cleanName, @"[_\s\-]+")
                .Where(s => s.Length > 1)
                .ToList();

            // 也按大小写驼峰拆分
            var camelSegments = new List<string>();
            foreach (var seg in segments)
            {
                var parts = System.Text.RegularExpressions.Regex.Split(seg, @"(?<!^)(?=[A-Z])");
                foreach (var p in parts)
                    if (p.Length > 1)
                        camelSegments.Add(p.ToLower());
            }

            if (camelSegments.Count == 0 && segments.Count == 0)
                return null;

            var allSegments = segments.Select(s => s.ToLower()).Concat(camelSegments).Distinct().ToList();

            // 搜索项目中所有 Mesh 资产
            string[] guids = AssetDatabase.FindAssets("t:Mesh");
            Mesh bestMatch = null;
            int bestScore = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var asset in assets)
                {
                    if (!(asset is Mesh mesh)) continue;

                    string meshNameLower = mesh.name.ToLower();
                    int score = 0;

                    foreach (var seg in allSegments)
                    {
                        if (meshNameLower.Contains(seg))
                            score += seg.Length; // 按匹配片段长度加权
                    }

                    // 额外加分：对象名包含在 mesh 名中，或 mesh 名包含在对象名中
                    if (meshNameLower.Contains(cleanName.ToLower()))
                        score += cleanName.Length * 2;
                    else if (cleanName.ToLower().Contains(meshNameLower))
                        score += meshNameLower.Length;

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMatch = mesh;
                    }
                }
            }

            // 设置最低匹配阈值：至少匹配到对象名长度的 40% 才认为有效
            int threshold = Mathf.Max(3, (int)(cleanName.Length * 0.4f));
            return bestScore >= threshold ? bestMatch : null;
        }

        /// 在与对象名相似的 FBX 模型文件内查找匹配的 Mesh
        /// 思路：先在所有模型资产上按文件名相似度 + LCS 双重筛选候选 FBX，再遍历候选 FBX 内部的子 Mesh，
        ///      按"对象名严格相等 → baseName 相等 → _SmoothNormal → 拆词匹配 → 兜底首 mesh"的多级策略挑选。
        /// 适用：Mesh 作为 FBX 子资产存在（mesh 名可能与 FBX 文件名不同，甚至拼写有差异），
        ///       但宿主 FBX 文件名通常与对象名高度相似时使用。
        private static Mesh SearchMeshInSimilarFBX(string objectName, string baseName, bool isOL)
        {
            if (string.IsNullOrEmpty(objectName)) return null;

            // 标准化对比用名（全小写，去掉 _OL/(Clone) 等）
            string cleanFull = CleanNameForMatch(objectName);
            string cleanBase = CleanNameForMatch(baseName);
            if (cleanFull.Length < 2) return null;

            // 将对象名拆词，用于相似度对比
            var objSegments = SplitNameToLowerSegments(objectName);
            if (objSegments.Count == 0 && cleanFull.Length < 4) return null;

            // 1) 收集文件名上有"可能同源"联系的 FBX 候选
            //    用候选池而非直接 Top 1：兼顾硬相似度高（如完全同名）和 LCS 高（拼写差异但主体同源）两种命中路径。
            //    进入候选池的最低门槛（满足任一）：
            //      - ScoreFBXFileAgainstObject ≥ 6（包含/相等/片段命中）
            //      - LCS ≥ max(4, cleanBase.Length / 3)（主体同源但拼写差异）
            string[] guids = AssetDatabase.FindAssets("t:Model");
            int lcsMinEnter = Mathf.Max(4, cleanBase.Length / 3);
            var candidates = new List<(string path, int score, int lcs)>(guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path)) continue;
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrEmpty(fileName)) continue;
                string fLow = fileName.ToLower();
                int s = ScoreFBXFileAgainstObject(fLow, cleanFull, cleanBase, objSegments);
                int lcs = LongestCommonSubsequence(fLow, cleanBase);
                if (s >= 6 || lcs >= lcsMinEnter)
                    candidates.Add((path, s, lcs));
            }
            if (candidates.Count == 0) return null;

            // 排序：先按 LCS 再按 score，LCS 更能反映"主体同源但拼写不同"
            candidates.Sort((a, b) =>
            {
                int c = b.lcs.CompareTo(a.lcs);
                return c != 0 ? c : b.score.CompareTo(a.score);
            });

            string bestFBXPath = candidates[0].path;
            int bestFBXScore = candidates[0].score;
            int bestFBXLCS = candidates[0].lcs;

            // 2) 加载该 FBX 内的 assets
            Object[] assets;
            try { assets = AssetDatabase.LoadAllAssetsAtPath(bestFBXPath); }
            catch { return null; }
            if (assets == null) return null;

            // a. 完全相等 objName（如 FQ_yuanzhumokuai_01.fbx 内 mesh 名就叫 FQ_yuanzhumokuai_01）
            foreach (var asset in assets)
            {
                if (asset is Mesh m && m.name == objectName) return m;
            }
            // b. 完全相等 baseName（去 _OL）
            foreach (var asset in assets)
            {
                if (asset is Mesh m && m.name == baseName) return m;
            }
            // c. _OL 对象偏好 _SmoothNormal 后缀
            if (isOL)
            {
                foreach (var asset in assets)
                {
                    if (asset is Mesh m && (m.name == objectName + "_SmoothNormal" || m.name == baseName + "_SmoothNormal"))
                        return m;
                }
            }
            // d. 拆词匹配最佳 mesh
            Mesh bestMesh = null;
            int bestScore = 0;
            foreach (var asset in assets)
            {
                if (!(asset is Mesh mesh)) continue;
                int ms = ScoreMeshNameAgainstObject(mesh.name, objectName, baseName, isOL, objSegments);
                if (ms > bestScore)
                {
                    bestScore = ms;
                    bestMesh = mesh;
                }
            }
            if (bestScore >= 30) return bestMesh;

            // e. 终极兜底：当 fbx 文件名与对象名高度匹配时，
            //    即便 fbx 内部 mesh 名为通用名（如 Mesh、Mesh_0、Geometry），也采用该 fbx 内的首个 mesh，
            //    优先于工程中的同名独立 .mesh 资产（后者往往是历史残留 / 衍生资源，不是用户期望的目标）。
            //    触发条件（满足任一即生效）：
            //      - 文件名硬相似度 ≥ 50（cleanBase 完全相等 / 文件名含 cleanBase）
            //      - 文件名与对象名 LCS ≥ 对象名长度的 2/3，且不少于 5（容错拼写差异场景）
            int lcsThreshold = Mathf.Max(5, (int)(cleanBase.Length * (2.0f / 3.0f)));
            if (bestFBXScore >= 50 || bestFBXLCS >= lcsThreshold)
            {
                foreach (var asset in assets)
                {
                    if (asset is Mesh m) return m;
                }
            }

            return null;
        }

        /// 将名称清理后转小写，用于相似度对比
        private static string CleanNameForMatch(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            string s = name
                .Replace("_OL", "")
                .Replace("_ol", "")
                .Replace("(Clone)", "")
                .Replace("(clone)", "")
                .Trim();
            return s.ToLower();
        }

        /// 拆词为小写片段集合（按 _ - 空格 与 大小写边界）
        private static List<string> SplitNameToLowerSegments(string name)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(name)) return list;
            var parts = System.Text.RegularExpressions.Regex.Split(name, @"[\s\-]+");
            foreach (var p in parts)
            {
                if (string.IsNullOrEmpty(p)) continue;
                var cam = System.Text.RegularExpressions.Regex.Split(p, @"(?<!^)(?=[A-Z])");
                foreach (var c in cam)
                {
                    if (c.Length >= 2)
                        list.Add(c.ToLower());
                }
            }
            return list;
        }

        /// FBX 文件名与对象名之间的相似度打分（越高越相关）
        private static int ScoreFBXFileAgainstObject(string fileLower, string cleanFull, string cleanBase, List<string> objSegments)
        {
            if (string.IsNullOrEmpty(fileLower) || fileLower.Length < 2) return 0;

            int score = 0;
            // 文件名与去后缀 baseName 完全一致：最强信号
            if (cleanBase.Length >= 2 && fileLower == cleanBase) score += 60;
            // 文件名包含 baseName（如 Tree_01.fbx 包含 tree_01）
            if (cleanBase.Length >= 2 && fileLower.Contains(cleanBase)) score += 50;
            // 文件名包含完整对象名（含 _OL 等后缀）
            if (cleanFull.Length >= 2 && fileLower.Contains(cleanFull)) score += 30;

            // 共享拆词片段：按片段长度加权
            for (int i = 0; i < objSegments.Count; i++)
            {
                string seg = objSegments[i];
                if (seg.Length >= 2 && fileLower.Contains(seg))
                    score += seg.Length;
            }
            return score;
        }

        /// 单个 mesh 名与对象名之间的匹配打分（越高越相关）
        private static int ScoreMeshNameAgainstObject(string meshName, string objectName, string baseName,
                                          bool isOL, List<string> objSegments)
        {
            if (string.IsNullOrEmpty(meshName)) return 0;
            string mLow = meshName.ToLower();
            string objLow = objectName.ToLower();
            string bLow = baseName.ToLower();

            // 精确匹配
            if (mLow == objLow) return 200;
            if (mLow == bLow) return 180;

            // _OL 对象偏好 _SmoothNormal 后缀
            if (isOL)
            {
                if (mLow == (objLow + "_smoothnormal")) return 170;
                if (mLow == (bLow + "_smoothnormal")) return 160;
            }

            // 互为子串（包含关系）
            if (objLow.Length >= 2 && mLow.Contains(objLow)) return 120;
            if (mLow.Length >= 2 && objLow.Contains(mLow)) return 110;

            // 拆词包含：越长越相关
            int score = 0;
            for (int i = 0; i < objSegments.Count; i++)
            {
                string seg = objSegments[i];
                if (seg.Length >= 2 && mLow.Contains(seg))
                    score += seg.Length;
            }
            return score;
        }

        /// 最长公共子序列长度（不要求连续，相对顺序一致即可；用于容忍拼写差异 / 子串错位 / 后缀不同）
        /// 典型场景：对象 mesh 名为 "huangsemene"，FBX 文件名 "FQ_Huangsedamen_01"，
        //            末尾拼写不同，但主体字母顺序依然一致，LCS 较高即可识别为同源。
        private static int LongestCommonSubsequence(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0;
            int n = a.Length;
            int m = b.Length;
            // 滚动数组实现：O(min(n,m)) 空间，对短字符串足够快
            var prev = new int[m + 1];
            var curr = new int[m + 1];
            for (int i = 1; i <= n; i++)
            {
                char ca = char.ToLower(a[i - 1]);
                for (int j = 1; j <= m; j++)
                {
                    char cb = char.ToLower(b[j - 1]);
                    if (ca == cb)
                        curr[j] = prev[j - 1] + 1;
                    else
                        curr[j] = prev[j] > curr[j - 1] ? prev[j] : curr[j - 1];
                }
                var tmp = prev;
                prev = curr;
                curr = tmp;
                for (int k = 0; k < m + 1; k++) curr[k] = 0;
            }
            return prev[m];
        }

        /// 选择所有层级 - 当选择了父物体时选择所有子物体，当选择了子物体时选择父物体及所有子物体
        /// 改进：当选中对象父物体是Prefab或者直接选择的是Prefab则只选择该Prefab以下的子对象
        /// 公共函数调用方法：SceneTools.SelectAllHierarchy();
        public static void SelectAllHierarchy()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects.Length == 0)
            {
                Debug.Log("未选中任何对象");
                return;
            }

            var allObjectsToSelect = new HashSet<GameObject>();
            
            foreach (var selectedObject in selectedObjects)
            {
                // 检查当前对象或其父对象是否是Prefab实例
                var isPrefabInstance = PrefabUtility.IsPartOfPrefabInstance(selectedObject);
                var parent = selectedObject.transform.parent;
                var parentIsPrefab = parent && PrefabUtility.IsPartOfPrefabInstance(parent.gameObject);
                
                // 如果当前对象是Prefab实例或其父对象是Prefab实例，找到Prefab根对象并选择整个Prefab层级
                if (isPrefabInstance || parentIsPrefab)
                {
                    // 找到Prefab实例根对象
                    var prefabRoot = selectedObject.transform;
                    while (prefabRoot.parent && PrefabUtility.IsPartOfPrefabInstance(prefabRoot.parent.gameObject))
                    {
                        prefabRoot = prefabRoot.parent;
                    }
                    
                    // 选择Prefab实例及其所有子物体
                    allObjectsToSelect.Add(prefabRoot.gameObject);
                    AddAllChildren(prefabRoot, allObjectsToSelect);
                    continue;
                }
                
                // 如果当前选择的是父物体，选择所有子物体
                if (selectedObject.transform.childCount > 0)
                {
                    // 选择父物体及其所有子物体
                    allObjectsToSelect.Add(selectedObject);
                    AddAllChildren(selectedObject.transform, allObjectsToSelect);
                }
                // 如果当前选择的是子物体，选择父物体及所有子物体
                else if (selectedObject.transform.parent)
                {
                    // 找到根父物体
                    var rootParent = selectedObject.transform;
                    while (rootParent.parent)
                    {
                        rootParent = rootParent.parent;
                    }
                    
                    // 选择根父物体及其所有子物体
                    allObjectsToSelect.Add(rootParent.gameObject);
                    AddAllChildren(rootParent, allObjectsToSelect);
                }
                // 如果当前选择的是没有子物体也没有父物体的物体，只选择它自己
                else
                {
                    allObjectsToSelect.Add(selectedObject);
                }
            }

            if (allObjectsToSelect.Count <= 0) return;
            Selection.objects = allObjectsToSelect.ToArray<Object>();
            EditorGUIUtility.PingObject(allObjectsToSelect.First());
            Debug.Log($"已选择 {allObjectsToSelect.Count} 个层级对象");
        }

        /// 递归添加所有子物体到集合中
        private static void AddAllChildren(Transform parent, HashSet<GameObject> objectSet)
        {
            foreach (Transform child in parent)
            {
                objectSet.Add(child.gameObject);
                AddAllChildren(child, objectSet);
            }
        }

        // ReSharper disable Unity.PerformanceAnalysis
        /// 将材质赋予场景中选中的模型（支持撤销）
        /// <param name="material">要赋予的材质</param>
        public static void AssignMaterialToSelectedModels(Material material)
        {
            if (!material)
            {
                Debug.LogWarning("无法赋予材质：材质为空");
                return;
            }

            // 获取当前选中的游戏对象
            var selectedObjects = Selection.gameObjects;
            
            if (selectedObjects.Length == 0)
            {
                // Debug.LogWarning("无法赋予材质：场景中没有选中的模型");
                EditorUtility.DisplayDialog("赋予材质失败", "请先在场景中选择要赋予材质的模型", "确定");
                return;
            }

            var assignedCount = 0;
            var objectsWithoutRenderer = new List<GameObject>();

            // 开始记录撤销操作
            Undo.RecordObjects(selectedObjects, $"Assign Material '{material.name}'");

            // 遍历所有选中的对象
            foreach (var o in selectedObjects)
            {
                var obj = (GameObject)o;
                // 获取对象的Renderer组件
                var renderer = obj.GetComponent<Renderer>();
                
                if (renderer)
                {
                    // 记录Renderer的材质状态用于撤销
                    Undo.RecordObject(renderer, $"Assign Material '{material.name}' to {obj.name}");
                    
                    // 将材质赋予Renderer
                    renderer.sharedMaterial = material;
                    assignedCount++;
                    Debug.Log($"已将材质 '{material.name}' 赋予对象 '{obj.name}'");
                }
                else
                {
                    objectsWithoutRenderer.Add(obj);
                }
            }

            // 显示结果
            if (assignedCount > 0)
            {
                var message = $"成功将材质 '{material.name}' 赋予 {assignedCount} 个模型";

                if (objectsWithoutRenderer.Count <= 0) return;
                message += $"\n\n有 {objectsWithoutRenderer.Count} 个对象没有Renderer组件：";
                objectsWithoutRenderer.Aggregate(message, (current, obj) => current + $"\n- {obj.name}");

                // EditorUtility.DisplayDialog("材质赋予完成", message, "确定");
            }
            else
            {
                EditorUtility.DisplayDialog("赋予材质失败", "选中的对象都没有Renderer组件，无法赋予材质", "确定");
            }
        }

        /// 设置选择的2个以上对象放入最后选择的对象中
        public static void SetSelectedObjectsAsChildren()
        {
            // 获取当前选中的所有 GameObject
            GameObject[] selected = Selection.gameObjects;

            // 至少需要两个对象：N-1 个子物体 + 1 个父物体
            if (selected.Length < 2)
            {
                Debug.LogWarning("请至少选择两个对象：最后一个将作为父物体。");
                return;
            }

            // 最后一个选中的是父物体
            GameObject parent = selected[selected.Length - 1];
            
            // 其余为子物体（排除父物体）
            var children = selected.Take(selected.Length - 1).Where(go => go != parent).ToArray();

            if (children.Length == 0)
            {
                Debug.LogWarning("没有有效的子物体可设置。");
                return;
            }

            // 开始 Undo 组
            Undo.SetCurrentGroupName("设置对象层级");
            int group = Undo.GetCurrentGroup();

            foreach (GameObject child in children)
            {
                // ✅ 使用专门的Undo.SetTransformParent方法，确保Undo操作正确
                // 这个方法会正确处理Transform的父对象变化，保持世界坐标不变
                Undo.SetTransformParent(child.transform, parent.transform, "设置父对象: " + child.name);
            }

            // 保持父物体被选中
            Selection.objects = new Object[] { parent };

            Undo.CollapseUndoOperations(group);
            
            // 操作成功提示
            string successMessage = $"层级设置完成：成功将 {children.Length} 个对象设置为 {parent.name} 的子对象\n（世界坐标已保持，可按 Ctrl+Z 撤销操作）";
            Debug.Log(successMessage);
        }
        /// 将选中对象放置到最外层级（移除父对象）
        public static void RemoveSelectedObjectsFromParent()
        {
            // 获取当前选中的所有 GameObject
            GameObject[] selected = Selection.gameObjects;

            if (selected.Length == 0)
            {
                Debug.LogWarning("请至少选择一个对象。");
                return;
            }

            // 筛选出有父对象的对象
            var objectsWithParent = selected.Where(go => go.transform.parent != null).ToArray();

            if (objectsWithParent.Length == 0)
            {
                Debug.LogWarning("选中的对象都已经在最外层级（没有父对象）。");
                return;
            }

            // 开始 Undo 组
            Undo.SetCurrentGroupName("跳出层级");
            int group = Undo.GetCurrentGroup();

            foreach (GameObject obj in objectsWithParent)
            {
                // ✅ 使用专门的Undo.SetTransformParent方法，确保Undo操作正确
                // 这个方法会正确处理Transform的父对象变化，保持世界坐标不变
                // 将父对象设置为null表示放到场景根层级
                Undo.SetTransformParent(obj.transform, null, "移除父对象: " + obj.name);
            }

            // 保持对象被选中
            Selection.objects = objectsWithParent;

            Undo.CollapseUndoOperations(group);
            
            // 操作成功提示
            string successMessage = $"跳出层级完成：成功将 {objectsWithParent.Length} 个对象放置到最外层级\n（世界坐标已保持，可按 Ctrl+Z 撤销操作）";
            Debug.Log(successMessage);
        }
        public static void PlaceObjectOnGround(bool forceRaycast = false)
        {
            GameObject[] selectedObjects = Selection.gameObjects;
            
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("请先选择要放置的对象");
                EditorUtility.DisplayDialog("提示", "请先选择要放置的对象", "确定");
                return;
            }

            Undo.SetCurrentGroupName("放置物体到表面");
            int group = Undo.GetCurrentGroup();
            
            int successCount = 0;
            int failCount = 0;
            
            foreach (GameObject obj in selectedObjects)
            {
                Undo.RecordObject(obj.transform, "放置物体到表面");
                
                if (forceRaycast)
                {
                    // Ctrl+点击：强制使用射线模式
                    if (PlaceWithRaycast(obj))
                        successCount++;
                    else
                        failCount++;
                }
                else
                {
                    Collider[] selfColliders = obj.GetComponentsInChildren<Collider>();
                    
                    if (selfColliders.Length > 0)
                    {
                        if (PlaceWithCollision(obj, selfColliders))
                            successCount++;
                        else
                            failCount++;
                    }
                    else
                    {
                        if (PlaceWithRaycast(obj))
                            successCount++;
                        else
                            failCount++;
                    }
                }
            }
            
            Undo.CollapseUndoOperations(group);
            
            string mode = forceRaycast ? "射线模式" : "碰撞模式";
            string message = $"放置完成（{mode}）：成功 {successCount} 个";
            if (failCount > 0)
                message += $"，失败 {failCount} 个";
            Debug.Log(message);
        }
        
        /// 使用碰撞检测方式落地：逐步下移对象，用 ComputePenetration 检测碰撞接触，精确贴合表面
        private static bool PlaceWithCollision(GameObject obj, Collider[] selfColliders)
        {
            // 收集场景中所有非自身的启用碰撞体
            Collider[] allColliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            List<Collider> sceneColliders = new List<Collider>();
            foreach (Collider col in allColliders)
            {
                if (col.gameObject == obj || col.transform.IsChildOf(obj.transform))
                    continue;
                if (!col.enabled || !col.gameObject.activeInHierarchy)
                    continue;
                sceneColliders.Add(col);
            }
            
            if (sceneColliders.Count == 0)
            {
                Debug.LogWarning($"对象 '{obj.name}' 场景中没有其他碰撞体");
                return false;
            }
            
            Vector3 startPos = obj.transform.position;
            float stepSize = 0.5f;       // 初始步长
            float minStep = 0.0005f;     // 最小步长精度
            float maxDropDistance = 1000f;
            float totalDropped = 0f;
            
            // 阶段1：大步下移，直到检测到碰撞
            bool foundContact = false;
            
            while (totalDropped < maxDropDistance)
            {
                obj.transform.position -= new Vector3(0, stepSize, 0);
                totalDropped += stepSize;
                
                Physics.SyncTransforms();
                
                if (CheckPenetration(selfColliders, sceneColliders))
                {
                    // 检测到碰撞，回退到上一步
                    obj.transform.position += new Vector3(0, stepSize, 0);
                    totalDropped -= stepSize;
                    foundContact = true;
                    break;
                }
            }
            
            if (!foundContact)
            {
                obj.transform.position = startPos;
                Debug.LogWarning($"对象 '{obj.name}' 下方 {maxDropDistance} 单位内没有检测到碰撞表面");
                return false;
            }
            
            // 阶段2：二分细化，逐步缩小步长精确定位
            while (stepSize > minStep)
            {
                stepSize *= 0.5f;
                
                obj.transform.position -= new Vector3(0, stepSize, 0);
                Physics.SyncTransforms();
                
                if (CheckPenetration(selfColliders, sceneColliders))
                {
                    // 穿透了，回退
                    obj.transform.position += new Vector3(0, stepSize, 0);
                }
                // 没穿透就保持当前位置，继续细化
            }
            
            Physics.SyncTransforms();
            Debug.Log($"对象 '{obj.name}' 已通过碰撞检测放置到表面");
            return true;
        }
        
        /// 检查自身碰撞体是否与场景碰撞体发生穿透
        private static bool CheckPenetration(Collider[] selfColliders, List<Collider> sceneColliders)
        {
            foreach (Collider self in selfColliders)
            {
                if (!self.enabled || !self.gameObject.activeInHierarchy)
                    continue;
                    
                foreach (Collider other in sceneColliders)
                {
                    if (Physics.ComputePenetration(
                        self, self.transform.position, self.transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out Vector3 dir, out float dist))
                    {
                        return true;
                    }
                }
            }
            return false;
        }
        
        /// 无碰撞体时的射线落地方案（回退方案）
        private static bool PlaceWithRaycast(GameObject obj)
        {
            Bounds bounds = GetObjectBounds(obj);
            if (bounds.size == Vector3.zero)
            {
                Debug.LogWarning($"对象 '{obj.name}' 没有Renderer或Collider，无法计算包围盒");
                return false;
            }
            
            // 计算网格顶点最低点
            float lowestY = float.MaxValue;
            bool hasVertex = false;
            
            MeshFilter[] meshFilters = obj.GetComponentsInChildren<MeshFilter>();
            foreach (MeshFilter mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                Vector3[] vertices = mf.sharedMesh.vertices;
                Transform meshTransform = mf.transform;
                foreach (Vector3 v in vertices)
                {
                    float worldY = meshTransform.TransformPoint(v).y;
                    if (worldY < lowestY) { lowestY = worldY; hasVertex = true; }
                }
            }
            
            SkinnedMeshRenderer[] skinRenderers = obj.GetComponentsInChildren<SkinnedMeshRenderer>();
            foreach (SkinnedMeshRenderer smr in skinRenderers)
            {
                Mesh bakedMesh = new Mesh();
                smr.BakeMesh(bakedMesh);
                Vector3[] vertices = bakedMesh.vertices;
                Transform meshTransform = smr.transform;
                foreach (Vector3 v in vertices)
                {
                    float worldY = meshTransform.TransformPoint(v).y;
                    if (worldY < lowestY) { lowestY = worldY; hasVertex = true; }
                }
                Object.DestroyImmediate(bakedMesh);
            }
            
            if (!hasVertex) lowestY = bounds.min.y;
            
            Vector3 bottomCenter = new Vector3(bounds.center.x, lowestY, bounds.center.z);
            Ray ray = new Ray(bottomCenter, Vector3.down);
            RaycastHit[] hits = Physics.RaycastAll(ray, 1000f);
            
            float closestDistance = Mathf.Infinity;
            RaycastHit closestHit = new RaycastHit();
            bool foundSurface = false;
            
            foreach (RaycastHit h in hits)
            {
                if (h.collider.gameObject == obj || h.collider.transform.IsChildOf(obj.transform))
                    continue;
                if (h.distance < closestDistance)
                {
                    closestDistance = h.distance;
                    closestHit = h;
                    foundSurface = true;
                }
            }
            
            if (foundSurface)
            {
                float bottomOffset = obj.transform.position.y - lowestY;
                Vector3 newPosition = obj.transform.position;
                newPosition.y = closestHit.point.y + bottomOffset;
                obj.transform.position = newPosition;
                Debug.Log($"对象 '{obj.name}' 已通过射线放置到表面：{closestHit.collider.gameObject.name}");
                return true;
            }
            
            Debug.LogWarning($"对象 '{obj.name}' 下方没有检测到表面");
            return false;
        }
        
        /// 获取GameObject的包围盒（包括所有子对象的Renderer和Collider）
        private static Bounds GetObjectBounds(GameObject obj)
        {
            Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
            bool hasBounds = false;
            
            // 获取所有Renderer
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                if (hasBounds)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                }
            }
            
            // 如果没有Renderer，尝试获取Collider
            if (!hasBounds)
            {
                Collider[] colliders = obj.GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders)
                {
                    if (hasBounds)
                    {
                        bounds.Encapsulate(collider.bounds);
                    }
                    else
                    {
                        bounds = collider.bounds;
                        hasBounds = true;
                    }
                }
            }
            
            return bounds;
        }
    }
}
