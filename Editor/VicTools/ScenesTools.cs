// 场景工具 v2.14 添加lighting材质(用于烘焙打灯时查看实时灯光效果)快速切换功能按钮
// 场景工具 v2.16 添加【选择材质】按钮，用于选择场景中选中对象的材质球
// 场景工具 v2.17 添加【↓】快速统一赋予最后选中对象的材质按钮；添加模型一键落地按钮
// 场景工具 v2.18 资源箱添加【按类型排序】按钮，对现有资源按类型排列，新增对象会自动按类型排列
// 场景工具 v2.19 修复丢失对象删除出现的Bug
// 场景工具 v2.20 添加晶格对象选择按钮
// 场景工具 v2.21 修复Gug：FindGameObjectByIdentifier 在查找场景对象时，如果保存的 InstanceID 与当前对象的 InstanceID 不匹配（domain reload、编辑器重启后 InstanceID 会变化），就会跳过该对象，即使名称和路径完全匹配。最终导致对象找不到，变成"未知对象"。
    // 改为"InstanceID 优先匹配，名称/路径兜底"策略。当按名称/路径找到对象但 InstanceID 不同时，仍然返回该对象作为候选。加载后自动检测标识符变化并重新保存更新的 InstanceID，形成自愈。
// 场景工具 v2.21 资源箱列表高亮显示打开的场景
// 场景工具 v2.22 修复资源箱对象异常丢失，添加 ResourceBoxBuildPreprocessor — 打包前确保资源箱数据已保存
// 场景工具 v2.23 修复资源箱对象错误复制Bug，验证场景匹配和名称匹配（防止Domain Reload后InstanceID被分配给不同对象）防止打包后产生灰色重复对象，又不会在刷新时误删已有的正常记录。
// 场景工具 v2.24 再次优化资源箱列表场景对象出错Bug
// 场景工具 v2.25 拆分 SceneTools.cs 为独立静态工具类，包含：灯光方向同步材质、PBR Lighting Shader 切换、按类型选择对象、丢失Mesh查找修复、全选层级、赋予材质、设置父子关系、模型落地（碰撞检测+射线兜底）
// 场景工具 v2.26 添加【MissMesh选项】挑选，新增【丢失mesh自动找回按钮】，优化 FindMissMeshs：原有精确匹配失败后，新增基于对象名称的模糊相似匹配（拆词加权+阈值过滤），提升丢失 Mesh 找回成功率，OL对象优先查找_SmoothNormal平滑处理过的mesh
// 场景工具 v2.27 修复【挑选-MissMat】逻辑，排除粒子Trail Material；添加重置场景对象位移旋转缩放变换工具按钮。
//               修复重置旋转按钮显示异常值（清除m_LocalEulerAnglesHint缓存）
// 场景工具 v2.28 资源箱场景对象存储机制增强：每个场景对象持久化记录 scenePath / sceneName / sceneGuid
//               标识符改为 ROOT:/SCENE:sceneGuid|sceneName:path:siblingIndex
//               加载时按 sceneGuid/sceneName 在已加载场景中再次尝试恢复对象
//               订阅 EditorSceneManager.sceneOpened / activeSceneChangedInEditMode 事件，
//               场景打开后自动重新尝试恢复 null 对象
//               【刷新→】按钮合并了按场景信息恢复 null 对象的能力
// 场景工具 v2.29 资源箱添加【场景】按钮，一键将当前打开的所有场景（多场景支持）放入资源箱
// 场景工具 v2.30 【选择名称:】按钮支持选中未激活对象。
// 场景工具 v2.31 防御：obj 可能是纹理 / Prefab / .meta / 子资产等非场景资产，直接传给 OpenScene 会抛 "Invalid version provided" 这种 YAML 解析异常，只接受 .unity 场景资产（含可选的 .unity.meta 后缀防御）。
// 场景工具 v2.32 添加【吸取位置】按钮，用于继承最后一个选中对象的位移旋转。
// 场景工具 v2.33 资源箱打开场景按钮兼容团结引擎（Tuanjie）使用的 .scene 文件，与 Unity 原生 .unity 同时识别。
// 场景工具 v2.34 添加资源箱存档目录打开按钮。
// 场景工具 v3.0 资源箱列表中 <GameObject> 类型右侧添加激活状态图标按钮（眼睛图标），点击可激活/关闭场景中对应的 GameObject（支持 Undo 撤销）。
// 场景工具 v3.0 修复资源箱【按类型排序】按钮：之前会破坏暂时无效的场景对象，使"打开包含该对象的场景后自动复原"机制失效（对象显示为"<未知类型> 未知对象"）。
//               改为排序前按旧索引备份 null 对象的 raw item / displayName / instanceID，排序后按 (obj, oldIndex) 元组直接查回，确保每个 null 对象始终对应自己的恢复信息。
// 场景工具 v3.0 修正资源箱灰色对象自动恢复逻辑：v3.37 曾要求未激活(activeSelf=false)对象跳过自动恢复、继续保持灰色，
//               但用户反馈刷新时也需获取未激活对象，不能跳过。改为：场景打开 / 刷新时不管 GameObject 是否激活都自动还原为有效，
//               眼睛图标按钮(v3.35)按对象在场景中的实际 activeSelf 状态显示：激活=绿色眼睛(点击关闭)，未激活=灰色眼睛(点击激活)。
// 场景工具 v3.0 真正修复未激活对象恢复：之前 8 处 Object.FindObjectsByType<GameObject> 全部用了单参数重载或 FindObjectsInactive.Exclude，
//               导致场景中 activeSelf=false 的 GameObject 在所有查找路径(精确ID/按名称/模糊匹配/跨场景兜底/InstanceID失效兜底)中都被静默排除，
//               所以"打开场景按钮"和"【刷新】"按钮无法解锁未激活的灰色对象。改为统一使用 FindObjectsInactive.Include 显式包含未激活对象，
//               任何 GameObject 都能在打开场景/刷新/重载时正确恢复，眼睛图标按钮按对象实际 activeSelf 状态显示。
// 场景工具 v3.0 修复【按类型排序】处理暂时未知对象：之前 null 对象用空字符串作为次级排序键，多个 null 之间会扎堆且没有按"保留的名称"参与排序，
//               破坏原有相对位置。改为：null 对象用 displayByOldIndex 中保存的 displayName 提取出的原始对象名作为排序键，
//               与有效对象的 obj.name 语义一致；配合 LINQ 稳定排序保证同名称的对象(包括暂时未知的灰色对象)保留原有排序位置。
// 场景工具 v3.0 修复【按类型排序】时灰色未知对象仍被排到末尾：v3.40 已让 null 按名称参与次级排序，但主排序键 GetTypeOrder(null)=999，
//               所有 null 都被统一归到末尾，无法与对应类型的有效对象聚在一起。改为：预先为每个 oldIndex 计算 sortTypeOrder——
//               有效对象用 GetTypeOrder(obj)；null 对象先取 _nullObjectRawItems[oldIdx].type，否则从 displayName 提取（如"<GameObject> Cube"->"GameObject"），
//               都取不到回退 999（向后兼容旧的"<未知类型>..."）。让"暂时找不到的 GameObject/Material/Mesh..."与对应类型的有效对象聚在一起按名称排序。
// 场景工具 v3.0 资源箱 GameObject 激活状态按钮支持多选批量设置：对资源箱多选场景不友好。改为：点击的 GameObject 在 _selectedObjectsInResourceBox 多选集合中且集合元素>1 时，批量把集合中所有 GameObject 的 activeSelf 设为目标状态 targetActive=!gameObj.activeSelf(与原 toggle 语义一致)；
//               批量操作统一在 Undo group 中，一次 Ctrl+Z 撤销全部。
// 场景工具 v3.1 修复资源箱"<未知类型> 未知对象"产生逻辑：
//   ① 修 ResourceBoxFileItem.Type 属性 → 字段 type（JsonUtility 不支持属性，存档丢失 type 是"未知类型"的根因之一）
//   ② 新增 BuildFallbackDisplayName 统一兜底构造器，9 处硬编码兜底全部走该函数
//   ③ SaveResourceBox null 项不再硬编码 type="System.Null"/name="NullObject"，改为用 _nullObjectRawItems/_recoveryRawItems 真实 type/name
//   ④ 加载兜底链（InstanceID 失效/EditorPrefs JSON 损坏/OnGUI 绘制）优先用 raw 缓存重建 displayName
//   ⑤ LoadResourceBoxFromFile/LoadResourceBoxFromGlobalFile 兜底用 BuildFallbackDisplayName 保留 type 信息
//   真正完全无信息时才最终回退到"<未知类型> 未知对象"，避免未知对象持久化到存档。
// 场景工具 v3.2 修复资源箱【同名同类型灰色对象】产生逻辑：
//   ① RecheckNullObjectsInResourceBox 第一层恢复与第二层 TryRecoverNullItemsBySceneInfo:457 对齐，
//      资产型 raw（ScriptableObject 等 isAsset=true）跳过全场景 GameObject 按名搜，
//      防止 UniversalRenderData 这类资产丢失变 null 后被错误恢复成同名 GameObject。
//   ② 按名匹配时统计同名候选数量，>1 时输出警告让用户感知歧义而非默默恢复到错对象。
//  资源箱列表保存路径"ResourceBox"改为"ResourceBox~"：
//   ① PathHelper.GetAssetsEditorPath() 返回路径 Editor/VicTools/ResourceBox → Editor/VicTools/ResourceBox~
//   ② OpenResourceBoxFolder() 中 RevealInFinder 调用去掉重复追加"/ResourceBox"的 bug（directoryPath 已包含完整目录），统一走 ResourceBox~。
// 【场景工具 v3.3】优化资源箱按钮布局；修复Mesh查找不到FBX中的Mesh问题。
// 【场景工具 v3.4】资源箱列表 <...> 类型标签区分场景对象 / 预制体资产 / 预制体实例 / Project 资产，根据分类优化类型排序。
// 【场景工具 v3.5】资源箱新增【UnityLogo 图标】按钮，一键把项目中 Assets 目录下的所有场景（.unity / .scene）放入资源箱；添加前先弹（扫描结果预览）确认。资源箱新增【渲染管线配置】按钮：扫描项目设置（GraphicsSettings/QualitySettings）引用的渲染管线资产、当前生效的默认/当前质量等级管线、项目内全部 RenderPipelineAsset 以及其引用的渲染器数据，同样走公共确认窗口 VicToolsListConfirmWindow 确认后再入箱。资源箱新增【添加当前场景主光】按钮：按 URP 主光规则（平行光 + 优先 Sun Source + 强度×颜色最大分量）判定当前场景主光，走公共确认窗口列出主光清单（名称/强度/颜色/阴影/所属场景），确认后把主光所在 GameObject 放入资源箱，并直接选中（Hierarchy 选中 + Ping）该主光。

using System;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
// using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using Object = UnityEngine.Object;
using Vic.Editor;
using Vic.Runtime;
using System.Runtime.InteropServices; // VicToolsIconLoader 等 Editor-only 公共工具

namespace VicTools
{
    /// 资源箱文件数据序列化类
    [Serializable]
    public class ResourceBoxFileData
    {
        public string creationTime;
        public string LastModifiedTime { get; set; }
        public List<ResourceBoxFileItem> items = new();
        
        public ResourceBoxFileData()
        {
            creationTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            LastModifiedTime = creationTime;
        }
    }
    
    /// 路径工具类，用于处理正确的Library目录路径
    public static class PathHelper
    {
        /// 获取项目根目录的Library目录路径
        /// <example>
        /// 示例：如果Application.dataPath为 ".../yd_gqsj/Assets"
        /// 则返回 ".../yd_gqsj/Library"
        /// </example>
        public static string GetLibraryPath()
        {
            // Application.dataPath 返回 Assets 目录的路径
            // 通过返回上一级目录再进入Library目录
            return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Library"));
        }
        
        /// 获取VicTools在Library目录中的完整路径
        /// <example>
        /// 示例：如果GetLibraryPath()返回 ".../yd_gqsj/Library"
        /// 则返回 ".../yd_gqsj/Library/VicTools"
        /// </example>
        public static string GetVicToolsLibraryPath()
        {
            return System.IO.Path.Combine(GetLibraryPath(), "VicTools");
        }
        
        /// 获取全局资源箱文件的完整路径
        /// <example>
        /// 示例：如果GetVicToolsLibraryPath()返回 ".../yd_gqsj/Library/VicTools"
        /// 则返回 ".../yd_gqsj/Library/VicTools/GlobalResourceBox.json"
        /// </example>
        public static string GetGlobalResourceBoxPath()
        {
            return System.IO.Path.Combine(GetVicToolsLibraryPath(), "GlobalResourceBox.json");
        }
        
        /// 获取项目Assets/Editor目录的完整路径
        /// <example>
        /// 示例：如果Application.dataPath为 ".../yd_gqsj/Assets"
        /// 则返回 ".../yd_gqsj/Assets/Editor"
        /// </example>
        public static string GetAssetsEditorPath()
        {
            // v3.3: 路径改为 ResourceBox~（~ 后缀表示"扩展/派生"目录，避免与同名资源同名混淆）
            return System.IO.Path.Combine(Application.dataPath, "Editor/VicTools/ResourceBox~");
        }
    }
    
/// 资源箱文件项目序列化类
/// v2.28 增加 scenePath / sceneName / sceneGuid 以记录场景信息
[Serializable]
public class ResourceBoxFileItem
{
    public string guid;
    public string type; // v2.41 新增：保存原始类型名；必须是字段（JsonUtility 不支持属性），否则存档丢失 type 导致"<未知类型>"
    public string name;
    public bool isAsset;
    public string displayName; // 添加displayName字段，用于保存对象的显示名称
    public string scenePath;
    public string sceneName;
    public string sceneGuid;
}

/// 资源箱恢复用快照项（不参与 JsonUtility 序列化，仅运行时缓存）
/// 用于在加载/恢复时统一 ResourceBoxItem 与 ResourceBoxFileItem 的字段访问
[Serializable]
public class ResourceBoxRecoveryItem
{
    public string guid;
    public string type; // v2.41 新增：保存原始类型名（如 GameObject / Material / Mesh），用于 null 对象按真实类型参与排序
    public string name;
    public bool isAsset;
    public string scenePath;
    public string sceneName;
    public string sceneGuid;
}

    /// 场景工具子窗口
    public class ScenesTools : SubWindow
    {
        private readonly List<Object> _resourceBox = new List<Object>(); // 资源箱列表
        private readonly Dictionary<Object, string> _resourceBoxDisplayNames = new Dictionary<Object, string>(); // 对象到displayName的映射
        private readonly Dictionary<int, string> _nullObjectDisplayNames = new Dictionary<int, string>(); // 用于存储null对象的displayName，使用索引作为键
        private readonly Dictionary<int, int> _resourceBoxInstanceIDs = new Dictionary<int, int>(); // 存储资源箱索引到对象InstanceID的映射
        private readonly Dictionary<int, string> _instanceIDToDisplayName = new Dictionary<int, string>(); // 存储InstanceID到displayName的映射
        // v2.28 缓存 null 对象的原始项快照（sceneGuid/sceneName/origGuid），用于场景重载后恢复
        private readonly Dictionary<int, ResourceBoxRecoveryItem> _nullObjectRawItems = new Dictionary<int, ResourceBoxRecoveryItem>();
        
        // 静态实例引用，用于打包前保存
        internal static ScenesTools ActiveInstance { get; private set; }
        private Vector2 _resourceBoxScrollPosition; // 资源箱滚动位置
        private string _searchText = ""; // 搜索文本
        private Material _selectedMaterial; // 用于存储用户手动选择的材质
        private Texture2D _lightDirIcon; // lightDir.png图标
        private Texture2D _SelectLattIcon; // lightDir.png图标
        private Texture2D _switchPBRMIcon; // lightDir.png图标
        private Texture2D _resetPositionIcon; // lightDir.png图标
        private Texture2D _resetRotationIcon; // lightDir.png图标
        private Texture2D _resetScaleIcon; // lightDir.png图标
        private Texture2D _levelIn; // 层级设置图标
        private Texture2D _levelOut; // 层级设置图标
        private Texture2D _selectLayer; // 层级设置图标
        private Texture2D _findMesh; // 层级设置图标
        // 跟踪需要手动销毁的 PNG 解码贴图(走 VicToolsIconLoader 创建,Unity 不会自动回收)
        // 在 OnDisable 中通过 VicToolsIconLoader.ReleaseAll(_ownedPngIcons) 统一释放。
        private readonly List<Texture2D> _ownedPngIcons = new List<Texture2D>();
        private bool _setStatic = false;
        private bool _selPrefab = false;
        private bool _selMesh = false;
        private bool _selLODGroup = false;
        private bool _selMissMat = false;
        private bool _selMissMesh = false;
        private bool _selMissScript = false;
        // 二级选项
        private bool _selAct = true;
        private bool _selMeshObj = true;
        private bool _selParticleObj = false;
        private bool _selParent = false;
        // 资源箱文件管理相关变量
        private string _resourceBoxFileName = ""; // 当前资源箱文件名
        private int _selectedFileIndex; // 选中的文件索引
        private readonly List<string> _availableFiles = new(); // 可用的资源箱文件列表
        // 注意：ResourceBoxDirectory和GlobalResourceBoxPath现在通过PathHelper类获取

        // 搜索历史记录管理器
        private readonly SearchHistoryManager _searchHistoryManager;
        
        // 选中反馈相关变量
        private readonly HashSet<Object> _selectedObjectsInResourceBox = new();

        public ScenesTools(string name, EditorWindow parent) : base("[场景工具 v3.5]", parent)
        {
            // 初始化搜索历史记录管理器
            _searchHistoryManager = new SearchHistoryManager("VicTools_ScenesTools");
        }

        // ReSharper disable Unity.PerformanceAnalysis
        public override void OnEnable()
        {
            base.OnEnable();
            // 注册静态实例
            ActiveInstance = this;
            // 注册游戏运行状态变化事件
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            // 注册选择变化事件
            Selection.selectionChanged += OnSelectionChanged;
            // v2.28 注册场景事件 - 用于场景打开时自动恢复资源箱对象
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosed += OnSceneClosed;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChangedInEditMode;

            // 加载上次搜索文本
            _searchText = _searchHistoryManager.LoadLastSearchText();
            _resourceBoxFileName = EditorPrefs.GetString("ProjectTools_RBFileName", "");
            _selectedFileIndex = EditorPrefs.GetInt("ProjectTools_RBnewSelectedFileIndex", 0);
            // 加载五个挑选选项的存档设置
            _selPrefab = EditorPrefs.GetBool("ScenesTools_selPrefab", false);
            _selMesh = EditorPrefs.GetBool("ScenesTools_selMesh", false);
            _selLODGroup = EditorPrefs.GetBool("ScenesTools_selLODGroup", false);
            _selMissMat = EditorPrefs.GetBool("ScenesTools_selMissMat", false);
            _selMissMesh = EditorPrefs.GetBool("ScenesTools_selMissMesh", false);
            _selMissScript = EditorPrefs.GetBool("ScenesTools_selMissScript", false);
            // 加载二级选项的存档设置
            _selAct = EditorPrefs.GetBool("ScenesTools_selAct", false);
            _selMeshObj = EditorPrefs.GetBool("ScenesTools_selMeshObj", true);
            _selParticleObj = EditorPrefs.GetBool("ScenesTools_selParticleObj", false);
            _selParent = EditorPrefs.GetBool("ScenesTools_selParent", false);
            _setStatic = EditorPrefs.GetBool("ScenesTools_selStatic", false);
            // 加载保存的资源箱数据
            LoadResourceBox();
            
            // 刷新可用文件列表
            RefreshAvailableFiles();
            
            // 自动刷新资源箱，尝试重新加载null对象
            RecheckNullObjectsInResourceBox();
            
            // 初始化选中对象状态
            UpdateSelectedObjectsInResourceBox();
            // 加载lightDir图标 - 使用Unity包路径（兼容开发环境和打包发布）
            // 方法1：直接使用包路径（推荐，因为package.json中的name是固定的）
            string lightDirIcon = "Packages/com.youdoo.victools/Editor/Icons~/lightDir.png";
            string selectLattIcon = "Packages/com.youdoo.victools/Editor/Icons~/icon_SelectLattIcon.png";
            string switchPBRMicon = "Packages/com.youdoo.victools/Editor/Icons~/switchPBRM.png";
            string resetPositionicon = "Packages/com.youdoo.victools/Editor/Icons~/resetPosition.png";
            string resetRotationicon = "Packages/com.youdoo.victools/Editor/Icons~/resetRotation.png";
            string resetScaleicon = "Packages/com.youdoo.victools/Editor/Icons~/resetScale.png";
            string levelIn = "Packages/com.youdoo.victools/Editor/Icons~/level-In.png";
            string levelOut = "Packages/com.youdoo.victools/Editor/Icons~/level-Out.png";
            string selectLayer = "Packages/com.youdoo.victools/Editor/Icons~/selectLayer.png";
            string findMesh = "Packages/com.youdoo.victools/Editor/Icons~/findMesh.png";
            // 方法2：备用方案，使用PackageInfo获取包路径（需要Unity 2019.3+）
            // #if UNITY_2019_3_OR_NEWER
            // var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.youdoo.victools");
            // if (packageInfo != null)
            // {
            //     lightDirIcon = System.IO.Path.Combine(packageInfo.resolvedPath, "Editor/VicTools/lightDir.png");
            //     // 注意：resolvedPath是绝对路径，需要转换为相对路径或使用其他方式加载
            // }
            // #endif
            
            // 加载 lightDir 图标(走公共 VicToolsIconLoader,得到可读 RGBA32,免受 TextureImporter 平台压缩)
            // 调用方需要把非 null 的返回 Texture2D 加入 _ownedPngIcons,在 OnDisable 一并释放
            _lightDirIcon = VicToolsIconLoader.LoadPngIcon(lightDirIcon);
            if (_lightDirIcon != null) _ownedPngIcons.Add(_lightDirIcon);
            _SelectLattIcon = VicToolsIconLoader.LoadPngIcon(selectLattIcon);
            if (_SelectLattIcon != null) _ownedPngIcons.Add(_SelectLattIcon);
            _switchPBRMIcon = VicToolsIconLoader.LoadPngIcon(switchPBRMicon);
            if (_switchPBRMIcon != null) _ownedPngIcons.Add(_switchPBRMIcon);
            _resetPositionIcon = VicToolsIconLoader.LoadPngIcon(resetPositionicon);
            if (_resetPositionIcon != null) _ownedPngIcons.Add(_resetPositionIcon);
            _resetRotationIcon = VicToolsIconLoader.LoadPngIcon(resetRotationicon);
            if (_resetRotationIcon != null) _ownedPngIcons.Add(_resetRotationIcon);
            _resetScaleIcon = VicToolsIconLoader.LoadPngIcon(resetScaleicon);
            if (_resetScaleIcon != null) _ownedPngIcons.Add(_resetScaleIcon);
            _levelIn = VicToolsIconLoader.LoadPngIcon(levelIn);
            if (_levelIn != null) _ownedPngIcons.Add(_levelIn);
            _levelOut = VicToolsIconLoader.LoadPngIcon(levelOut);
            if (_levelOut != null) _ownedPngIcons.Add(_levelOut);
            _selectLayer = VicToolsIconLoader.LoadPngIcon(selectLayer);
            if (_selectLayer != null) _ownedPngIcons.Add(_selectLayer);
            _findMesh = VicToolsIconLoader.LoadPngIcon(findMesh);
            if (_findMesh != null) _ownedPngIcons.Add(_findMesh);
            
            // 如果加载失败，尝试使用相对路径（针对某些特殊情况）
            // if (_lightDirIcon == null)
            // {
            //     Debug.LogWarning($"无法加载lightDir图标，路径: {lightDirIcon}");
            //     // 可以尝试其他备用路径或使用默认图标
            // }
        }

        public override void OnDisable()
        {
            base.OnDisable();
            // 清除静态实例
            if (ActiveInstance == this) ActiveInstance = null;
            // 释放 PNG 解码出来的 owned 贴图(VicToolsIconLoader.LoadPngIcon 创建的实例,Unity 不会自动回收)
            VicToolsIconLoader.ReleaseAll(_ownedPngIcons);
            // 取消注册游戏运行状态变化事件
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            // 取消注册选择变化事件
            Selection.selectionChanged -= OnSelectionChanged;
            // v2.28 取消注册场景事件
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened -= OnSceneOpened;
            UnityEditor.SceneManagement.EditorSceneManager.sceneClosed -= OnSceneClosed;
            UnityEditor.SceneManagement.EditorSceneManager.activeSceneChangedInEditMode -= OnActiveSceneChangedInEditMode;

            // 保存当前搜索文本
            _searchHistoryManager.SaveLastSearchText(_searchText);
            EditorPrefs.SetString("ProjectTools_RBFileName", _resourceBoxFileName);
            EditorPrefs.SetInt("ProjectTools_RBnewSelectedFileIndex", _selectedFileIndex);
            // 保存五个挑选选项的存档设置
            EditorPrefs.SetBool("ScenesTools_selPrefab", _selPrefab);
            EditorPrefs.SetBool("ScenesTools_selMesh", _selMesh);
            EditorPrefs.SetBool("ScenesTools_selLODGroup", _selLODGroup);
            EditorPrefs.SetBool("ScenesTools_selMissMat", _selMissMat);
            EditorPrefs.SetBool("ScenesTools_selMissMesh", _selMissMesh);
            EditorPrefs.SetBool("ScenesTools_selMissScript", _selMissScript);
            // 保存二级选项的存档设置
            EditorPrefs.SetBool("ScenesTools_selAct", _selAct);
            EditorPrefs.SetBool("ScenesTools_selMeshObj", _selMeshObj);
            EditorPrefs.SetBool("ScenesTools_selParticleObj", _selParticleObj);
            EditorPrefs.SetBool("ScenesTools_selParent", _selParent);
            EditorPrefs.SetBool("ScenesTools_selStatic", _setStatic);
            // 保存资源箱数据（自动保存，不显示提示框）
            SaveResourceBox(false);
        }

        /// 当游戏运行状态发生变化时调用
        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            switch (state)
            {
                case PlayModeStateChange.ExitingEditMode:
                    // 游戏开始运行前保存资源箱数据（自动保存，不显示提示框）
                    SaveResourceBox(false);
                    Debug.Log("游戏开始运行前保存资源箱数据");
                    break;
                    
                case PlayModeStateChange.EnteredEditMode:
                    // 游戏停止后重新加载资源箱数据
                    LoadResourceBox();
                    Debug.Log("游戏停止后重新加载资源箱数据");
                    break;
                    
                case PlayModeStateChange.ExitingPlayMode:
                    // 游戏停止前保存资源箱数据（自动保存，不显示提示框）
                    SaveResourceBox(false);
                    Debug.Log("游戏停止前保存资源箱数据");
                    break;
                    
                case PlayModeStateChange.EnteredPlayMode:
                    // 游戏开始运行后重新加载资源箱数据
                    LoadResourceBox();
                    Debug.Log("游戏开始运行后重新加载资源箱数据");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        /// 当选择发生变化时调用
        private void OnSelectionChanged()
        {
            UpdateSelectedObjectsInResourceBox();
        }

        // v2.28 场景事件回调：场景打开时自动尝试恢复资源箱中丢失的对象
        private void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, UnityEditor.SceneManagement.OpenSceneMode mode)
        {
            // 延迟一帧执行，确保场景完全加载
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (_resourceBox == null || _resourceBox.Count == 0) return;
                int recovered = TryRecoverNullItemsBySceneInfo();
                if (recovered > 0)
                {
                    Debug.Log($"[ResourceBox] 场景打开后自动恢复了 {recovered} 个对象");
                    SaveResourceBox(false);
                    if (Parent) Parent.Repaint();
                }
            };
        }

        // v2.28 场景事件回调：场景关闭时仅记录（不删除已加载场景中的对象）
        private void OnSceneClosed(UnityEngine.SceneManagement.Scene scene)
        {
            // 不主动删除；因为只要对象存在于已加载场景即可保留引用
        }

        // v2.28 场景事件回调：激活场景变化时尝试恢复
        private void OnActiveSceneChangedInEditMode(UnityEngine.SceneManagement.Scene previous, UnityEngine.SceneManagement.Scene current)
        {
            EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                if (_resourceBox == null || _resourceBox.Count == 0) return;
                int recovered = TryRecoverNullItemsBySceneInfo();
                if (recovered > 0)
                {
                    Debug.Log($"[ResourceBox] 激活场景变化后自动恢复了 {recovered} 个对象");
                    SaveResourceBox(false);
                    if (Parent) Parent.Repaint();
                }
            };
        }

        /// v2.28 重新尝试恢复资源箱中所有 null 对象
        /// 使用全局存储的 sceneGuid/sceneName/guid 在已加载场景中按优先级匹配：
        /// 1) 原 GameObject 标识符 (ROOT:/SCENE:)
        /// 2) sceneGuid 匹配的已加载场景中按名称
        /// 3) sceneName 匹配的已加载场景中按名称
        /// 找不到时保留 null，不删除
        private int TryRecoverNullItemsBySceneInfo()
        {
            if (_resourceBox == null || _resourceBox.Count == 0) return 0;
            int recovered = 0;
            var existing = new HashSet<int>();
            for (int i = 0; i < _resourceBox.Count; i++)
            {
                var ex = _resourceBox[i];
                if (ex) existing.Add(VicToolsUnityCompat.GetReversibleId(ex));
            }

            // 加载原始 JSON 缓存（仅首次调用读取）
            if (_recoveryRawItems == null)
            {
                _recoveryRawItems = LoadRawResourceBoxItems();
            }
            var rawItems = _recoveryRawItems;

            for (int i = 0; i < _resourceBox.Count; i++)
            {
                if (_resourceBox[i]) continue;

                ResourceBoxRecoveryItem raw = null;
                // 优先使用 _nullObjectRawItems 缓存
                if (_nullObjectRawItems != null && _nullObjectRawItems.TryGetValue(i, out var cachedRaw) && cachedRaw != null)
                {
                    raw = cachedRaw;
                }
                else if (rawItems != null && i < rawItems.Count)
                {
                    raw = rawItems[i];
                }
                if (raw == null) continue;
                if (raw.isAsset) continue;
                if (string.IsNullOrEmpty(raw.name)) continue;

                Object found = null;

                // 1) 先用原 GUID 标识符精确匹配
                if (!string.IsNullOrEmpty(raw.guid) &&
                    (raw.guid.StartsWith("ROOT:") || raw.guid.StartsWith("SCENE:")))
                {
                    found = FindGameObjectByIdentifier(raw.guid);
                }

                // 2) 退到场景信息 + 名称匹配
                if (found == null)
                {
                    TryFindGameObjectBySceneInfo(false, raw.sceneGuid, raw.sceneName, raw.name, out found);
                }

                if (found == null) continue;
                if (existing.Contains(VicToolsUnityCompat.GetReversibleId(found))) continue;
                if (_resourceBox.Contains(found)) continue;

                _resourceBox[i] = found;
                existing.Add(VicToolsUnityCompat.GetReversibleId(found));

                // 恢复 displayName
                if (_nullObjectDisplayNames.TryGetValue(i, out var dn))
                {
                    _resourceBoxDisplayNames[found] = dn;
                    _nullObjectDisplayNames.Remove(i);
                }
                // v2.28: 清除 _nullObjectRawItems 中对应项
                _nullObjectRawItems.Remove(i);
                recovered++;
            }

            return recovered;
        }

        // v2.28 缓存：用于恢复时的原始项快照（统一 ResourceBoxItem / ResourceBoxFileItem 字段访问）
        private List<ResourceBoxRecoveryItem> _recoveryRawItems;

        // v2.28 从全局文件加载原始项快照
        private List<ResourceBoxRecoveryItem> LoadRawResourceBoxItems()
        {
            var path = PathHelper.GetGlobalResourceBoxPath();
            if (!System.IO.File.Exists(path)) return null;
            try
            {
                var json = System.IO.File.ReadAllText(path);
                if (string.IsNullOrEmpty(json)) return null;
                var data = JsonUtility.FromJson<ResourceBoxData>(json);
                if (data?.items == null) return null;
                var list = new List<ResourceBoxRecoveryItem>(data.items.Count);
                foreach (var it in data.items) list.Add(ConvertToRecoveryItem(it));
                return list;
            }
            catch
            {
                return null;
            }
        }

        // v2.28 把 ResourceBoxItem 转为快照项
        private static ResourceBoxRecoveryItem ConvertToRecoveryItem(ResourceBoxItem it)
        {
            if (it == null) return null;
            return new ResourceBoxRecoveryItem
            {
                guid = it.guid,
                type = it.type, // v2.41: 保留类型名，让 null 对象按真实类型参与排序
                name = it.name,
                isAsset = it.isAsset,
                scenePath = it.scenePath,
                sceneName = it.sceneName,
                sceneGuid = it.sceneGuid
            };
        }

        // v2.28 把 ResourceBoxFileItem 转为快照项
        private static ResourceBoxRecoveryItem ConvertToRecoveryItem(ResourceBoxFileItem it)
        {
            if (it == null) return null;
            return new ResourceBoxRecoveryItem
            {
                guid = it.guid,
                type = it.type, // v2.41: 保留类型名，让 null 对象按真实类型参与排序；ResourceBoxFileItem.type 与 ResourceBoxRecoveryItem.type 都已是字段
                name = it.name,
                isAsset = it.isAsset,
                scenePath = it.scenePath,
                sceneName = it.sceneName,
                sceneGuid = it.sceneGuid
            };
        }

        // v2.28 把 ResourceBoxItem 列表转为快照列表
        private static List<ResourceBoxRecoveryItem> ConvertToRecoveryList(List<ResourceBoxItem> src)
        {
            if (src == null) return null;
            var list = new List<ResourceBoxRecoveryItem>(src.Count);
            foreach (var it in src) list.Add(ConvertToRecoveryItem(it));
            return list;
        }

        // v2.28 把 ResourceBoxFileItem 列表转为快照列表
        private static List<ResourceBoxRecoveryItem> ConvertToRecoveryList(List<ResourceBoxFileItem> src)
        {
            if (src == null) return null;
            var list = new List<ResourceBoxRecoveryItem>(src.Count);
            foreach (var it in src) list.Add(ConvertToRecoveryItem(it));
            return list;
        }

        // v2.43 新增：统一兜底 displayName 构造函数
        // 旧实现所有 9 个兜底分支都直接硬编码"<未知类型> 未知对象"，导致：
        //   ① 丢失资源箱对象原始类型/名称信息（即使 item.type/name 已知也会被覆盖）
        //   ② SaveResourceBox 兜底时把"<未知类型> 未知对象"固化进 JSON，下次加载读不到任何 type/name，形成"未知对象"持久化
        // 新实现：尽量用 type/name/parentName 重建与正常路径一致的 displayName，只有真正完全无信息时才回退到"<未知类型> 未知对象"
        private static string BuildFallbackDisplayName(string typeName, string objName, string parentName = null)
        {
            bool hasType = !string.IsNullOrEmpty(typeName);
            bool hasName = !string.IsNullOrEmpty(objName);

            // 与正常路径 1086-1087 行的格式保持一致："<{type}> [{parent}]← {name}" 或 "<{type}> {name}"
            if (hasType && hasName)
            {
                return !string.IsNullOrEmpty(parentName)
                    ? $"<{typeName}> [{parentName}]← {objName}"
                    : $"<{typeName}> {objName}";
            }

            // 只有类型没有名字：仍保留类型信息（用于排序/恢复），名称位填占位
            if (hasType)
            {
                return $"<{typeName}> 未知对象";
            }

            // 只有名字没有类型：保留名字用于恢复（避免下次加载彻底丢失线索）
            if (hasName)
            {
                return $"<未知类型> {objName}";
            }

            // 真正完全无任何信息时：硬编码最终兜底
            return "<未知类型> 未知对象";
        }

        /// 更新资源箱中选中对象的状态
        private void UpdateSelectedObjectsInResourceBox()
        {
            _selectedObjectsInResourceBox.Clear();
            
            // 获取当前选中的所有对象
            var selectedObjects = Selection.objects;
            if (selectedObjects == null || selectedObjects.Length == 0)
                return;

            // 检查每个选中的对象是否在资源箱中
            foreach (var selectedObj in selectedObjects)
            {
                if (selectedObj != null && _resourceBox.Contains(selectedObj))
                {
                    _selectedObjectsInResourceBox.Add(selectedObj);
                }
            }

            // 强制重绘窗口以更新显示
            if (Parent)
                Parent.Repaint();
        }
        // ReSharper disable Unity.PerformanceAnalysis
        public override void OnGUI()
        {
            var style = EditorStyle.Get;

            // 处理回车键事件 - 必须在GUI元素创建之前处理
            _searchHistoryManager.HandleEnterKeyEvent(_searchText, "ScenesTools_SearchTextField", (searchText) =>
            {
                // 回车键按下时的回调函数
                Debug.Log($"回车执行搜索: {searchText}");
                // 这里可以添加实际的搜索逻辑
            });
            // [1]VerticalLayoutStart();
            EditorGUILayout.BeginVertical(style.area);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("★ 速选工具", style.subheading, GUILayout.Width(110));
            GUI.contentColor = Color.cyan;
            
            // 获取选中对象数量 - 支持独立窗口和主窗口
            var currentSelectedCount = 0;
            
            // 检查是否是独立窗口
            WinScenesTools standaloneWindow = Parent as WinScenesTools;
            if (standaloneWindow != null)
            {
                // 从独立窗口获取选中数量
                currentSelectedCount = standaloneWindow.StandaloneSelectedCount;
            }
            else
            {
                // 从主窗口获取全局选中数量
                VicToolsWindow mainWindow = Parent as VicToolsWindow;
                if (mainWindow != null)
                {
                    currentSelectedCount = mainWindow.globalSelectedObjectsCount;
                }
            }
            
            EditorGUILayout.LabelField($"   选中：( {currentSelectedCount} )", style.subheading);
            GUI.contentColor = Color.white;
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            // 搜索功能区域
            EditorGUILayout.BeginVertical(style.subheading);
            EditorGUILayout.Space();

            // 搜索文本输入框 - 改进版本：回车键保存历史记录
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(false));
            // GUIStyle searchTextFieldStyle = new GUIStyle(style.normalfont);
            // searchTextFieldStyle.fixedHeight = 20;
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button("选择相似名称对象", style.normalButton, GUILayout.Width(140)))
            {
                SelectSimilarObjects();
            }
            if (GUILayout.Button("选择名称：", style.normalButton, GUILayout.Width(80)))
            {
                SelectObjectsByName(_searchText);
            }
            GUI.backgroundColor = Color.white;
            _searchText = CreateTextFieldWithStyle("", _searchText,
            _ =>
            {
                // 文本变化时不立即保存到历史记录，等待回车或搜索按钮
                // 这里只更新文本，不保存历史
            }, 1, 0, TextAnchor.MiddleLeft, style.normalfont, null, "ScenesTools_SearchTextField");

            // 搜索按钮
            // GUI.backgroundColor = Color.green;
            // if (GUILayout.Button("搜索", GUILayout.Width(60)))
            // {
            //     if (!string.IsNullOrEmpty(searchText))
            //     {
            //         searchHistoryManager.AddToSearchHistory(searchText);
            //         // 这里可以添加实际的搜索逻辑
            //         Debug.Log($"执行搜索: {searchText}");
            //     }
            // }
            // GUI.backgroundColor = Color.white;

            // 使用搜索历史管理器绘制历史记录选择器
            _searchHistoryManager.DrawSearchHistorySelector(ref _searchText, SelectObjectsByName);
            EditorGUILayout.EndHorizontal();

            // 清除历史记录按钮 - 右对齐
            // searchHistoryManager.DrawClearHistoryButton("删除历史", style.normalfont_Hui, true);

            // 资源箱功能
            DrawResourceBoxSection();
            EditorGUILayout.EndVertical();

            EditorGUILayout.EndVertical();
            // [1]VerticalLayoutEnd();

            // 显示选择的对象数量
            // if (selectedObjectsCount > 0)
            // {
            //     EditorGUILayout.LabelField($"选中对象数量: 【 {selectedObjectsCount} 】", EditorStyles.miniLabel);
            // }
        }

        /// 选择场景中名称包含指定文本的所有对象
        private void SelectObjectsByName(string searchText)
        {
            if (string.IsNullOrEmpty(searchText))
            {
                EditorUtility.DisplayDialog("提示", "请输入要搜索的名称", "确定");
                return;
            }

            // 在场景中查找所有名称包含搜索文本的对象
            var matchingObjects = FindObjectsByName(searchText);

            if (matchingObjects.Count == 0)
            {
                // EditorUtility.DisplayDialog("提示", $"未找到名称包含 \"{searchText}\" 的对象", "确定");
                return;
            }

            // 选择这些对象
            Selection.objects = matchingObjects.ToArray();
            
            // 更新选中数量 - 支持独立窗口和主窗口
            var standaloneWindow = Parent as WinScenesTools;
            if (standaloneWindow)
            {
                // 更新独立窗口的选中数量
                standaloneWindow.StandaloneSelectedCount = matchingObjects.Count;
                standaloneWindow.Repaint();
            }
            else
            {
                // 更新主窗口的全局选中数量
                var mainWindow = Parent as VicToolsWindow;
                if (mainWindow)
                {
                    mainWindow.globalSelectedObjectsCount = matchingObjects.Count;
                    mainWindow.Repaint();
                }
            }

            // 添加到搜索历史
            _searchHistoryManager.AddToSearchHistory(searchText);

            // Debug.Log($"已选择 {matchingObjects.Count} 个名称包含 \"{searchText}\" 的对象");
        }

        /// 选择场景中与当前选中对象名称相似的所有物体
        private void SelectSimilarObjects()
        {
            // 获取当前选中的对象
            var selectedObjects = Selection.gameObjects;

            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先选择一个对象", "确定");
                return;
            }

            // 以选择的第一个对象名作为基础
            var selectedObject = selectedObjects[0];
            var selectedName = selectedObject.name;

            // 提取名称模式（去掉数字后缀等）
            var namePattern = ExtractNamePattern(selectedName);

            if (string.IsNullOrEmpty(namePattern))
            {
                EditorUtility.DisplayDialog("提示", "无法识别名称模式", "确定");
                return;
            }

            // 在场景中查找所有相似名称的对象
            var similarObjects = FindSimilarObjects(namePattern);

            if (similarObjects.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到名称相似的对象", "确定");
                return;
            }

            // 选择这些对象
            Selection.objects = similarObjects.ToArray();
            
            // 更新选中数量 - 支持独立窗口和主窗口
            var standaloneWindow = Parent as WinScenesTools;
            if (standaloneWindow)
            {
                // 更新独立窗口的选中数量
                standaloneWindow.StandaloneSelectedCount = similarObjects.Count;
                standaloneWindow.Repaint();
            }
            else
            {
                // 更新主窗口的全局选中数量
                var mainWindow = Parent as VicToolsWindow;
                if (!mainWindow) return;
                mainWindow.globalSelectedObjectsCount = similarObjects.Count;
                mainWindow.Repaint();
            }
            // EditorUtility.DisplayDialog("完成", $"已选择 {similarObjects.Count} 个相似对象", "确定");
        }

        /// 从对象名称中提取模式（去掉数字后缀等）
        private static string ExtractNamePattern(string objectName)
        {
            // 常见的命名模式匹配
            // 1. 带数字后缀：Object_01, Object_02 等
            // 2. 带括号数字：Object (1), Object (2) 等
            // 3. 纯数字后缀：Object1, Object2 等

            // 移除常见的数字后缀模式
            var pattern = objectName;

            // 匹配下划线加数字：_01, _02, _1, _2 等
            pattern = Regex.Replace(pattern, @"_\d+$", "");

            // 匹配括号数字：(1), (2), (01), (02) 等
            pattern = Regex.Replace(pattern, @"\s*\(\d+\)$", "");

            // 匹配纯数字后缀：1, 2, 01, 02 等
            pattern = Regex.Replace(pattern, @"\d+$", "");

            // 如果处理后名称太短，返回原始名称
            if (pattern.Length < 2)
            {
                return objectName;
            }

            return pattern.Trim();
        }

        /// 在场景中查找所有名称包含指定文本的对象
        private static List<GameObject> FindObjectsByName(string searchText)
        {
            // 获取场景中的所有游戏对象（包含未激活对象）
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            return allObjects.Where(obj => obj.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }

        /// 在场景中查找所有名称相似的对象
        private static List<GameObject> FindSimilarObjects(string namePattern)
        {
            // 获取场景中的所有游戏对象（包含未激活对象）
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            return allObjects.Where(obj => obj.name.StartsWith(namePattern)).ToList();
        }

        // ReSharper disable Unity.PerformanceAnalysis
        /// 绘制资源箱界面
        private void DrawResourceBoxSection()
        {
            var style = EditorStyle.Get;

            // 添加对象到资源箱按钮
            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUI.backgroundColor = Color.cyan;
            _selectedMaterial = EditorGUILayout.ObjectField("", _selectedMaterial, typeof(Material), false, GUILayout.MinWidth(115)) as Material;

            // 放入按钮 - 将selectedMaterial材质放入资源箱
            GUI.backgroundColor = Color.magenta;
            if (_selectedMaterial != null)
            {
                var putButtonContent = new GUIContent("↓", "将左边获取的材质放入资源箱");
                if (GUILayout.Button(putButtonContent, style.normalButton, GUILayout.Width(25)))
                {
                    PutSelectedMaterialToResourceBox();
                }
            }
            var putButton2Content = new GUIContent("●↓", "将选中对象的材质放入资源箱");
            if (GUILayout.Button(putButton2Content, style.normalButton, GUILayout.Width(40)))
            {
                OnlySelectedMaterialToResourceBox();
            }
            GUI.backgroundColor = Color.cyan;
            
            var selectButtonContent = new GUIContent("选择相同材质对象", "选择使用选中对象相同材质的所有对象");
            if (GUILayout.Button(selectButtonContent, style.normalButton, GUILayout.Width(130)))
            {
                SelectObjectsUsingSelectedObjectMaterial();
            }
            
            var selectObjMatContent = new GUIContent("选择材质", "选择场景中选中对象的材质球\n（注意：多个材质球选择时要3击鼠标进行选择）");
            if (GUILayout.Button(selectObjMatContent, style.normalButton, GUILayout.Width(80)))
            {
                SelectObjectsMaterial();
            }
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button(new GUIContent("←", "吸取材质\n继承最后选择对象的材质球"), style.normalButton, GUILayout.Width(25)))
            {
                GetLastMat();
            }
            // GUI.backgroundColor = Color.black;
            // 在 GUILayout.Button 的 MouseDown 事件回调里，Event.current.modifiers 准确反映当前按下的修饰键。
            // 单击 → 只继承旋转；按住 Ctrl 单击 → 只继承位移（不再同步旋转）。
            bool ctrlDown = (Event.current.modifiers & EventModifiers.Control) != 0;
            string posRotTooltip = ctrlDown
                ? "吸取位置（Ctrl 已按下）\n本次只继承最后选择对象的位移"
                : "吸取位置\n单击：只继承最后选择对象的旋转\n按住 Ctrl 单击：只继承位移";
            // 图标与功能一致：旋转功能用自定义 _resetRotationIcon（resetRotation.png，1674 行同款）；
// 按住 Ctrl（位移功能）保留 d_Transform Icon；自定义图标加载失败时回退到 d_RotateTool
            Texture posRotIcon;
            if (ctrlDown)
            {
                posRotIcon = EditorGUIUtility.IconContent("d_Transform Icon").image;
            }
            else
            {
                posRotIcon = _resetRotationIcon != null ? (Texture)_resetRotationIcon : EditorGUIUtility.IconContent("d_RotateTool").image;
            }
            if (GUILayout.Button(new GUIContent(posRotIcon, posRotTooltip), new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(25), GUILayout.Height(25)))
            {
                if (ctrlDown) GetLastPos();
                else GetLastRot();
            }

            EditorGUILayout.EndHorizontal();

            // 拖拽区域
            EditorGUILayout.BeginVertical(style.subheading);

            EditorGUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            // 创建拖拽区域
            GUI.backgroundColor = Color.white;
            var dropArea = GUILayoutUtility.GetRect(210, 50, GUILayout.ExpandWidth(true));
            // ★使用 Unity 系统图标"StateMachineEditor.Background"作为资源箱拖拽区域的背景
            //   关键：该图标是 9-slice 圆角纹理（圆角+中心可拉伸），
            //   必须拿到带 border 的完整 GUIStyle，否则 normal.background 会被拉成扁椭圆。
            //   但它既不在 EditorStyles 公开字段、也不在 GUI.skin.FindStyle 的查询列表里。
            //   改用反射翻 EditorStyles 静态字段 + GUISkin.m_Styles 内部数组，
            //   命中后克隆样式（保留 9-slice border，不被压缩）。
            //   全部拿不到时退到 IconContent + 手动 border(4)，最后兜底 helpBox。
            var dropBg = TryFindBuiltinStyle("StateMachineEditor.Background");
            if (dropBg == null) {
                var dropIcon = EditorGUIUtility.IconContent("StateMachineEditor.Background");
                dropBg = new GUIStyle();
                if (dropIcon != null && dropIcon.image is Texture2D dropTex) {
                    dropBg.normal.background = dropTex;
                    dropBg.border = new RectOffset(4, 4, 4, 4);
                } else {
                    dropBg = new GUIStyle(EditorStyles.helpBox);
                }
            }
            dropBg.alignment = TextAnchor.MiddleCenter;
            dropBg.fontSize = 12;
            // ★深色状态机背景上的文字需要强制白色，否则默认 textColor 在深底上几乎看不见
            dropBg.normal.textColor = new Color(0.7f, 0.4f, 0.7f);
            // 绘制带状态机背景的拖拽区域
            GUI.Box(dropArea, "拖拽区域：从 Project 或场景 Hierarchy 中拖入资源箱 ↓", dropBg);

            GUI.backgroundColor = Color.magenta;
            // 处理拖拽事件
            var evt = Event.current;
            switch (evt.type)
            {
                case EventType.DragUpdated:
                    if (dropArea.Contains(evt.mousePosition))
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        evt.Use(); // 消耗事件，防止进一步处理
                    }
                    break;
                case EventType.DragPerform:
                    if (dropArea.Contains(evt.mousePosition))
                    {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                        DragAndDrop.AcceptDrag();
                        HandleDragAndDropObjects(DragAndDrop.objectReferences);
                        evt.Use(); // 消耗事件，防止进一步处理
                    }
                    break;
                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.MouseMove:
                case EventType.MouseDrag:
                case EventType.KeyDown:
                case EventType.KeyUp:
                case EventType.ScrollWheel:
                case EventType.Repaint:
                case EventType.Layout:
                case EventType.DragExited:
                case EventType.Ignore:
                case EventType.Used:
                case EventType.ValidateCommand:
                case EventType.ExecuteCommand:
                case EventType.ContextClick:
                case EventType.MouseEnterWindow:
                case EventType.MouseLeaveWindow:
                case EventType.TouchDown:
                case EventType.TouchUp:
                case EventType.TouchMove:
                case EventType.TouchEnter:
                case EventType.TouchLeave:
                case EventType.TouchStationary:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            var cusButtonStyle = new GUIStyle(style.normalButton)
            {
                fixedHeight = 46
            };
            if (GUILayout.Button("放入资源箱 ↓", cusButtonStyle, GUILayout.Width(100)))
            {
                AddSelectedToResourceBox();
            }
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"资源箱: ( {_resourceBox.Count} )", style.subheading, GUILayout.Height(24));
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Lighting").image, "添加当前场景主光"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(20), GUILayout.Height(22)))
            {
                AddCurrentSceneMainLightToResourceBox();
            }
            // 添加项目中渲染管线配置文件按钮-渲染管线配置文件图标
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_ScriptableObject Icon").image, "添加项目中渲染管线配置文件"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(20), GUILayout.Height(22)))
            {
                AddAllProjectRenderPipelineAssetsToResourceBox();
            }
            // 清除资源箱按钮（带确认）
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("UnityLogo").image, "添加项目中所有场景"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(20), GUILayout.Height(22)))
            {
                AddAllProjectScenesToResourceBox();
            }
            GUILayout.Space(10);
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_CacheServerDisconnected").image, "清空资源箱中所有内容！"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(20), GUILayout.Height(22)))
            {
                ClearResourceBoxWithConfirmation();
            }
            // 按类型排序按钮
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("按类型排序"))
            {
                SortResourceBoxByType();
            }
            // 一键将当前打开的所有场景放入资源箱
            GUI.backgroundColor = Color.magenta;
            if (GUILayout.Button(new GUIContent("场景↓", "将当前打开的所有场景(支持多场景)放入资源箱")))
            {
                AddOpenScenesToResourceBox();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 资源箱文件管理区域
            EditorGUILayout.BeginVertical(style.subheading);
            // EditorGUILayout.LabelField("资源箱存档管理", style.subheading2);
            
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.yellow;
            //EditorGUIUtility.IconContent("Favorite").image
            // GUILayout.Button(GUIContent content, GUIStyle style, params GUILayoutOption[] options)
            //   三个参数必须平级：图标+tooltip 的 GUIContent、居中无边框样式、固定宽度
            //   （原先多套了一层 new GUIContent(...)，把 GUIStyle 误塞进 GUIContent 构造器 → CS1503）
            if (GUILayout.Button(
                new GUIContent(
                    EditorGUIUtility.IconContent("CacheServerConnected").image,
                    "收藏当前资源箱列表（资源名留空时使用当前场景名）"),
                new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter },
                GUILayout.Width(28), GUILayout.Height(22)))
            {
                SaveResourceBoxToFile();
            }
            // 文件名输入和操作按钮
            // EditorGUILayout.LabelField("资源名:", GUILayout.Width(42));
            _resourceBoxFileName = EditorGUILayout.TextField(_resourceBoxFileName);
            
            // GUI.backgroundColor = Color.white;
            // 文件选择下拉列表
            // EditorGUILayout.LabelField("选择资源:", GUILayout.Width(55));
            int newSelectedFileIndex = EditorGUILayout.Popup(_selectedFileIndex, _availableFiles.ToArray());
            if (newSelectedFileIndex != _selectedFileIndex)
            {
                _selectedFileIndex = newSelectedFileIndex;
                if (_selectedFileIndex >= 0 && _selectedFileIndex < _availableFiles.Count)
                {
                    string selectedFileName = _availableFiles[_selectedFileIndex];
                    if (selectedFileName != "新建资源箱")
                    {
                        _resourceBoxFileName = selectedFileName;
                        // LoadResourceBoxFromFile();
                    }
                }
            }
            // 打开资源箱存档目录文件夹按钮（位于"载入↓"左侧）
            // GUI.backgroundColor = new Color(1f, 0.55f, 0.4f);
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_FolderOpened Icon").image, "打开资源箱列表存档目录文件夹"), new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(22), GUILayout.Height(22)))
            {
                OpenResourceBoxFolder();
            }
            // 刷新文件列表按钮
            if (GUILayout.Button("刷新↻", GUILayout.Width(50)))
            {
                RefreshAvailableFiles();
                LoadResourceBox();
                // 重新检查null对象，尝试在当前场景中重新加载它们
                RecheckNullObjectsInResourceBox();
                // v2.28: 同步按场景信息恢复 null 场景对象
                int recovered = TryRecoverNullItemsBySceneInfo();
                if (recovered > 0)
                {
                    Debug.Log($"[ResourceBox] 刷新时按场景信息恢复了 {recovered} 个对象");
                    SaveResourceBox(false);
                }
            }
            // 保存和加载按钮
            // GUI.backgroundColor = Color.magenta;
            if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("Favorite").image, "载入收藏的资源箱列表存档"), new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(22), GUILayout.Height(22)))
            {
                LoadResourceBoxFromFile();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            // 显示资源箱内容
            if (_resourceBox.Count > 0)
            {
                // 在绘制之前，检测并保存刚变为null的对象的displayName
                for (int i = 0; i < _resourceBox.Count; i++)
                {
                    Object obj = _resourceBox[i];
                    bool isObjectNull = !obj;
                    
                    if (!isObjectNull)
                    {
                        // 对象有效，保存或更新它的InstanceID和displayName映射
                        int instanceID = VicToolsUnityCompat.GetReversibleId(obj);
                        _resourceBoxInstanceIDs[i] = instanceID;
                        
                        // 计算并保存displayName
                        var parentName = GetTopLevelParentName(obj);
                        var displayName = !string.IsNullOrEmpty(parentName)
                            ? $"<{GetTypeDisplayName(obj)}> [{parentName}]← {obj.name}"
                            : $"<{GetTypeDisplayName(obj)}> {obj.name}";
                        _instanceIDToDisplayName[instanceID] = displayName;
                    }
                    else if (!_nullObjectDisplayNames.ContainsKey(i))
                    {
                        // 对象为null，但_nullObjectDisplayNames中还没有保存它的displayName
                        // 尝试通过InstanceID找到它之前的displayName
                        if (_resourceBoxInstanceIDs.TryGetValue(i, out int instanceID))
                        {
                            if (_instanceIDToDisplayName.TryGetValue(instanceID, out string savedDisplayName))
                            {
                                _nullObjectDisplayNames[i] = savedDisplayName;
                            }
                            else
                            {
                                // v2.43: InstanceID 已查不到 displayName（通常跨 Domain Reload 后 _instanceIDToDisplayName 被清空）。
                                // 优先用 _nullObjectRawItems / _recoveryRawItems 中保存的 type/name 重建，避免直接"<未知类型> 未知对象"。
                                string fallbackType = null;
                                string fallbackName = null;
                                if (_nullObjectRawItems.TryGetValue(i, out var rawItem) && rawItem != null)
                                {
                                    fallbackType = rawItem.type;
                                    fallbackName = rawItem.name;
                                }
                                else if (_recoveryRawItems != null && i < _recoveryRawItems.Count && _recoveryRawItems[i] != null)
                                {
                                    fallbackType = _recoveryRawItems[i].type;
                                    fallbackName = _recoveryRawItems[i].name;
                                }
                                _nullObjectDisplayNames[i] = BuildFallbackDisplayName(fallbackType, fallbackName);
                            }
                        }
                        else
                        {
                            // 如果没有保存InstanceID，尝试从保存的数据中加载
                            var jsonData = EditorPrefs.GetString("VicTools_ScenesTools_ResourceBox", "");
                            if (!string.IsNullOrEmpty(jsonData))
                            {
                                try
                                {
                                    var resourceBoxData = JsonUtility.FromJson<ResourceBoxData>(jsonData);
                                    if (resourceBoxData != null && resourceBoxData.items != null && i < resourceBoxData.items.Count)
                                    {
                                        var item = resourceBoxData.items[i];
                                        if (!string.IsNullOrEmpty(item.displayName))
                                        {
                                            _nullObjectDisplayNames[i] = item.displayName;
                                        }
                                        else
                                        {
                                            // v2.43: 优先用 item.type/item.name 重建（ResourceBoxItem.type 已 v2.41 改为字段，可被 JsonUtility 序列化）
                                            _nullObjectDisplayNames[i] = BuildFallbackDisplayName(item.type, item.name);
                                        }
                                    }
                                    else
                                    {
                                        // items[i] 不存在：仍走统一兜底（无任何信息时返回"<未知类型> 未知对象"）
                                        _nullObjectDisplayNames[i] = BuildFallbackDisplayName(null, null);
                                    }
                                }
                                catch
                                {
                                    // JSON 损坏：仍走统一兜底
                                    _nullObjectDisplayNames[i] = BuildFallbackDisplayName(null, null);
                                }
                            }
                            else
                            {
                                // EditorPrefs 空：仍走统一兜底
                                _nullObjectDisplayNames[i] = BuildFallbackDisplayName(null, null);
                            }
                        }
                        
                        // 不在GUI绘制循环中保存，避免修改列表导致显示错乱
                        // displayName会在下次正常保存时（OnDisable/PlayMode切换等）被持久化
                    }
                }
                
                // 添加使用提示
                EditorGUILayout.LabelField("提示：按住 Ctrl 键点击\"选择\"按钮可添加选择，按住 Shift 键选择整个层级", style.normalfont_Hui_Cen);
                // 滚动视图显示资源箱内容 - 自适应高度，使用剩余空间
                _resourceBoxScrollPosition = EditorGUILayout.BeginScrollView(_resourceBoxScrollPosition, GUILayout.ExpandHeight(true));

                EditorGUILayout.BeginVertical(EditorStyles.textArea, GUILayout.ExpandWidth(true));

                // 获取当前打开场景的路径，用于高亮匹配的 SceneAsset
                string currentScenePath = SceneManager.GetActiveScene().path;

                for (int i = 0; i < _resourceBox.Count; i++)
                {
                    EditorGUILayout.BeginHorizontal();

                    // 显示对象名称和类型 - 靠右对齐
                    Object obj = _resourceBox[i];
                    
                    // 检查对象是否在选中列表中
                    bool isSelected = _selectedObjectsInResourceBox.Contains(obj);
                    
                    string displayName;
                    bool isObjectNull = !obj;
                    
                    if (isObjectNull)
                    {
                        // 如果对象不存在，从null对象字典中获取保存的displayName
                        if (_nullObjectDisplayNames.TryGetValue(i, out string savedDisplayName))
                        {
                            displayName = savedDisplayName;
                        }
                        else
                        {
                            // 这种情况理论上不应该发生，因为我们在绘制前已经处理了。
                            // v2.43: 仍然尝试用 _nullObjectRawItems / _recoveryRawItems 兜底，绝不再硬编码"<未知类型> 未知对象"
                            string fbType = null, fbName = null;
                            if (_nullObjectRawItems.TryGetValue(i, out var rawItem) && rawItem != null)
                            {
                                fbType = rawItem.type; fbName = rawItem.name;
                            }
                            else if (_recoveryRawItems != null && i < _recoveryRawItems.Count && _recoveryRawItems[i] != null)
                            {
                                fbType = _recoveryRawItems[i].type; fbName = _recoveryRawItems[i].name;
                            }
                            displayName = BuildFallbackDisplayName(fbType, fbName);
                        }
                    }
                    else
                    {
                        var parentName = GetTopLevelParentName(obj);
                        displayName =
                            // 有父对象：显示 [父对象名] 对象名 (类型·来源)
                            !string.IsNullOrEmpty(parentName) ? $"<{GetTypeDisplayName(obj)}> [{parentName}]← {obj.name}" :
                            // 没有父对象：显示 对象名 (类型·来源)
                            $"<{GetTypeDisplayName(obj)}> {obj.name}";
                    }

                    // 使用自定义GUIStyle实现真正的右对齐，并设置最小宽度
                    var rightAlignedLabel2 = new GUIStyle(EditorStyles.label);
                    rightAlignedLabel2.alignment = TextAnchor.MiddleRight;
                    rightAlignedLabel2.clipping = TextClipping.Clip;
                    
                    // 如果对象被选中，改变文本颜色为黄色高亮
                    if (isSelected)
                    {
                        rightAlignedLabel2.normal.textColor = Color.yellow;
                        rightAlignedLabel2.hover.textColor = new Color(0.99f, 0.8f, 0.2f);
                    }
                    // 如果是当前打开的场景，蓝色高亮
                    else if (!isObjectNull && obj is SceneAsset &&
                             AssetDatabase.GetAssetPath(obj) == currentScenePath)
                    {
                        rightAlignedLabel2.normal.textColor = new Color(0.6f, 0.4f, 1f);
                        rightAlignedLabel2.hover.textColor = new Color(0.8f, 0.6f, 1f);
                    }
                    // 如果对象不存在，设置为灰色
                    else if (isObjectNull)
                    {
                        rightAlignedLabel2.normal.textColor = Color.gray;
                        rightAlignedLabel2.hover.textColor = Color.gray;
                    }
                    
                    // 计算可用宽度（总宽度减去按钮宽度），并设置最小宽度限制
                    const float buttonAreaWidth2 = 120; // 按钮区域固定宽度
                    const float minLabelWidth2 = 100; // 标签最小宽度
                    var availableWidth2 = Mathf.Max(EditorGUIUtility.currentViewWidth - buttonAreaWidth2, minLabelWidth2);
                    
                    // 使用GUILayout.Width限制标签最大宽度，确保按钮区域始终可见
                    var labelRect2 = GUILayoutUtility.GetRect(
                        new GUIContent(displayName), 
                        rightAlignedLabel2, 
                        GUILayout.ExpandWidth(true),
                        GUILayout.MinWidth(minLabelWidth2),
                        GUILayout.MaxWidth(availableWidth2)
                    );
                    
                    // 绘制右对齐的标签，使用文本裁剪
                    GUI.Label(labelRect2, displayName, rightAlignedLabel2);

                    // 如果是Material类型，添加赋予按钮
                    if (!isObjectNull && obj is Material material)
                    {
                        GUI.backgroundColor = Color.green;
                        if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("tree_icon_leaf").image, "赋予材质"), new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(25), GUILayout.Height(20), GUILayout.ExpandWidth(false)))
                        {
                            SceneTools.AssignMaterialToSelectedModels(material);
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    // 如果是GameObject类型，添加激活/关闭切换按钮（眼睛图标：激活=绿色，未激活=灰色）
                    // 场景工具 v2.35：点击按钮切换场景中对应 GameObject 的 activeSelf 状态
                    // 场景工具 v2.42：当点击的 GameObject 属于 _selectedObjectsInResourceBox 多选集合时，
                    //               批量切换所有选中 GameObject 的激活状态；目标状态 = !gameObj.activeSelf（与原单对象 toggle 语义一致），
                    //               批量操作统一在一个 Undo group，可一次 Ctrl+Z 撤销全部。
                    // 场景工具 v3.4：Project 中的预制体资产（PrefabUtility.IsPartOfPrefabAsset）跳过眼睛按钮，
                    //               落到下方通用资源图标分支（与 Material/Texture/Mesh/AudioClip 等 Project 资产一致），
                    //               显示预制体资源图标（EditorGUIUtility.ObjectContent 返回的 Prefab 图标）。
                    //               预制体实例（PrefabUtility.IsPartOfPrefabInstance）仍保留眼睛按钮——它在场景中，
                    //               activeSelf 状态可被实际控制。场景中普通 GameObject 同样保留眼睛按钮。
                    else if (!isObjectNull && obj is GameObject gameObj && !PrefabUtility.IsPartOfPrefabAsset(gameObj))
                    {
                        // 取对象自身的激活设置（与 Hierarchy 视图眼睛图标语义一致）
                        bool isActive = gameObj.activeSelf;
                        // d_scenevis_visible_hover / d_scenevis_hidden_hover 为 Unity Hierarchy 窗口内置的眼睛图标
                        string iconName = isActive ? "scenevis_visible_hover" : "scenevis_hidden";
                        Texture activeToggleIcon = null;
                        try
                        {
                            activeToggleIcon = EditorGUIUtility.IconContent(iconName).image;
                        }
                        catch { /* 极少数 Unity 版本可能没有此图标，吞掉走默认图标兜底 */ }
                        if (activeToggleIcon == null) activeToggleIcon = EditorGUIUtility.IconContent("_Help").image;

                        // v2.42：判断是否进入"批量多选"模式——当前 gameObj 在选中集合中，且选中集合里至少还有一个 GameObject 可被批量操作
                        bool isBatchMode = _selectedObjectsInResourceBox.Contains(gameObj) && _selectedObjectsInResourceBox.Count > 1;

                        // 激活=绿色背景，未激活=灰色背景，与功能含义一致
                        GUI.backgroundColor = isActive ? new Color(0.55f, 0.9f, 0.55f) : new Color(0.65f, 0.65f, 0.65f);
                        if (GUILayout.Button(
                            new GUIContent(
                                activeToggleIcon,
                                isBatchMode
                                    ? (isActive ? $"批量：已将选中 {_selectedObjectsInResourceBox.Count} 个对象全部关闭（点击撤销）" : $"批量：已将选中 {_selectedObjectsInResourceBox.Count} 个对象全部激活（点击撤销）")
                                    : (isActive ? "已激活，点击关闭" : "已关闭，点击激活")),
                            new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter },
                            GUILayout.Width(25), GUILayout.Height(20), GUILayout.ExpandWidth(false)))
                        {
                            // 收集本次操作的目标 GameObject 集合
                            var targets = new List<GameObject>();
                            if (isBatchMode)
                            {
                                foreach (var sel in _selectedObjectsInResourceBox)
                                {
                                    if (sel is GameObject go && go) targets.Add(go);
                                }
                            }
                            else
                            {
                                targets.Add(gameObj);
                            }

                            // 目标状态：toggle 后的状态 = !gameObj.activeSelf，保持与单对象原行为语义一致
                            // 批量执行后所有目标 GameObject 激活状态将统一（避免选中对象之间状态不一致造成视觉混乱）
                            bool targetActive = !gameObj.activeSelf;

                            // v2.42：批量 Undo——开新 group → 逐个 RecordObject → CollapseUndoOperations，让一次 Ctrl+Z 撤销全部
                            int undoGroup = UnityEditor.Undo.GetCurrentGroup();
                            UnityEditor.Undo.SetCurrentGroupName(isBatchMode ? "Batch Toggle GameObjects Active" : "Toggle GameObject Active");
                            foreach (var go in targets)
                            {
                                UnityEditor.Undo.RecordObject(go, "Toggle GameObject Active");
                                go.SetActive(targetActive);
                            }
                            UnityEditor.Undo.CollapseUndoOperations(undoGroup);

                            // 触发 SceneView 重绘，确保激活/关闭效果立即可见
                            UnityEditor.SceneView.RepaintAll();
                        }
                        GUI.backgroundColor = Color.white;
                    }
                    // 除场景和材质外，其他类型直接显示类型预览图标（不做成按钮，避免图标过小看不清）
                    else if (!isObjectNull && !(obj is SceneAsset))
                    {
                        Texture iconImg = null;
                        try
                        {
                            // ObjectContent 按对象实际类型返回 Project 窗口预览图标，
                            // 覆盖 Texture/Sprite/Mesh/Prefab/AudioClip/AnimationClip/Font/Shader/自定义 ScriptableObject 等
                            iconImg = EditorGUIUtility.ObjectContent(obj, obj.GetType()).image;
                        }
                        catch { /* 极少数自定义类型可能抛异常，吞掉用默认图标兜底 */ }
                        if (iconImg == null) iconImg = EditorGUIUtility.IconContent("_Help").image;

                        // GUIStyle.none 默认 alignment=UpperLeft 会让图标贴左上角，
                        // 复制出 MiddleCenter 对齐版本使图标在宽高范围内居中（不能直接改静态 GUIStyle.none，会污染全局）
                        GUILayout.Label(
                            new GUIContent(iconImg, GetTypeDisplayName(obj)),
                            new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter },
                            GUILayout.Width(25), GUILayout.Height(20));
                    }

                    // 选择按钮 - 支持Ctrl键和Shift键添加选择
                    GUI.backgroundColor = Color.cyan;
                    
                    // 在创建按钮之前检查对象是否有效且没有DontSaveInEditor标志
                    var canCreateButton = obj && !obj.Equals(null);
                    
                    // 额外的安全检查：确保对象在GUI操作前是有效的
                    if (canCreateButton && obj != null)
                    {
                        try
                        {
                            // 这里应该包含选择按钮的创建逻辑
                            // 如果是SceneAsset类型，添加打开按钮
                            if (obj is SceneAsset)
                            {
                                // Play Mode 下 EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo() / OpenScene()
                                // 会被 Unity 拒绝并抛 "This cannot be used during play mode"。
                                // 之前没有在 GUI 层做防御，按钮点击后异常会被外层 catch 捕获并误报为"创建选择按钮时出错"。
                                // 这里直接根据 isPlaying 禁用按钮并改提示文字，从源头避免触发。
                                bool canOpenScene = !EditorApplication.isPlaying;
                                string openSceneTooltip = canOpenScene
                                    ? "打开此场景"
                                    : "播放模式下无法打开场景，请先退出 Play Mode";
                                bool isCurrentScene = canOpenScene && AssetDatabase.GetAssetPath(obj) == currentScenePath;
                                // 当前场景已修改未保存=深紫色，已保存=灰色，非当前场景=黑色
                                // 图标与状态联动：默认用 BuildSettings.Editor；当前打开且已修改（dirty）→ 选中状态图标 d_BuildSettings.SelectedIcon
                                string iconName;
                                if (isCurrentScene)
                                {
                                    bool isDirty = SceneManager.GetActiveScene().isDirty;
                                    GUI.backgroundColor = isDirty ? new Color(0.94f, 0.31f, 0.96f) : Color.gray;
                                    iconName = isDirty ? "TerrainInspector.TerrainToolSplat" : "d_CloudConnect";
                                }
                                else
                                {
                                    GUI.backgroundColor = Color.black;
                                    iconName = "UnityLogo";
                                }
                                GUI.enabled = canOpenScene;
                                // 打开场景按钮（图标按钮，宽度提到 25 适配图标尺寸）
                                if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent(iconName).image, openSceneTooltip),  new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(25), GUILayout.Height(20), GUILayout.ExpandWidth(false)))
                                {
                                    // 打开场景
                                    string scenePath = AssetDatabase.GetAssetPath(obj);
                                    if (!string.IsNullOrEmpty(scenePath))
                                    {
                                        // 防御：obj 可能是纹理 / Prefab / .meta / 子资产等非场景资产，
                                        // 直接传给 OpenScene 会抛 "Invalid version provided" 这种 YAML 解析异常。
                                        // 这里只接受 .unity / .scene 场景资产（含可选的 .unity.meta / .scene.meta 后缀防御）。
                                        // 同时兼容 Unity（.unity）与 团结引擎（Tuanjie，.scene）。
                                        string trimmedPath = scenePath;
                                        if (trimmedPath.EndsWith(".meta", System.StringComparison.OrdinalIgnoreCase))
                                            trimmedPath = trimmedPath.Substring(0, trimmedPath.Length - 5);

                                        bool isSceneAsset = !string.IsNullOrEmpty(trimmedPath)
                                            && (trimmedPath.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase)
                                             || trimmedPath.EndsWith(".scene", System.StringComparison.OrdinalIgnoreCase))
                                            && System.IO.File.Exists(trimmedPath);

                                        if (!isSceneAsset)
                                        {
                                            Debug.LogWarning($"[ScenesTools] 跳过打开：'{scenePath}' 不是有效的场景资产（仅支持 .unity / .scene）");
                                        }
                                        else if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                                        {
                                            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(trimmedPath);
                                            //刷新→
                                            RefreshAvailableFiles();
                                            LoadResourceBox();
                                            RecheckNullObjectsInResourceBox();
                                        }
                                    }
                                }
                                GUI.enabled = true;
                            }
                            GUI.backgroundColor = Color.cyan;
                            // ★根据 obj 实际类型从 7 个不同颜色的选中按钮图标 (sv_icon_name1~7) 中选一个
                            //   映射规则（与 GetTypeOrder 优先级表前 7 个桶 1:1 对应）：
                            //     name1 = SceneAsset                  (priority =  0)
                            //     name2 = GameObject·Scene            (priority = 10)
                            //     name3 = GameObject·PrefabInstance   (priority = 11)
                            //     name4 = GameObject·Prefab           (priority = 12)
                            //     name5 = Material·Asset              (priority = 20)
                            //     name6 = Mesh·Asset                  (priority = 30)
                            //     name7 = Texture / AudioClip / AnimationClip / 其它  (priority >= 40)
                            string selectIconName = GetSelectionIconName(obj);
                            if (GUILayout.Button(EditorGUIUtility.IconContent(selectIconName).image, new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(30), GUILayout.Height(20), GUILayout.ExpandWidth(false)))
                            {
                                // 再次验证对象有效性
                                if (obj != null && !obj.Equals(null))
                                {
                                    // 检查是否按住Ctrl键或Shift键
                                    Event currentEvent = Event.current;
                                    bool isCtrlPressed = currentEvent.control || currentEvent.command; // command键用于Mac
                                    bool isShiftPressed = currentEvent.shift;
                                    
                                    if (isCtrlPressed)
                                    {
                                        // Ctrl+点击：添加到当前选择
                                        List<Object> currentSelection = new List<Object>(Selection.objects);
                                        if (!currentSelection.Contains(obj))
                                        {
                                            currentSelection.Add(obj);
                                            Selection.objects = currentSelection.ToArray();
                                        }
                                    }
                                    else if (isShiftPressed)
                                    {
                                        // 注意：不能用 Selection.activeObject = null（Play Mode 下被 Unity 拒绝抛 "This cannot be used during play mode"）
                                        // 用 Selection.objects = new[] { obj } 一次性替换即可
                                        Selection.objects = new Object[] { obj };
                                        SceneTools.SelectAllHierarchy();
                                    }
                                    else
                                    {
                                        // 普通点击：替换当前选择
                                        // 同样用 Selection.objects 避免 Play Mode 下 activeObject = null 的限制
                                        Selection.objects = new Object[] { obj };
                                    }

                                    EditorGUIUtility.PingObject(obj);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"创建选择按钮时出错: {e.Message}");
                        }
                    }
                    else
                    {
                        // 如果对象无效，显示禁用的按钮
                        GUI.enabled = false;
                        EditorGUILayout.Space(20);
                        GUILayout.Button(EditorGUIUtility.IconContent("sv_icon_name0").image, new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(30), GUILayout.Height(20), GUILayout.ExpandWidth(false));
                        GUI.enabled = true;
                    }

                    // GUI.backgroundColor = Color.white;
                    // 编号 Label 默认在 Light 主题下文字偏深不可读，基于 EditorStyles.label 派生一个灰字居中样式
                    var numberLabelStyle = new GUIStyle(EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
                    numberLabelStyle.normal.textColor = Color.gray;
                    // numberLabelStyle.hover.textColor = Color.white;
                    // numberLabelStyle.focused.textColor = Color.white;
                    EditorGUILayout.LabelField($"{i + 1}", numberLabelStyle, GUILayout.Height(16), GUILayout.Width(20), GUILayout.ExpandWidth(false));
                    // 移除按钮 - 设置为红色
                    // GUI.backgroundColor = Color.red;
                    // if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.ExpandWidth(false)))
                    if (GUILayout.Button(new GUIContent(EditorGUIUtility.IconContent("d_TreeEditor.Trash").image), new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Width(18), GUILayout.Height(20), GUILayout.ExpandWidth(false)))
                    {
                        // 删除对象前，先清理相关的映射
                        // 仅当被删除的对象本身是null时，才安全地移除instanceID映射
                        if (_resourceBoxInstanceIDs.TryGetValue(i, out int removedInstanceID))
                        {
                            // 确认这个instanceID不属于列表中其他有效对象，避免误删
                            bool isUsedByOther = false;
                            for (int j = 0; j < _resourceBox.Count; j++)
                            {
                                if (j != i && _resourceBox[j] && VicToolsUnityCompat.GetReversibleId(_resourceBox[j]) == removedInstanceID)
                                {
                                    isUsedByOther = true;
                                    break;
                                }
                            }
                            if (!isUsedByOther)
                            {
                                _instanceIDToDisplayName.Remove(removedInstanceID);
                            }
                        }
                        
                        _resourceBox.RemoveAt(i);
                        
                        // 重新构建null对象显示名称字典和InstanceID映射，更新所有受影响的索引
                        var tempNullDisplayNames = new Dictionary<int, string>();
                        var tempInstanceIDs = new Dictionary<int, int>();
                        
                        foreach (var kvp in _nullObjectDisplayNames)
                        {
                            if (kvp.Key < i)
                            {
                                // 删除位置之前的索引保持不变
                                tempNullDisplayNames[kvp.Key] = kvp.Value;
                            }
                            else if (kvp.Key > i)
                            {
                                // 删除位置之后的索引需要减1
                                tempNullDisplayNames[kvp.Key - 1] = kvp.Value;
                            }
                            // kvp.Key == i 的项被删除，不添加到新字典中
                        }
                        
                        foreach (var kvp in _resourceBoxInstanceIDs)
                        {
                            if (kvp.Key < i)
                            {
                                tempInstanceIDs[kvp.Key] = kvp.Value;
                            }
                            else if (kvp.Key > i)
                            {
                                tempInstanceIDs[kvp.Key - 1] = kvp.Value;
                            }
                        }
                        
                        _nullObjectDisplayNames.Clear();
                        _resourceBoxInstanceIDs.Clear();
                        
                        foreach (var kvp in tempNullDisplayNames)
                        {
                            _nullObjectDisplayNames[kvp.Key] = kvp.Value;
                        }
                        
                        foreach (var kvp in tempInstanceIDs)
                        {
                            _resourceBoxInstanceIDs[kvp.Key] = kvp.Value;
                        }

                        // v2.28: 同步重建 _nullObjectRawItems
                        var tempRawItems = new Dictionary<int, ResourceBoxRecoveryItem>();
                        foreach (var kvp in _nullObjectRawItems)
                        {
                            if (kvp.Key < i) tempRawItems[kvp.Key] = kvp.Value;
                            else if (kvp.Key > i) tempRawItems[kvp.Key - 1] = kvp.Value;
                        }
                        _nullObjectRawItems.Clear();
                        foreach (var kvp in tempRawItems) _nullObjectRawItems[kvp.Key] = kvp.Value;

                        // 立即保存资源箱数据
                        SaveResourceBox(false);
                        
                        // 结束当前行的布局，然后跳出循环
                        // 避免在IMGUI循环中继续渲染已变更的列表，防止Layout/Repaint不一致
                        GUI.backgroundColor = Color.white;
                        EditorGUILayout.EndHorizontal();
                        break;
                    }

                    EditorGUILayout.EndHorizontal();
                }

                // 结束资源箱内容列表
                EditorGUILayout.EndVertical();
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.Space(30);
                EditorGUILayout.LabelField("> 资源箱为空 <", style.normalfont_Hui_Cen);
            }
            
            // 添加层级设置按钮 - 紧靠在资源箱列表的底部
            // 参数设置
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            // 一级选项
            _selPrefab = base.CreateToggleWithStyle(new GUIContent("Prefab", "挑选 预设 对象"), _selPrefab, null, null, null, null, 50, 20);
            _selMesh = base.CreateToggleWithStyle(new GUIContent("Mesh", "挑选 非预设 对象"), _selMesh, null, null, null, null, 40, 20);
            _selLODGroup = base.CreateToggleWithStyle(new GUIContent("LOD", "挑选带 LODGroup 对象"), _selLODGroup, null, null, null, null, 30, 20);
            _selMissMat = base.CreateToggleWithStyle(new GUIContent("Miss Mat", "挑选 丢失材质球 的模型对象"), _selMissMat, null, null, null, null, 60, 20);
            _selMissMesh = base.CreateToggleWithStyle(new GUIContent("Miss Mesh", "挑选 丢失Mesh 的对象（MeshFilter或SkinnedMeshRenderer的Mesh为空）"), _selMissMesh, null, null, null, null, 69, 20);
            _selMissScript = base.CreateToggleWithStyle(new GUIContent("Miss Scirpt", "挑选 丢失脚本 的对象"), _selMissScript, null, null, null, null, 75, 20);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
            // 二级选项
            _selAct = base.CreateToggleWithStyle(new GUIContent("Active", "勾选时只挑选 激活 的对象，取消时只挑选 未激活 的对象"), _selAct, null, null, null, null, 50, 20);
            
            // _selMeshObj和_selParticleObj的自动勾选逻辑
            _selMeshObj = base.CreateToggleWithStyle(new GUIContent("isMesh", "挑选 模型 对象"), _selMeshObj, null, null, null, null, 50, 20);
            _selParticleObj = base.CreateToggleWithStyle(new GUIContent("Particle", "挑选 粒子 对象"), _selParticleObj, null, null, null, null, 55, 20);
            _selParent = base.CreateToggleWithStyle(new GUIContent("Parent", "选中 父对象"), _selParent, null, null, null, null, 50, 20);
            if (GUILayout.Button("Reset", GUILayout.Width(45), GUILayout.Height(22), GUILayout.ExpandWidth(false)))
            {
                _selPrefab = false;
                _selMesh = false;
                _selLODGroup = false;
                _selMissMat = false;
                _selMissMesh = false;
                _selMissScript = false;
                _selMeshObj = false;
                _selParticleObj = false;
                _selParent = false;
                _selAct = true;
            }
            GUI.backgroundColor = new Color(0.6f, 0.8f, 0.6f);
            if (GUILayout.Button(new GUIContent("挑选", "根据选项选择场景中的对象\n按住 Ctrl+点击：只在当前选中对象的父级范围内挑选"), GUILayout.Width(70), GUILayout.Height(22)))
            {
                // 按住 Ctrl 时只在选中对象的父级范围内挑选，并自动忽略 selParent 选项
                bool localScope = Event.current.control;
                bool effectiveSelParent = localScope ? false : _selParent;
                SceneTools.SelectObjectsByType(_selMesh, _selPrefab, _selLODGroup, _selMissMat, _selMissMesh, _selMissScript, _selAct, _selMeshObj, _selParticleObj, effectiveSelParent, localScope);
            }
            // 处理自动勾选逻辑
            /*
            if (newSelMeshObj != _selMeshObj || newSelParticleObj != _selParticleObj)
            {
                // 如果取消勾选一项且另一项未勾选，则自动勾选另一项
                if (!newSelMeshObj && !newSelParticleObj)
                {
                    // 如果两个都取消了，根据哪个被取消来决定勾选哪个
                    if (!newSelMeshObj && _selMeshObj) // 刚刚取消了isMesh
                    {
                        newSelParticleObj = true;
                    }
                    else if (!newSelParticleObj && _selParticleObj) // 刚刚取消了Particle
                    {
                        newSelMeshObj = true;
                    }
                }
                
                _selMeshObj = newSelMeshObj;
                _selParticleObj = newSelParticleObj;
            }
            */
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            var boxStyle = new GUIStyle(EditorStyles.helpBox) { margin = new RectOffset(0, 2, 0, 0), padding = new RectOffset(2, 2, 2, 2) };
            // var prevBgColor = GUI.backgroundColor;
            GUI.backgroundColor = Color.black;
            using (new GUILayout.HorizontalScope(boxStyle, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(96)))
            {
                GUI.backgroundColor = Color.white;
                _setStatic = base.CreateToggleWithStyle(new GUIContent("Static", "设置所有用于烘焙的静态对象材质"), _setStatic, null, null, null, null, 40, 20);
                
                if (_switchPBRMIcon != null)
                {
                    if (GUILayout.Button(new GUIContent(_switchPBRMIcon, "PBR_Mobile_NEW材质切换可接收灯光材质"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Height(28), GUILayout.Width(32)))
                    {
                        SceneTools.SwitchPBRLightingShader(_setStatic);
                    }
                }
            }
            GUI.backgroundColor = new Color(0.66f, 0.66f, 0.66f);
            if (GUILayout.Button(new GUIContent(_levelIn, "设置所有选择对象放入最后选择的对象中"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter },GUILayout.Height(30), GUILayout.Width(38)))
            {
                SceneTools.SetSelectedObjectsAsChildren();
            }
            if (GUILayout.Button(new GUIContent(_levelOut, "将选中对象放置到最外层级（Prefab需要Unpack）"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter },GUILayout.Height(30), GUILayout.Width(38)))
            {
                SceneTools.RemoveSelectedObjectsFromParent();
            }
            GUI.backgroundColor = Color.cyan;
            if (GUILayout.Button(new GUIContent(_selectLayer, "选择当前对象所在层级的所有对象"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Height(30), GUILayout.Width(38)))
            {
                SceneTools.SelectAllHierarchy();
            }
            if (_SelectLattIcon != null)
            {
                if (GUILayout.Button(new GUIContent(_SelectLattIcon, "选择晶格对象 / 模型对象（双向切换）\n[按 Esc 可以选中晶格体]\n• 选中模型 → 点击：选中对应的 Lattice_ 晶格对象\n• 选中晶格 → 点击：反向选中晶格控制的模型\n• Ctrl+点击：加选（不替换当前选择）"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Height(30), GUILayout.Width(38)))
                {
                    SelectAssociatedLattice();
                }
            }
            
            GUI.backgroundColor = Color.black;
            if (_lightDirIcon != null)
            {
                if (GUILayout.Button(new GUIContent(_lightDirIcon, "以碰撞体落地操作（Ctrl+点击：以模型底部落地）"), new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Height(30), GUILayout.Width(38)))
                {
                    bool useRaycast = Event.current.control;
                    SceneTools.PlaceObjectOnGround(useRaycast);
                }
            }
            GUI.backgroundColor = Color.black;
            using (new GUILayout.HorizontalScope(boxStyle, GUILayout.ExpandWidth(false), GUILayout.MaxWidth(105)))
            {
                if (GUILayout.Button(new GUIContent(_resetPositionIcon, "重置位移"), new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Height(30), GUILayout.Width(38)))
                {
                    var selected = Selection.gameObjects;
                    if (selected.Length > 0)
                    {
                        Undo.RecordObjects(System.Array.ConvertAll(selected, go => (Object)go.transform), "重置位移");
                        foreach (var go in selected)
                            go.transform.localPosition = Vector3.zero;
                    }
                }
                if (GUILayout.Button(new GUIContent(_resetRotationIcon, "重置旋转"), new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Height(30), GUILayout.Width(38)))
                {
                    var selected = Selection.gameObjects;
                    if (selected.Length > 0)
                    {
                        Undo.RecordObjects(System.Array.ConvertAll(selected, go => (Object)go.transform), "重置旋转");
                        foreach (var go in selected)
                        {
                            go.transform.localRotation = Quaternion.identity;
                            // 通过 SerializedObject 清除 euler hint 缓存，确保 Inspector 显示 (0,0,0)
                            var so = new SerializedObject(go.transform);
                            var eulerHint = so.FindProperty("m_LocalEulerAnglesHint");
                            if (eulerHint != null)
                            {
                                eulerHint.vector3Value = Vector3.zero;
                                so.ApplyModifiedPropertiesWithoutUndo();
                            }
                        }
                    }
                }
                if (GUILayout.Button(new GUIContent(_resetScaleIcon, "重置缩放"), new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Height(30), GUILayout.Width(38)))
                {
                    var selected = Selection.gameObjects;
                    if (selected.Length > 0)
                    {
                        Undo.RecordObjects(System.Array.ConvertAll(selected, go => (Object)go.transform), "重置缩放");
                        foreach (var go in selected)
                            go.transform.localScale = Vector3.one;
                    }
                }
            }

            GUI.backgroundColor = new Color(0.66f, 0.66f, 0.66f);
            // 仅当选中对象中存在丢失Mesh时显示找回按钮
            bool hasSelMissingMesh = false;
            foreach (var go in Selection.gameObjects)
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh == null) { hasSelMissingMesh = true; break; }
                var smr = go.GetComponent<SkinnedMeshRenderer>();
                if (smr != null && smr.sharedMesh == null) { hasSelMissingMesh = true; break; }
            }
            if (!hasSelMissingMesh)
            {
                // 兜底图标:不覆盖 _findMesh(避免丢失 LoadPngIcon 创建的 owned 贴图引用导致泄漏),
                // 用临时变量传给按钮即可
                var fallbackIcon = EditorGUIUtility.IconContent("BuildSettings.Broadcom").image;
                if (GUILayout.Button(new GUIContent(fallbackIcon, "对丢失Mesh的模型对象尝试自动找回Mesh"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Height(30), GUILayout.Width(38)))
                {
                    SceneTools.FindMissMeshs();
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent(_findMesh, "对丢失Mesh的模型对象尝试自动找回Mesh"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter }, GUILayout.Height(30), GUILayout.Width(38)))
                {
                    SceneTools.FindMissMeshs();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        /// 选择当前选中模型对象对应的晶格对象（Lattice_ 前缀）
        /// 如果当前选中的已经是晶格对象，则反向选中它控制的模型对象
        private void SelectAssociatedLattice()
        {
            // 捕获 Ctrl 键状态（按住 Ctrl 为加选模式）
            bool additive = Event.current != null && Event.current.control;

            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("[VicTools] 请先选中模型对象或晶格对象");
                return;
            }

            // 检查当前选中的是否是晶格对象（带 Lattice_ 前缀且有 LatticeModifier 组件）
            bool selectedIsLattice = false;
            foreach (var go in selectedObjects)
            {
                if (go.name.StartsWith("Lattice_", StringComparison.Ordinal) && go.GetComponent("LatticeModifier") != null)
                {
                    selectedIsLattice = true;
                    break;
                }
            }

            if (selectedIsLattice)
            {
                // 反向：选中晶格控制的模型对象
                SelectLatticeTargets(selectedObjects, additive);
            }
            else
            {
                // 正向：选中模型对应的晶格对象
                SelectLatticeFromTargets(selectedObjects, additive);
            }
        }

        /// 从晶格对象反向选中它控制的模型对象
        private void SelectLatticeTargets(GameObject[] latticeGOs, bool additive)
        {
            var targetObjects = new List<GameObject>();

            // 加选模式：保留当前已选中的对象
            if (additive)
            {
                foreach (var obj in Selection.gameObjects)
                    if (!targetObjects.Contains(obj))
                        targetObjects.Add(obj);
            }

            foreach (var go in latticeGOs)
            {
                var comp = go.GetComponent("LatticeModifier");
                if (comp == null) continue;

                var type = comp.GetType();
                var modeField = type.GetField("targetMode");
                var rendField = type.GetField("targetRenderer");
                var rootField = type.GetField("targetRoot");
                var manualField = type.GetField("manualRenderers");

                if (modeField == null) continue;
                int mode = (int)modeField.GetValue(comp);

                if (mode == 0) // SingleRenderer
                {
                    if (rendField != null)
                    {
                        var rend = rendField.GetValue(comp) as Renderer;
                        if (rend != null && !targetObjects.Contains(rend.gameObject))
                            targetObjects.Add(rend.gameObject);
                    }
                }
                else // MultiRenderer
                {
                    if (rootField != null)
                    {
                        var root = rootField.GetValue(comp) as Transform;
                        if (root != null && !targetObjects.Contains(root.gameObject))
                            targetObjects.Add(root.gameObject);
                    }

                    if (manualField != null)
                    {
                        var manualList = manualField.GetValue(comp) as List<Renderer>;
                        if (manualList != null)
                        {
                            foreach (var r in manualList)
                            {
                                if (r != null && !targetObjects.Contains(r.gameObject))
                                    targetObjects.Add(r.gameObject);
                            }
                        }
                    }
                }
            }

            if (targetObjects.Count > 0)
            {
                Selection.objects = targetObjects.ToArray();
                EditorGUIUtility.PingObject(targetObjects[0]);
            }
            else
            {
                Debug.LogWarning("[VicTools] 晶格对象未关联任何模型目标");
            }
        }

        /// 从模型对象正向查找对应的晶格对象
        private void SelectLatticeFromTargets(GameObject[] selectedObjects, bool additive)
        {
            var latticeObjects = new List<GameObject>();

            // 加选模式：保留当前已选中的对象
            if (additive)
            {
                foreach (var obj in Selection.gameObjects)
                    if (!latticeObjects.Contains(obj))
                        latticeObjects.Add(obj);
            }

            var allRoots = new List<GameObject>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                    allRoots.AddRange(scene.GetRootGameObjects());
            }

            // 收集所有带 Lattice_ 前缀的对象
            var allLatticeGOs = new List<GameObject>();
            foreach (var root in allRoots)
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("Lattice_", StringComparison.Ordinal))
                        allLatticeGOs.Add(t.gameObject);
                }
            }

            foreach (var go in selectedObjects)
            {
                // 方式1：按命名规则查找 Lattice_ + 对象名
                string latticeName = "Lattice_" + go.name;
                foreach (var latGO in allLatticeGOs)
                {
                    if (latGO.name == latticeName && !latticeObjects.Contains(latGO))
                    {
                        latticeObjects.Add(latGO);
                        break;
                    }
                }

                // 方式2：检查所有 Lattice_ 对象上的 LatticeModifier 组件的目标引用
                if (!latticeObjects.Exists(l => l.name == latticeName))
                {
                    foreach (var latGO in allLatticeGOs)
                    {
                        var comp = latGO.GetComponent("LatticeModifier");
                        if (comp == null) continue;

                        // 通过反射检查 targetRenderer 和 targetRoot
                        var type = comp.GetType();
                        var rendField = type.GetField("targetRenderer");
                        var rootField = type.GetField("targetRoot");
                        var manualField = type.GetField("manualRenderers");

                        if (rendField != null)
                        {
                            var rend = rendField.GetValue(comp) as Renderer;
                            if (rend != null && rend.gameObject == go)
                            {
                                if (!latticeObjects.Contains(latGO))
                                    latticeObjects.Add(latGO);
                                continue;
                            }
                        }

                        if (rootField != null)
                        {
                            var root = rootField.GetValue(comp) as Transform;
                            if (root != null && root.gameObject == go)
                            {
                                if (!latticeObjects.Contains(latGO))
                                    latticeObjects.Add(latGO);
                                continue;
                            }
                        }

                        if (manualField != null)
                        {
                            var manualList = manualField.GetValue(comp) as List<Renderer>;
                            if (manualList != null)
                            {
                                foreach (var r in manualList)
                                {
                                    if (r != null && r.gameObject == go)
                                    {
                                        if (!latticeObjects.Contains(latGO))
                                            latticeObjects.Add(latGO);
                                        break;
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (latticeObjects.Count > 0)
            {
                Selection.objects = latticeObjects.ToArray();
                EditorGUIUtility.PingObject(latticeObjects[0]);
            }
            else
            {
                Debug.LogWarning("[VicTools] 未找到对应的晶格对象（Lattice_ 前缀）");
            }
        }

        /// 处理拖拽的对象
        /// 资源箱成员在 7 类排序桶中的索引（0~6），与 GetSelectionIconName 的 7 个图标颜色一一对应。
        /// v3.5 优化：把原 9 个排序桶（0/10/11/12/20/30/40/50/60 + 500）按图标分类合并为 7 类：
        ///   idx 0  = SceneAsset                       → name1
        ///   idx 1  = GameObject·Scene                 → name2
        ///   idx 2  = GameObject·PrefabInstance        → name4
        ///   idx 3  = GameObject·Prefab                → name5
        ///   idx 4  = Material·Asset                   → name3
        ///   idx 5  = Mesh·Asset                       → name6
        ///   idx 6  = Texture / AudioClip / AnimationClip / 其它  → name7
        ///   null / 不可识别 → 999（保持向后兼容：原 GetTypeOrder 对 null 返 999 → 排到末尾）
        /// 作为单一事实源：GetTypeOrder(Object) / GetSelectionIconName(Object) 都从这里取，
        /// 保证"排序桶"和"图标颜色桶"永远一致。
        private static int GetTypeSortIndex(Object obj)
        {
            if (obj == null) return 999;
            if (obj is SceneAsset) return 0;
            var (baseType, sourceTag) = ParseTypeAndSource(obj);
            if (baseType == nameof(GameObject))
            {
                if (sourceTag == "Prefab")   return 3; // GameObject·Prefab
                if (sourceTag == "Instance") return 2; // GameObject·PrefabInstance（ParseTypeAndSource 实际返回的 sourceTag）
                return 1;                              // GameObject·Scene / "" 兜底（旧存档无后缀）
            }
            if (baseType == nameof(Material)) return 4;
            if (baseType == nameof(Mesh))     return 5;
            return 6; // Texture / AudioClip / AnimationClip / 其它自定义 ScriptableObject 等
        }

        /// 获取对象类型的排序优先级（数值越小越靠前）。
        /// v3.5 优化：按 GetTypeSortIndex 7 类桶生成优先级 = (idx + 1) * 10，
        /// 留 10 倍空间给未来可能加的子桶偏移（参考旧 v3.4 设计）。
        ///   SceneAsset                                →  10
        ///   GameObject·Scene                          →  20
        ///   GameObject·PrefabInstance                 →  30
        ///   GameObject·Prefab                         →  40
        ///   Material·Asset                            →  50
        ///   Mesh·Asset                                →  60
        ///   Texture/AudioClip/AnimationClip/Other     →  70
        ///   null                                      → 999
        private static int GetTypeOrder(Object obj)
        {
            int idx = GetTypeSortIndex(obj);
            return idx == 999 ? 999 : (idx + 1) * 10;
        }

        /// 将资源箱列表按类型集中排列（同类型保持原有相对顺序）
        /// v2.36 修复：排序后 null 对象的 _nullObjectRawItems / _nullObjectDisplayNames / _resourceBoxInstanceIDs 不再错位。
        /// 旧实现调用 RebuildIndexMappings() + RebuildNullObjectRawItemsIndex()，后者按 _recoveryRawItems 的顺序
        /// 逐个填充新索引的 null 位置。但 _recoveryRawItems 与 _resourceBox 的对齐关系在排序后会被打乱，
        /// 导致 null 对象被错误地指向其他类型/对象的 raw item / displayName，
        /// 进而使"打开包含该对象的场景后自动复原"机制失效，对象显示为"<未知类型> 未知对象"。
        /// 现在改为：排序前按 oldIndex 备份所有元数据，排序后按 (obj, oldIndex) 元组直接查回。
        private void SortResourceBoxByType()
        {
            if (_resourceBox.Count == 0) return;

            // 步骤1：按原索引备份 null 对象的 raw item / displayName / instanceID
            // 这些数据在排序后无法仅通过 _recoveryRawItems 顺序对应回原对象，必须显式按 oldIndex 保存
            var rawByOldIndex = new ResourceBoxRecoveryItem[_resourceBox.Count];
            var displayByOldIndex = new string[_resourceBox.Count];
            var iidByOldIndex = new int[_resourceBox.Count];
            for (int oldIdx = 0; oldIdx < _resourceBox.Count; oldIdx++)
            {
                if (_resourceBox[oldIdx]) continue; // 只对 null 项备份
                _nullObjectRawItems.TryGetValue(oldIdx, out rawByOldIndex[oldIdx]);
                _nullObjectDisplayNames.TryGetValue(oldIdx, out displayByOldIndex[oldIdx]);
                _resourceBoxInstanceIDs.TryGetValue(oldIdx, out iidByOldIndex[oldIdx]);
            }

            // 步骤2：稳定排序：按类型优先级 + 名称，原索引作为元组保留
            // v2.40 修复：暂时未知对象(null)在按类型排序时，之前用空字符串作为次级排序键，
            //               多个 null 之间会扎堆且没有按"保留的名称"参与排序，破坏原有相对位置。
            //               改为：null 对象用 displayByOldIndex[oldIndex] 中保存的 displayName
            //               提取出的原始对象名作为排序键，与有效对象的 obj.name 语义一致，
            //               配合 LINQ 稳定排序保证同名称的对象保留原有位置。
            // v2.41 修复：v2.40 让 null 对象按名称参与次级排序，但主排序键 GetTypeOrder(t.obj) 对 null 始终返回 999，
            //               导致所有 null 都被统一归到末尾，无法与"对应类型的有效对象"聚在一起。
            //               改为：预先为每个 oldIndex 计算 sortTypeOrder——
            //                 · 有效对象：用 GetTypeOrder(obj)；
            //                 · null 对象：先取 rawByOldIndex[oldIndex].type；取不到再从 displayName 提取类型（如"<GameObject> Cube" -> "GameObject"）；
            //                   仍取不到则回退到 999（向后兼容旧的"<未知类型> ..."条目）。
            //               让"暂时找不到的 GameObject / Material / Mesh..."与对应类型的有效对象聚在一起按名称排序。
            var sortTypeOrderByOldIndex = new int[_resourceBox.Count];
            var sortNameByOldIndex = new string[_resourceBox.Count];
            for (int i = 0; i < _resourceBox.Count; i++)
            {
                var obj = _resourceBox[i];
                if (obj)
                {
                    sortTypeOrderByOldIndex[i] = GetTypeOrder(obj);
                    sortNameByOldIndex[i] = obj.name ?? "";
                }
                else
                {
                    // null 对象：先尝试 raw item 保存的真实类型，再尝试 displayName 中提取的类型，都拿不到就 999
                    string typeName = null;
                    string sourceTag = "";
                    var raw = rawByOldIndex[i];
                    // v3.4 增强：displayName 路径同时拿 (基础类型名, 来源后缀)，让 GameObject·Scene / ·Prefab / ·PrefabInstance
                    //                三个子桶在 null 状态下也参与排序区分（不只是有效对象时区分）。
                    var (baseTypeFromName, sourceTagFromName) = ExtractTypeAndSourceFromDisplayName(displayByOldIndex[i] ?? "");
                    sourceTag = sourceTagFromName;
                    if (!string.IsNullOrEmpty(baseTypeFromName))
                    {
                        // displayName 已包含 <...> 类型标签 → 优先使用（既已归一化为 short name，又能拿到 sourceTag）
                        typeName = baseTypeFromName;
                    }
                    else if (raw != null && !string.IsNullOrEmpty(raw.type))
                    {
                        // displayName 拿不到（如"<未知类型> ..."）→ 用 raw.type 兜底，GetTypeOrder 内会做命名空间归一化
                        typeName = raw.type;
                    }
                    sortTypeOrderByOldIndex[i] = GetTypeOrder(typeName, sourceTag);
                    // null 对象：从备份的 displayName 中提取原始对象名（如"<GameObject> Cube" -> "Cube"）
                    sortNameByOldIndex[i] = ExtractOriginalNameFromDisplayName(displayByOldIndex[i] ?? "");
                }
            }
            var sorted = _resourceBox
                .Select((obj, oldIndex) => (obj, oldIndex))
                .OrderBy(t => sortTypeOrderByOldIndex[t.oldIndex])
                .ThenBy(t => sortNameByOldIndex[t.oldIndex] ?? "", StringComparer.OrdinalIgnoreCase)
                .ToList();

            _resourceBox.Clear();
            foreach (var t in sorted)
            {
                _resourceBox.Add(t.obj);
            }

            // 步骤3：基于 newIndex 和 oldIndex 重建三个字典
            // - 有效对象：用新的 InstanceID（排序不会改变对象身份，但保险起见刷新）
            // - null 对象：从备份数组按 oldIndex 直接查回，避免与其它 null 项争抢/错位
            var newInstanceIDs = new Dictionary<int, int>();
            var newNullDisplayNames = new Dictionary<int, string>();
            var newNullRawItems = new Dictionary<int, ResourceBoxRecoveryItem>();
            for (int newIdx = 0; newIdx < sorted.Count; newIdx++)
            {
                var (obj, oldIdx) = sorted[newIdx];
                if (obj)
                {
                    newInstanceIDs[newIdx] = VicToolsUnityCompat.GetReversibleId(obj);
                }
                else
                {
                    if (rawByOldIndex[oldIdx] != null)
                        newNullRawItems[newIdx] = rawByOldIndex[oldIdx];
                    if (!string.IsNullOrEmpty(displayByOldIndex[oldIdx]))
                        newNullDisplayNames[newIdx] = displayByOldIndex[oldIdx];
                    if (iidByOldIndex[oldIdx] != 0)
                        newInstanceIDs[newIdx] = iidByOldIndex[oldIdx];
                }
            }

            _resourceBoxInstanceIDs.Clear();
            foreach (var kvp in newInstanceIDs) _resourceBoxInstanceIDs[kvp.Key] = kvp.Value;
            _nullObjectDisplayNames.Clear();
            foreach (var kvp in newNullDisplayNames) _nullObjectDisplayNames[kvp.Key] = kvp.Value;
            _nullObjectRawItems.Clear();
            foreach (var kvp in newNullRawItems) _nullObjectRawItems[kvp.Key] = kvp.Value;

            SaveResourceBox(false);
            if (Parent) Parent.Repaint();
        }

        /// 重建 null 对象显示名称和 InstanceID 的索引映射
        private void RebuildIndexMappings()
        {
            // 重建 instanceID -> index 映射
            var newInstanceIDs = new Dictionary<int, int>();
            var newNullDisplayNames = new Dictionary<int, string>();

            // 先建立 instanceID -> displayName 的反查表（旧索引）
            var idToDisplay = new Dictionary<int, string>(_instanceIDToDisplayName);

            for (int i = 0; i < _resourceBox.Count; i++)
            {
                var obj = _resourceBox[i];
                if (obj != null)
                {
                    int iid = VicToolsUnityCompat.GetReversibleId(obj);
                    newInstanceIDs[i] = iid;
                }
                else
                {
                    // null 对象：从旧 instanceID 映射中找 displayName
                    // 遍历旧 _resourceBoxInstanceIDs 找到对应 instanceID
                    foreach (var kvp in _resourceBoxInstanceIDs)
                    {
                        if (!newInstanceIDs.ContainsValue(kvp.Value) &&
                            idToDisplay.TryGetValue(kvp.Value, out var dn))
                        {
                            newInstanceIDs[i] = kvp.Value;
                            newNullDisplayNames[i] = dn;
                            break;
                        }
                    }
                }
            }

            _resourceBoxInstanceIDs.Clear();
            _nullObjectDisplayNames.Clear();
            foreach (var kvp in newInstanceIDs) _resourceBoxInstanceIDs[kvp.Key] = kvp.Value;
            foreach (var kvp in newNullDisplayNames) _nullObjectDisplayNames[kvp.Key] = kvp.Value;
            // v2.28: 重建 null 原始项索引（按当前 _resourceBox 顺序重新映射）
            RebuildNullObjectRawItemsIndex();
        }

        /// 将对象插入资源箱，按类型插入到同类型末尾
        private void InsertToResourceBoxByType(Object obj)
        {
            int order = GetTypeOrder(obj);
            // 找到同类型最后一个的位置，插入其后；若无同类型则找第一个更大类型的位置
            int insertIndex = _resourceBox.Count; // 默认追加到末尾
            bool foundSameType = false;
            for (int i = 0; i < _resourceBox.Count; i++)
            {
                int cur = GetTypeOrder(_resourceBox[i]);
                if (cur == order)
                {
                    insertIndex = i + 1;
                    foundSameType = true;
                }
                else if (!foundSameType && cur > order)
                {
                    insertIndex = i;
                    break;
                }
            }
            _resourceBox.Insert(insertIndex, obj);

            var parentName = GetTopLevelParentName(obj);
            var displayName = !string.IsNullOrEmpty(parentName)
                ? $"<{GetTypeDisplayName(obj)}> [{parentName}]← {obj.name}"
                : $"<{GetTypeDisplayName(obj)}> {obj.name}";
            _resourceBoxDisplayNames[obj] = displayName;
        }

        private void HandleDragAndDropObjects(Object[] draggedObjects)
        {
            if (draggedObjects == null || draggedObjects.Length == 0)
                return;

            var addedCount = 0;
            foreach (var obj in draggedObjects)
            {
                if (!obj || _resourceBox.Contains(obj)) continue;
                if (((int)obj.hideFlags & (int)HideFlags.DontSaveInEditor) != 0)
                {
                    obj.hideFlags &= ~HideFlags.DontSaveInEditor;
                    Debug.Log($"已清除对象 {obj.name} 的DontSaveInEditor标志");
                }

                InsertToResourceBoxByType(obj);
                addedCount++;
            }

            if (addedCount <= 0) return;
            RebuildIndexMappings();
            UpdateSelectedObjectsInResourceBox();
            if (Parent) Parent.Repaint();
            // 立即保存资源箱数据（自动保存，不显示提示框）
            SaveResourceBox(false);
        }

        /// 添加选中的对象到资源箱
        private void AddSelectedToResourceBox()
        {
            var selectedObjects = Selection.objects;

            if (selectedObjects.Length == 0)
            {
                EditorUtility.DisplayDialog("提示", "请先选择要添加到资源箱的对象", "确定");
                return;
            }

            var addedCount = 0;
            foreach (var obj in selectedObjects)
            {
                if (!obj || _resourceBox.Contains(obj)) continue;
                if (((int)obj.hideFlags & (int)HideFlags.DontSaveInEditor) != 0)
                {
                    obj.hideFlags &= ~HideFlags.DontSaveInEditor;
                    Debug.Log($"已清除对象 {obj.name} 的DontSaveInEditor标志");
                }

                InsertToResourceBoxByType(obj);
                addedCount++;
            }

            if (addedCount <= 0) return;
            RebuildIndexMappings();
            UpdateSelectedObjectsInResourceBox();
            if (Parent) Parent.Repaint();
            SaveResourceBox(false);
        }

        /// v2.29 一键将当前打开的所有场景（支持多场景）放入资源箱
        /// 遍历 SceneManager 中已加载的 Scene，按场景路径加载对应的 SceneAsset 并加入资源箱
        private void AddOpenScenesToResourceBox()
        {
            var addedCount = 0;
            var skippedCount = 0;
            var notAssetCount = 0;
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // 防止多场景打开同一文件时重复

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded) continue;

                var path = scene.path;
                if (string.IsNullOrEmpty(path))
                {
                    // 未保存的新建场景没有路径，无法取 SceneAsset
                    notAssetCount++;
                    continue;
                }

                if (!seenPaths.Add(path)) continue; // 同路径已处理过

                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (sceneAsset == null)
                {
                    notAssetCount++;
                    Debug.LogWarning($"无法加载场景资源: {path}");
                    continue;
                }

                if (_resourceBox.Contains(sceneAsset))
                {
                    skippedCount++;
                    continue;
                }

                InsertToResourceBoxByType(sceneAsset);
                addedCount++;
            }

            if (addedCount <= 0)
            {
                var msg = "没有新场景被加入资源箱";
                if (skippedCount > 0) msg += $"\n已存在: {skippedCount} 个";
                if (notAssetCount > 0) msg += $"\n未保存/无法加载: {notAssetCount} 个（请先保存场景）";
                EditorUtility.DisplayDialog("提示", msg, "确定");
                return;
            }

            RebuildIndexMappings();
            UpdateSelectedObjectsInResourceBox();
            if (Parent) Parent.Repaint();
            SaveResourceBox(false);

            var summary = $"已添加 {addedCount} 个场景到资源箱";
            if (skippedCount > 0) summary += $"\n已存在跳过: {skippedCount} 个";
            if (notAssetCount > 0) summary += $"\n未保存/无法加载: {notAssetCount} 个";
            Debug.Log(summary);
        }

        /// v3.4 新增：一键将【项目中 Assets 目录下的所有场景】放入资源箱
        /// 实现要点：
        ///   ① 用 AssetDatabase.FindAssets("t:Scene") 取出全项目场景 GUID → 路径，
        ///      只保留 "Assets/" 开头的场景（自动排除 Packages 下的包内示例场景）。
        ///   ② 兜底：团结引擎(Tuanjie) 使用 .scene 扩展名，个别情况下 AssetDatabase 检索不全，
        ///      这里直接扫描 Assets 目录下的 .unity / .scene 文件补齐（Unity 原生 .unity 同样兼容）。
        ///   ③ 先做【扫描结果预览】：打开非模态的 SceneScanPreviewWindow 窗口
        ///      （待添加数量 / 已在资源箱数量 / 失败数量 / 完整场景清单），
        ///      同时把扫描明细输出到 Console；窗口不锁定焦点，用户可随时切到 Console 查看完整信息。
        ///      在窗口里点【确定添加】后才真正写入资源箱；点【取消】/关窗不做任何改动。
        ///   ④ 写入时按类型插入（InsertToResourceBoxByType），最后统一重建索引映射 /
        ///      刷新选中状态 / 重绘 / 自动保存。
        private void AddAllProjectScenesToResourceBox()
        {
            var scenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // ---------- ① AssetDatabase 检索 ----------
            try
            {
                var guids = AssetDatabase.FindAssets("t:Scene");
                if (guids != null)
                {
                    foreach (var guid in guids)
                    {
                        var path = AssetDatabase.GUIDToAssetPath(guid);
                        if (string.IsNullOrEmpty(path)) continue;
                        // 只取项目 Assets 下的场景（排除 Packages 内的场景）
                        if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase)) continue;
                        scenePaths.Add(path);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ResourceBox] 检索项目场景失败: {e.Message}");
            }

            // ---------- ② 文件系统兜底扫描（兼容 .unity / .scene） ----------
            try
            {
                foreach (var ext in new[] { ".unity", ".scene" })
                {
                    var files = System.IO.Directory.GetFiles(
                        Application.dataPath, "*" + ext, System.IO.SearchOption.AllDirectories);
                    foreach (var file in files)
                    {
                        // 绝对路径 → Assets/ 开头的资源路径
                        var relative = file.Substring(Application.dataPath.Length).Replace('\\', '/');
                        scenePaths.Add("Assets" + relative);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ResourceBox] 扫描 Assets 目录场景文件失败(可忽略): {e.Message}");
            }

            if (scenePaths.Count == 0)
            {
                Debug.LogWarning("[ResourceBox] 项目中未找到任何场景（Assets 目录下没有 .unity / .scene 文件）");
                if (Parent) Parent.ShowNotification(new GUIContent("项目中未找到任何场景"));
                return;
            }

            // ---------- ③ 扫描并分类（只读，不修改资源箱） ----------
            var newScenes = new List<SceneAsset>();   // 待添加的场景
            var skippedCount = 0;                     // 已在资源箱中的场景
            var failedCount = 0;                      // 加载失败的场景

            // 按路径排序，保证预览清单与添加顺序稳定（与 Project 窗口目录顺序无关）
            var sortedPaths = new List<string>(scenePaths);
            sortedPaths.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var path in sortedPaths)
            {
                var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
                if (sceneAsset == null)
                {
                    failedCount++;
                    Debug.LogWarning($"[ResourceBox] 无法加载场景资源: {path}");
                    continue;
                }

                if (_resourceBox.Contains(sceneAsset))
                {
                    skippedCount++;
                    continue;
                }

                newScenes.Add(sceneAsset);
            }

            if (newScenes.Count == 0)
            {
                var msg = "没有新场景被加入资源箱";
                if (skippedCount > 0) msg += $"，已在资源箱中 {skippedCount} 个";
                if (failedCount > 0) msg += $"，加载失败 {failedCount} 个";
                // Debug.Log($"[ResourceBox][扫描] {msg}");
                if (Parent) Parent.ShowNotification(new GUIContent(msg));
                return;
            }

            // ---------- ④ 扫描结果预览窗口（非模态，确认后才开始添加） ----------
            // 窗口不锁定焦点，用户可切到 Console 查看信息；需要完整明细时可打开下面这行日志
            // Debug.Log($"[ResourceBox][扫描] 待添加 {newScenes.Count} 个，已在资源箱 {skippedCount} 个，加载失败 {failedCount} 个\n" +
            //             string.Join("\n", newScenes.Select(s => s ? AssetDatabase.GetAssetPath(s) : "(失效)")));

            int capturedSkipped = skippedCount;
            int capturedFailed = failedCount;

            // 组装通用【处理列表确认窗口】的清单项（带定位目标）
            var confirmItems = new List<VicToolsListConfirmWindow.Item>(newScenes.Count);
            foreach (var sceneAsset in newScenes)
            {
                if (!sceneAsset) continue;
                confirmItems.Add(new VicToolsListConfirmWindow.Item(
                    AssetDatabase.GetAssetPath(sceneAsset), sceneAsset));
            }

            var summary = $"待添加场景：{newScenes.Count} 个";
            if (skippedCount > 0) summary += $"\n已在资源箱（跳过）：{skippedCount} 个";
            if (failedCount > 0) summary += $"\n加载失败：{failedCount} 个";

            // 公共窗口（Vic.Editor.VicToolsListConfirmWindow）：非模态，用户确认后才执行添加
            VicToolsListConfirmWindow.Show(
                "添加项目场景",
                summary,
                confirmItems,
                () => AddScenesToResourceBox(newScenes, capturedSkipped, capturedFailed),
                $"确定添加（{newScenes.Count}）",
                () => Debug.Log("[ResourceBox] 已取消添加项目场景"));
            // 实际写入由预览窗口的【确定添加】按钮触发；此处直接返回
        }

        /// 把确认后的场景列表写入资源箱（由预览窗口的【确定添加】按钮回调）
        private void AddScenesToResourceBox(List<SceneAsset> scenes, int skippedCount, int failedCount)
        {
            if (scenes == null || scenes.Count == 0) return;

            var addedCount = 0;
            foreach (var sceneAsset in scenes)
            {
                if (!sceneAsset) continue;                  // 资源被删除等异常
                if (_resourceBox.Contains(sceneAsset)) continue; // 二次校验，避免重复添加
                InsertToResourceBoxByType(sceneAsset);
                addedCount++;
            }

            if (addedCount <= 0)
            {
                Debug.Log("[ResourceBox] 没有新场景被加入资源箱");
                if (Parent) Parent.ShowNotification(new GUIContent("没有新场景被加入资源箱"));
                return;
            }

            RebuildIndexMappings();
            UpdateSelectedObjectsInResourceBox();
            if (Parent) Parent.Repaint();
            SaveResourceBox(false);

            var summary = $"已添加 {addedCount} 个项目场景到资源箱";
            if (skippedCount > 0) summary += $"（已存在跳过 {skippedCount} 个）";
            if (failedCount > 0) summary += $"（加载失败 {failedCount} 个）";
            Debug.Log($"[ResourceBox] {summary}");
            // 完成提示同样不做模态弹窗：主窗口顶部气泡提示，不阻塞 Console
            if (Parent) Parent.ShowNotification(new GUIContent(summary));
        }

        /// v3.5 新增：一键把【当前场景主光】放入资源箱
        /// 主光判定（贴近 URP 主光选取规则）：
        ///   ① 只看平行光（Directional Light，URP 主光只可能是平行光）；
        ///   ② 优先取 RenderSettings.sun（Lighting 面板里指定的 Sun Source）；
        ///   ③ 同等条件下取"强度 × 颜色最大分量"最高者（URP 按可见光权重挑最亮的方向光）；
        ///   ④ 场景范围：先在活动场景里找，活动场景没有方向灯时再退到其它已加载场景。
        /// 流程：只读扫描 → 公共确认窗口（Vic.Editor.VicToolsListConfirmWindow）列出主光清单 → 用户确认后才写入资源箱
        private void AddCurrentSceneMainLightToResourceBox()
        {
            var mainLight = FindCurrentSceneMainLight(out var scope, out var allDirectional);
            if (mainLight == null)
            {
                Debug.LogWarning("[ResourceBox] 当前场景未找到方向光（主光）");
                if (Parent) Parent.ShowNotification(new GUIContent("当前场景未找到方向光（主光）"));
                return;
            }

            var mainGo = mainLight.gameObject;
            bool alreadyInBox = _resourceBox.Contains(mainGo);

            var lightTypeText = mainLight.type == LightType.Directional ? "平行光" : mainLight.type.ToString();
            var sunTag = mainLight == RenderSettings.sun ? "（Sun Source）" : "";

            // 清单只放主光本身，确认后加入资源箱（其它方向灯仅在概要里提示，不入箱）
            var items = new List<VicToolsListConfirmWindow.Item>
            {
                new VicToolsListConfirmWindow.Item(
                    $"主光：{mainGo.name}{sunTag}（{lightTypeText} / 强度 {mainLight.intensity:0.##} / 颜色 {ColorToHexString(mainLight.color)} / 阴影 {(mainLight.shadows == LightShadows.None ? "无" : "有")} / 场景 {mainGo.scene.name}）",
                    mainGo)
            };

            var summary = $"当前场景主光：1 个\n判定范围：{scope}\n范围内平行光：{allDirectional.Count} 个";
            if (alreadyInBox) summary += "\n已在资源箱（跳过）";

            int capturedSkipped = alreadyInBox ? 1 : 0;

            // 公共窗口（Vic.Editor.VicToolsListConfirmWindow）：非模态，用户确认后才执行添加
            VicToolsListConfirmWindow.Show(
                "添加当前场景主光",
                summary,
                items,
                () => AddMainLightToResourceBox(mainGo, capturedSkipped),
                "确定添加（1）",
                () => Debug.Log("[ResourceBox] 已取消添加当前场景主光"));
        }

        /// 把确认后的主光写入资源箱（由公共确认窗口的【确定添加】按钮回调）
        private void AddMainLightToResourceBox(GameObject mainLightGo, int skippedCount)
        {
            if (mainLightGo == null) return;

            if (_resourceBox.Contains(mainLightGo))
            {
                Debug.Log("[ResourceBox] 主光已在资源箱中");
                // 已在资源箱：同样直接选中主光
                SelectMainLightInHierarchy(mainLightGo);
                if (Parent) Parent.ShowNotification(new GUIContent("主光已在资源箱中"));
                return;
            }

            // 资源箱里存 GameObject（与"放入资源箱↓"保持一致），便于场景打开后自动复原
            InsertToResourceBoxByType(mainLightGo);

            // 添加后直接选中主光（选中后再刷新资源箱高亮，保证资源箱里同步显示选中态）
            SelectMainLightInHierarchy(mainLightGo);

            RebuildIndexMappings();
            UpdateSelectedObjectsInResourceBox();
            if (Parent) Parent.Repaint();
            SaveResourceBox(false);

            var summary = $"已添加当前场景主光到资源箱：{mainLightGo.name}";
            if (skippedCount > 0) summary += "（已存在跳过）";
            Debug.Log($"[ResourceBox] {summary}");
            // 完成提示不做模态弹窗：主窗口顶部气泡提示，不阻塞 Console
            if (Parent) Parent.ShowNotification(new GUIContent(summary));
        }

        /// 选中主光（Hierarchy 中选中 + Ping 高亮，便于直接调整灯光参数）
        private static void SelectMainLightInHierarchy(GameObject mainLightGo)
        {
            if (mainLightGo == null) return;
            try
            {
                // 用 Selection.objects 一次性替换，避免 Play Mode 下 activeObject = null 的限制
                Selection.objects = new Object[] { mainLightGo };
                EditorGUIUtility.PingObject(mainLightGo);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ResourceBox] 选中主光失败: {e.Message}");
            }
        }

        /// 在当前打开的场景中查找主光（平行光）
        /// scope：实际判定范围说明；allDirectional：范围内所有平行光
        private static Light FindCurrentSceneMainLight(out string scope, out List<Light> allDirectional)
        {
            scope = "";
            allDirectional = new List<Light>();

            // 显式包含未激活对象，避免主光所在物体 activeSelf=false 时被漏掉
            var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // ① 先在活动场景内查找
            var activeScene = SceneManager.GetActiveScene();
            var scopeLights = new List<Light>();
            foreach (var light in lights)
            {
                if (light == null || light.type != LightType.Directional) continue;
                if (light.gameObject.scene != activeScene) continue;
                scopeLights.Add(light);
            }

            bool activeSceneUsed = true;
            if (scopeLights.Count == 0)
            {
                // ② 活动场景没有方向灯时，退到其它已加载场景
                activeSceneUsed = false;
                foreach (var light in lights)
                {
                    if (light == null || light.type != LightType.Directional) continue;
                    var scene = light.gameObject.scene;
                    if (!scene.IsValid() || !scene.isLoaded) continue;
                    if (scene == activeScene) continue;
                    scopeLights.Add(light);
                }
            }

            allDirectional = scopeLights;
            scope = activeSceneUsed
                ? $"活动场景「{activeScene.name}」"
                : "全部已加载场景（活动场景内无方向光）";

            // ③ 选出主光：启用中 > Sun Source > 强度×颜色最大分量
            Light best = null;
            float bestWeight = float.NegativeInfinity;
            bool bestIsSun = false;

            foreach (var light in scopeLights)
            {
                bool isActive = light.enabled && light.gameObject.activeInHierarchy;
                bool isSun = light == RenderSettings.sun;
                float weight = light.intensity * Mathf.Max(light.color.r, Mathf.Max(light.color.g, light.color.b));

                bool better;
                if (best == null)
                {
                    better = true;
                }
                else
                {
                    bool bestActive = best.enabled && best.gameObject.activeInHierarchy;
                    if (isActive != bestActive) better = isActive;
                    else if (isSun != bestIsSun) better = isSun;
                    else better = weight > bestWeight;
                }

                if (better)
                {
                    best = light;
                    bestWeight = weight;
                    bestIsSun = isSun;
                }
            }

            return best;
        }

        /// 颜色转 #RRGGBB 文本（用于清单展示）
        private static string ColorToHexString(Color c)
        {
            return $"#{Mathf.RoundToInt(c.r * 255):X2}{Mathf.RoundToInt(c.g * 255):X2}{Mathf.RoundToInt(c.b * 255):X2}";
        }

        /// v3.4 新增：一键把【当前项目相关的渲染管线配置文件】放入资源箱
        /// 扫描范围：
        ///   ① 项目设置（ProjectSettings/GraphicsSettings.asset、QualitySettings.asset）中引用的
        ///      渲染管线资产（RenderPipelineAsset）与管线全局设置（RenderPipelineGlobalSettings）
        ///   ② 当前生效的默认管线（GraphicsSettings.defaultRenderPipeline）与当前质量等级管线（QualitySettings.renderPipeline）
        ///   ③ 项目内全部 RenderPipelineAsset（未被任何设置引用的标记为"未启用"）
        ///   ④ 管线资产引用的渲染器数据（URP m_RendererDataList / HDRP m_RendererData）
        /// 流程：只读扫描 → 公共确认窗口（Vic.Editor.VicToolsListConfirmWindow）列出清单 → 用户确认后才写入资源箱
        private void AddAllProjectRenderPipelineAssetsToResourceBox()
        {
            // 资产 → 使用状态标签（同一资产被多处引用时合并标签）
            var tagMap = new Dictionary<Object, string>();
            var candidates = new List<Object>();

            // ---------- ① 项目设置里引用的渲染管线配置 ----------
            foreach (var settingsPath in new[]
                     {
                         "ProjectSettings/GraphicsSettings.asset",
                         "ProjectSettings/QualitySettings.asset"
                     })
            {
                foreach (var guid in ExtractGuidFromProjectSettings(settingsPath))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    if (!asset) continue;

                    if (asset is UnityEngine.Rendering.RenderPipelineAsset)
                    {
                        AddRenderPipelineCandidate(tagMap, candidates, asset, "项目设置引用");
                    }
                    else if (asset.GetType().Name == "RenderPipelineGlobalSettings")
                    {
                        // 渲染管线全局设置（URP/HDRP 的 GlobalSettings 资产），按类型名判断避免版本依赖
                        AddRenderPipelineCandidate(tagMap, candidates, asset, "管线全局设置");
                    }
                }
            }

            // ---------- ② 当前生效的管线（Graphics 默认 / 当前质量等级） ----------
            try
            {
                var defaultPipeline = UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline;
                if (defaultPipeline) AddRenderPipelineCandidate(tagMap, candidates, defaultPipeline, "Graphics默认");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ResourceBox] 读取默认渲染管线失败: {e.Message}");
            }

            try
            {
                var currentPipeline = QualitySettings.renderPipeline;
                if (currentPipeline) AddRenderPipelineCandidate(tagMap, candidates, currentPipeline, "当前质量等级");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ResourceBox] 读取当前质量等级渲染管线失败: {e.Message}");
            }

            // ---------- ③ 项目内全部渲染管线资产（未被引用的标记为"未启用"） ----------
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:RenderPipelineAsset"))
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(assetPath)) continue;

                    var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    if (!(asset is UnityEngine.Rendering.RenderPipelineAsset)) continue;

                    // 已经被上面标记过引用关系的资产不再覆盖标签
                    if (tagMap.ContainsKey(asset)) continue;

                    AddRenderPipelineCandidate(tagMap, candidates, asset, "未启用");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ResourceBox] 检索渲染管线资产失败: {e.Message}");
            }

            // ---------- ④ 管线资产引用的渲染器数据（URP / HDRP） ----------
            foreach (var asset in candidates.ToList()) // 复制一份，避免遍历时修改集合
            {
                if (!(asset is UnityEngine.Rendering.RenderPipelineAsset)) continue;
                CollectRendererDataAssets(asset, tagMap, candidates);
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[ResourceBox] 当前项目未找到任何渲染管线配置文件（可能使用的是内置渲染管线 Built-in）");
                if (Parent) Parent.ShowNotification(new GUIContent("未找到渲染管线配置文件"));
                return;
            }

            // ---------- ⑤ 组装清单（带定位目标） ----------
            var items = new List<VicToolsListConfirmWindow.Item>(candidates.Count);
            int pipelineAssetCount = 0;
            int rendererDataCount = 0;
            int disabledCount = 0;
            int alreadyInBox = 0;

            foreach (var asset in candidates)
            {
                if (!asset) continue;

                tagMap.TryGetValue(asset, out var tag);
                if (string.IsNullOrEmpty(tag)) tag = "未启用";

                if (asset is UnityEngine.Rendering.RenderPipelineAsset) pipelineAssetCount++;
                else rendererDataCount++;
                if (tag.Contains("未启用")) disabledCount++;

                var assetPath = AssetDatabase.GetAssetPath(asset);
                string display = string.IsNullOrEmpty(assetPath) ? asset.name : assetPath;

                bool inBox = _resourceBox.Contains(asset);
                if (inBox) alreadyInBox++;

                var line = $"[{tag}] {display}" + (inBox ? "（已在资源箱）" : "");
                items.Add(new VicToolsListConfirmWindow.Item(line, asset));
            }

            var summary = $"渲染管线配置：{candidates.Count} 个" +
                          $"\n管线资产 {pipelineAssetCount} 个 / 渲染器数据 {rendererDataCount} 个";
            if (disabledCount > 0) summary += $"\n未启用：{disabledCount} 个";
            if (alreadyInBox > 0) summary += $"\n已在资源箱（跳过）：{alreadyInBox} 个";

            int capturedSkipped = alreadyInBox;
            int willAdd = candidates.Count - alreadyInBox;

            // 公共窗口（Vic.Editor.VicToolsListConfirmWindow）：非模态，用户确认后才执行添加
            VicToolsListConfirmWindow.Show(
                "添加渲染管线配置",
                summary,
                items,
                () => AddRenderPipelineConfigsToResourceBox(candidates, capturedSkipped),
                $"确定添加（{willAdd}）",
                () => Debug.Log("[ResourceBox] 已取消添加渲染管线配置"));
        }

        /// 把确认后的渲染管线配置写入资源箱（由公共确认窗口的【确定添加】按钮回调）
        private void AddRenderPipelineConfigsToResourceBox(List<Object> assets, int skippedCount)
        {
            if (assets == null || assets.Count == 0) return;

            var addedCount = 0;
            foreach (var asset in assets)
            {
                if (!asset) continue;                     // 资源被删除等异常
                if (_resourceBox.Contains(asset)) continue; // 二次校验，避免重复添加
                InsertToResourceBoxByType(asset);
                addedCount++;
            }

            if (addedCount <= 0)
            {
                Debug.Log("[ResourceBox] 没有新的渲染管线配置被加入资源箱");
                if (Parent) Parent.ShowNotification(new GUIContent("没有新的渲染管线配置被加入资源箱"));
                return;
            }

            RebuildIndexMappings();
            UpdateSelectedObjectsInResourceBox();
            if (Parent) Parent.Repaint();
            SaveResourceBox(false);

            var summary = $"已添加 {addedCount} 个渲染管线配置到资源箱";
            if (skippedCount > 0) summary += $"（已存在跳过 {skippedCount} 个）";
            Debug.Log($"[ResourceBox] {summary}");
            // 完成提示不做模态弹窗：主窗口顶部气泡提示，不阻塞 Console
            if (Parent) Parent.ShowNotification(new GUIContent(summary));
        }

        /// 记录一个渲染管线相关候选资产（同一资产被多处引用时合并标签）
        private static void AddRenderPipelineCandidate(Dictionary<Object, string> tagMap, List<Object> candidates, Object asset, string tag)
        {
            if (!asset) return;

            if (tagMap.TryGetValue(asset, out var existTag))
            {
                if (string.IsNullOrEmpty(existTag)) tagMap[asset] = tag;
                else if (!existTag.Contains(tag)) tagMap[asset] = existTag + " / " + tag;
                return;
            }

            tagMap[asset] = tag;
            candidates.Add(asset);
        }

        /// 读取渲染管线资产引用的渲染器数据资产（URP: m_RendererDataList / HDRP: m_RendererData）
        private static void CollectRendererDataAssets(Object pipelineAsset, Dictionary<Object, string> tagMap, List<Object> candidates)
        {
            try
            {
                var so = new SerializedObject(pipelineAsset);

                var listProp = so.FindProperty("m_RendererDataList"); // URP
                if (listProp != null && listProp.isArray)
                {
                    for (int i = 0; i < listProp.arraySize; i++)
                    {
                        var data = listProp.GetArrayElementAtIndex(i).objectReferenceValue;
                        if (data) AddRenderPipelineCandidate(tagMap, candidates, data, "渲染器数据");
                    }
                }

                var singleProp = so.FindProperty("m_RendererData"); // HDRP
                if (singleProp != null)
                {
                    var data = singleProp.objectReferenceValue;
                    if (data) AddRenderPipelineCandidate(tagMap, candidates, data, "渲染器数据");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ResourceBox] 读取渲染器数据失败: {e.Message}");
            }
        }

        /// 从 ProjectSettings 下的设置文件（YAML 文本）中提取全部 32 位资源 guid
        private static List<string> ExtractGuidFromProjectSettings(string settingsPath)
        {
            var result = new List<string>();
            try
            {
                if (!System.IO.File.Exists(settingsPath)) return result;

                var text = System.IO.File.ReadAllText(settingsPath);
                var matches = Regex.Matches(text, @"guid:\s*([0-9a-fA-F]{32})");
                foreach (Match m in matches)
                {
                    var guid = m.Groups[1].Value;
                    if (!result.Contains(guid)) result.Add(guid);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[ResourceBox] 读取 {settingsPath} 失败: {e.Message}");
            }
            return result;
        }

        /// 带确认的清除资源箱
        private void ClearResourceBoxWithConfirmation()
        {
            if (_resourceBox.Count == 0)
            {
                Debug.Log("资源箱已经是空的");
                return;
            }

            var confirm = EditorUtility.DisplayDialog(
                "确认清除",
                $"确定要清除资源箱中的所有 {_resourceBox.Count} 个对象吗？",
                "确定清除",
                "取消"
            );

            if (!confirm) return;
            _resourceBox.Clear();
            _nullObjectDisplayNames.Clear(); // 清空null对象显示名称字典
            _resourceBoxInstanceIDs.Clear(); // 清空InstanceID映射
            _instanceIDToDisplayName.Clear(); // 清空InstanceID到displayName的映射
            _nullObjectRawItems.Clear(); // v2.28 清空 null 原始项缓存
            _recoveryRawItems = null; // v2.28 重置恢复缓存
            // 清除保存的数据
            EditorPrefs.DeleteKey("VicTools_ScenesTools_ResourceBox");
            // 强制重绘窗口
            if (Parent)
                Parent.Repaint();
                
            Debug.Log("资源箱已清空，保存的数据已清除");
        }

        /// 获取对象的最高级父对象名
        private static string GetTopLevelParentName(Object obj)
        {
            if (obj is not GameObject gameObject) return string.Empty;
            var root = gameObject.transform.root;
            // 如果根对象不是自己，说明有父对象
            return root != gameObject.transform ? root.name : string.Empty;
        }

        /// v3.4 新增：返回带来源后缀的类型显示名，用于在资源箱 <...> 标签中区分
        /// "场景对象 / Project 资产 / 预制体资产 / 预制体实例" 四种来源（后缀统一为英文）。
        ///   场景中普通 GameObject          → "GameObject·Scene"
        ///   场景中预制体实例（Hierarchy 蓝名）→ "GameObject·PrefabInstance"
        ///   Project 里的预制体资产         → "GameObject·Prefab"
        ///   Project 资产(Material/Texture 等) → "Material·Asset" 等
        ///   其它 / 兜底                   → obj.GetType().Name
        /// 注意：返回的字符串会参与 displayName 文本与存档 JSON，ExtractTypeAndSourceFromDisplayName
        ///       会按 · 切分取出 (基础类型名, 来源后缀)，用于【按类型排序】的子桶区分。
        private static string GetTypeDisplayName(Object obj)
        {
            var (baseType, sourceTag) = ParseTypeAndSource(obj);
            return string.IsNullOrEmpty(sourceTag) ? baseType : $"{baseType}·{sourceTag}";
        }

        /// v3.4 新增：把对象拆成 (基础类型名, 来源后缀) 元组，是 GetTypeDisplayName / GetTypeOrder(Object)
        /// 共用的核心判定逻辑，保证显示名与排序桶完全一致。
        ///   来源后缀取值："Scene" / "Prefab" / "PrefabInstance" / "Asset" / ""（无后缀兜底）
        ///   null 对象 / 无法归类 → (基础类型名, "")
        private static (string baseType, string sourceTag) ParseTypeAndSource(Object obj)
        {
            if (obj == null) return ("Null", "");
            string baseName = obj.GetType().Name;

            // 预制体资产（Project 里的 .prefab 资产根）
            if (obj is GameObject goPrefab && PrefabUtility.IsPartOfPrefabAsset(goPrefab))
                return (baseName, "Prefab");

            // 场景中的预制体实例（Hierarchy 中蓝名 / 可在 Prefab 模式编辑的实例）
            if (obj is GameObject goInst && PrefabUtility.IsPartOfPrefabInstance(goInst))
                return (baseName, "Instance");

            // 场景中的普通 GameObject（scene.IsValid() 排除未加入场景的临时对象）
            if (obj is GameObject goScene && goScene.scene.IsValid())
                return (baseName, "Scene");

            // Project 资产（非 GameObject，例如 Material / Texture / Mesh / AudioClip /
            // AnimationClip / Font / Shader / ScriptableObject / 自定义资产等）
            if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(obj)))
                return (baseName, "Asset");

            // 兜底：无法归类的对象（如内存中临时 ScriptableObject 实例等）
            return (baseName, "");
        }

        /// 7 类排序桶（GetTypeSortIndex 返回值 0~6）到 sv_icon_name* 的视觉映射。
        /// v3.5 改：原硬编码 7 条 if/switch 现统一走索引查表，排序桶与图标颜色永远同步。
        /// 注意：此表与"排序优先级"不是简单 +1 关系——比如 idx=4 (Material) 对应 name3，
        ///       idx=2 (Instance) 对应 name4，是用户定义的视觉分布。
        private static readonly string[] s_SelectionIconNamesByIndex =
        {
            "sv_icon_name1", // idx 0 = SceneAsset
            "sv_icon_name2", // idx 1 = GameObject·Scene
            "sv_icon_name4", // idx 2 = GameObject·PrefabInstance
            "sv_icon_name5", // idx 3 = GameObject·Prefab
            "sv_icon_name3", // idx 4 = Material
            "sv_icon_name6", // idx 5 = Mesh
            "sv_icon_name7", // idx 6 = Other (Texture/AudioClip/AnimationClip/...)
        };

        /// 根据 obj 实际类型从 7 个不同颜色的 Unity 系统选中按钮图标 (sv_icon_name1~7) 中选一个，
        /// 让用户能一眼从颜色看出资源箱成员所属的类型桶。
        /// 索引从 GetTypeSortIndex 单一事实源取，保证和 GetTypeOrder(Object) 排序桶完全一致。
        private static string GetSelectionIconName(Object obj)
        {
            // null 走 Other 桶（idx 6 → name7）
            int idx = (obj == null) ? 6 : GetTypeSortIndex(obj);
            return s_SelectionIconNamesByIndex[idx];
        }

        /// 自动获取当前场景中选择的物体的材质，并选择所有使用相同材质的物体
        private void SelectObjectsUsingSelectedObjectMaterial()
        {
            // 获取当前选中的GameObject
            var selectedGameObjects = Selection.gameObjects;
            
            if (selectedGameObjects == null || selectedGameObjects.Length == 0)
            {
                Debug.LogWarning("请先在场景中选择一个对象");
                EditorUtility.DisplayDialog("提示", "请先在场景中选择一个对象", "确定");
                return;
            }

            // 获取第一个选中对象的第一个材质
            var firstObject = selectedGameObjects[0];
            var renderer = firstObject.GetComponent<Renderer>();
            
            if (renderer == null || renderer.sharedMaterials == null || renderer.sharedMaterials.Length == 0)
            {
                Debug.LogWarning($"选中的对象 '{firstObject.name}' 没有Renderer组件或材质");
                EditorUtility.DisplayDialog("提示", $"选中的对象 '{firstObject.name}' 没有Renderer组件或材质", "确定");
                return;
            }

            var targetMaterial = renderer.sharedMaterials[0];
            if (targetMaterial == null)
            {
                Debug.LogWarning($"选中的对象 '{firstObject.name}' 的第一个材质为空");
                EditorUtility.DisplayDialog("提示", $"选中的对象 '{firstObject.name}' 的第一个材质为空", "确定");
                return;
            }

            // 调用现有的材质选择方法
            SelectObjectsUsingMaterial(targetMaterial);
            
            _selectedMaterial = targetMaterial;
        }

        /// 自动获取当前场景中选择的物体的材质，并选择所有使用相同材质的物体
        private void SelectObjectsUsingMaterial(Material targetMaterial)
        {
            // 在方法开头声明变量，避免作用域冲突
            var standaloneWindow = Parent as WinScenesTools;

            if (!targetMaterial)
            {
                Debug.LogWarning("无法选择模型：材质为空");
                // 更新选中数量为0 - 支持独立窗口和主窗口
                if (standaloneWindow)
                {
                    // 更新独立窗口的选中数量
                    standaloneWindow.StandaloneSelectedCount = 0;
                    standaloneWindow.Repaint();
                }
                else
                {
                    // 更新主窗口的全局选中数量
                    var mainWindow = Parent as VicToolsWindow;
                    if (!mainWindow) return;
                    mainWindow.globalSelectedObjectsCount = 0;
                    mainWindow.Repaint();
                }
                return;
            }
            // 获取场景中的所有根对象
            var allGameObjects = SceneManager.GetActiveScene().GetRootGameObjects();
            var objectsUsingMaterial = new List<GameObject>();
            // 遍历所有对象查找使用指定材质的Renderer
            foreach (var rootObject in allGameObjects)
            {
                var renderers = rootObject.GetComponentsInChildren<Renderer>(true);

                foreach (var renderer in renderers)
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material != targetMaterial) continue;
                        objectsUsingMaterial.Add(renderer.gameObject);
                        break; // 找到匹配后跳出内层循环
                    }
                }
            }

            // 检查是否按住Ctrl键（Windows）或Command键（Mac）
            bool isAdditive = Event.current != null && (Event.current.control || Event.current.command);

            int finalCount;

            if (isAdditive && objectsUsingMaterial.Count > 0)
            {
                // 加选模式：将新找到的对象添加到当前选择中
                var currentSelection = new List<Object>(Selection.objects);
                foreach (var obj in objectsUsingMaterial)
                {
                    if (!currentSelection.Contains(obj))
                    {
                        currentSelection.Add(obj);
                    }
                }
                Selection.objects = currentSelection.ToArray();
                finalCount = currentSelection.Count;
                Debug.Log($"已加选 {objectsUsingMaterial.Count} 个使用材质 '{targetMaterial.name}' 的模型，当前共选中 {finalCount} 个对象");
            }
            else if (objectsUsingMaterial.Count > 0)
            {
                // 普通模式：替换当前选择
                Selection.objects = objectsUsingMaterial.ToArray();
                finalCount = objectsUsingMaterial.Count;
                Debug.Log($"已选择 {finalCount} 个使用材质 '{targetMaterial.name}' 的模型");

                // 如果只有一个对象，聚焦到该对象
                if (finalCount == 1)
                {
                    EditorGUIUtility.PingObject(objectsUsingMaterial[0]);
                }
            }
            else
            {
                finalCount = 0;
                Debug.LogWarning($"场景中没有找到使用材质 '{targetMaterial.name}' 的模型");
            }

            // 更新选中数量 - 支持独立窗口和主窗口
            if (standaloneWindow)
            {
                // 更新独立窗口的选中数量
                standaloneWindow.StandaloneSelectedCount = finalCount;
                standaloneWindow.Repaint();
            }
            else
            {
                // 更新主窗口的全局选中数量
                VicToolsWindow mainWindow = Parent as VicToolsWindow;
                if (mainWindow)
                {
                    mainWindow.globalSelectedObjectsCount = finalCount;
                    mainWindow.Repaint();
                }
            }
        }

        /// 选择场景中选中对象的材质球
        /// 将选中对象使用的所有材质在Project窗口中选中
        private void SelectObjectsMaterial()
        {
            // 获取当前选中的所有GameObject
            var selectedGameObjects = Selection.gameObjects;
            
            if (selectedGameObjects == null || selectedGameObjects.Length == 0)
            {
                Debug.LogWarning("请先在场景中选择一个或多个对象");
                EditorUtility.DisplayDialog("提示", "请先在场景中选择一个或多个对象", "确定");
                return;
            }

            // 收集所有选中对象的材质
            var materials = new HashSet<Material>();
            
            foreach (var go in selectedGameObjects)
            {
                // 获取对象上的所有Renderer组件（包括子对象）
                var renderers = go.GetComponentsInChildren<Renderer>(true);
                
                foreach (var renderer in renderers)
                {
                    if (renderer.sharedMaterials == null) continue;
                    
                    foreach (var mat in renderer.sharedMaterials)
                    {
                        // 排除null材质
                        if (mat != null)
                        {
                            materials.Add(mat);
                        }
                    }
                }
            }

            if (materials.Count == 0)
            {
                Debug.LogWarning("选中的对象没有找到任何材质");
                EditorUtility.DisplayDialog("提示", "选中的对象没有找到任何材质", "确定");
                return;
            }

            // 在Project窗口中选中这些材质
            Selection.objects = materials.ToArray();
            
            // 高亮显示第一个材质
            if (materials.Count > 0)
            {
                EditorGUIUtility.PingObject(materials.First());
            }

            Debug.Log($"已选择 {materials.Count} 个材质球");
        }

        /// 选择多个模型，统一使用最后选择对象的材质球
        private void GetLastMat()
        {
            // 获取当前选中的所有GameObject
            var selectedGameObjects = Selection.gameObjects;

            if (selectedGameObjects == null || selectedGameObjects.Length < 2)
            {
                Debug.LogWarning("请至少选择两个对象（最后选择的对象作为材质来源）");
                EditorUtility.DisplayDialog("提示", "请至少选择两个对象\n最后选择的对象将作为材质来源", "确定");
                return;
            }

            // 获取最后选择的对象（Selection.gameObjects中最后一个）
            var lastSelectedObject = selectedGameObjects[selectedGameObjects.Length - 1];

            // 获取最后选择对象的Renderer组件
            var lastRenderer = lastSelectedObject.GetComponent<Renderer>();
            if (lastRenderer == null)
            {
                Debug.LogWarning($"最后选择的对象 '{lastSelectedObject.name}' 没有Renderer组件");
                EditorUtility.DisplayDialog("提示", $"最后选择的对象 '{lastSelectedObject.name}' 没有Renderer组件", "确定");
                return;
            }

            // 获取最后选择对象的材质
            var sourceMaterials = lastRenderer.sharedMaterials;
            if (sourceMaterials == null || sourceMaterials.Length == 0)
            {
                Debug.LogWarning($"最后选择的对象 '{lastSelectedObject.name}' 没有材质");
                EditorUtility.DisplayDialog("提示", $"最后选择的对象 '{lastSelectedObject.name}' 没有材质", "确定");
                return;
            }

            // 记录操作用于Undo
            int appliedCount = 0;

            // 将材质应用到其他所有选中的对象
            for (int i = 0; i < selectedGameObjects.Length - 1; i++)
            {
                var targetObject = selectedGameObjects[i];
                var targetRenderer = targetObject.GetComponent<Renderer>();

                if (targetRenderer != null)
                {
                    // 记录Undo操作
                    Undo.RecordObject(targetRenderer, "应用材质");

                    // 应用材质
                    targetRenderer.sharedMaterials = sourceMaterials;

                    // 标记对象为已修改
                    EditorUtility.SetDirty(targetRenderer);

                    appliedCount++;
                    Debug.Log($"已将材质应用到对象: {targetObject.name}");
                }
                else
                {
                    Debug.LogWarning($"对象 '{targetObject.name}' 没有Renderer组件，跳过");
                }
            }

            if (appliedCount > 0)
            {
                string materialNames = string.Join(", ", sourceMaterials.Where(m => m != null).Select(m => m.name));
                Debug.Log($"成功将 {appliedCount} 个对象的材质统一为: {materialNames}");
                // EditorUtility.DisplayDialog("完成", $"已将 {appliedCount} 个对象的材质统一为:\n{materialNames}", "确定");
            }
            else
            {
                Debug.LogWarning("没有对象被应用材质");
                EditorUtility.DisplayDialog("提示", "没有对象被应用材质\n请确保选中的对象有Renderer组件", "确定");
            }
        }

        /// 选择多个模型，统一使用最后选择对象的旋转（单击按钮时调用）。
        /// <para>只同步 world rotation，不动 position / scale。</para>
        private void GetLastRot() => ApplyLastTransformFromLastSelected(applyPosition: false, applyRotation: true);

        /// 选择多个模型，统一使用最后选择对象的位移（按住 Ctrl 单击按钮时调用）。
        /// <para>只同步 world position，不动 rotation / scale——和单击按钮的"只继承旋转"互补。</para>
        private void GetLastPos() => ApplyLastTransformFromLastSelected(applyPosition: true, applyRotation: false);

        /// 选择多个模型，统一使用最后选择对象的位移和旋转（同时继承两种，扩展入口）。
        /// <para>实现细节与 GetLastMat 对齐：</para>
        /// <list type="bullet">
        ///   <item>"最后选择的对象"取 Selection.gameObjects 数组末尾（与工具栏上"吸取材质"的语义保持一致）。</item>
        ///   <item>捕获该对象的 world position / world rotation 作为源。</item>
        ///   <item>把其他所有选中的对象（不包括源对象本身）的 transform.position / rotation 同步到源值。</item>
        ///   <item>每步通过 Undo.RecordObject 记录，撤销时可一键还原。</item>
        ///   <item>不修改 scale（缩放不影响"位置/旋转"的吸取语义，保持各对象原有缩放）。</item>
        /// </list>
        private void GetLastPosRot() => ApplyLastTransformFromLastSelected(applyPosition: true, applyRotation: true);

        /// 把"用 Selection.gameObjects 末尾对象作为来源，把其它选中的对象 transform 对齐"的共同实现抽出来。
        /// <param name="applyPosition">是否同步 world position。</param>
        /// <param name="applyRotation">是否同步 world rotation。</param>
        /// <remarks>两者必须至少有一个为 true，否则函数会直接返回（避免空操作）。</remarks>
        private void ApplyLastTransformFromLastSelected(bool applyPosition, bool applyRotation)
        {
            // 防御：两个 flag 都为 false 等于"什么都不做"，应当早返回
            if (!applyPosition && !applyRotation)
            {
                Debug.LogWarning("ApplyLastTransformFromLastSelected: applyPosition 和 applyRotation 都为 false，无任何操作可执行");
                return;
            }

            // 根据模式生成统一的 Undo 分组名、RecordObject Op 名、完成摘要文案
            string groupName;
            string opName;
            string summary;
            if (applyPosition && applyRotation)
            {
                groupName = "吸取位置/旋转";
                opName = "应用位置/旋转";
                summary = "位置/旋转";
            }
            else if (applyPosition)
            {
                groupName = "吸取位置";
                opName = "应用位置";
                summary = "位移";
            }
            else // applyRotation
            {
                groupName = "吸取旋转";
                opName = "应用旋转";
                summary = "旋转";
            }

            // 获取当前选中的所有GameObject
            var selectedGameObjects = Selection.gameObjects;

            if (selectedGameObjects == null || selectedGameObjects.Length < 2)
            {
                Debug.LogWarning("请至少选择两个对象（最后选择的对象作为来源）");
                EditorUtility.DisplayDialog("提示", "请至少选择两个对象\n最后选择的对象将作为来源", "确定");
                return;
            }

            // 获取最后选择的对象（与 GetLastMat 一致：用数组末尾元素）
            var lastSelectedObject = selectedGameObjects[selectedGameObjects.Length - 1];
            if (lastSelectedObject == null)
            {
                Debug.LogWarning("最后选择的对象为空");
                EditorUtility.DisplayDialog("提示", "最后选择的对象为空", "确定");
                return;
            }

            // 捕获源对象的 world position / rotation（只读一次，避免循环中被同批目标干扰）
            var srcTransform = lastSelectedObject.transform;
            Vector3 srcPos = srcTransform.position;
            Quaternion srcRot = srcTransform.rotation;

            int appliedCount = 0;
            int skippedCount = 0;

            // Undo 分组：把整批"应用 transform"折叠为单步 Undo，便于一键还原全部目标
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(groupName);

            // 把其他所有选中的对象的 transform 对齐到源对象的 transform（不含源对象本身）
            for (int i = 0; i < selectedGameObjects.Length - 1; i++)
            {
                var targetObject = selectedGameObjects[i];
                if (targetObject == null) { skippedCount++; continue; }
                var targetTransform = targetObject.transform;

                // 在赋值前记录 Undo，让 Undo 能正确回滚
                Undo.RecordObject(targetTransform, opName);

                // 按 flag 决定同步哪些字段：单击继承旋转，Ctrl+Click 继承位移，两者都开就都同步
                if (applyPosition) targetTransform.position = srcPos;
                if (applyRotation) targetTransform.rotation = srcRot;

                // 标记对象已修改，确保场景脏标记、跨域重载后仍生效
                EditorUtility.SetDirty(targetObject);

                appliedCount++;
                Debug.Log($"已将 '{targetObject.name}' 的{summary}对齐到 '{lastSelectedObject.name}'");
            }

            // 折叠为单步 Undo
            Undo.CollapseUndoOperations(undoGroup);

            if (appliedCount > 0)
            {
                // 日志里只输出真正应用了的字段，节省控制台空间
                string detail;
                if (applyPosition && applyRotation) detail = $"pos={srcPos}, rotEuler={srcRot.eulerAngles}";
                else if (applyPosition) detail = $"pos={srcPos}";
                else detail = $"rotEuler={srcRot.eulerAngles}";

                Debug.Log($"[VicTools] 已成功将 {appliedCount} 个对象的{summary}统一为 '{lastSelectedObject.name}'" +
                          $"（{detail}）" +
                          (skippedCount > 0 ? $"；跳过 {skippedCount} 个空对象" : ""));
            }
            else
            {
                Debug.LogWarning($"没有对象被应用{summary}");
                EditorUtility.DisplayDialog("提示", $"没有对象被应用{summary}\n请检查选中对象是否有效", "确定");
            }
        }

        /// 将selectedMaterial材质放入资源箱
        private void OnlySelectedMaterialToResourceBox()
        {
            Material materialToAdd = null;
            // 检查场景中是否选择了物体
            if (Selection.activeGameObject)
            {
                // 获取选择物体的Renderer组件
                var selectedRenderer = Selection.activeGameObject.GetComponent<Renderer>();
                if (selectedRenderer)
                {
                    // 获取选择物体的材质
                    Material[] selectedMaterials = selectedRenderer.sharedMaterials;
                    if (selectedMaterials != null && selectedMaterials.Length > 0 && selectedMaterials[0] != null)
                    {
                        // 使用第一个材质作为要添加的材质
                        materialToAdd = selectedMaterials[0];
                        Debug.Log($"将场景中选中第一个对象材质 '{materialToAdd.name}' 放入资源箱");
                    }
                }
            }

            // 如果仍然没有材质，显示警告
            if (!materialToAdd)
            {
                Debug.LogWarning("无法放入材质：selectedMaterial为空且选中对象没有材质");
                EditorUtility.DisplayDialog("放入失败", "请先在材质选择框中选择一个材质，或者选择一个有材质的对象", "确定");
                return;
            }

            // 检查材质是否已经在资源箱中
            if (_resourceBox.Contains(materialToAdd))
            {
                // 如果材质已经在资源箱中，选中该材质对象
                Selection.activeObject = materialToAdd;
                EditorGUIUtility.PingObject(materialToAdd);
                Debug.Log($"材质 '{materialToAdd.name}' 已经在资源箱中，已选中该材质");
                return;
            }

            // 检查对象是否具有kDontSaveInEditor标志，如果有则清除它
            if (((int)materialToAdd.hideFlags & (int)HideFlags.DontSaveInEditor) != 0)
            {
                materialToAdd.hideFlags &= ~HideFlags.DontSaveInEditor;
                Debug.Log($"已清除材质 {materialToAdd.name} 的DontSaveInEditor标志");
            }

            // 将材质按类型排序添加到资源箱
            InsertToResourceBoxByType(materialToAdd);
            
            // 强制重绘窗口
            if (Parent)
                Parent.Repaint();

            // 立即保存资源箱数据（自动保存，不显示提示框）
            SaveResourceBox(false);

            Debug.Log($"已将材质 '{materialToAdd.name}' 放入资源箱");
        }
        
        /// 将selectedMaterial材质放入资源箱
        private void PutSelectedMaterialToResourceBox()
        {
            Material materialToAdd;

            // 逻辑：当selectedMaterial不为空时，将selectedMaterial中的材质放入资源箱
            // 当selectedMaterial为空时，将场景中选中第一个对象材质放入资源箱
            if (_selectedMaterial)
            {
                materialToAdd = _selectedMaterial;
                Debug.Log($"将selectedMaterial中的材质 '{materialToAdd.name}' 放入资源箱");
            }else
            {
                Debug.LogWarning("无法放入材质：selectedMaterial为空");
                return;
            }

            // 检查材质是否已经在资源箱中
            if (_resourceBox.Contains(materialToAdd))
            {
                // Debug.LogWarning($"材质 '{materialToAdd.name}' 已经在资源箱中");
                EditorUtility.DisplayDialog("重复材质", $"材质 '{materialToAdd.name}' 已经在资源箱中", "确定");
                return;
            }

            // 检查对象是否具有kDontSaveInEditor标志，如果有则清除它
            if (((int)materialToAdd.hideFlags & (int)HideFlags.DontSaveInEditor) != 0)
            {
                materialToAdd.hideFlags &= ~HideFlags.DontSaveInEditor;
                Debug.Log($"已清除材质 {materialToAdd.name} 的DontSaveInEditor标志");
            }

            // 将材质添加到资源箱
            _resourceBox.Add(materialToAdd);
            
            // 计算并保存displayName到字典
            var parentName = GetTopLevelParentName(materialToAdd);
            var displayName = !string.IsNullOrEmpty(parentName)
                ? $"<{GetTypeDisplayName(materialToAdd)}> [{parentName}]← {materialToAdd.name}"
                : $"<{GetTypeDisplayName(materialToAdd)}> {materialToAdd.name}";
            _resourceBoxDisplayNames[materialToAdd] = displayName;
            
            // 强制重绘窗口
            if (Parent)
                Parent.Repaint();

            // 立即保存资源箱数据
            SaveResourceBox(false);

            Debug.Log($"已将材质 '{materialToAdd.name}' 放入资源箱");
        }

        /// 保存资源箱数据到全局文件（优化版本，解决重名问题）
        /// <param name="showConfirmation">是否显示确认提示框</param>
        /// 供 ResourceBoxBuildPreprocessor 在打包前调用的保存方法
        internal void SaveResourceBoxBeforeBuild()
        {
            // 打包前保存，此时对象引用应该还有效
            SaveResourceBox(false);
            Debug.Log("[ResourceBox] 打包前已保存资源箱数据");
        }
        
        private void SaveResourceBox(bool showConfirmation = true)
        {
            // 如果需要显示确认提示框，则显示
            if (showConfirmation)
            {
                var confirm = EditorUtility.DisplayDialog(
                    "确认保存",
                    $"确定要保存资源箱中的 {_resourceBox.Count} 个对象吗？",
                    "确定保存",
                    "取消"
                );

                if (!confirm)
                {
                    Debug.Log("用户取消了保存操作");
                    return;
                }
            }

            // Domain Reload 保护：如果资源箱中有场景对象记录，但大部分都变成了null，
            // 说明正在经历 Domain Reload（打包/进入PlayMode等），此时不应覆盖已保存的数据
            if (_resourceBox != null && _resourceBox.Count > 0)
            {
                int nullCount = 0;
                int sceneObjectCount = 0;
                int assetObjectCount = 0;
                for (int i = 0; i < _resourceBox.Count; i++)
                {
                    var checkObj = _resourceBox[i];
                    if (!checkObj)
                    {
                        // 如果之前有保存过InstanceID（说明这个位置原来是有效的场景对象）
                        if (_resourceBoxInstanceIDs.ContainsKey(i))
                        {
                            nullCount++;
                        }
                    }
                    else
                    {
                        var assetPath = AssetDatabase.GetAssetPath(checkObj);
                        if (string.IsNullOrEmpty(assetPath))
                        {
                            sceneObjectCount++;
                        }
                        else
                        {
                            assetObjectCount++;
                        }
                    }
                }
                
                // 如果之前有场景对象记录变成了null，说明可能是 Domain Reload 导致的
                // 此时不应覆盖已保存的数据（因为 null 对象无法正确序列化标识符）
                if (nullCount >= 1 && sceneObjectCount == 0)
                {
                    Debug.Log($"[ResourceBox] 检测到可能的 Domain Reload（{nullCount}个场景对象变为null），跳过保存以保护已有数据");
                    return;
                }
            }

            // 创建资源箱数据列表，保存所有对象（包括null对象）
            var resourceBoxData = new List<ResourceBoxItem>();
            
            if (_resourceBox != null)
            {
                for (int i = 0; i < _resourceBox.Count; i++)
                {
                    var obj = _resourceBox[i];
                    
                    if (obj)
                    {
                        // 检查对象是否具有HideAndDontSave标志（真正的临时对象），跳过保存
                        // 注意：不检查DontSaveInEditor，因为打包期间正常对象可能临时带有此标志
                        if ((obj.hideFlags & HideFlags.HideAndDontSave) == HideFlags.HideAndDontSave)
                        {
                            Debug.LogWarning($"对象 {obj.name} 具有HideAndDontSave标志，跳过保存");
                            continue;
                        }

                        // 获取对象的全局唯一标识符
                        string guid;
                        var assetPath = AssetDatabase.GetAssetPath(obj);

                        // v2.28：记录场景对象所属的场景信息，用于场景重载时正确恢复
                        string scenePath = "";
                        string sceneName = "";
                        string sceneGuid = "";

                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            // 如果是项目资源，使用GUID（持久化）
                            guid = AssetDatabase.AssetPathToGUID(assetPath);
                        }
                        else
                        {
                            // 如果是场景对象，使用优化的唯一标识符
                            if (obj is GameObject gameObject)
                            {
                                // 收集场景信息
                                scenePath = gameObject.scene.path;
                                sceneName = gameObject.scene.name;
                                if (!string.IsNullOrEmpty(scenePath))
                                {
                                    sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                                }

                                // 使用新的唯一标识符系统
                                guid = GetGameObjectUniqueIdentifier(gameObject);
                            }
                            else
                            {
                                // 其他场景对象使用GlobalObjectId（持久化，Domain Reload后仍有效）
                                var globalId = GlobalObjectId.GetGlobalObjectIdSlow(obj);
                                if (globalId.identifierType != 0)
                                {
                                    guid = $"GLOBALID:{globalId.ToString()}";
                                }
                                else
                                {
                                    // 回退到实例ID（临时，但附带名称用于恢复）
                                    guid = $"INSTANCE:{VicToolsUnityCompat.GetReversibleId(obj)}:{obj.name}";
                                }

                                // 也尝试记录场景信息（非 GameObject 也可能具有 scene 字段）
                                if (obj is Component component)
                                {
                                    scenePath = component.gameObject.scene.path;
                                    sceneName = component.gameObject.scene.name;
                                    if (!string.IsNullOrEmpty(scenePath))
                                    {
                                        sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                                    }
                                }
                            }
                        }

                        // 计算displayName（与DrawResourceBoxSection中相同的逻辑）
                        string displayName;
                        var parentName = GetTopLevelParentName(obj);
                        displayName = !string.IsNullOrEmpty(parentName)
                            ? $"<{GetTypeDisplayName(obj)}> [{parentName}]← {obj.name}"
                            : $"<{GetTypeDisplayName(obj)}> {obj.name}";

                        resourceBoxData.Add(new ResourceBoxItem
                        {
                            guid = guid,
                            type = obj.GetType().FullName,
                            name = obj.name,
                            isAsset = !string.IsNullOrEmpty(assetPath),
                            displayName = displayName,  // 保存displayName
                            scenePath = scenePath,
                            sceneName = sceneName,
                            sceneGuid = sceneGuid
                        });
                    }
                    else
                    {
                        // 处理null对象
                        // 尝试从null对象字典中获取displayName
                        string displayName;
                        // v2.43: 同时从 raw 缓存取 type/name，用于持久化到 JSON 的 ResourceBoxItem.type/name 字段，
                        // 避免下次加载读不到任何信息（之前只用 _nullObjectDisplayNames 的旧 displayName，丢了结构化数据）
                        string nullType = null;
                        string nullName = null;
                        string scenePath = "";
                        string sceneName = "";
                        string sceneGuid = "";
                        if (_nullObjectDisplayNames.TryGetValue(i, out string savedDisplayName))
                        {
                            displayName = savedDisplayName;
                        }
                        else
                        {
                            // 没有保存的displayName：优先用 _nullObjectRawItems / _recoveryRawItems 的 type/name 重建
                            if (_nullObjectRawItems.TryGetValue(i, out var rawItem) && rawItem != null)
                            {
                                nullType = rawItem.type;
                                nullName = rawItem.name;
                            }
                            else if (_recoveryRawItems != null && i < _recoveryRawItems.Count && _recoveryRawItems[i] != null)
                            {
                                nullType = _recoveryRawItems[i].type;
                                nullName = _recoveryRawItems[i].name;
                            }
                            displayName = BuildFallbackDisplayName(nullType, nullName);
                        }
                        // 与正常路径一致：保存时把 raw 的 type/name 写入 ResourceBoxItem，让下次加载能恢复结构化信息
                        if (nullType == null && nullName == null)
                        {
                            if (_nullObjectRawItems.TryGetValue(i, out var rawItem2) && rawItem2 != null)
                            {
                                nullType = rawItem2.type; nullName = rawItem2.name;
                            }
                            else if (_recoveryRawItems != null && i < _recoveryRawItems.Count && _recoveryRawItems[i] != null)
                            {
                                nullType = _recoveryRawItems[i].type; nullName = _recoveryRawItems[i].name;
                            }
                        }

                        // 如果之前保存过该索引对应的 InstanceID，尝试反查对应的 scene 信息
                        if (_resourceBoxInstanceIDs.TryGetValue(i, out int savedIID))
                        {
                            // 旧路径：直接通过 InstanceIDToObject 拿到对象（可能已失效），但仍可获取场景
                            var recoverObj = VicToolsEditorCompat.IdToObject(savedIID);
                            if (recoverObj is GameObject go)
                            {
                                scenePath = go.scene.path;
                                sceneName = go.scene.name;
                                if (!string.IsNullOrEmpty(scenePath))
                                {
                                    sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                                }
                            }
                            else if (recoverObj is Component comp)
                            {
                                scenePath = comp.gameObject.scene.path;
                                sceneName = comp.gameObject.scene.name;
                                if (!string.IsNullOrEmpty(scenePath))
                                {
                                    sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                                }
                            }
                        }

                        // 对于null对象，使用特殊的标识符
                        // v2.28: 优先从 _nullObjectRawItems 恢复原始场景信息和原始 guid
                        string nullObjectGuid = "NULL:OBJECT";
                        if (_nullObjectRawItems.TryGetValue(i, out var nullRaw) && nullRaw != null)
                        {
                            if (string.IsNullOrEmpty(sceneGuid))
                            {
                                scenePath = nullRaw.scenePath ?? "";
                                sceneName = nullRaw.sceneName ?? "";
                                sceneGuid = nullRaw.sceneGuid ?? "";
                            }
                            if (!string.IsNullOrEmpty(nullRaw.guid))
                            {
                                nullObjectGuid = nullRaw.guid;
                            }
                        }
                        // v2.43 修复：之前硬编码 type="System.Null" / name="NullObject"，
// 即使 _nullObjectRawItems / _recoveryRawItems 里有真实类型/名称（GameObject Cube 等）也会被覆盖，
// 下次加载时拿到的是无意义的 "System.Null" / "NullObject"，按类型排序、恢复、显示全部失效。
// 改为：有 raw 时用 raw 的真实 type/name；都没有才回退到旧占位。
string savedType = !string.IsNullOrEmpty(nullType) ? nullType : "System.Null";
string savedName = !string.IsNullOrEmpty(nullName) ? nullName : "NullObject";
resourceBoxData.Add(new ResourceBoxItem
                        {
                            guid = nullObjectGuid,
                            type = savedType,
                            name = savedName,
                            isAsset = false,
                            displayName = displayName,
                            scenePath = scenePath,
                            sceneName = sceneName,
                            sceneGuid = sceneGuid
                        });
                    }
                }
            }

            // 序列化数据
            var jsonData = JsonUtility.ToJson(new ResourceBoxData { 
                items = resourceBoxData
            });

            // v2.28: 同步 _recoveryRawItems，确保后续场景事件能正确恢复
            _recoveryRawItems = ConvertToRecoveryList(resourceBoxData);

            // 保存到全局文件
            SaveResourceBoxToGlobalFile(jsonData);
        }
        
        /// 保存资源箱数据到全局文件
        /// <param name="jsonData">JSON格式的资源箱数据</param>
        private void SaveResourceBoxToGlobalFile(string jsonData)
        {
            try
            {
                // 使用PathHelper获取正确的全局文件路径
                var globalResourceBoxPath = PathHelper.GetGlobalResourceBoxPath();
                
                // 确保目录存在
                var directoryPath = System.IO.Path.GetDirectoryName(globalResourceBoxPath);
                if (!string.IsNullOrEmpty(directoryPath) && !System.IO.Directory.Exists(directoryPath))
                {
                    System.IO.Directory.CreateDirectory(directoryPath);
                }
                
                // 保存到文件
                System.IO.File.WriteAllText(globalResourceBoxPath, jsonData);
                Debug.Log($"资源箱数据已保存到全局文件: {globalResourceBoxPath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"保存资源箱数据到全局文件时出错: {e.Message}");
                // 如果保存到文件失败，回退到EditorPrefs
                EditorPrefs.SetString("VicTools_ScenesTools_ResourceBox", jsonData);
                Debug.LogWarning($"已回退到EditorPrefs保存资源箱数据");
            }
        }

        /// 从全局文件加载资源箱数据（优化版本，解决重名问题）
        private void LoadResourceBox()
        {
            string jsonData = "";
            
            // 使用PathHelper获取正确的全局文件路径
            var globalResourceBoxPath = PathHelper.GetGlobalResourceBoxPath();
            
            // 首先尝试从全局文件加载
            if (System.IO.File.Exists(globalResourceBoxPath))
            {
                try
                {
                    jsonData = System.IO.File.ReadAllText(globalResourceBoxPath);
                    Debug.Log($"从全局文件加载资源箱数据: {globalResourceBoxPath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"读取全局文件时出错: {e.Message}");
                    // 如果读取文件失败，回退到EditorPrefs
                    jsonData = EditorPrefs.GetString("VicTools_ScenesTools_ResourceBox", "");
                    Debug.LogWarning($"已回退到EditorPrefs加载资源箱数据");
                }
            }
            else
            {
                // 如果全局文件不存在，尝试从EditorPrefs加载（向后兼容）
                jsonData = EditorPrefs.GetString("VicTools_ScenesTools_ResourceBox", "");
                Debug.Log($"全局文件不存在，从EditorPrefs加载资源箱数据");
            }
            
            if (string.IsNullOrEmpty(jsonData))
            {
                // 没有保存的数据
                return;
            }

            try
            {
                var resourceBoxData = JsonUtility.FromJson<ResourceBoxData>(jsonData);
                
                if (resourceBoxData == null)
                {
                    return;
                }

                // 加载资源箱对象
                _resourceBox.Clear();
                _nullObjectDisplayNames.Clear(); // 清空null对象显示名称字典
                _resourceBoxInstanceIDs.Clear(); // 清空InstanceID映射
                _instanceIDToDisplayName.Clear(); // 清空InstanceID到displayName的映射
                _nullObjectRawItems.Clear(); // v2.28 清空 null 原始项缓存
                _recoveryRawItems = null; // v2.28 重置恢复缓存
                var hasSceneObjectChanges = false; // 标记是否有场景对象变化
                var hasDataInconsistency = false; // 标记数据不一致（用于内部逻辑）

                if (resourceBoxData.items != null)
                {
                    foreach (var item in resourceBoxData.items)
                    {
                        Object obj = null;
                        var debugInfo = ""; // 添加debugInfo变量定义
                        
                        if (item.isAsset)
                        {
                            // 加载项目资源
                            var assetPath = AssetDatabase.GUIDToAssetPath(item.guid);
                            if (!string.IsNullOrEmpty(assetPath))
                            {
                                obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                                debugInfo = $"项目资源: {item.name} (GUID: {item.guid})";
                                
                                // 检查项目资源是否仍然存在且类型匹配
                                if (obj && obj.GetType().FullName != item.type)
                                {
                                    Debug.LogWarning($"项目资源类型不匹配: 期望={item.type}, 实际={obj.GetType().FullName}");
                                    hasDataInconsistency = true;
                                }
                            }
                            else
                            {
                                debugInfo = $"项目资源未找到: {item.name} (GUID: {item.guid})";
                                hasDataInconsistency = true;
                            }
                        }
                        else
                        {
                            // 处理场景对象 - 使用新的标识符系统
                            if (item.guid.StartsWith("ROOT:") || item.guid.StartsWith("SCENE:"))
                            {
                                // 使用新的FindGameObjectByIdentifier方法查找对象
                                obj = FindGameObjectByIdentifier(item.guid);
                                debugInfo = $"场景对象: {item.name} (标识符: {item.guid})";
                                
                                // 检查是否需要更新标识符
                                if (obj)
                                {
                                    var currentIdentifier = GetGameObjectUniqueIdentifier(obj as GameObject);
                                    if (currentIdentifier != item.guid)
                                    {
                                        // 标识符发生变化，标记需要更新
                                        hasSceneObjectChanges = true;
                                        debugInfo += $" → 标识符已更新: {currentIdentifier}";
                                    }
                                    // else
                                    // {
                                    //     Debug.Log($"场景对象标识符匹配: {currentIdentifier}");
                                    // }
                                    
                                    // 检查场景对象类型是否匹配
                                    if (obj.GetType().FullName != item.type)
                                    {
                                        Debug.LogWarning($"场景对象类型不匹配: 期望={item.type}, 实际={obj.GetType().FullName}");
                                        hasDataInconsistency = true;
                                    }
                                }
                                    
                                if (obj == null)
                                {
                                    // v2.28：通过场景信息在已加载场景中再次尝试查找
                                    if (TryFindGameObjectBySceneInfo(item, out obj))
                                    {
                                        debugInfo += $" → 通过场景信息找到: {obj.name} (场景: {item.sceneName})";
                                    }
                                    else
                                    {
                                        // 如果通过标识符找不到，尝试通过实例ID查找
                                        var parts = item.guid.Split(':');
                                        if (parts.Length >= 4 && int.TryParse(parts[3], out int instanceID))
                                        {
                                            // 使用InstanceIDToObject查找对象
                                            var instanceObj = VicToolsEditorCompat.IdToObject(instanceID);
                                            if (instanceObj && instanceObj is GameObject gameObject)
                                            {
                                                // 验证场景匹配和名称匹配（防止Domain Reload后InstanceID被分配给不同对象）
                                                var currentScene = SceneManager.GetActiveScene();
                                                if (gameObject.scene == currentScene && gameObject.name == item.name)
                                                {
                                                    obj = gameObject;
                                                    debugInfo += $" → 通过实例ID找到: {obj.name} (InstanceID: {instanceID})";
                                                }
                                                else if (gameObject.scene == currentScene)
                                                {
                                                    // 场景匹配但名称不匹配，InstanceID可能已被重新分配
                                                    debugInfo += $" → 实例ID指向不同对象: 期望'{item.name}'，实际'{gameObject.name}'，已忽略";
                                                }
                                                else
                                                {
                                                    debugInfo += $" → 实例ID对象场景不匹配: {instanceID}";
                                                }

                                                hasDataInconsistency = true;
                                            }
                                            else
                                            {
                                                debugInfo += $" → 实例ID查找失败: {instanceID}";
                                                hasDataInconsistency = true;
                                            }
                                        }
                                        else
                                        {
                                            hasDataInconsistency = true;
                                        }
                                    }
                                }
                            }
                            else if (item.guid.StartsWith("GLOBALID:"))
                            {
                                // GlobalObjectId对象：使用GlobalObjectId查找（持久化标识符）
                                var globalIdStr = item.guid.Substring("GLOBALID:".Length);
                                if (GlobalObjectId.TryParse(globalIdStr, out GlobalObjectId globalId))
                                {
                                    var globalObj = GlobalObjectId.GlobalObjectIdentifierToObjectSlow(globalId);
                                    if (globalObj)
                                    {
                                        obj = globalObj;
                                        debugInfo = $"GlobalObjectId对象: {item.name} (ID: {globalIdStr})";
                                    }
                                    else
                                    {
                                        debugInfo = $"GlobalObjectId查找失败: {item.name} (ID: {globalIdStr})";
                                        hasDataInconsistency = true;
                                    }
                                }
                                else
                                {
                                    debugInfo = $"GlobalObjectId格式错误: {item.name} (标识符: {item.guid})";
                                    hasDataInconsistency = true;
                                }
                            }
                            else if (item.guid.StartsWith("INSTANCE:"))
                            {
                                // 实例ID对象：尝试通过实例ID查找（兼容旧格式和新格式 INSTANCE:{id}:{name}）
                                var instancePart = item.guid.Substring("INSTANCE:".Length);
                                // 新格式可能包含名称: "12345:ObjectName"
                                var colonIdx = instancePart.IndexOf(':');
                                var idStr = colonIdx >= 0 ? instancePart.Substring(0, colonIdx) : instancePart;
                                var savedName = colonIdx >= 0 ? instancePart.Substring(colonIdx + 1) : item.name;
                                
                                if (int.TryParse(idStr, out int instanceID))
                                {
                                    var instanceObj = VicToolsEditorCompat.IdToObject(instanceID);
                                    if (instanceObj)
                                    {
                                        // 验证场景匹配（如果是GameObject）
                                        if (instanceObj is GameObject gameObject)
                                        {
                                            var currentScene = SceneManager.GetActiveScene();
                                            if (gameObject.scene == currentScene)
                                            {
                                                obj = gameObject;
                                                debugInfo = $"实例对象: {item.name} (InstanceID: {instanceID})";
                                            }
                                            else
                                            {
                                                debugInfo = $"实例ID对象场景不匹配: {item.name} (InstanceID: {instanceID})";
                                            }
                                        }
                                        else
                                        {
                                            // 非GameObject对象（如Component），直接使用
                                            obj = instanceObj;
                                            debugInfo = $"实例对象(非GO): {item.name} (InstanceID: {instanceID})";
                                        }
                                    }
                                    else
                                    {
                                        // InstanceID失效，尝试通过名称在场景中查找
                                        if (!string.IsNullOrEmpty(savedName))
                                        {
                                            var foundByName = FindObjectByNameInCurrentScene(savedName);
                                            if (foundByName)
                                            {
                                                obj = foundByName;
                                                debugInfo = $"通过名称恢复实例对象: {savedName}";
                                                hasDataInconsistency = true;
                                            }
                                            else
                                            {
                                                debugInfo = $"实例ID查找失败且名称未匹配: {item.name} (InstanceID: {instanceID})";
                                            }
                                        }
                                        else
                                        {
                                            debugInfo = $"实例ID查找失败: {item.name} (InstanceID: {instanceID})";
                                        }
                                    }

                                    hasDataInconsistency = true; // 使用实例ID说明需要更新为新的标识符系统
                                }
                                else
                                {
                                    debugInfo = $"实例ID格式错误: {item.name} (标识符: {item.guid})";
                                    hasDataInconsistency = true;
                                }
                            }
                            else if (item.guid == "NULL:OBJECT")
                            {
                                // 处理null对象
                                obj = null;
                                debugInfo = $"null对象: {item.displayName}";
                                // v2.28: 缓存 null 原始项，便于场景打开时再次尝试恢复
                                if (!item.isAsset && (!string.IsNullOrEmpty(item.sceneGuid) || !string.IsNullOrEmpty(item.sceneName)))
                                {
                                    int nullItemIdx = _resourceBox.Count; // 即将添加的索引
                                    _nullObjectRawItems[nullItemIdx] = ConvertToRecoveryItem(item);
                                }
                            }
                            else
                            {
                                // 向后兼容：处理旧的SCENE:格式标识符
                                if (item.guid.StartsWith("SCENE:"))
                                {
                                    var parts = item.guid.Split(':');
                                    if (parts.Length >= 3)
                                    {
                                        var fullPath = parts[2];
                                        
                                        // 尝试使用旧的方法查找
                                        obj = FindGameObjectByPath(fullPath);
                                        debugInfo = $"旧格式场景对象: {item.name} (路径: {fullPath})";
                                        hasDataInconsistency = true; // 旧格式需要更新
                                    }
                                }
                                else
                                {
                                    debugInfo = $"未知标识符格式: {item.name} (标识符: {item.guid})";
                                    hasDataInconsistency = true;
                                }
                            }
                        }

                        // 即使对象为null，也添加到资源箱，以便显示对象名称
                        if (obj)
                        {
                            // 检查对象是否已经存在于资源箱中（防止重复添加）
                            if (_resourceBox.Contains(obj))
                            {
                                Debug.LogWarning($"✗ 对象已存在于资源箱中，跳过重复添加: {obj.name}");
                                hasDataInconsistency = true;
                                continue;
                            }
                            
                            // 额外的安全检查：确保对象在添加到资源箱前是有效的
                            if (obj && !obj.Equals(null))
                            {
                                _resourceBox.Add(obj);
                                
                                // 保存displayName到字典（如果ResourceBoxItem中有displayName字段）
                                if (!string.IsNullOrEmpty(item.displayName))
                                {
                                    _resourceBoxDisplayNames[obj] = item.displayName;
                                }
                                // Debug.Log($"✓ 成功加载对象: {debugInfo}");
                            }
                            else
                            {
                                Debug.LogWarning($"✗ 对象无效，跳过添加: {debugInfo}");
                                hasDataInconsistency = true;
                            }
                        }
                        else
                        {
                            // 对象为null，但仍然添加到资源箱以便显示
                            _resourceBox.Add(null);
                            hasDataInconsistency = true;
                            
                            // 保存displayName到null对象字典，使用当前索引作为键
                            if (!string.IsNullOrEmpty(item.displayName))
                            {
                                _nullObjectDisplayNames[_resourceBox.Count - 1] = item.displayName;
                            }
                            else
                            {
                                // v2.43: 用 BuildFallbackDisplayName 统一兜底，保留 item.type（v2.41 起 ResourceBoxFileItem.type 已是字段），
                                // 让 null 对象按真实类型参与排序/恢复，不再硬编码"<未知类型> {item.name}"。
                                _nullObjectDisplayNames[_resourceBox.Count - 1] = BuildFallbackDisplayName(item.type, item.name);
                            }
                            
                            // Debug.LogWarning($"✗ 无法加载对象，但保留显示名称: {debugInfo}");
                        }
                    }
                }

                // Debug.Log($"资源箱数据已加载，成功恢复 {loadedCount} 个对象");

                // v2.28: 设置 _recoveryRawItems 为当前加载的 items，便于场景打开时直接恢复
                _recoveryRawItems = ConvertToRecoveryList(resourceBoxData.items);

                // 检查是否有场景对象变化，如果有则自动保存更新数据
                // 注意：仅在成功恢复了对象时才保存，避免将null状态覆盖已保存的有效数据
                if (hasSceneObjectChanges)
                {
                    Debug.Log($"检测到场景对象标识符变化，自动更新资源箱数据...");
                    SaveResourceBox(false);
                }
                else if (hasDataInconsistency)
                {
                    // 数据不一致但未成功恢复对象，仅记录日志不覆盖保存
                    Debug.Log("[ResourceBox] 部分对象未能恢复，保留已保存数据不覆盖");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"加载资源箱数据时出错: {e.Message}");
                // 在出错时清空资源箱，避免使用损坏的数据
                _resourceBox.Clear();
                _nullObjectDisplayNames.Clear(); // 清空null对象显示名称字典
                _resourceBoxInstanceIDs.Clear(); // 清空InstanceID映射
                _instanceIDToDisplayName.Clear(); // 清空InstanceID到displayName的映射
                _nullObjectRawItems.Clear(); // v2.28 清空 null 原始项缓存
                _recoveryRawItems = null;
            }
        }

        /// 从displayName中提取原始对象名称
        private string ExtractOriginalNameFromDisplayName(string displayName)
        {
            // displayName格式示例: 
            // 1. "<GameObject> [Parent]← ObjectName" - 提取"ObjectName"
            // 2. "<GameObject> ObjectName" - 提取"ObjectName"
            // 3. "<GameObject> Object Name With Spaces" - 提取"Object Name With Spaces"
            
            // 查找"← "分隔符（注意包含空格）
            int arrowIndex = displayName.LastIndexOf("← ");
            if (arrowIndex != -1)
            {
                // 提取箭头后面的部分
                return displayName.Substring(arrowIndex + 2).Trim();
            }
            
            // 如果没有箭头，查找第一个"> "后的内容
            int typeEndIndex = displayName.IndexOf("> ");
            if (typeEndIndex != -1)
            {
                // 提取类型后面的部分
                string afterType = displayName.Substring(typeEndIndex + 2).Trim();
                
                // 检查是否有方括号父对象标记
                int bracketStart = afterType.IndexOf('[');
                if (bracketStart != -1)
                {
                    // 有父对象标记，查找"]← "分隔符
                    int bracketArrowIndex = afterType.LastIndexOf("]← ");
                    if (bracketArrowIndex != -1)
                    {
                        return afterType.Substring(bracketArrowIndex + 3).Trim();
                    }
                }
                else
                {
                    // 没有父对象标记，直接返回类型后面的所有内容
                    return afterType;
                }
            }
            
            // 如果以上方法都失败，尝试查找最后一个空格（备用方案）
            int spaceIndex = displayName.LastIndexOf(' ');
            if (spaceIndex != -1 && spaceIndex > 0)
            {
                // 确保不是提取类型名（如"GameObject"）
                string beforeSpace = displayName.Substring(0, spaceIndex);
                if (!beforeSpace.EndsWith(">") && !beforeSpace.EndsWith("]"))
                {
                    return displayName.Substring(spaceIndex + 1).Trim();
                }
            }
            
            return displayName;
        }

        /// 从displayName中同时提取 (基础类型名, 来源后缀)：
        ///   "<GameObject> Cube"             → ("GameObject", "")
        ///   "<GameObject·Scene> Cube"       → ("GameObject", "Scene")
        ///   "<GameObject·PrefabInstance> X" → ("GameObject", "PrefabInstance")
        ///   "<GameObject·Prefab> X"         → ("GameObject", "Prefab")
        ///   "<Material·Asset> Wood"         → ("Material",   "Asset")
        ///   "<未知类型> ..."                 → ("",           "")
        /// v3.4 新增：替代 v2.41 的 ExtractTypeFromDisplayName，把来源后缀也带出来用于【按类型排序】子桶。
        ///           同时向后兼容旧存档 "<GameObject> ..." 无后缀格式（sourceTag 留空 → GameObject·Scene 桶）。
        private (string baseType, string sourceTag) ExtractTypeAndSourceFromDisplayName(string displayName)
        {
            if (string.IsNullOrEmpty(displayName)) return ("", "");
            // 提取尖括号里的内容：<...>
            int start = displayName.IndexOf('<');
            if (start < 0) return ("", "");
            int end = displayName.IndexOf('>', start + 1);
            if (end < 0 || end <= start + 1) return ("", "");
            string raw = displayName.Substring(start + 1, end - start - 1).Trim();
            // 按 · 切分：左边是基础类型名，右边是来源后缀（不存在时为空）
            int sep = raw.IndexOf('·');
            if (sep <= 0) return (raw, "");
            return (raw.Substring(0, sep).Trim(), raw.Substring(sep + 1).Trim());
        }

        /// v2.41 兼容：从displayName中提取类型名（如"<GameObject> Cube" -> "GameObject"）。
        /// 现已委托给 ExtractTypeAndSourceFromDisplayName，丢弃 sourceTag。
        /// 若无法识别（如"<未知类型> ..."）则返回空串，调用方会回退到 999。
        private string ExtractTypeFromDisplayName(string displayName)
        {
            return ExtractTypeAndSourceFromDisplayName(displayName).baseType;
        }

        /// v2.41 新增：根据类型名字符串返回排序优先级（与 GetTypeOrder(Object) 一致的优先级表）。
        /// 用于暂时未知的灰色对象（obj == null）按其保存的真实类型参与【按类型排序】，
        /// 让"暂时找不到对象的 GameObject / Material / Mesh..."与其对应类型的有效对象聚在一起，
        /// 而非被统一归到 999 排到末尾。无法识别的类型返回 999（保持向后兼容）。
        /// v3.4 增强：
        ///   ① 加 sourceTag 参数，传递来源后缀（"Scene" / "Prefab" / "PrefabInstance" / "Asset" / ""），
        ///      让 GameObject 三个来源桶在排序时也分开（baseOrder * 10 + sourceOrder）。
        ///   ② 归一化 typeName：去掉 "UnityEngine." 等命名空间前缀，使 raw.type 保存的 FullName
        ///      （如 "UnityEngine.GameObject"）也能正确匹配 nameof(GameObject) = "GameObject"，
        ///      修复 v2.41 注释承诺的"先尝试 raw.type"路径实际不命中的隐性 bug。
        ///   ③ 旧签名 GetTypeOrder(string) 保留为 thin wrapper，调用方传 sourceTag="" 即可。
        /// v3.5 优化：按 7 类桶（与 GetSelectionIconName / GetTypeSortIndex(Object) 一致）生成优先级。
        private static int GetTypeSortIndexFromString(string typeName, string sourceTag)
        {
            if (string.IsNullOrEmpty(typeName)) return 999;
            string shortName = NormalizeTypeName(typeName);
            if (shortName == nameof(SceneAsset)) return 0;
            if (shortName == nameof(GameObject))
            {
                if (sourceTag == "Prefab") return 3;                                  // GameObject·Prefab
                // 同时兼容 "Instance"（ParseTypeAndSource 当前返回值）
                // 和 "PrefabInstance"（GetTypeOrder 旧注释 / 旧 raw 数据中可能保留的字面值）
                if (sourceTag == "Instance" || sourceTag == "PrefabInstance") return 2; // GameObject·PrefabInstance
                return 1;                                                            // GameObject·Scene / "" 兜底
            }
            if (shortName == nameof(Material)) return 4;
            if (shortName == nameof(Mesh))     return 5;
            return 6; // Texture / AudioClip / AnimationClip / 其它
        }

        private static int GetTypeOrder(string typeName, string sourceTag = "")
        {
            int idx = GetTypeSortIndexFromString(typeName, sourceTag);
            return idx == 999 ? 999 : (idx + 1) * 10;
        }

        /// v3.4 新增：把可能带命名空间的类型名归一化为 short name（"UnityEngine.GameObject" → "GameObject"），
        /// 让 GetTypeOrder(string) 能正确匹配 nameof(GameObject) 等。
        private static string NormalizeTypeName(string typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return typeName;
            int dot = typeName.LastIndexOf('.');
            return dot > 0 ? typeName.Substring(dot + 1) : typeName;
        }

        /// 获取GameObject的唯一标识符（解决重名问题）
        /// v2.28：标识符中包含场景路径（scenePath）用于跨场景匹配；未保存场景时使用 sceneName 兜底。
        /// 格式：
        ///   根对象：ROOT:scenePath|sceneName:objectName:siblingIndex
        ///   子对象：SCENE:scenePath|sceneName:fullPath:siblingIndex
        private static string GetGameObjectUniqueIdentifier(GameObject gameObject)
        {
            if (!gameObject) return "";

            var fullPath = GetGameObjectFullPath(gameObject);
            var scenePath = gameObject.scene.path;
            var sceneName = gameObject.scene.name;
            var sceneGuid = AssetDatabase.GUIDFromAssetPath(scenePath).ToString();

            if (string.IsNullOrEmpty(sceneName))
                sceneName = "UnsavedScene";

            // 场景键：优先使用场景路径 GUID（持久化），未保存时使用场景名
            // 格式: sceneGuid|sceneName，便于在不同场景中通过 GUID 或 name 都能定位
            string sceneKey;
            if (!string.IsNullOrEmpty(sceneGuid) && sceneGuid != "00000000000000000000000000000000")
            {
                sceneKey = sceneGuid + "|" + sceneName;
            }
            else
            {
                sceneKey = "|" + sceneName;
            }

            int siblingIndex = gameObject.transform.GetSiblingIndex();

            if (!gameObject.transform.parent)
            {
                // 根对象：使用 ROOT:sceneKey:名称:siblingIndex
                return $"ROOT:{sceneKey}:{gameObject.name}:{siblingIndex}";
            }

            // 有层级的对象：使用 SCENE:sceneKey:完整路径:siblingIndex
            return $"SCENE:{sceneKey}:{fullPath}:{siblingIndex}";
        }

        /// 解析唯一标识符中的场景键
        /// <returns>(sceneGuid, sceneName)</returns>
        private static (string guid, string name) ParseSceneKey(string sceneKey)
        {
            if (string.IsNullOrEmpty(sceneKey)) return ("", "");
            var idx = sceneKey.IndexOf('|');
            if (idx < 0) return ("", sceneKey);
            return (sceneKey.Substring(0, idx), sceneKey.Substring(idx + 1));
        }

        /// 通过唯一标识符查找场景中的GameObject（v2.28 支持跨场景）
        /// 查找顺序：
        /// 1. 优先在保存的场景中精确查找（通过 Scene GUID 或场景名匹配）
        /// 2. 退而求其次在所有已加载场景中按路径+名称匹配
        /// 3. 仍找不到返回 null
        private GameObject FindGameObjectByIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier)) return null;

            // 解析标识符
            var parts = identifier.Split(':');
            if (parts.Length < 3) return null;

            var identifierType = parts[0];
            var sceneKey = parts[1];
            var objectPath = parts[2];

            // 第4部分可能是siblingIndex（新格式）或InstanceID（旧格式）
            int fourthPart = -1;
            bool hasFourthPart = parts.Length >= 4 && int.TryParse(parts[3], out fourthPart);

            // 解析场景键
            var (savedSceneGuid, savedSceneName) = ParseSceneKey(sceneKey);

            // 构造场景查找顺序
            var targetScenes = BuildSceneLookupOrder(savedSceneGuid, savedSceneName);

            GameObject foundObject = null;
            GameObject nameMatch = null;

            foreach (var scene in targetScenes)
            {
                if (identifierType == "ROOT")
                {
                    // 查找根对象
                    var rootObjects = scene.GetRootGameObjects();
                    foreach (var rootObject in rootObjects)
                    {
                        if (rootObject.name != objectPath) continue;
                        if (hasFourthPart && fourthPart >= 0)
                        {
                            // 优先通过siblingIndex精确匹配
                            if (rootObject.transform.GetSiblingIndex() == fourthPart)
                            {
                                foundObject = rootObject;
                                break;
                            }
                            // 也尝试InstanceID匹配（兼容旧格式）
                            if (VicToolsUnityCompat.GetReversibleId(rootObject) == fourthPart)
                            {
                                foundObject = rootObject;
                                break;
                            }
                            // 名称匹配作为候选
                            if (nameMatch == null) nameMatch = rootObject;
                        }
                        else
                        {
                            foundObject = rootObject;
                            break;
                        }
                    }
                    if (foundObject != null) break;
                }
                else if (identifierType == "SCENE")
                {
                    // 查找有层级路径的对象
                    var rootObjects = scene.GetRootGameObjects();
                    foreach (var rootObject in rootObjects)
                    {
                        string rootPrefix = rootObject.name + "/";
                        string relativePath = null;

                        if (objectPath.StartsWith(rootPrefix))
                        {
                            relativePath = objectPath.Substring(rootPrefix.Length);
                        }
                        else if (objectPath == rootObject.name)
                        {
                            if (hasFourthPart && fourthPart >= 0)
                            {
                                if (rootObject.transform.GetSiblingIndex() == fourthPart || VicToolsUnityCompat.GetReversibleId(rootObject) == fourthPart)
                                {
                                    foundObject = rootObject;
                                    break;
                                }
                                if (nameMatch == null) nameMatch = rootObject;
                            }
                            else
                            {
                                foundObject = rootObject;
                                break;
                            }
                            continue;
                        }

                        if (relativePath == null) continue;

                        Transform foundTransform = rootObject.transform.Find(relativePath);
                        if (foundTransform != null)
                        {
                            if (hasFourthPart && fourthPart >= 0)
                            {
                                // 优先siblingIndex匹配，兼容InstanceID
                                if (foundTransform.GetSiblingIndex() == fourthPart || VicToolsUnityCompat.GetReversibleId(foundTransform.gameObject) == fourthPart)
                                {
                                    foundObject = foundTransform.gameObject;
                                    break;
                                }
                                if (nameMatch == null) nameMatch = foundTransform.gameObject;
                            }
                            else
                            {
                                foundObject = foundTransform.gameObject;
                                break;
                            }
                        }
                    }
                    if (foundObject != null) break;
                }
            }

            // 如果在目标场景中未找到，按名称+完整路径在所有已加载场景中兜底查找
            if (foundObject == null)
            {
                foundObject = FindByFullPathAcrossAllScenes(objectPath, fourthPart, hasFourthPart, out nameMatch);
            }

            // 使用名称匹配候选
            if (foundObject == null && nameMatch != null)
                foundObject = nameMatch;

            return foundObject;
        }

        /// 根据保存的场景 GUID / 名称构造场景查找顺序
        private static List<UnityEngine.SceneManagement.Scene> BuildSceneLookupOrder(string savedSceneGuid, string savedSceneName)
        {
            var order = new List<UnityEngine.SceneManagement.Scene>();
            var seen = new HashSet<ulong>();
            int activeSceneIndex = SceneManager.GetActiveScene().buildIndex;

            UnityEngine.SceneManagement.Scene? savedScene = null;

            // 1. 优先查找与保存的场景 GUID 或 name 匹配的场景
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.IsValid() || !s.isLoaded) continue;
                if (!string.IsNullOrEmpty(savedSceneGuid))
                {
                    var path = s.path;
                    var guid = AssetDatabase.GUIDFromAssetPath(path).ToString();
                    if (guid == savedSceneGuid)
                    {
                        savedScene = s;
                        break;
                    }
                }
                if (savedScene == null && !string.IsNullOrEmpty(savedSceneName) && s.name == savedSceneName)
                {
                    savedScene = s;
                }
            }

            if (savedScene.HasValue && seen.Add(VicToolsEditorCompat.GetSceneHandle(savedScene.Value)))
            {
                order.Add(savedScene.Value);
            }

            // 2. 添加当前激活场景（如果与已添加的场景不同）
            var active = SceneManager.GetActiveScene();
            if (active.IsValid() && active.isLoaded && seen.Add(VicToolsEditorCompat.GetSceneHandle(active)))
            {
                order.Add(active);
            }

            // 3. 添加所有其他已加载场景
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var s = SceneManager.GetSceneAt(i);
                if (!s.IsValid() || !s.isLoaded) continue;
                if (seen.Add(VicToolsEditorCompat.GetSceneHandle(s))) order.Add(s);
            }

            return order;
        }

        /// 在所有已加载场景中按完整路径查找对象
        private static GameObject FindByFullPathAcrossAllScenes(string objectPath, int fourthPart, bool hasFourthPart, out GameObject nameMatch)
        {
            nameMatch = null;
            // v2.39 修复：跨场景兜底查找必须包含 inactive 对象，避免资源箱灰色对象场景打开后无法恢复。
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            // 注：包含 inactive 对象的查找以 GetRootGameObjects().transform.Find() 链为主，这里只用于跨场景兜底
            foreach (var obj in allObjects)
            {
                string currentPath = GetGameObjectFullPath(obj);
                if (currentPath != objectPath) continue;
                if (hasFourthPart && fourthPart >= 0)
                {
                    if (obj.transform.GetSiblingIndex() == fourthPart || VicToolsUnityCompat.GetReversibleId(obj) == fourthPart)
                    {
                        return obj;
                    }
                    if (nameMatch == null) nameMatch = obj;
                }
                else
                {
                    return obj;
                }
            }
            return null;
        }

        /// 通过完整路径查找场景中的GameObject
        private GameObject FindGameObjectByPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;

            var currentScene = SceneManager.GetActiveScene();
            var rootObjects = currentScene.GetRootGameObjects();

            return (from rootObject in rootObjects select rootObject.transform.Find(fullPath) into foundTransform where foundTransform select foundTransform.gameObject).FirstOrDefault();
        }

        /// v2.28：通过保存的 sceneGuid/sceneName 在已加载场景中再次尝试恢复场景对象
        /// 当 ROOT/SCENE 标识符查找失败时使用。优先匹配 sceneGuid，其次 sceneName，
        /// 最后退到任意已加载场景中按名称+路径匹配。
        /// 返回 true 表示成功恢复。
        private bool TryFindGameObjectBySceneInfo(ResourceBoxItem item, out Object obj)
        {
            obj = null;
            if (item == null) return false;
            if (item.isAsset) return false;
            return TryFindGameObjectBySceneInfo(item.isAsset, item.sceneGuid, item.sceneName, item.name, out obj);
        }

        /// 通用版本：直接传入场景 GUID/名称/对象名
        private bool TryFindGameObjectBySceneInfo(bool isAsset, string sceneGuid, string sceneName, string objectName, out Object obj)
        {
            obj = null;
            if (isAsset) return false;
            if (string.IsNullOrEmpty(sceneGuid) && string.IsNullOrEmpty(sceneName)) return false;
            if (string.IsNullOrEmpty(objectName)) return false;

            UnityEngine.SceneManagement.Scene? targetScene = null;
            // 1. 通过 sceneGuid 精确匹配已加载场景
            if (!string.IsNullOrEmpty(sceneGuid))
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (!s.IsValid() || !s.isLoaded) continue;
                    var guid = AssetDatabase.AssetPathToGUID(s.path);
                    if (string.Equals(guid, sceneGuid, StringComparison.OrdinalIgnoreCase))
                    {
                        targetScene = s;
                        break;
                    }
                }
            }

            // 2. 退到 sceneName 匹配（兼容未保存场景）
            if (!targetScene.HasValue && !string.IsNullOrEmpty(sceneName))
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var s = SceneManager.GetSceneAt(i);
                    if (!s.IsValid() || !s.isLoaded) continue;
                    if (s.name == sceneName)
                    {
                        targetScene = s;
                        break;
                    }
                }
            }

            if (!targetScene.HasValue) return false;

            // 在目标场景中按名称+根对象 siblingIndex 模糊匹配
            var scene = targetScene.Value;
            var roots = scene.GetRootGameObjects();
            // 优先匹配同名根对象
            foreach (var root in roots)
            {
                if (root.name == objectName)
                {
                    obj = root;
                    return true;
                }
            }
            // 其次在所有 GameObject 中按名称匹配
            // v2.39 修复：必须包含 inactive，否则场景中 activeSelf=false 的子物体找不到，灰色对象恢复失败。
            var allInScene = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var go in allInScene)
            {
                if (go == null) continue;
                if (go.scene != scene) continue;
                if (go.name == objectName)
                {
                    obj = go;
                    return true;
                }
            }
            return false;
        }

        /// 通过模糊匹配查找场景中的GameObject（最终备用方案）
        private static GameObject FindGameObjectByFuzzyMatching(string objectPath)
        {
            // v2.39 修复：模糊匹配兜底也要包含 inactive，避免未激活 GameObject 找不到
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            
            // 尝试基于名称的模糊匹配
            var pathParts = objectPath.Split('/');
            var targetName = pathParts.Length > 0 ? pathParts[pathParts.Length - 1] : objectPath;

            return (from obj in allObjects where obj.name == targetName let currentPath = GetGameObjectFullPath(obj) where currentPath.EndsWith(objectPath) || objectPath.EndsWith(currentPath) select obj).FirstOrDefault();
        }
        
        /// 资源箱数据序列化类
        [Serializable]
        private class ResourceBoxData
        {
            public List<ResourceBoxItem> items;
        }

        /// 资源箱项目序列化类
        /// v2.28 增强：记录场景对象所属的场景信息（scenePath/sceneName/sceneGuid），
        /// 用于在场景重载/未打开等情况下丢失对象时能正确恢复。
        [Serializable]
        private class ResourceBoxItem
        {
            public string guid;
            public string type;
            public string name;
            public bool isAsset;
            public string displayName; // 添加displayName字段，用于保存对象的显示名称
            public string scenePath;   // 场景资源路径（Assets/...），如未保存则为空
            public string sceneName;   // 场景名（与 scenePath 不一致时为当前 name）
            public string sceneGuid;   // 场景资产 GUID（持久化标识，跨 Domain Reload 仍有效）
        }

        #region 资源箱文件管理方法

        /// 打开资源箱列表存档目录文件夹
        private void OpenResourceBoxFolder()
        {
            var directoryPath = PathHelper.GetAssetsEditorPath();

            // 确保目录存在，避免 RevealInFinder 因路径不存在而失效
            if (!System.IO.Directory.Exists(directoryPath))
            {
                System.IO.Directory.CreateDirectory(directoryPath);
            }

            // 在系统文件管理器中定位并打开该目录
            // v3.3: 路径已改为 ResourceBox~；directoryPath 已通过 PathHelper.GetAssetsEditorPath() 包含完整目录，
            //       这里直接用 directoryPath，避免原代码重复追加"/ResourceBox"产生 ResourceBox~/ResourceBox 子目录的 bug。
            EditorUtility.RevealInFinder(directoryPath+"/ResourceBox~");
        }

        /// 刷新可用资源箱文件列表
        private void RefreshAvailableFiles()
        {
            _availableFiles.Clear();
            
            // 使用PathHelper获取正确的资源箱目录路径
            var fullDirectoryPath = PathHelper.GetAssetsEditorPath();
            if (!System.IO.Directory.Exists(fullDirectoryPath))
            {
                System.IO.Directory.CreateDirectory(fullDirectoryPath);
                return;
            }

            // 获取所有JSON文件
            var jsonFiles = System.IO.Directory.GetFiles(fullDirectoryPath, "*.json");
            foreach (var filePath in jsonFiles)
            {
                var fileName = System.IO.Path.GetFileNameWithoutExtension(filePath);
                _availableFiles.Add(fileName);
            }

            // 如果没有文件，添加默认选项
            if (_availableFiles.Count == 0)
            {
                _availableFiles.Add("新建资源箱");
            }
        }

        /// 保存资源箱数据到文件（优化版本，解决重名问题）
        /// <param name="showConfirmation">是否显示确认提示框</param>
        private void SaveResourceBoxToFile(bool showConfirmation = true)
        {
            // 如果资源箱文件名为空，自动获取当前场景名作为资源箱名
            if (string.IsNullOrEmpty(_resourceBoxFileName))
            {
                var currentSceneName = SceneManager.GetActiveScene().name;
                if (string.IsNullOrEmpty(currentSceneName))
                {
                    currentSceneName = "UntitledScene";
                }
                _resourceBoxFileName = currentSceneName;
            }

            // 如果需要显示确认提示框，则显示
            if (showConfirmation)
            {
                var confirm = EditorUtility.DisplayDialog(
                    "确认保存",
                    $"确定要将资源箱中的 {_resourceBox.Count} 个对象保存到\n文件 <{_resourceBoxFileName}> 吗？",
                    "确定保存",
                    "取消"
                );

                if (!confirm)
                {
                    Debug.Log("用户取消了保存操作");
                    return;
                }
            }

            // 创建资源箱文件数据
            var fileData = new ResourceBoxFileData
            {
                LastModifiedTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
            };

            // 转换资源箱对象为序列化数据
            foreach (var obj in _resourceBox.Where(obj => obj))
            {
                // 检查对象是否具有kDontSaveInEditor标志，如果有则跳过保存
                if (((int)obj.hideFlags & (int)HideFlags.DontSaveInEditor) != 0)
                {
                    Debug.LogWarning($"对象 {obj.name} 具有DontSaveInEditor标志，跳过保存到文件");
                    continue;
                }
                    
                string guid;
                var assetPath = AssetDatabase.GetAssetPath(obj);
                string scenePath = "";
                string sceneName = "";
                string sceneGuid = "";

                if (!string.IsNullOrEmpty(assetPath))
                {
                    // 项目资源使用GUID
                    guid = AssetDatabase.AssetPathToGUID(assetPath);
                }
                else
                {
                    // 场景对象使用优化的唯一标识符
                    if (obj is GameObject gameObject)
                    {
                        // v2.28：收集场景信息
                        scenePath = gameObject.scene.path;
                        sceneName = gameObject.scene.name;
                        if (!string.IsNullOrEmpty(scenePath))
                        {
                            sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                        }

                        // 使用新的唯一标识符系统
                        guid = GetGameObjectUniqueIdentifier(gameObject);
                    }
                    else
                    {
                        if (obj is Component comp)
                        {
                            scenePath = comp.gameObject.scene.path;
                            sceneName = comp.gameObject.scene.name;
                            if (!string.IsNullOrEmpty(scenePath))
                            {
                                sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                            }
                        }
                        guid = $"INSTANCE:{VicToolsUnityCompat.GetReversibleId(obj)}";
                    }
                }

                // 计算displayName（与DrawResourceBoxSection中相同的逻辑）
                string displayName;
                var parentName = GetTopLevelParentName(obj);
                displayName = !string.IsNullOrEmpty(parentName)
                    ? $"<{GetTypeDisplayName(obj)}> [{parentName}]← {obj.name}"
                    : $"<{GetTypeDisplayName(obj)}> {obj.name}";

                fileData.items.Add(new ResourceBoxFileItem
                {
                    guid = guid,
                    type = obj.GetType().FullName, // v2.43: 字段名改为 type（v2.41 之前是 Type 属性，JsonUtility 不支持属性导致存档丢 type）
                    name = obj.name,
                    isAsset = !string.IsNullOrEmpty(assetPath),
                    displayName = displayName,  // 保存displayName
                    scenePath = scenePath,
                    sceneName = sceneName,
                    sceneGuid = sceneGuid
                });
            }

            // 序列化并保存到文件
            var jsonData = JsonUtility.ToJson(fileData, true);
            // 使用PathHelper获取正确的资源箱目录路径
            var resourceBoxDirectoryPath = PathHelper.GetAssetsEditorPath();
            var filePath = System.IO.Path.Combine(resourceBoxDirectoryPath, _resourceBoxFileName + ".json");
            
            try
            {
                System.IO.File.WriteAllText(filePath, jsonData);
                AssetDatabase.Refresh();
                RefreshAvailableFiles();
                
                // 更新选中文件索引
                _selectedFileIndex = _availableFiles.IndexOf(_resourceBoxFileName);
                if (_selectedFileIndex == -1) _selectedFileIndex = 0;
                
                Debug.Log($"资源箱数据已保存到文件: {_resourceBoxFileName}.json");
                // EditorUtility.DisplayDialog("保存成功", $"资源箱数据已保存到文件: {resourceBoxFileName}.json", "确定");
            }
            catch (Exception e)
            {
                Debug.LogError($"保存资源箱文件时出错: {e.Message}");
                EditorUtility.DisplayDialog("保存失败", $"保存文件时出错: {e.Message}", "确定");
            }
        }

        /// 从文件加载资源箱数据
        private void LoadResourceBoxFromFile()
        {
            if (_availableFiles.Count == 0 || _selectedFileIndex < 0 || _selectedFileIndex >= _availableFiles.Count)
            {
                EditorUtility.DisplayDialog("错误", "没有可用的资源箱文件", "确定");
                return;
            }

            string fileName = _availableFiles[_selectedFileIndex];
            if (fileName == "新建资源箱")
            {
                EditorUtility.DisplayDialog("提示", "请先保存一个新的资源箱文件", "确定");
                return;
            }

            // 如果资源箱中有内容，提示用户是否替换
            if (_resourceBox.Count > 0)
            {
                var confirm = EditorUtility.DisplayDialog(
                    "确认替换",
                    "资源箱中已有内容，确定要替换为文件中的内容吗？",
                    "确定替换",
                    "取消"
                );

                if (!confirm)
                {
                    Debug.Log("用户取消了替换操作");
                    return;
                }
            }

            // 使用PathHelper获取正确的资源箱目录路径
            var resourceBoxDirectoryPath = PathHelper.GetAssetsEditorPath();
            var filePath = System.IO.Path.Combine(resourceBoxDirectoryPath, fileName + ".json");
            
            if (!System.IO.File.Exists(filePath))
            {
                EditorUtility.DisplayDialog("错误", $"文件不存在: {fileName}.json", "确定");
                return;
            }

            try
            {
                var jsonData = System.IO.File.ReadAllText(filePath);
                var fileData = JsonUtility.FromJson<ResourceBoxFileData>(jsonData);
                
                if (fileData == null)
                {
                    EditorUtility.DisplayDialog("错误", "文件格式不正确", "确定");
                    return;
                }

                // 清空当前资源箱
                _resourceBox.Clear();
                _nullObjectDisplayNames.Clear(); // 清空null对象显示名称字典
                _resourceBoxInstanceIDs.Clear(); // 清空InstanceID映射
                _instanceIDToDisplayName.Clear(); // 清空InstanceID到displayName的映射
                _nullObjectRawItems.Clear(); // v2.28 清空 null 原始项缓存
                _recoveryRawItems = null; // v2.28 重置恢复缓存
                var loadedCount = 0;
                var failedCount = 0;
                var hasSceneObjectChanges = false; // 标记是否有场景对象变化

                Debug.Log($"开始从文件加载资源箱数据: {fileName}.json，包含 {fileData.items.Count} 个项目");

                // 加载资源箱对象
                for (var i = 0; i < fileData.items.Count; i++)
                {
                    var item = fileData.items[i];
                    Object obj = null;
                    var debugInfo = "";
                    
                    if (item.isAsset)
                    {
                        // 加载项目资源
                        var assetPath = AssetDatabase.GUIDToAssetPath(item.guid);
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                            debugInfo = $"项目资源: {item.name} (GUID: {item.guid})";
                        }
                        else
                        {
                            debugInfo = $"项目资源未找到: {item.name} (GUID: {item.guid})";
                        }
                    }
                    else
                    {
                        // 处理场景对象 - 使用新的标识符系统
                        if (item.guid.StartsWith("ROOT:") || item.guid.StartsWith("SCENE:"))
                        {
                            // 使用新的FindGameObjectByIdentifier方法查找对象
                            obj = FindGameObjectByIdentifier(item.guid);
                            debugInfo = $"场景对象: {item.name} (标识符: {item.guid})";
                            
                            // 检查是否需要更新标识符
                            if (obj)
                            {
                                var currentIdentifier = GetGameObjectUniqueIdentifier(obj as GameObject);
                                if (currentIdentifier != item.guid)
                                {
                                    // 标识符发生变化，标记需要更新
                                    hasSceneObjectChanges = true;
                                    debugInfo += $" → 标识符已更新: {currentIdentifier}";
                                }
                            }
                            
                            if (!obj)
                            {
                                // v2.28：通过场景信息在已加载场景中再次尝试恢复
                                if (TryFindGameObjectBySceneInfo(false, item.sceneGuid, item.sceneName, item.name, out Object recoveredObj) && recoveredObj)
                                {
                                    obj = recoveredObj;
                                    debugInfo += $" → 通过场景信息找到: {obj.name} (场景: {item.sceneName})";
                                }
                                else
                                {
                                    // 如果通过标识符找不到，尝试通过实例ID查找
                                    var parts = item.guid.Split(':');
                                    if (parts.Length >= 4 && int.TryParse(parts[3], out int instanceID))
                                    {
                                        var instanceObj = VicToolsEditorCompat.IdToObject(instanceID);
                                        if (instanceObj && instanceObj is GameObject gameObject)
                                        {
                                            // 验证场景匹配
                                            var currentScene = SceneManager.GetActiveScene();
                                            if (gameObject.scene == currentScene)
                                            {
                                                obj = gameObject;
                                                debugInfo += $" → 通过实例ID找到: {obj.name} (InstanceID: {instanceID})";
                                            }
                                            else
                                            {
                                                debugInfo += $" → 实例ID对象场景不匹配: {instanceID}";
                                            }
                                        }
                                        else
                                        {
                                            // 如果InstanceIDToObject失败，尝试在场景中查找具有相同实例ID的对象
                                            // v2.39 修复：包含 inactive，否则未激活对象兜底失败
                                            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                                            foreach (var sceneObj in allObjects)
                                            {
                                                if (VicToolsUnityCompat.GetReversibleId(sceneObj) != instanceID) continue;
                                                // 验证场景匹配
                                                var currentScene = SceneManager.GetActiveScene();
                                                if (sceneObj.scene != currentScene) continue;
                                                obj = sceneObj;
                                                debugInfo += $" → 通过场景遍历找到: {obj.name} (InstanceID: {instanceID})";
                                                break;
                                            }
                                            if (!obj)
                                            {
                                                debugInfo += $" → 实例ID查找失败: {instanceID}";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        else if (item.guid.StartsWith("INSTANCE:"))
                        {
                            // 实例ID对象：尝试通过实例ID查找
                            var instancePart = item.guid.Substring("INSTANCE:".Length);
                            if (int.TryParse(instancePart, out int instanceID))
                            {
                                var instanceObj = VicToolsEditorCompat.IdToObject(instanceID);
                                if (instanceObj && instanceObj is GameObject gameObject)
                                {
                                    // 验证场景匹配
                                    var currentScene = SceneManager.GetActiveScene();
                                    if (gameObject.scene == currentScene)
                                    {
                                        obj = gameObject;
                                        debugInfo = $"实例对象: {item.name} (InstanceID: {instanceID})";
                                    }
                                    else
                                    {
                                        debugInfo = $"实例ID对象场景不匹配: {item.name} (InstanceID: {instanceID})";
                                    }
                                }
                                else
                                {
                                    debugInfo = $"实例ID查找失败: {item.name} (InstanceID: {instanceID})";
                                }
                            }
                            else
                            {
                                debugInfo = $"实例ID格式错误: {item.name} (标识符: {item.guid})";
                            }
                        }
                        else
                        {
                            // 向后兼容：处理旧的SCENE:格式标识符
                            if (item.guid.StartsWith("SCENE:"))
                            {
                                var parts = item.guid.Split(':');
                                if (parts.Length >= 3)
                                {
                                    var fullPath = parts[2];
                                    
                                    // 尝试使用旧的方法查找
                                    obj = FindGameObjectByPath(fullPath);
                                    debugInfo = $"旧格式场景对象: {item.name} (路径: {fullPath})";
                                }
                            }
                            else
                            {
                                    debugInfo = $"未知标识符格式: {item.name} (标识符: {item.guid})";
                                }
                            }
                        }

                        // v2.28: 对于 null 对象，缓存原始 item 用于场景重载后恢复
                        if (obj == null && !item.isAsset &&
                            (!string.IsNullOrEmpty(item.sceneGuid) || !string.IsNullOrEmpty(item.sceneName)))
                        {
                            // 构造一个轻量快照项（沿用 item 的字段）放入 _nullObjectRawItems
                            int cacheIdx = _resourceBox.Count; // 即将被添加的索引
                            _nullObjectRawItems[cacheIdx] = ConvertToRecoveryItem(item);
                        }

                    // 即使对象为null，也添加到资源箱，以便显示对象名称
                    if (obj)
                    {
                        // 检查对象是否具有kDontSaveInEditor标志，如果有则清除它
                        if (((int)obj.hideFlags & (int)HideFlags.DontSaveInEditor) != 0)
                        {
                            obj.hideFlags &= ~HideFlags.DontSaveInEditor;
                        }
                        
                        _resourceBox.Add(obj);
                        loadedCount++;
                        
                        // 保存displayName到字典（如果ResourceBoxFileItem中有displayName字段）
                        if (!string.IsNullOrEmpty(item.displayName))
                        {
                            _resourceBoxDisplayNames[obj] = item.displayName;
                        }
                    }
                    else
                    {
                        // 对象为null，但仍然添加到资源箱以便显示
                        _resourceBox.Add(null);
                        failedCount++;
                        
                        // 保存displayName到null对象字典，使用当前索引作为键
                        if (!string.IsNullOrEmpty(item.displayName))
                        {
                            _nullObjectDisplayNames[_resourceBox.Count - 1] = item.displayName;
                        }
                        else
                        {
                            // v2.43: 用 BuildFallbackDisplayName 统一兜底，保留 item.type（v2.41 起 ResourceBoxFileItem.type 已是字段），
                            // 让 null 对象按真实类型参与排序/恢复，不再硬编码"<未知类型> {item.name}"。
                            _nullObjectDisplayNames[_resourceBox.Count - 1] = BuildFallbackDisplayName(item.type, item.name);
                        }
                        
                        // Debug.LogWarning($"[{i+1}/{fileData.items.Count}] ✗ 无法加载对象，但保留显示名称: {debugInfo}");
                    }
                }

                _resourceBoxFileName = fileName;

                Debug.Log($"从文件加载资源箱数据完成: {fileName}.json (成功: {loadedCount}, 失败: {failedCount}, 总计: {fileData.items.Count})");

                // v2.28: 设置 _recoveryRawItems 用于场景打开时恢复
                _recoveryRawItems = ConvertToRecoveryList(fileData.items);
                
                // 检查是否有场景对象变化，如果有则自动保存更新数据
                if (hasSceneObjectChanges)
                {
                    Debug.Log("检测到场景对象标识符变化，自动更新资源箱数据...");
                    // 同时更新EditorPrefs和文件数据，确保数据一致性
                    // SaveResourceBox(false);
                    SaveResourceBoxToFile(false);
                    Debug.Log("资源箱数据已自动更新（EditorPrefs和文件）");
                }
                SaveResourceBox(false);
                // 强制重绘窗口
                if (Parent)
                    Parent.Repaint();
            }
            catch (Exception e)
            {
                Debug.LogError($"加载资源箱文件时出错: {e.Message}");
                EditorUtility.DisplayDialog("加载失败", $"加载文件时出错: {e.Message}", "确定");
            }
        }

        #endregion

        /// 更新选中数量显示
        private void UpdateSelectedCountDisplay()
        {
            // 支持独立窗口和主窗口
            var standaloneWindow = Parent as WinScenesTools;
            if (standaloneWindow != null)
            {
                // 更新独立窗口的选中数量
                standaloneWindow.StandaloneSelectedCount = Selection.gameObjects.Length;
                standaloneWindow.Repaint();
            }
            else
            {
                // 更新主窗口的全局选中数量
                var mainWindow = Parent as VicToolsWindow;
                if (mainWindow != null)
                {
                    mainWindow.globalSelectedObjectsCount = Selection.gameObjects.Length;
                    mainWindow.Repaint();
                }
            }
        }

        /// 重新检查资源箱中的null对象，尝试在当前场景中重新加载它们
        private void RecheckNullObjectsInResourceBox()
        {
            bool hasChanges = false;
            
            // 收集已经在资源箱中的有效对象的InstanceID，用于防止重复
            var existingInstanceIDs = new HashSet<int>();
            for (int i = 0; i < _resourceBox.Count; i++)
            {
                var existing = _resourceBox[i];
                if (existing && existing is GameObject go)
                    existingInstanceIDs.Add(VicToolsUnityCompat.GetReversibleId(go));
            }
            
            for (int i = 0; i < _resourceBox.Count; i++)
            {
                var obj = _resourceBox[i];

                // 如果对象为null，尝试重新加载
                if (!obj)
                {
                    // 尝试从null对象字典中获取保存的displayName
                    if (_nullObjectDisplayNames.TryGetValue(i, out string savedDisplayName))
                    {
                        // v3.2 修复：与第二层 TryRecoverNullItemsBySceneInfo:457 对齐，资产型 null 项直接跳过
                        // 不走 GameObject 全场景按名搜。否则 UniversalRenderData 这类 ScriptableObject 资产
                        // （isAsset=true）丢失变 null 后，按 "Mobile_Renderer" 名字会命中第一个同名 GameObject，
                        // 误恢复成 GameObject，产生"同名同类型灰色对象"现象。
                        if (_nullObjectRawItems.TryGetValue(i, out var rawForCheck) && rawForCheck != null && rawForCheck.isAsset)
                        {
                            continue;
                        }

                        // 从displayName中提取原始对象名称
                        string originalName = ExtractOriginalNameFromDisplayName(savedDisplayName);

                        // 尝试通过保存的标识符精确查找（优先）
                        GameObject foundObject = null;
                        if (_resourceBoxInstanceIDs.TryGetValue(i, out int savedInstanceID))
                        {
                            var instanceObj = VicToolsEditorCompat.IdToObject(savedInstanceID);
                            if (instanceObj is GameObject go && go.name == originalName)
                            {
                                foundObject = go;
                            }
                        }

                        // 如果精确查找失败，通过名称查找（但要确保不会匹配到已在资源箱中的对象）
                        // v2.39 修复：必须用 FindObjectsInactive.Include，否则场景中 activeSelf=false 的 GameObject 会被静默跳过，
                        // 导致"打开场景"按钮 / 【刷新】按钮无法恢复这些未激活的灰色对象。
                        // v3.2 修复：统计全场景同名 GameObject 数量，>1 时输出警告，让用户感知歧义而非默默恢复到错的。
                        if (foundObject == null)
                        {
                            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                            int sameNameCount = 0;
                            foreach (var candidate in allObjects)
                            {
                                if (candidate.name == originalName && !existingInstanceIDs.Contains(VicToolsUnityCompat.GetReversibleId(candidate)))
                                {
                                    sameNameCount++;
                                    if (sameNameCount == 1)
                                    {
                                        foundObject = candidate;
                                    }
                                }
                            }
                            if (sameNameCount > 1)
                            {
                                Debug.LogWarning($"[ResourceBox] 资源箱 index={i} 名称='{originalName}' 在场景中找到 {sameNameCount} 个同名 GameObject，已选择第一个（{foundObject?.name} InstanceID={(foundObject != null ? VicToolsUnityCompat.GetReversibleId(foundObject).ToString() : "")}）。若恢复错对象，请人工校验或恢复原 null 状态。");
                            }
                        }
                        
                        if (foundObject != null)
                        {
                            // 检查找到的对象是否已经存在于资源箱中（防止重复添加）
                            if (_resourceBox.Contains(foundObject))
                            {
                                // 对象已存在于资源箱中，不重复添加，直接移除这个null条目
                                _resourceBox.RemoveAt(i);
                                _nullObjectDisplayNames.Remove(i);
                                
                                // 重建索引映射
                                RebuildNullDisplayNameIndices(i);
                                
                                hasChanges = true;
                                i--; // 调整索引，因为列表缩短了
                                continue;
                            }
                            
                            // 找到对象，替换资源箱中的null
                            _resourceBox[i] = foundObject;
                            existingInstanceIDs.Add(VicToolsUnityCompat.GetReversibleId(foundObject));
                            
                            // 将displayName移动到_resourceBoxDisplayNames字典
                            _resourceBoxDisplayNames[foundObject] = savedDisplayName;
                            
                            // 从null对象字典中移除
                            _nullObjectDisplayNames.Remove(i);
                            // v2.28: 同步从 _nullObjectRawItems 移除
                            _nullObjectRawItems.Remove(i);

                            hasChanges = true;
                        }
                    }
                }
            }
            
            if (hasChanges)
            {
                // 保存更新后的资源箱数据
                SaveResourceBox(false);
            }
        }
        
        /// 重建_nullObjectDisplayNames和_resourceBoxInstanceIDs的索引（在移除元素后）
        private void RebuildNullDisplayNameIndices(int removedIndex)
        {
            var tempNullDisplayNames = new Dictionary<int, string>();
            var tempInstanceIDs = new Dictionary<int, int>();
            foreach (var kvp in _nullObjectDisplayNames)
            {
                if (kvp.Key < removedIndex)
                    tempNullDisplayNames[kvp.Key] = kvp.Value;
                else if (kvp.Key > removedIndex)
                    tempNullDisplayNames[kvp.Key - 1] = kvp.Value;
            }
            foreach (var kvp in _resourceBoxInstanceIDs)
            {
                if (kvp.Key < removedIndex)
                    tempInstanceIDs[kvp.Key] = kvp.Value;
                else if (kvp.Key > removedIndex)
                    tempInstanceIDs[kvp.Key - 1] = kvp.Value;
            }
            _nullObjectDisplayNames.Clear();
            foreach (var kvp in tempNullDisplayNames)
                _nullObjectDisplayNames[kvp.Key] = kvp.Value;
            _resourceBoxInstanceIDs.Clear();
            foreach (var kvp in tempInstanceIDs)
                _resourceBoxInstanceIDs[kvp.Key] = kvp.Value;
            // v2.28: 同步重建 _nullObjectRawItems 索引
            var tempRawItems = new Dictionary<int, ResourceBoxRecoveryItem>();
            foreach (var kvp in _nullObjectRawItems)
            {
                if (kvp.Key < removedIndex) tempRawItems[kvp.Key] = kvp.Value;
                else if (kvp.Key > removedIndex) tempRawItems[kvp.Key - 1] = kvp.Value;
            }
            _nullObjectRawItems.Clear();
            foreach (var kvp in tempRawItems) _nullObjectRawItems[kvp.Key] = kvp.Value;
        }

        /// v2.28: 按当前 _resourceBox 顺序重建 _nullObjectRawItems 索引（用于按类型排序等场景）
        private void RebuildNullObjectRawItemsIndex()
        {
            _nullObjectRawItems.Clear();
            // 排序后原键错位，需要从 _recoveryRawItems 重新匹配位置
            // _recoveryRawItems 已经是当前 null 项的原始列表
            if (_recoveryRawItems == null) return;
            int rawIdx = 0;
            for (int i = 0; i < _resourceBox.Count; i++)
            {
                if (!_resourceBox[i])
                {
                    if (rawIdx < _recoveryRawItems.Count)
                    {
                        _nullObjectRawItems[i] = _recoveryRawItems[rawIdx];
                        rawIdx++;
                    }
                }
            }
        }

        /// 在当前场景中按名称查找对象
        private GameObject FindObjectByNameInCurrentScene(string objectName)
        {
            // v2.39 修复：包含 inactive，否则未激活的灰色对象在 InstanceID 失效后无法按名称恢复。
            var allObjects = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var obj in allObjects)
            {
                if (obj.name == objectName)
                {
                    return obj;
                }
            }
            return null;
        }

        /// 获取GameObject的完整路径（从根对象到当前对象的路径）
        private static string GetGameObjectFullPath(GameObject gameObject)
        {
            if (!gameObject) return "";
            
            var path = gameObject.name;
            var current = gameObject.transform.parent;
            
            while (current)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }
            
            return path;
        }

        /// 反射查找 Unity 内置 GUIStyle（按 name 匹配，命中后克隆返回，避免污染原样式）
        /// 用于拿到 GUI.skin.FindStyle 查不到、又不在 EditorStyles 公开字段上的样式
        /// （例如 StateMachineEditor.Background 属于 Animator 内部样式表，公开 API 拿不到）。
        /// 返回 null 时调用方需自行处理（如 IconContent + 手动 border）。
        private static GUIStyle TryFindBuiltinStyle(string styleName)
        {
            // 1) 翻 EditorStyles 上的所有静态 GUIStyle 字段（含 internal）
            var editorFields = typeof(EditorStyles).GetFields(
                System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public);
            foreach (var f in editorFields)
            {
                if (f.FieldType != typeof(GUIStyle)) continue;
                try
                {
                    var s = (GUIStyle)f.GetValue(null);
                    if (s != null && s.name == styleName) return new GUIStyle(s);
                }
                catch { /* 忽略该字段的访问错误，继续翻下一个 */ }
            }
            // 2) 翻 GUI.skin 内部 m_Styles 数组
            var skin = GUI.skin;
            if (skin != null)
            {
                var stylesField = typeof(GUISkin).GetField("m_Styles",
                    System.Reflection.BindingFlags.NonPublic
                    | System.Reflection.BindingFlags.Instance);
                if (stylesField != null)
                {
                    var arr = stylesField.GetValue(skin) as GUIStyle[];
                    if (arr != null)
                    {
                        foreach (var s in arr)
                        {
                            if (s != null && s.name == styleName) return new GUIStyle(s);
                        }
                    }
                }
            }
            return null;
        }

    }
    
    /// 打包前自动保存资源箱数据，防止 Domain Reload 导致数据丢失
    public class ResourceBoxBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => -100; // 尽早执行

        public void OnPreprocessBuild(BuildReport report)
        {
            // 在打包前直接保存资源箱数据（此时对象引用仍然有效）
            var instance = ScenesTools.ActiveInstance;
            if (instance != null)
            {
                instance.SaveResourceBoxBeforeBuild();
                Debug.Log("[ResourceBox] 打包前通过 ActiveInstance 保存资源箱数据成功");
            }
            else
            {
                // 如果没有活跃实例，确认全局文件存在
                var globalResourceBoxPath = PathHelper.GetGlobalResourceBoxPath();
                if (System.IO.File.Exists(globalResourceBoxPath))
                {
                    Debug.Log($"[ResourceBox] 打包前确认资源箱数据已保存: {globalResourceBoxPath}");
                }
            }
        }
    }

    /// 【资源箱】项目场景扫描结果预览窗口（非模态）
    /// 已上移到公共工具文件 VicToolsEditorUtility.cs，作为通用【处理列表确认窗口】共用：
    ///   Vic.Editor.VicToolsListConfirmWindow.Show(title, summary, items, onConfirm, confirmButtonText)
    /// 其它工具需要"先列清单再确认执行"时，直接复用该公共窗口即可。
}
