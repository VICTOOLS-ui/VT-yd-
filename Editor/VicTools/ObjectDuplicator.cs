// 批量复制工具 v1.0
//   - 支持网格 / 圆形 / 螺旋 / 线性 阵列对齐
//   - 支持盒形区域内的随机散布（含最小距离避让）
//   - 多选模板：每个被选中的对象都作为模板按相同规则生成副本
//   - 支持 Undo / 折叠为一步操作，命名自动加序号
// 批量复制工具 v1.1 实现实时操作功能，优化圆形阵列头尾重叠问题。
// 批量复制工具 v1.2 优化随机缩放参数添加“锁定比例”选项；添加参数修改记录；添加阵列模式图标。
// 批量复制工具 v1.3 优化UI，添加参数还原功能按钮；升级线性阵列模式支持“按体积”步进，随机参数共用到各阵列模式。

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Vic.Editor; // VicToolsIconLoader 等 Editor-only 公共工具
using Object = UnityEngine.Object;
using System.Runtime.InteropServices;

namespace VicTools
{
    /// 批量复制工具 - 规则排列对齐 / 随机摆放
    /// 入口：主窗口 VicToolsWindow 子窗口列表（自动按类名 ObjectDuplicator 注册）
    ///       独立窗口 WinObjectDuplicator（菜单 Tools/VicTools(YD)/[ ScenesTools ] 独立窗口 >/[批量复制]）
    public class ObjectDuplicator : SubWindow
    {
        /// 窗口标签名（包含版本号），与 ScenesTools / ProjectTools 等脚本保持一致，由本类自行管理
        public const string WindowName = "[批量复制 v1.3]";

        // EditorPrefs 键前缀,所有参数存档都加此前缀,避免与其他 VicTools 工具冲突
        private const string PREFS_PREFIX = "ObjectDuplicator_";

        // ───────────────────────── 模式枚举 ─────────────────────────
        private enum DupMode { Grid, Circle, Spiral, Linear, Random }
        private DupMode _mode = DupMode.Grid;

        // 模式中文标签 (顺序与 DupMode 枚举对应)
        private static readonly DupMode[] _allModes = { DupMode.Grid, DupMode.Circle, DupMode.Spiral, DupMode.Linear, DupMode.Random };
        private static readonly string[] _modeLabels = { "网格", "圆形", "螺旋", "线性", "随机" };

        // 模式按钮图标 (25x25 PNG,放在 Editor/Icons~/ 下,缺图时显示白色占位)
        // 路径与 VicToolsWindow 等保持一致;走 VicToolsIconLoader 解码,绕开 AssetDatabase/TextureImporter
        private const string MODE_ICON_FOLDER = "Packages/com.youdoo.victools/Editor/Icons~/";
        private const float MODE_BUTTON_SIZE = 29f;
        private static readonly string[] _modeIconFiles = {
            "ModeGrid.png", "ModeCircle.png", "ModeSpiral.png", "ModeLinear.png", "ModeRandom.png"
        };

        // 模式图标:实例级缓存(避免重复解码) + 持有列表(窗口关闭时统一释放,防泄漏)
        // VicToolsIconLoader 创建的 Texture2D Unity 不会自动回收,必须显式 DestroyImmediate
        private Texture2D[] _modeIconCache;
        private readonly List<Texture2D> _ownedModeIcons = new List<Texture2D>();

        /// 获取枚举对应的中文显示名
        private static string GetModeLabel(DupMode mode)
        {
            int idx = Array.IndexOf(_allModes, mode);
            return idx >= 0 ? _modeLabels[idx] : mode.ToString();
        }

        /// 延迟加载模式按钮图标(从 MODE_ICON_FOLDER 读取,缺图时该位置返回 null,UI 用白色占位)
        /// <remarks>
        /// 走公共 <see cref="VicToolsIconLoader"/> 解码原始 PNG 字节,绕开 AssetDatabase / TextureImporter,
        /// 强制 RGBA32 + readable,免受平台压缩 / isReadable=0 影响。
        /// 加载成功的 Texture2D 登记到 <see cref="_ownedModeIcons"/>,由 OnDisable 统一释放,防泄漏。
        /// </remarks>
        private Texture2D[] GetModeIcons()
        {
            if (_modeIconCache != null && _modeIconCache.Length == _allModes.Length)
                return _modeIconCache;
            _modeIconCache = new Texture2D[_allModes.Length];
            for (int i = 0; i < _modeIconFiles.Length; i++)
            {
                string path = MODE_ICON_FOLDER + _modeIconFiles[i];
                var tex = VicToolsIconLoader.LoadPngIcon(path);
                if (tex != null) _ownedModeIcons.Add(tex);   // 持有引用,OnDisable 统一释放
                _modeIconCache[i] = tex;
            }
            return _modeIconCache;
        }

        // ───────────────────────── 通用参数 ─────────────────────────
        private bool _asChildren;             // true: 作为模板子物体; false: 与模板同父级
        private string _namePrefix = "";       // 名称前缀
        private int _nameStartIndex;          // 名称起始序号

        // ───────────────────────── 网格阵列 ─────────────────────────
        private int _gridCountX = 5;          // 列数
        private int _gridCountZ = 5;          // 行数
        private Vector3 _gridSpacing = new Vector3(2f, 0f, 2f); // 间距
        private bool _gridSpacingLocal;             // true: 间距向量使用源物体局部坐标空间
        private bool _gridCenterOnSource = true; // 网格以模板为中心向四周扩散(否则从模板开始向+X+Z扩展)

        // ───────────────────────── 圆形阵列 ─────────────────────────
        private int _circleCount = 12;
        private float _circleRadius = 5f;
        private float _circleStartAngle;      // 度
        private float _circleArcDegrees = 360f;
        private bool _circleFaceCenter = true;// 副本朝向圆心
        private float _circleYOffset;
        private Vector3 _circleSelfRotation;       // 自身坐标轴基础旋转 (Euler °) - 应用于每个副本
        private Vector3 _circleSelfRotationStep;   // 自身旋转每副本步进 (Euler °) - 第 i 个副本 = 基础 + i*步进

        // ───────────────────────── 螺旋阵列 ─────────────────────────
        private int _spiralCount = 30;
        private float _spiralStartRadius = 1f;
        private float _spiralRadiusStep = 0.2f;
        private float _spiralHeightStep = 0.3f;
        private float _spiralAngleStep = 20f; // 度
        private float _spiralScaleStart = 1f; // 起始缩放(锁定比例:三轴同步);默认 1 → 与原行为一致
        private float _spiralScaleStep  = 0f; // 缩放步进(每副本 +此值);默认 0 → 不缩放

        // ───────────────────────── 线性阵列 ─────────────────────────
        private int _linearCount = 10;
        private Vector3 _linearStep = new Vector3(2f, 0f, 0f);
        private bool _linearStepLocal;                      // true: 步进向量使用源物体局部坐标空间
        private bool _linearStepBySize;                    // true: 每轴步进自动 + 源物体对应轴的世界尺寸(避免相邻副本重叠)
        private bool _linearScale;                          // true: 启用缩放步进
        private Vector3 _linearScaleStart = Vector3.one;    // 起始缩放(三轴独立,锁定比例时仅 Uniform 字段生效)
        private Vector3 _linearScaleStep = Vector3.zero;    // 缩放步进(每副本 +此值)
        private bool _linearScaleLockAspect = true;         // 锁定比例(X/Y/Z 同步缩放)
        private float _linearScaleUniformStart = 1f;        // 锁定比例下的统一起始缩放
        private float _linearScaleUniformStep  = 0f;        // 锁定比例下的统一步进(0 = 不缩放)

        // ───────────────────────── 随机摆放 ─────────────────────────
        private int _randomCount = 20;
        private Vector3 _randomAreaMin = new Vector3(-5f, 0f, -5f);
        private Vector3 _randomAreaMax = new Vector3(5f, 0f, 5f);
        private bool _randomRotation = true;
        private Vector3 _randomRotMin = new Vector3(0f, 0f, 0f);
        private Vector3 _randomRotMax = new Vector3(0f, 360f, 0f);
        private bool _randomScale;
        private Vector3 _randomScaleMin = new Vector3(0.9f, 0.9f, 0.9f);
        private Vector3 _randomScaleMax = new Vector3(1.1f, 1.1f, 1.1f);
        private bool _randomScaleLockAspect = true;        // 默认勾选:X/Y/Z 三轴同步缩放,只需一个 min/max
        private float _randomScaleUniformMin = 0.9f;      // 锁定比例下的统一 min 缩放
        private float _randomScaleUniformMax = 1.1f;      // 锁定比例下的统一 max 缩放
        private bool _useSeed;
        private int _randomSeed;
        private bool _randomAvoidOverlap = true;
        private float _randomMinDistance = 0.5f;
        private int _randomMaxAttempts = 30; // 每个副本最多尝试次数(避让失败则直接放置)

        // ───────────────────────── 状态 ─────────────────────────
        private Vector2 _scrollPos;
        private string _lastStatus = "";
        private bool _showHelp;
        private bool _foldoutGeneral = true;
        private bool _foldoutMode = true;

        // ───────────────────────── 实时更新模式 ─────────────────────────
        private bool _liveMode;                 // 实时模式开关
        private GameObject _liveParent;          // 实时副本统一父物体
        private List<GameObject> _liveSourceCache; // 实时模式下首次生成时锁定的模板(避免选中变化干扰)
        private bool _hasLiveResult;            // 是否已存在实时预览
        private bool _isLiveExecution;          // 内部标志: 标记当前 ExecuteXxx 应输出到 _liveParent
        private const string LIVE_PARENT_NAME = "~VicTools_LiveDuplicates";

        public ObjectDuplicator(string name, EditorWindow parent) : base(WindowName, parent)
        {
        }

        // OnEnable: 由 win_ObjectDuplicator 在窗口启用时调用,负责恢复上次参数
        public override void OnEnable()
        {
            base.OnEnable();
            LoadPrefs();
        }

        // OnDisable: 先持久化当前参数,再清理实时预览引用(不自动销毁,留作"应用"或"清除"按钮处理)
        public override void OnDisable()
        {
            SavePrefs();
            _liveParent = null;
            _liveSourceCache = null;
            _hasLiveResult = false;
            _isLiveExecution = false;

            // 释放通过 VicToolsIconLoader 解码的模式按钮图标(Unity 不会自动回收,必须显式销毁)
            VicToolsIconLoader.ReleaseAll(_ownedModeIcons);
            _modeIconCache = null;   // 缓存数组本身是普通引用,置空即可
        }

        // ───────────────────────── GUI ─────────────────────────
        public override void OnGUI()
        {
            var style = EditorStyle.Get;
            EditorGUILayout.BeginVertical(style.area);

            // ── 标题栏 ──
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("★ 批量复制工具", style.subheading, GUILayout.Width(140));

            // 选中数量 - 兼容独立窗口与主窗口
            int selCount = 0;
            var standaloneWin = Parent as WinObjectDuplicator;
            var mainWin = Parent as VicToolsWindow;
            if (standaloneWin != null) selCount = standaloneWin.StandaloneSelectedCount;
            else if (mainWin != null) selCount = mainWin.globalSelectedObjectsCount;

            GUI.contentColor = Color.cyan;
            EditorGUILayout.LabelField($"   选中: ({selCount})", style.subheading);
            GUI.contentColor = Color.white;

            GUILayout.FlexibleSpace();
            // 还原当前阵列模式参数
            if (GUILayout.Button(
                new GUIContent(EditorGUIUtility.IconContent("d_Refresh").image,
                    $"还原【{GetModeLabel(_mode)}】阵列模式的所有参数为默认值"),new GUIStyle(GUIStyle.none) { alignment = TextAnchor.MiddleCenter },
                GUILayout.Width(20), GUILayout.Height(20)))
            {
                ResetCurrentModeParams();
                _lastStatus = $"已还原【{GetModeLabel(_mode)}】阵列模式所有参数为默认值";

                // 实时模式激活且已有预览: 立即重建预览,使场景与还原后的默认值同步
                // 条件与下方参数变更检测 (paramChanged && _liveMode && _hasLiveResult) 保持一致
                if (_liveMode && _hasLiveResult)
                {
                    RefreshLive();
                }

                GUI.FocusControl(null);   // 移除焦点,确保 IntField/Slider 等立即按新值刷新
                GUIUtility.ExitGUI();     // 立刻结束本帧 GUI,避免 OnGUI 继续绘制旧值造成一帧错位
            }

            // 实时模式开关
            GUI.contentColor = _liveMode ? Color.yellow : Color.white;
            bool newLive = GUILayout.Toggle(_liveMode,
                new GUIContent("实时", "勾选后启用实时预览模式:\n点击【开始实时预览】即可生成预览对象\n之后修改任何参数都会自动重新生成"),
                "Button", GUILayout.Width(40));
            GUI.contentColor = Color.white;
            if (newLive != _liveMode)
            {
                _liveMode = newLive;
                if (!_liveMode) _lastStatus = "实时模式已关闭 (预览仍保留在场景中,可手动应用或清除)";
            }

            _showHelp = GUILayout.Toggle(_showHelp, new GUIContent("?", "显示/隐藏使用帮助"), "Button", GUILayout.Width(22));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // ── 模式选择 ── 5 个 25x25 图标按钮,单选(按下时其他取消)
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("阵列模式:", style.normalfont, GUILayout.Width(70));
            var modeIcons = GetModeIcons();
            // 模式按钮样式:继承自内置 "Button" 保留 normal/hover/active 状态视觉反馈,
            // 仅覆盖 alignment 让图标在固定格中居中显示
            // 关键: padding 必须 = 0,否则 Unity 内置 "Button" 的 ~4-6px 内边距
            //       会把 25x25 图标缩到约 17x17,无法 1:1 显示
            var modeBtnStyle = new GUIStyle("Button")
            {
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 2, 2),
            };
            for (int i = 0; i < _allModes.Length; i++)
            {
                var icon = modeIcons[i];
                var content = new GUIContent(
                    icon != null ? icon : Texture2D.whiteTexture,
                    _modeLabels[i] + " 阵列模式");
                bool isActive = _mode == _allModes[i];
                bool clicked = GUILayout.Toggle(isActive, content, modeBtnStyle,
                    GUILayout.Width(MODE_BUTTON_SIZE), GUILayout.Height(MODE_BUTTON_SIZE));
                if (clicked && !isActive)
                {
                    _mode = _allModes[i];
                    _lastStatus = "";
                }
                EditorGUILayout.Space(10);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // ── 可滚动参数区 ──
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            if (_showHelp)
            {
                EditorGUILayout.HelpBox(
                    "操作流程:\n" +
                    "  1. 在 Hierarchy 中选择 1 个或多个对象作为复制模板\n" +
                    "  2. 选择阵列模式 (网格/圆形/螺旋/线性/随机)\n" +
                    "  3. 调整下方参数\n" +
                    "  4. 点击【执行复制】批量生成副本 (支持 Undo 撤销)\n\n" +
                    "实时更新模式 (Live):\n" +
                    "  • 勾选标题栏 [实时] 开关 → 启用实时预览\n" +
                    "  • 点击【开始实时预览】生成预览副本(场景中会产生 ~VicTools_LiveDuplicates 父物体)\n" +
                    "  • 之后修改任何阵列参数(列数/间距/半径/角度/区域…)\n" +
                    "    都会自动销毁并重新生成预览(不写入 Undo 栈,免污染)\n" +
                    "  • 满意后点【应用到场景】将预览正式落盘(写入 Undo 一步)\n" +
                    "  • 不满意点【清除预览】一键销毁\n\n" +
                    "提示:\n" +
                    "  • 阵列模式 (网格/圆形/螺旋/线性): 副本位置 = 模板位置 + 偏移\n" +
                    "  • 随机模式: 副本位置 = 模板位置 + 区域内随机偏移\n" +
                    "  • 默认以模板世界位置为锚点;子物体模式下副本将作为模板子级",
                    MessageType.Info);
                EditorGUILayout.Space();
            }

            // ── 变更检测开始 (实时模式) ──
            EditorGUI.BeginChangeCheck();

            // ── 通用设置 ──
            EditorStyle.DrawSubheadingFoldout(ref _foldoutGeneral, "通用设置", style.subheading);
            if (_foldoutGeneral) DrawGeneralSection();

            EditorGUILayout.Space();

            // ── 模式参数 ──
            EditorStyle.DrawSubheadingFoldout(ref _foldoutMode, $"{GetModeLabel(_mode)} 阵列参数", style.subheading);
            if (_foldoutMode)
            {
                switch (_mode)
                {
                    case DupMode.Grid: DrawGridSection(); break;
                    case DupMode.Circle: DrawCircleSection(); break;
                    case DupMode.Spiral: DrawSpiralSection(); break;
                    case DupMode.Linear: DrawLinearSection(); break;
                    case DupMode.Random: DrawRandomSection(); break;
                }

                // ── 公共参数: 随机扰动(旋转/缩放) ──
                // 在所有模式中都能用,与下方变更检测 + live 模式自动重生成配合,
                // 修改随机参数会立即看到新副本的旋转/缩放。
                DrawRandomVariationSection();
            }

            EditorGUILayout.Space();

            // ── 变更检测结束 ──
            bool paramChanged = EditorGUI.EndChangeCheck();

            // 实时模式: 任意参数变更 → 自动重新生成
            if (paramChanged && _liveMode && _hasLiveResult)
            {
                RefreshLive();
            }

            // ── 执行按钮 ──
            DrawExecuteButton();

            if (!string.IsNullOrEmpty(_lastStatus))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(_lastStatus, MessageType.Info);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        /// 还原 <b>当前</b> 阵列模式(<see cref="_mode"/>)的所有参数为类内联初始化时的默认值。
        /// <para>不修改其它模式的参数,也不修改通用参数(作为子物体/名称前缀等) ——
        /// 标题栏的"还原"按钮仅作用于当前选中的阵列模式,避免误操作波及无关参数。</para>
        /// <para>默认值与字段声明保持一致(网格 5x5@2/圆形 12@360°/螺旋 30/随机 20 等),
        /// 修改字段默认值后请同步更新此处。</para>
        private void ResetCurrentModeParams()
        {
            switch (_mode)
            {
                case DupMode.Grid:
                    _gridCountX = 5;
                    _gridCountZ = 5;
                    _gridSpacing = new Vector3(2f, 0f, 2f);
                    _gridSpacingLocal = false;
                    _gridCenterOnSource = true;
                    break;

                case DupMode.Circle:
                    _circleCount = 12;
                    _circleRadius = 5f;
                    _circleStartAngle = 0f;
                    _circleArcDegrees = 360f;
                    _circleFaceCenter = true;
                    _circleYOffset = 0f;
                    _circleSelfRotation = Vector3.zero;
                    _circleSelfRotationStep = Vector3.zero;
                    break;

                case DupMode.Spiral:
                    _spiralCount = 30;
                    _spiralStartRadius = 1f;
                    _spiralRadiusStep = 0.2f;
                    _spiralHeightStep = 0.3f;
                    _spiralAngleStep = 20f;
                    _spiralScaleStart = 1f;
                    _spiralScaleStep = 0f;
                    break;

                case DupMode.Linear:
                    _linearCount = 10;
                    _linearStep = new Vector3(2f, 0f, 0f);
                    _linearStepLocal = false;
                    _linearStepBySize = false;
                    _linearScale = false;
                    _linearScaleStart = Vector3.one;
                    _linearScaleStep = Vector3.zero;
                    _linearScaleLockAspect = true;
                    _linearScaleUniformStart = 1f;
                    _linearScaleUniformStep = 0f;
                    break;

                case DupMode.Random:
                    _randomCount = 20;
                    _randomAreaMin = new Vector3(-5f, 0f, -5f);
                    _randomAreaMax = new Vector3(5f, 0f, 5f);
                    _randomRotation = true;
                    _randomRotMin = new Vector3(0f, 0f, 0f);
                    _randomRotMax = new Vector3(0f, 360f, 0f);
                    _randomScale = false;
                    _randomScaleMin = new Vector3(0.9f, 0.9f, 0.9f);
                    _randomScaleMax = new Vector3(1.1f, 1.1f, 1.1f);
                    _randomScaleLockAspect = true;
                    _randomScaleUniformMin = 0.9f;
                    _randomScaleUniformMax = 1.1f;
                    _useSeed = false;
                    _randomSeed = 0;
                    _randomAvoidOverlap = true;
                    _randomMinDistance = 0.5f;
                    _randomMaxAttempts = 30;
                    break;
            }
        }

        // 折叠头已统一为 EditorStyle.DrawSubheadingFoldout(...) (见 VicToolsStyle.cs)

        private void DrawGeneralSection()
        {
            var style = EditorStyle.Get;
            EditorGUILayout.BeginVertical(style.subheading2);

            _asChildren = EditorGUILayout.ToggleLeft(
                new GUIContent("作为子物体 (否则与模板同父级)", "勾选: 副本为模板的子物体\n不勾选: 副本与模板平级(共享父物体)"),
                _asChildren);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("名称前缀:", style.normalfont, GUILayout.Width(60));
            _namePrefix = EditorGUILayout.TextField(_namePrefix);
            EditorGUILayout.LabelField("起始序号:", style.normalfont, GUILayout.Width(60));
            _nameStartIndex = EditorGUILayout.IntField(_nameStartIndex);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
        }

        private void DrawGridSection()
        {
            var style = EditorStyle.Get;
            EditorGUILayout.BeginVertical(style.subheading2);
            _gridCountX = CreateIntSliderWithEvent("列数 X", _gridCountX, 1, 100, null, 0, 0, 70);
            _gridCountZ = CreateIntSliderWithEvent("行数 Z", _gridCountZ, 1, 100, null, 0, 0, 70);

            // ── 间距(水平布局: 100px 标签 + Vector3 字段 + 右侧"局部坐标" toggle, 与线性模式对齐) ──
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("间距 (X,Y,Z)", GUILayout.Width(100));
            _gridSpacingLocal = GUILayout.Toggle(
                _gridSpacingLocal,
                new GUIContent("局部坐标", "勾选: 间距向量在源物体局部坐标系下解释(随源物体旋转);\n不勾选: 间距向量在世界坐标系下解释。"),
                GUILayout.Width(84f));
            EditorGUILayout.EndHorizontal();
            _gridSpacing = EditorGUILayout.Vector3Field(GUIContent.none, _gridSpacing);

            _gridCenterOnSource = EditorGUILayout.ToggleLeft(
                new GUIContent("以模板为中心", "勾选: 网格以模板位置为对称中心; 不勾选: 从模板向 +X/+Z 扩展"),
                _gridCenterOnSource);
            EditorGUILayout.EndVertical();
        }

        private void DrawCircleSection()
        {
            var style = EditorStyle.Get;
            EditorGUILayout.BeginVertical(style.subheading2);
            _circleCount = CreateIntSliderWithEvent("数量", _circleCount, 1, 200, null, 0, 0, 70);
            _circleRadius = EditorGUILayout.Slider("半径", _circleRadius, 0f, 100f);
            _circleStartAngle = EditorGUILayout.Slider("起始角度 (°)", _circleStartAngle, 0f, 360f);
            _circleArcDegrees = EditorGUILayout.Slider("弧度范围 (°)", _circleArcDegrees, 0f, 360f);
            _circleFaceCenter = EditorGUILayout.ToggleLeft("副本朝向圆心", _circleFaceCenter);
            _circleYOffset = EditorGUILayout.Slider("Y 偏移", _circleYOffset, -50f, 50f);

            EditorGUILayout.Space();
            // ── 自身坐标轴旋转 (在副本自身本地坐标系下生效) ──
            EditorGUILayout.LabelField("自身坐标轴旋转 (Euler °):", style.normalfont);
            _circleSelfRotation = EditorGUILayout.Vector3Field(
                new GUIContent("  基础旋转", "应用于每个副本的自身坐标系旋转,先于步进"),
                _circleSelfRotation);
            _circleSelfRotationStep = EditorGUILayout.Vector3Field(
                new GUIContent("  每副本步进", "每多一个副本,自身旋转额外叠加的角度\n例如 (0,10,0) = 每个副本绕自身 Y 多转 10°"),
                _circleSelfRotationStep);

            EditorGUILayout.EndVertical();
        }

        private void DrawSpiralSection()
        {
            var style = EditorStyle.Get;
            EditorGUILayout.BeginVertical(style.subheading2);
            _spiralCount = CreateIntSliderWithEvent("数量", _spiralCount, 1, 500, null, 0, 0, 70);
            _spiralStartRadius = EditorGUILayout.Slider("起始半径", _spiralStartRadius, 0f, 100f);
            _spiralRadiusStep = EditorGUILayout.Slider("半径步进", _spiralRadiusStep, -10f, 10f);
            _spiralHeightStep = EditorGUILayout.Slider("高度步进", _spiralHeightStep, -10f, 10f);
            _spiralAngleStep = EditorGUILayout.Slider("角度步进 (°)", _spiralAngleStep, -360f, 360f);
            // 缩放步进(锁定比例:三轴同步)
            // 起始=1 且 步进=0 → 不缩放,与原行为一致
            _spiralScaleStart = EditorGUILayout.Slider(
                new GUIContent("起始缩放", "第 0 个副本的缩放(锁定比例:三轴同步)。默认 1.0 表示与源物体同尺寸。"),
                _spiralScaleStart, 0f, 10f);
            _spiralScaleStep = EditorGUILayout.Slider(
                new GUIContent("缩放步进", "每副本缩放增量。第 i 个副本缩放 = 起始 + i * 步进。0 = 不缩放。"),
                _spiralScaleStep, -2f, 2f);
            EditorGUILayout.EndVertical();
        }

        private void DrawLinearSection()
        {
            var style = EditorStyle.Get;
            EditorGUILayout.BeginVertical(style.subheading2);
            _linearCount = CreateIntSliderWithEvent("数量", _linearCount, 1, 200, null, 0, 0, 70);

            // ── 步进向量(水平布局: 100px 标签 + Vector3 字段 + 右侧"局部坐标" toggle) ──
            // 锁定比例/局部坐标 toggle 统一宽度,与下方"锁定比例"对齐
            const float LOCK_TOGGLE_WIDTH = 84f;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("步进向量:", GUILayout.Width(100));
            _linearStepLocal = GUILayout.Toggle(
                _linearStepLocal,
                new GUIContent("局部坐标", "勾选: 步进向量在源物体局部坐标系下解释(随源物体旋转);\n不勾选: 步进向量在世界坐标系下解释。"),
                GUILayout.Width(LOCK_TOGGLE_WIDTH));
            _linearStepBySize = GUILayout.Toggle(
                _linearStepBySize,
                new GUIContent("按体积",
                    "根据复制体X轴长度计算偏移，可避免复制体重叠。"),
                GUILayout.Width(60));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            GUI.backgroundColor = Color.blue;
            if (GUILayout.Button(new GUIContent("MIN.x", "x轴设置最小步进值"), GUILayout.Width(46)))
            {
                _linearStep.x = 0.00001f;
            }
            _linearStep = EditorGUILayout.Vector3Field(GUIContent.none, _linearStep);
            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
            // ── 缩放步进(参照随机模式布局: toggle + 锁定 toggle 在同一行;两行 Start/Step 输入)──
            // Toggle 行
            EditorGUILayout.BeginHorizontal();
            _linearScale = EditorGUILayout.ToggleLeft("线性缩放", _linearScale, GUILayout.Width(100));
            _linearScaleLockAspect = GUILayout.Toggle(
                _linearScaleLockAspect,
                new GUIContent("锁定比例", "勾选后三轴同步缩放(X=Y=Z),只用一个 Start/Step;\n取消勾选可分别为三轴设置 Start/Step。"),
                GUILayout.Width(LOCK_TOGGLE_WIDTH));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (_linearScale)
            {
                // Start 行
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("  Scale Start:", GUILayout.Width(70));
                if (_linearScaleLockAspect)
                    _linearScaleUniformStart = EditorGUILayout.Slider(GUIContent.none, _linearScaleUniformStart, 0f, 10f);
                else
                    _linearScaleStart = EditorGUILayout.Vector3Field(GUIContent.none, _linearScaleStart);
                EditorGUILayout.EndHorizontal();

                // Step 行
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("  Scale Step:", GUILayout.Width(70));
                if (_linearScaleLockAspect)
                    _linearScaleUniformStep = EditorGUILayout.Slider(GUIContent.none, _linearScaleUniformStep, -2f, 2f);
                else
                    _linearScaleStep = EditorGUILayout.Vector3Field(GUIContent.none, _linearScaleStep);
                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRandomSection()
        {
            var style = EditorStyle.Get;
            EditorGUILayout.BeginVertical(style.subheading2);

            _randomCount = CreateIntSliderWithEvent("数量", _randomCount, 1, 1000, null, 0, 0, 70);

            EditorGUILayout.LabelField("区域范围 (相对模板位置):");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Min:", GUILayout.Width(40));
            _randomAreaMin = EditorGUILayout.Vector3Field(GUIContent.none, _randomAreaMin);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Max:", GUILayout.Width(40));
            _randomAreaMax = EditorGUILayout.Vector3Field(GUIContent.none, _randomAreaMax);
            EditorGUILayout.EndHorizontal();

            _randomAvoidOverlap = EditorGUILayout.ToggleLeft("简单最小距离避让", _randomAvoidOverlap);
            if (_randomAvoidOverlap)
            {
                _randomMinDistance = EditorGUILayout.Slider("最小距离", _randomMinDistance, 0f, 50f);
                _randomMaxAttempts = EditorGUILayout.IntSlider("最大尝试次数", _randomMaxAttempts, 1, 500);
            }

            EditorGUILayout.EndVertical();
        }

        // ── 公共参数: 随机扰动(旋转/缩放) ──
        // 适用于所有模式(线性/网格/圆形/螺旋/随机),在 mode 折叠底部统一呈现。
        // 实现: 见 ApplyRandomVariation,在每个 Execute* 的 CreateCopyEx 前调用。
        private void DrawRandomVariationSection()
        {
            var style = EditorStyle.Get;
            EditorGUILayout.Space(4);

            // ── 顶部横线: 与上方模式参数分隔 ──
            Rect sep = EditorGUILayout.GetControlRect(false, 2);
            EditorGUI.DrawRect(sep, new Color(0.9f, 0.8f, 0.3f, 0.55f));

            EditorGUILayout.BeginVertical(style.subheading2);

            // ── 随机旋转 (Euler) ──
            _randomRotation = EditorGUILayout.ToggleLeft("随机旋转 (Local)", _randomRotation);
            if (_randomRotation)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("  Rot Min:", GUILayout.Width(70));
                _randomRotMin = EditorGUILayout.Vector3Field(GUIContent.none, _randomRotMin);
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("  Rot Max:", GUILayout.Width(70));
                _randomRotMax = EditorGUILayout.Vector3Field(GUIContent.none, _randomRotMax);
                EditorGUILayout.EndHorizontal();
            }

            // ── 随机缩放 (锁定比例 toggle) ──
            const float LOCK_TOGGLE_WIDTH = 84f;
            EditorGUILayout.BeginHorizontal();
            _randomScale = EditorGUILayout.ToggleLeft("随机缩放", _randomScale, GUILayout.Width(100));
            _randomScaleLockAspect = GUILayout.Toggle(
                _randomScaleLockAspect,
                new GUIContent("锁定比例", "勾选后三轴同步缩放(X=Y=Z),只用一个 Min/Max;\n取消勾选可分别为三轴设置 Min/Max。"),
                GUILayout.Width(LOCK_TOGGLE_WIDTH));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            if (_randomScale)
            {
                // Min 行
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("  Scale Min:", GUILayout.Width(70));
                if (_randomScaleLockAspect)
                    _randomScaleUniformMin = EditorGUILayout.Slider(GUIContent.none, _randomScaleUniformMin, 0f, 10f);
                else
                    _randomScaleMin = EditorGUILayout.Vector3Field(GUIContent.none, _randomScaleMin);
                EditorGUILayout.EndHorizontal();

                // Max 行
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("  Scale Max:", GUILayout.Width(70));
                if (_randomScaleLockAspect)
                    _randomScaleUniformMax = EditorGUILayout.Slider(GUIContent.none, _randomScaleUniformMax, 0f, 10f);
                else
                    _randomScaleMax = EditorGUILayout.Vector3Field(GUIContent.none, _randomScaleMax);
                EditorGUILayout.EndHorizontal();
            }

            // ── 固定随机种子: 所有模式共享(配合 _useSeed 在 5 个 Execute* 中按种子创建 rng) ──
            _useSeed = EditorGUILayout.ToggleLeft("使用固定随机种子", _useSeed);
            if (_useSeed)
            {
                _randomSeed = EditorGUILayout.IntField("种子", _randomSeed);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawExecuteButton()
        {
            var style = EditorStyle.Get;
            var selected = Selection.gameObjects;

            if (_liveMode)
            {
                // ── 实时模式按钮组 ──
                bool hasSel = selected.Length > 0;

                // 主按钮: 开始实时预览 / 重新生成
                EditorGUI.BeginDisabledGroup(!hasSel);
                GUI.backgroundColor = new Color(1f, 0.85f, 0.2f);
                string mainLabel = _hasLiveResult ? "重新生成预览" : "开始实时预览";
                string mainTip = _hasLiveResult
                    ? "使用当前选中的对象重新生成实时预览 (Undo 不可用)"
                    : $"基于当前选中的 {selected.Length} 个模板按 {GetModeLabel(_mode)} 模式生成预览";
                if (GUILayout.Button(new GUIContent(mainLabel, mainTip), style.normalButton_G, GUILayout.Height(32)))
                {
                    ExecuteDuplicate(); // 内部根据 _liveMode 走 RefreshLive 路径
                }
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.BeginHorizontal();

                // 应用到场景
                EditorGUI.BeginDisabledGroup(!_hasLiveResult);
                GUI.backgroundColor = new Color(0.4f, 0.9f, 0.4f);
                if (GUILayout.Button(new GUIContent("应用到场景", "将实时预览中的所有副本正式应用到场景 (写入 Undo 一步,支持撤销)"), style.normalButton, GUILayout.Height(24)))
                {
                    BakeLiveToScene();
                }
                GUI.backgroundColor = Color.white;

                // 清除预览
                GUI.backgroundColor = new Color(0.95f, 0.5f, 0.5f);
                if (GUILayout.Button(new GUIContent("清除预览", "销毁实时预览所有副本 (不可撤销)"), style.normalButton, GUILayout.Height(24)))
                {
                    ClearLive();
                }
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup();

                EditorGUILayout.EndHorizontal();

                if (!hasSel && !_hasLiveResult)
                {
                    EditorGUILayout.HelpBox("请先在 Hierarchy 中选择 1 个或多个对象作为复制模板", MessageType.Warning);
                }
                else if (_hasLiveResult)
                {
                    int childCount = _liveParent != null ? _liveParent.transform.childCount : 0;
                    string selTip = hasSel ? $"  当前选中: {selected.Length}" : "  (未选中,沿用首次锁定的模板)";
                    EditorGUILayout.LabelField($"LIVE • 预览父物体: {LIVE_PARENT_NAME}  子物体: {childCount}{selTip}", style.normalfont_Hui);
                }
            }
            else
            {
                // ── 普通模式 ──
                EditorGUI.BeginDisabledGroup(selected.Length == 0);
                GUI.backgroundColor = Color.green;
                if (GUILayout.Button(new GUIContent("执行复制", $"基于选中的 {selected.Length} 个模板对象按 {GetModeLabel(_mode)} 模式生成副本"), style.normalButton_G, GUILayout.Height(32)))
                {
                    ExecuteDuplicate();
                }
                GUI.backgroundColor = Color.white;
                EditorGUI.EndDisabledGroup();

                if (selected.Length == 0)
                {
                    EditorGUILayout.HelpBox("请先在 Hierarchy 中选择 1 个或多个对象作为复制模板", MessageType.Warning);
                }
                else
                {
                    EditorGUILayout.LabelField($"将基于 {selected.Length} 个模板对象生成副本 (每模板 {GetModeLabel(_mode)} 数量依模式而定)", style.normalfont_Hui);
                }
            }
        }

        // ───────────────────────── 执行入口 ─────────────────────────
        private void ExecuteDuplicate()
        {
            // 实时模式: 走 RefreshLive (无 Undo)
            if (_liveMode)
            {
                RefreshLive();
                return;
            }

            var sources = Selection.gameObjects;
            if (sources == null || sources.Length == 0) return;

            // 过滤掉 null / 模板对象不在场景中的项 (不允许 PrefabAsset)
            var validSources = new List<GameObject>(sources.Length);
            foreach (var s in sources)
            {
                if (s == null) continue;
                if (EditorUtility.IsPersistent(s)) continue; // 资产对象不能复制到场景
                validSources.Add(s);
            }
            if (validSources.Count == 0)
            {
                EditorUtility.DisplayDialog("批量复制", "所选对象均为项目资产,无法复制到场景。请选择 Hierarchy 中的场景对象。", "确定");
                return;
            }

            _isLiveExecution = false;
            int total = 0;
            Undo.SetCurrentGroupName($"批量复制 {validSources.Count} 个模板 [{GetModeLabel(_mode)}]");
            int group = Undo.GetCurrentGroup();

            try
            {
                switch (_mode)
                {
                    case DupMode.Grid:    total += ExecuteGrid(validSources); break;
                    case DupMode.Circle:  total += ExecuteCircle(validSources); break;
                    case DupMode.Spiral:  total += ExecuteSpiral(validSources); break;
                    case DupMode.Linear:  total += ExecuteLinear(validSources); break;
                    case DupMode.Random:  total += ExecuteRandom(validSources); break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[批量复制] 失败: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("批量复制失败", ex.Message, "确定");
                Undo.RevertAllDownToGroup(group);
                return;
            }

            Undo.CollapseUndoOperations(group);
            _lastStatus = $"完成! 共生成 {total} 个副本 (基于 {validSources.Count} 个模板,模式 {GetModeLabel(_mode)})";
            Parent?.Repaint();
        }

        // ───────────────────────── 网格阵列 ─────────────────────────
        private int ExecuteGrid(List<GameObject> sources)
        {
            int created = 0;
            int halfX = _gridCenterOnSource ? (_gridCountX - 1) / 2 : 0;
            int halfZ = _gridCenterOnSource ? (_gridCountZ - 1) / 2 : 0;
            System.Random rng = _useSeed ? new System.Random(_randomSeed) : new System.Random();

            foreach (var src in sources)
            {
                Vector3 srcPos = src.transform.position;
                Quaternion srcRot = src.transform.rotation;

                for (int ix = 0; ix < _gridCountX; ix++)
                for (int iz = 0; iz < _gridCountZ; iz++)
                {
                    int idx = ix * _gridCountZ + iz;
                    Vector3 offset = new Vector3(
                        (ix - halfX) * _gridSpacing.x,
                        (iz - halfZ) * _gridSpacing.y,
                        (iz - halfZ) * _gridSpacing.z);
                    // 间距向量: 局部坐标 → 乘以源物体旋转;世界坐标 → 直接相加
                    if (_gridSpacingLocal)
                        offset = srcRot * offset;
                    // 注: Y 间距沿 Z 方向递增,若需要 Y 沿 X 递增请调整 spacing
                    Vector3 worldPos = srcPos + offset;
                    ApplyRandomVariation(srcRot, Vector3.one, rng, out Quaternion rot, out Vector3 scl);
                    CreateCopy(src, worldPos, rot, scl, idx, ref created);
                }
            }
            return created;
        }

        // ───────────────────────── 圆形阵列 ─────────────────────────
        private int ExecuteCircle(List<GameObject> sources)
        {
            int created = 0;
            float arc = Mathf.Max(0.0001f, _circleArcDegrees);
            // 分母用 _circleCount 而非 (_circleCount-1):
            //   避免 arc=360° 时首尾副本完全重叠
            //   例: arc=360°, count=12 → step=30°, 副本在 0°/30°/…/330°, 末副本不再撞回首副本
            float stepAngle = _circleCount > 0 ? arc / _circleCount : 0f;
            System.Random rng = _useSeed ? new System.Random(_randomSeed) : new System.Random();

            foreach (var src in sources)
            {
                Vector3 srcPos = src.transform.position;
                Quaternion srcRot = src.transform.rotation;

                for (int i = 0; i < _circleCount; i++)
                {
                    float deg = _circleStartAngle + i * stepAngle;
                    float rad = deg * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(
                        Mathf.Cos(rad) * _circleRadius,
                        _circleYOffset,
                        Mathf.Sin(rad) * _circleRadius);
                    Vector3 worldPos = srcPos + offset;

                    Quaternion rot = srcRot;
                    if (_circleFaceCenter)
                    {
                        // 朝向模板(圆心)
                        Vector3 dir = (srcPos - worldPos);
                        dir.y = 0f;
                        if (dir.sqrMagnitude > 0.0001f)
                            rot = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    }

                    // 自身坐标轴旋转: 在副本本地坐标系下叠加 (基础 + i*步进)
                    // rot * Euler 表示在 rot 的本地空间内进一步旋转,符合"自身坐标轴"语义
                    Vector3 selfEuler = _circleSelfRotation + _circleSelfRotationStep * i;
                    if (selfEuler.sqrMagnitude > 0.0001f)
                        rot = rot * Quaternion.Euler(selfEuler);

                    ApplyRandomVariation(rot, Vector3.one, rng, out Quaternion finalRot, out Vector3 scl);
                    CreateCopy(src, worldPos, finalRot, scl, i, ref created);
                }
            }
            return created;
        }

        // ───────────────────────── 螺旋阵列 ─────────────────────────
        private int ExecuteSpiral(List<GameObject> sources)
        {
            int created = 0;
            float angleStep = Mathf.Max(0.0001f, _spiralAngleStep);
            System.Random rng = _useSeed ? new System.Random(_randomSeed) : new System.Random();

            foreach (var src in sources)
            {
                Vector3 srcPos = src.transform.position;
                Quaternion srcRot = src.transform.rotation;

                for (int i = 0; i < _spiralCount; i++)
                {
                    float r = _spiralStartRadius + i * _spiralRadiusStep;
                    float deg = i * angleStep;
                    float rad = deg * Mathf.Deg2Rad;
                    Vector3 offset = new Vector3(
                        Mathf.Cos(rad) * r,
                        i * _spiralHeightStep,
                        Mathf.Sin(rad) * r);
                    Vector3 worldPos = srcPos + offset;

                    // 缩放(锁定比例:三轴同步);步进=0 且 起始=1 时与原行为(Vector3.one)一致
                    float uniformScale = _spiralScaleStart + i * _spiralScaleStep;
                    Vector3 worldScale = new Vector3(uniformScale, uniformScale, uniformScale);

                    ApplyRandomVariation(srcRot, worldScale, rng, out Quaternion finalRot, out Vector3 finalScale);
                    CreateCopyEx(src, worldPos, finalRot, finalScale, i, ref created);
                }
            }
            return created;
        }

        // ───────────────────────── 线性阵列 ─────────────────────────
        private int ExecuteLinear(List<GameObject> sources)
        {
            int created = 0;
            System.Random rng = _useSeed ? new System.Random(_randomSeed) : new System.Random();
            foreach (var src in sources)
            {
                Vector3 srcPos = src.transform.position;
                Quaternion srcRot = src.transform.rotation;

                // ── 按体积模式: 预计算每副本的世界尺寸 + 累计尺寸 ──
                // 目的: 让"任意两副本边界间距 == userStep.x"恒成立。
                //       当【线性缩放】导致副本大小不一时,旧的 userStep+srcSize 写法
                //       会让小副本留缝、大副本重叠;改为按链累加每副本自身厚度即可消除。
                // 关闭 _linearStepBySize 时 copySizes/cumSizes 保持 null,走原简单路径(轻量分支)。
                Vector3 srcSize = Vector3.zero;
                Vector3[] copySizes = null;
                Vector3[] cumSizes  = null;
                if (_linearStepBySize)
                {
                    srcSize   = GetSourceWorldSize(src);
                    copySizes = new Vector3[_linearCount];
                    cumSizes  = new Vector3[_linearCount];
                    Vector3 runningSum = Vector3.zero;
                    for (int k = 0; k < _linearCount; k++)
                    {
                        // 该副本应用 _linearScale 后的"世界尺寸缩放" (相对源)
                        Vector3 scaleK = _linearScale ? ComputeLinearScale(k) : Vector3.one;
                        copySizes[k] = Vector3.Scale(srcSize, scaleK);
                        cumSizes[k]  = runningSum;     // cumSizes[0] = 0
                        runningSum  += copySizes[k];
                    }
                }

                // 缓存轴向常量,内层循环直接用,避免每帧重复 Sign/Abs/==0f。
                bool   hasX = _linearStep.x != 0f;
                bool   hasY = _linearStep.y != 0f;
                bool   hasZ = _linearStep.z != 0f;
                float signX = hasX ? Mathf.Sign(_linearStep.x) : 0f;
                float absX  = hasX ? Mathf.Abs (_linearStep.x) : 0f;
                float stepX = _linearStep.x;
                float stepY = _linearStep.y;
                float stepZ = _linearStep.z;

                for (int i = 0; i < _linearCount; i++)
                {
                    // 偏移 = (i + 1) * step 而非 i * step —— 让源物体自身作为第 0 个,
                    // 首个副本位于 1*step 处,避免重叠在源位置(原行为 i=0 → offset=0 → 完全重合)
                    //
                    // 按体积(X 链式): 公式 center[i] = srcPos + Sign(x) * ((i+1)*|x|
                    //                                  + (srcSize.x+size[i].x)/2 + cumSize[i].x)
                    //   → 相邻副本边界间距恒 = |userStep.x|,与线性缩放无关(小物体不显空,大物体不重叠)
                    // Y/Z 纯步进: pivot 居底 / 深度方向不堆叠,加 size 会悬空/陷地/无意义。
                    // 0 轴 mask: 用户填 0 的轴完全不进入 size/cumSize 累加,保证零位移。
                    float xTerm, yTerm, zTerm;
                    if (_linearStepBySize && copySizes != null)
                    {
                        // X 链式: 相邻副本边界间距恒 = |userStep.x|
                        xTerm = hasX
                            ? signX * ((i + 1) * absX + (srcSize.x + copySizes[i].x) * 0.5f + cumSizes[i].x)
                            : 0f;
                    }
                    else
                    {
                        // 关闭【按体积】: 原行为,偏移 = (i + 1) * step(无 size 调整)
                        xTerm = hasX ? (i + 1) * stepX : 0f;
                    }
                    yTerm = hasY ? (i + 1) * stepY : 0f;
                    zTerm = hasZ ? (i + 1) * stepZ : 0f;
                    Vector3 stepOffset = new Vector3(xTerm, yTerm, zTerm);

                    if (_linearStepLocal)
                        stepOffset = srcRot * stepOffset;
                    Vector3 worldPos = srcPos + stepOffset;

                    // 缩放: 与预计算 copySizes 时的 scaleK 保持一致,保证 size 预测 = 实际渲染
                    Vector3 worldScale = _linearScale ? ComputeLinearScale(i) : Vector3.one;

                    ApplyRandomVariation(srcRot, worldScale, rng, out Quaternion finalRot, out Vector3 finalScale);
                    CreateCopyEx(src, worldPos, finalRot, finalScale, i, ref created);
                }
            }
            return created;
        }

        /// 计算线性阵列中第 i 个副本(0-based)的"世界缩放"——即会作为 <c>worldScale</c> 传入 <see cref="CreateCopyEx"/> 的值。
        /// <para>在 <see cref="ExecuteLinear"/> 的按体积预计算中,副本 i 的世界尺寸 = <c>Vector3.Scale(srcSize, ComputeLinearScale(i))</c>,
        /// 两者必须用同一函数,否则预测的尺寸会与实际渲染的尺寸不一致(出现"链算对了,渲染却重叠/留缝")。</para>
        /// <para>复制自原 ExecuteLinear 内联逻辑,语义不变;外部调用前请确保 <c>_linearScale == true</c>。</para>
        private Vector3 ComputeLinearScale(int i)
        {
            if (_linearScaleLockAspect)
            {
                float s = _linearScaleUniformStart + i * _linearScaleUniformStep;
                return new Vector3(s, s, s);
            }
            else
            {
                return new Vector3(
                    _linearScaleStart.x + i * _linearScaleStep.x,
                    _linearScaleStart.y + i * _linearScaleStep.y,
                    _linearScaleStart.z + i * _linearScaleStep.z);
            }
        }

        // ───────────────────────── 随机摆放 ─────────────────────────
        private int ExecuteRandom(List<GameObject> sources)
        {
            int created = 0;

            // 区域校正: 确保 min < max
            Vector3 areaMin = Vector3.Min(_randomAreaMin, _randomAreaMax);
            Vector3 areaMax = Vector3.Max(_randomAreaMin, _randomAreaMax);

            // 随机源
            System.Random rng;
            if (_useSeed)
                rng = new System.Random(_randomSeed);
            else
                rng = new System.Random();

            // 每个模板独立一组副本 + 各自的已占用位置
            foreach (var src in sources)
            {
                Vector3 srcPos = src.transform.position;
                Quaternion srcRot = src.transform.rotation;
                var occupied = new List<Vector3>(_randomCount);

                for (int i = 0; i < _randomCount; i++)
                {
                    Vector3 worldPos;
                    if (_randomAvoidOverlap && _randomMinDistance > 0f)
                    {
                        worldPos = SampleAvoidPosition(rng, srcPos, areaMin, areaMax, occupied);
                    }
                    else
                    {
                        worldPos = srcPos + new Vector3(
                            NextRange(rng, areaMin.x, areaMax.x),
                            NextRange(rng, areaMin.y, areaMax.y),
                            NextRange(rng, areaMin.z, areaMax.z));
                    }

                    // 随机旋转 + 缩放: 由 ApplyRandomVariation 统一处理(与其它模式共用)
                    ApplyRandomVariation(srcRot, Vector3.one, rng, out Quaternion rot, out Vector3 scl);
                    CreateCopyEx(src, worldPos, rot, scl, i, ref created);
                    occupied.Add(worldPos);
                }
            }
            return created;
        }

        /// 在区域内采样一个满足最小距离约束的位置
        private Vector3 SampleAvoidPosition(System.Random rng, Vector3 anchor, Vector3 min, Vector3 max, List<Vector3> occupied)
        {
            float minDistSqr = _randomMinDistance * _randomMinDistance;
            int attempts = Mathf.Max(1, _randomMaxAttempts);

            for (int a = 0; a < attempts; a++)
            {
                Vector3 candidate = anchor + new Vector3(
                    NextRange(rng, min.x, max.x),
                    NextRange(rng, min.y, max.y),
                    NextRange(rng, min.z, max.z));

                bool ok = true;
                for (int j = 0; j < occupied.Count; j++)
                {
                    if ((occupied[j] - candidate).sqrMagnitude < minDistSqr)
                    {
                        ok = false;
                        break;
                    }
                }
                if (ok) return candidate;
            }

            // 失败则返回最后一次尝试(允许重叠)
            return anchor + new Vector3(
                NextRange(rng, min.x, max.x),
                NextRange(rng, min.y, max.y),
                NextRange(rng, min.z, max.z));
        }

        private static float NextRange(System.Random rng, float a, float b)
        {
            if (a > b) (a, b) = (b, a);
            return a + (float)(rng.NextDouble() * (b - a));
        }

        // ───────────────────────── 副本创建核心 ─────────────────────────
        private void CreateCopy(GameObject src, Vector3 worldPos, Quaternion worldRot, int index, ref int counter)
        {
            CreateCopyEx(src, worldPos, worldRot, Vector3.one, index, ref counter);
        }

        private void CreateCopy(GameObject src, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, int index, ref int counter)
        {
            CreateCopyEx(src, worldPos, worldRot, worldScale, index, ref counter);
        }

        /// 公共随机扰动: 叠加"随机旋转"和"随机缩放"到 (srcRot, baseScale) 上,用于所有模式 (线性/网格/圆形/螺旋/随机)。
        /// <para>语义与原 ExecuteRandom 内联逻辑一致: <c>_randomRotation</c> 为 true 时,旋转完全替换为
        /// <c>Quaternion.Euler(NextRange(min, max) per axis)</c>;<c>_randomScale</c> 为 true 时,
        /// 各轴乘以独立的 NextRange (或锁定比例时三轴同步乘一个 uniform)。</para>
        /// <para>未勾选时按原 (srcRot, baseScale) 透传,零开销。</para>
        private void ApplyRandomVariation(Quaternion srcRot, Vector3 baseScale, System.Random rng,
                                          out Quaternion outRot, out Vector3 outScale)
        {
            if (_randomRotation)
            {
                Vector3 e = new Vector3(
                    NextRange(rng, _randomRotMin.x, _randomRotMax.x),
                    NextRange(rng, _randomRotMin.y, _randomRotMax.y),
                    NextRange(rng, _randomRotMin.z, _randomRotMax.z));
                // 局部坐标轴: 副本先按 srcRot 朝向,再在 srcRot 的局部空间内叠加 Euler 随机旋转。
                // 即"绕物体自身 X/Y/Z 轴"转,而不是绕世界轴 —— 与 Circle 模式的
                // _circleSelfRotation (rot * Euler) 语义一致。
                outRot = srcRot * Quaternion.Euler(e);
            }
            else
            {
                outRot = srcRot;
            }

            if (_randomScale)
            {
                if (_randomScaleLockAspect)
                {
                    float u = NextRange(rng, _randomScaleUniformMin, _randomScaleUniformMax);
                    outScale = new Vector3(baseScale.x * u, baseScale.y * u, baseScale.z * u);
                }
                else
                {
                    outScale = new Vector3(
                        baseScale.x * NextRange(rng, _randomScaleMin.x, _randomScaleMax.x),
                        baseScale.y * NextRange(rng, _randomScaleMin.y, _randomScaleMax.y),
                        baseScale.z * NextRange(rng, _randomScaleMin.z, _randomScaleMax.z));
                }
            }
            else
            {
                outScale = baseScale;
            }
        }

        private void CreateCopyEx(GameObject src, Vector3 worldPos, Quaternion worldRot, Vector3 worldScale, int index, ref int counter)
        {
            if (src == null) return;

            GameObject copy;

            if (_isLiveExecution && _liveParent != null)
            {
                // ── 实时模式: 副本统一挂到 _liveParent 下,保持世界变换,不写 Undo ──
                copy = UnityEngine.Object.Instantiate(src, worldPos, worldRot);
                copy.name = BuildCopyName(src.name, index);
                copy.transform.SetParent(_liveParent.transform, worldPositionStays: true);
            }
            else if (_asChildren)
            {
                // 作为子物体: 先按世界坐标实例化,再挂到模板下保持世界变换不变
                copy = UnityEngine.Object.Instantiate(src, worldPos, worldRot);
                copy.name = BuildCopyName(src.name, index);
                Undo.RegisterCreatedObjectUndo(copy, $"创建副本 {copy.name}");
                Undo.SetTransformParent(copy.transform, src.transform, "挂到模板下");
            }
            else
            {
                // 与模板同级
                copy = UnityEngine.Object.Instantiate(src, worldPos, worldRot, src.transform.parent);
                copy.name = BuildCopyName(src.name, index);
                Undo.RegisterCreatedObjectUndo(copy, $"创建副本 {copy.name}");
            }

            // 应用缩放(相对原模板缩放)
            copy.transform.localScale = Vector3.Scale(src.transform.localScale, worldScale);
            counter++;
        }

        /// 构造副本名称: "{prefix}{srcName}{globalIndex}"
        private string BuildCopyName(string srcName, int localIndex)
        {
            int globalIndex = _nameStartIndex + localIndex;
            string prefix = string.IsNullOrEmpty(_namePrefix) ? "" : _namePrefix;
            return $"{prefix}{srcName}_{globalIndex}";
        }

        /// 获取源物体在世界坐标系下的"等效尺寸"用于线性阵列的【按体积】步进自适应。
        /// <para>优先用所有子 <see cref="Renderer"/> 的合并 AABB(已应用 transform / scale),
        /// 无 Renderer 时回退到 <see cref="MeshFilter"/>.sharedMesh.bounds × lossyScale,
        /// 都没有则返回 <see cref="Vector3.zero"/>(此时【按体积】不起作用,效果等同未勾选)。</para>
        /// <para>返回值始终为非负各分量,可直接与 <c>userStep</c> 相加。</para>
        private Vector3 GetSourceWorldSize(GameObject src)
        {
            if (src == null) return Vector3.zero;

            // 路径 A: Renderer 集合 (推荐 — 已包含所有子物体的世界 AABB)
            var renderers = src.GetComponentsInChildren<Renderer>();
            if (renderers != null && renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++)
                {
                    if (renderers[r] != null) b.Encapsulate(renderers[r].bounds);
                }
                return new Vector3(
                    Mathf.Max(b.size.x, 0f),
                    Mathf.Max(b.size.y, 0f),
                    Mathf.Max(b.size.z, 0f));
            }

            // 路径 B: MeshFilter 回退 (仅模板自身 + 应用 lossyScale)
            var mf = src.GetComponentInChildren<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                Vector3 ls = mf.sharedMesh.bounds.size;
                Vector3 lossy = src.transform.lossyScale;
                return new Vector3(
                    Mathf.Abs(ls.x * lossy.x),
                    Mathf.Abs(ls.y * lossy.y),
                    Mathf.Abs(ls.z * lossy.z));
            }

            // 完全无几何信息: 返回零向量,等效于未勾选【按体积】
            return Vector3.zero;
        }

        // ───────────────────────── 实时模式核心 ─────────────────────────
        /// 获取/创建实时预览父物体
        private GameObject EnsureLiveParent()
        {
            if (_liveParent != null) return _liveParent;
            // 查找场景中是否已存在(防止窗口切换/重复创建)
            var existing = GameObject.Find(LIVE_PARENT_NAME);
            if (existing != null) { _liveParent = existing; return _liveParent; }
            _liveParent = new GameObject(LIVE_PARENT_NAME);
            _liveParent.transform.position = Vector3.zero;
            return _liveParent;
        }

        /// 销毁实时父物体的所有子物体 (不销毁 _liveParent 本身,便于后续复用)
        private void DestroyLiveChildren()
        {
            if (_liveParent == null) return;
            // 倒序销毁,避免迭代器问题
            for (int i = _liveParent.transform.childCount - 1; i >= 0; i--)
            {
                var child = _liveParent.transform.GetChild(i).gameObject;
                if (Application.isPlaying)
                    Object.Destroy(child);
                else
                    Object.DestroyImmediate(child);
            }
        }

        /// 重建实时预览: 销毁旧子物体 → 创建 _liveParent → 重新生成副本 (无 Undo)
        private void RefreshLive()
        {
            // 1. 确定模板源: 首次走当前选中,后续走缓存
            List<GameObject> sources;
            if (!_hasLiveResult || _liveSourceCache == null)
            {
                var cur = Selection.gameObjects;
                if (cur == null || cur.Length == 0)
                {
                    _lastStatus = "实时模式: 当前未选中对象,且无历史预览模板";
                    Parent?.Repaint();
                    return;
                }
                sources = new List<GameObject>(cur.Length);
                foreach (var s in cur)
                {
                    if (s == null) continue;
                    if (EditorUtility.IsPersistent(s)) continue;
                    sources.Add(s);
                }
                if (sources.Count == 0)
                {
                    _lastStatus = "实时模式: 所选对象均为项目资产,无法复制";
                    Parent?.Repaint();
                    return;
                }
                _liveSourceCache = sources;
            }
            else
            {
                // 复用缓存,但剔除已销毁的源
                sources = new List<GameObject>(_liveSourceCache.Count);
                foreach (var s in _liveSourceCache)
                {
                    if (s != null) sources.Add(s);
                }
                if (sources.Count == 0)
                {
                    _lastStatus = "实时模式: 缓存的模板对象已全部销毁,请重新选择后点击【重新生成预览】";
                    _liveSourceCache = null;
                    _hasLiveResult = false;
                    Parent?.Repaint();
                    return;
                }
                _liveSourceCache = sources;
            }

            // 2. 创建/获取父物体
            EnsureLiveParent();

            // 3. 销毁旧子物体
            DestroyLiveChildren();

            // 4. 重新生成 (不写 Undo)
            _isLiveExecution = true;
            int total = 0;
            try
            {
                switch (_mode)
                {
                    case DupMode.Grid:    total += ExecuteGrid(sources); break;
                    case DupMode.Circle:  total += ExecuteCircle(sources); break;
                    case DupMode.Spiral:  total += ExecuteSpiral(sources); break;
                    case DupMode.Linear:  total += ExecuteLinear(sources); break;
                    case DupMode.Random:  total += ExecuteRandom(sources); break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[批量复制-实时] 失败: {ex.Message}\n{ex.StackTrace}");
                _isLiveExecution = false;
                _lastStatus = $"实时刷新失败: {ex.Message}";
                Parent?.Repaint();
                return;
            }
            _isLiveExecution = false;

            _hasLiveResult = true;
            _lastStatus = $"LIVE • 已更新预览: {total} 个副本 (模式 {GetModeLabel(_mode)},模板 {sources.Count} 个)";
            // 实时刷新不要持续聚焦,避免打断其他操作;只刷新本窗口
            Parent?.Repaint();
        }

        /// 将实时预览中的副本正式应用到场景 (按原始 _asChildren 规则重新挂父 + Undo 一步)
        private void BakeLiveToScene()
        {
            if (_liveParent == null || _liveParent.transform.childCount == 0)
            {
                _lastStatus = "没有可应用的实时预览";
                Parent?.Repaint();
                return;
            }

            // 缓存原始模板列表 (按 name 解析): 名称格式 "{prefix}{srcName}_{globalIndex}"
            // 我们采用另一种简单做法: 让用户在使用前保证选中与实时预览对应即可
            // 但更稳妥的做法: 在 RefreshLive 时记录每个副本对应的源 transform 索引
            // 这里采用简化的重命名解析: 用户使用【应用到场景】前应保持模板选中状态

            var sources = Selection.gameObjects;
            if (sources == null || sources.Length == 0)
            {
                if (!EditorUtility.DisplayDialog("应用到场景",
                    "当前未选中任何模板对象。\n若直接应用,副本将作为 _liveParent 的子物体保留(且 _liveParent 会被销毁)。\n\n" +
                    "建议: 重新选中首次生成实时预览时的模板对象,再点击【应用到场景】以获得正确的父子结构。\n\n是否仍要继续?",
                    "仍要应用", "取消"))
                {
                    return;
                }
            }

            // 收集 _liveParent 的所有子物体 (按当前 hierarchy 顺序)
            int childCount = _liveParent.transform.childCount;
            var copies = new List<GameObject>(childCount);
            for (int i = 0; i < childCount; i++)
            {
                copies.Add(_liveParent.transform.GetChild(i).gameObject);
            }

            Undo.SetCurrentGroupName($"应用实时预览 [{GetModeLabel(_mode)}] ({copies.Count} 个)");
            int group = Undo.GetCurrentGroup();

            // 计算平均源数(用于按源分组)
            int srcCount = sources != null && sources.Length > 0 ? sources.Length : 1;
            int perSrc = Mathf.Max(1, Mathf.CeilToInt(copies.Count / (float)srcCount));

            try
            {
                if (sources != null && sources.Length > 0)
                {
                    // 按源分配: 每 perSrc 个副本对应一个源
                    for (int i = 0; i < copies.Count; i++)
                    {
                        var copy = copies[i];
                        if (copy == null) continue;
                        int srcIdx = Mathf.Min(i / perSrc, sources.Length - 1);
                        var src = sources[srcIdx];
                        if (src == null) continue;

                        Undo.SetTransformParent(copy.transform,
                            _asChildren ? src.transform : src.transform.parent,
                            "应用到场景-挂父");
                    }
                }
                // 销毁空 _liveParent (不写 Undo,作为清理操作)
                Undo.DestroyObjectImmediate(_liveParent);

                Undo.CollapseUndoOperations(group);

                _liveParent = null;
                _hasLiveResult = false;
                _liveSourceCache = null;
                _lastStatus = $"已应用 {copies.Count} 个副本到场景 (支持 Undo 撤销)";
            }
            catch (Exception ex)
            {
                Debug.LogError($"[批量复制-应用] 失败: {ex.Message}\n{ex.StackTrace}");
                EditorUtility.DisplayDialog("应用失败", ex.Message, "确定");
                Undo.RevertAllDownToGroup(group);
            }

            Parent?.Repaint();
        }

        /// 清除实时预览 (销毁 _liveParent 及所有子物体,不可撤销)
        private void ClearLive()        {
            if (_liveParent == null)
            {
                _hasLiveResult = false;
                _liveSourceCache = null;
                _lastStatus = "实时预览已不存在";
                Parent?.Repaint();
                return;
            }

            if (!EditorUtility.DisplayDialog("清除实时预览",
                $"确定要销毁 {LIVE_PARENT_NAME} 及其所有子物体吗?\n此操作不可撤销。",
                "确定清除", "取消"))
            {
                return;
            }

            int count = _liveParent.transform.childCount;
            DestroyLiveChildren();
            if (Application.isPlaying) Object.Destroy(_liveParent);
            else Object.DestroyImmediate(_liveParent);

            _liveParent = null;
            _hasLiveResult = false;
            _liveSourceCache = null;
            _lastStatus = $"实时预览已清除 (销毁 {count} 个副本)";
            Parent?.Repaint();
        }

        // ───────────────────────── 菜单项: 一键复制选中 ─────────────────────────
        /// 从 Hierarchy 右键菜单或顶部菜单快速复制(默认网格 5×5)
        [MenuItem("GameObject/VicTools(YD)/批量复制选中对象", false, 0)]
        private static void QuickDuplicate()
        {
            var sources = Selection.gameObjects;
            if (sources == null || sources.Length == 0)
            {
                EditorUtility.DisplayDialog("批量复制", "请先在 Hierarchy 中选择对象", "确定");
                return;
            }
            // 打开主窗口并切换到批量复制
            var win = EditorWindow.GetWindow<VicToolsWindow>();
            win.Show();
            win.SetCurrentWindowByTypeName("ObjectDuplicator");
        }

        [MenuItem("GameObject/VicTools(YD)/批量复制选中对象", true, 0)]
        private static bool QuickDuplicateValidate()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        // ───────────────────────── EditorPrefs 存档读写 ─────────────────────────
        // 全部参数都通过 Load/SavePrefs 走 EditorPrefs,重启工具时自动还原。
        // 加载语义:键不存在时保留字段当前默认值(类内联初始化);键存在则覆盖。
        // 这种"无 key 用默认"的写法保证:旧版本/未保存过参数时也能正常显示。

        /// 把所有参数写入 EditorPrefs,在 OnDisable 时调用
        private void SavePrefs()
        {
            // ── 通用参数 ──
            EditorPrefs.SetInt   (PREFS_PREFIX + "mode",              (int)_mode);
            EditorPrefs.SetBool  (PREFS_PREFIX + "asChildren",        _asChildren);
            EditorPrefs.SetString(PREFS_PREFIX + "namePrefix",        _namePrefix ?? string.Empty);
            EditorPrefs.SetInt   (PREFS_PREFIX + "nameStartIndex",    _nameStartIndex);

            // ── 网格 ──
            EditorPrefs.SetInt   (PREFS_PREFIX + "gridCountX",        _gridCountX);
            EditorPrefs.SetInt   (PREFS_PREFIX + "gridCountZ",        _gridCountZ);
            SaveVector3          (PREFS_PREFIX + "gridSpacing",       _gridSpacing);
            EditorPrefs.SetBool  (PREFS_PREFIX + "gridSpacingLocal",  _gridSpacingLocal);
            EditorPrefs.SetBool  (PREFS_PREFIX + "gridCenterOnSource", _gridCenterOnSource);

            // ── 圆形 ──
            EditorPrefs.SetInt   (PREFS_PREFIX + "circleCount",       _circleCount);
            EditorPrefs.SetFloat (PREFS_PREFIX + "circleRadius",      _circleRadius);
            EditorPrefs.SetFloat (PREFS_PREFIX + "circleStartAngle",  _circleStartAngle);
            EditorPrefs.SetFloat (PREFS_PREFIX + "circleArcDegrees",  _circleArcDegrees);
            EditorPrefs.SetBool  (PREFS_PREFIX + "circleFaceCenter",  _circleFaceCenter);
            EditorPrefs.SetFloat (PREFS_PREFIX + "circleYOffset",     _circleYOffset);
            SaveVector3          (PREFS_PREFIX + "circleSelfRotation",      _circleSelfRotation);
            SaveVector3          (PREFS_PREFIX + "circleSelfRotationStep",  _circleSelfRotationStep);

            // ── 螺旋 ──
            EditorPrefs.SetInt   (PREFS_PREFIX + "spiralCount",       _spiralCount);
            EditorPrefs.SetFloat (PREFS_PREFIX + "spiralStartRadius", _spiralStartRadius);
            EditorPrefs.SetFloat (PREFS_PREFIX + "spiralRadiusStep",  _spiralRadiusStep);
            EditorPrefs.SetFloat (PREFS_PREFIX + "spiralHeightStep",  _spiralHeightStep);
            EditorPrefs.SetFloat (PREFS_PREFIX + "spiralAngleStep",   _spiralAngleStep);
            EditorPrefs.SetFloat (PREFS_PREFIX + "spiralScaleStart",  _spiralScaleStart);
            EditorPrefs.SetFloat (PREFS_PREFIX + "spiralScaleStep",   _spiralScaleStep);

            // ── 线性 ──
            EditorPrefs.SetInt   (PREFS_PREFIX + "linearCount",       _linearCount);
            SaveVector3          (PREFS_PREFIX + "linearStep",        _linearStep);
            EditorPrefs.SetBool  (PREFS_PREFIX + "linearStepLocal",   _linearStepLocal);
            EditorPrefs.SetBool  (PREFS_PREFIX + "linearStepBySize",  _linearStepBySize);
            EditorPrefs.SetBool  (PREFS_PREFIX + "linearScale",       _linearScale);
            SaveVector3          (PREFS_PREFIX + "linearScaleStart",  _linearScaleStart);
            SaveVector3          (PREFS_PREFIX + "linearScaleStep",   _linearScaleStep);
            EditorPrefs.SetBool  (PREFS_PREFIX + "linearScaleLockAspect", _linearScaleLockAspect);
            EditorPrefs.SetFloat (PREFS_PREFIX + "linearScaleUniformStart", _linearScaleUniformStart);
            EditorPrefs.SetFloat (PREFS_PREFIX + "linearScaleUniformStep",  _linearScaleUniformStep);

            // ── 随机 ──
            EditorPrefs.SetInt   (PREFS_PREFIX + "randomCount",       _randomCount);
            SaveVector3          (PREFS_PREFIX + "randomAreaMin",     _randomAreaMin);
            SaveVector3          (PREFS_PREFIX + "randomAreaMax",     _randomAreaMax);
            EditorPrefs.SetBool  (PREFS_PREFIX + "randomRotation",    _randomRotation);
            SaveVector3          (PREFS_PREFIX + "randomRotMin",      _randomRotMin);
            SaveVector3          (PREFS_PREFIX + "randomRotMax",      _randomRotMax);
            EditorPrefs.SetBool  (PREFS_PREFIX + "randomScale",       _randomScale);
            SaveVector3          (PREFS_PREFIX + "randomScaleMin",    _randomScaleMin);
            SaveVector3          (PREFS_PREFIX + "randomScaleMax",    _randomScaleMax);
            EditorPrefs.SetBool  (PREFS_PREFIX + "randomScaleLockAspect", _randomScaleLockAspect);
            EditorPrefs.SetFloat (PREFS_PREFIX + "randomScaleUniformMin", _randomScaleUniformMin);
            EditorPrefs.SetFloat (PREFS_PREFIX + "randomScaleUniformMax", _randomScaleUniformMax);
            EditorPrefs.SetBool  (PREFS_PREFIX + "useSeed",           _useSeed);
            EditorPrefs.SetInt   (PREFS_PREFIX + "randomSeed",        _randomSeed);
            EditorPrefs.SetBool  (PREFS_PREFIX + "randomAvoidOverlap", _randomAvoidOverlap);
            EditorPrefs.SetFloat (PREFS_PREFIX + "randomMinDistance", _randomMinDistance);
            EditorPrefs.SetInt   (PREFS_PREFIX + "randomMaxAttempts", _randomMaxAttempts);

            // ── UI 状态 ──
            EditorPrefs.SetBool  (PREFS_PREFIX + "foldoutGeneral",    _foldoutGeneral);
            EditorPrefs.SetBool  (PREFS_PREFIX + "foldoutMode",       _foldoutMode);
            EditorPrefs.SetBool  (PREFS_PREFIX + "liveMode",          _liveMode);
        }

        /// 从 EditorPrefs 读取所有参数,在 OnEnable 时调用
        private void LoadPrefs()
        {
            // ── 通用参数 ──
            _mode              = (DupMode)EditorPrefs.GetInt (PREFS_PREFIX + "mode",              (int)_mode);
            _asChildren        = EditorPrefs.GetBool           (PREFS_PREFIX + "asChildren",        _asChildren);
            _namePrefix        = EditorPrefs.GetString         (PREFS_PREFIX + "namePrefix",        _namePrefix ?? string.Empty);
            _nameStartIndex    = EditorPrefs.GetInt            (PREFS_PREFIX + "nameStartIndex",    _nameStartIndex);

            // ── 网格 ──
            _gridCountX        = EditorPrefs.GetInt            (PREFS_PREFIX + "gridCountX",        _gridCountX);
            _gridCountZ        = EditorPrefs.GetInt            (PREFS_PREFIX + "gridCountZ",        _gridCountZ);
            _gridSpacing       = LoadVector3                   (PREFS_PREFIX + "gridSpacing",       _gridSpacing);
            _gridSpacingLocal  = EditorPrefs.GetBool           (PREFS_PREFIX + "gridSpacingLocal",  _gridSpacingLocal);
            _gridCenterOnSource = EditorPrefs.GetBool          (PREFS_PREFIX + "gridCenterOnSource", _gridCenterOnSource);

            // ── 圆形 ──
            _circleCount       = EditorPrefs.GetInt            (PREFS_PREFIX + "circleCount",       _circleCount);
            _circleRadius      = EditorPrefs.GetFloat          (PREFS_PREFIX + "circleRadius",      _circleRadius);
            _circleStartAngle  = EditorPrefs.GetFloat          (PREFS_PREFIX + "circleStartAngle",  _circleStartAngle);
            _circleArcDegrees  = EditorPrefs.GetFloat          (PREFS_PREFIX + "circleArcDegrees",  _circleArcDegrees);
            _circleFaceCenter  = EditorPrefs.GetBool           (PREFS_PREFIX + "circleFaceCenter",  _circleFaceCenter);
            _circleYOffset     = EditorPrefs.GetFloat          (PREFS_PREFIX + "circleYOffset",     _circleYOffset);
            _circleSelfRotation      = LoadVector3              (PREFS_PREFIX + "circleSelfRotation",      _circleSelfRotation);
            _circleSelfRotationStep  = LoadVector3              (PREFS_PREFIX + "circleSelfRotationStep",  _circleSelfRotationStep);

            // ── 螺旋 ──
            _spiralCount       = EditorPrefs.GetInt            (PREFS_PREFIX + "spiralCount",       _spiralCount);
            _spiralStartRadius = EditorPrefs.GetFloat          (PREFS_PREFIX + "spiralStartRadius", _spiralStartRadius);
            _spiralRadiusStep  = EditorPrefs.GetFloat          (PREFS_PREFIX + "spiralRadiusStep",  _spiralRadiusStep);
            _spiralHeightStep  = EditorPrefs.GetFloat          (PREFS_PREFIX + "spiralHeightStep",  _spiralHeightStep);
            _spiralAngleStep   = EditorPrefs.GetFloat          (PREFS_PREFIX + "spiralAngleStep",   _spiralAngleStep);
            _spiralScaleStart  = EditorPrefs.GetFloat          (PREFS_PREFIX + "spiralScaleStart",  _spiralScaleStart);
            _spiralScaleStep   = EditorPrefs.GetFloat          (PREFS_PREFIX + "spiralScaleStep",   _spiralScaleStep);

            // ── 线性 ──
            _linearCount       = EditorPrefs.GetInt            (PREFS_PREFIX + "linearCount",       _linearCount);
            _linearStep        = LoadVector3                   (PREFS_PREFIX + "linearStep",        _linearStep);
            _linearStepLocal   = EditorPrefs.GetBool           (PREFS_PREFIX + "linearStepLocal",   _linearStepLocal);
            _linearStepBySize  = EditorPrefs.GetBool           (PREFS_PREFIX + "linearStepBySize",  _linearStepBySize);
            _linearScale       = EditorPrefs.GetBool           (PREFS_PREFIX + "linearScale",       _linearScale);
            _linearScaleStart  = LoadVector3                   (PREFS_PREFIX + "linearScaleStart",  _linearScaleStart);
            _linearScaleStep   = LoadVector3                   (PREFS_PREFIX + "linearScaleStep",   _linearScaleStep);
            _linearScaleLockAspect = EditorPrefs.GetBool       (PREFS_PREFIX + "linearScaleLockAspect", _linearScaleLockAspect);
            _linearScaleUniformStart = EditorPrefs.GetFloat    (PREFS_PREFIX + "linearScaleUniformStart", _linearScaleUniformStart);
            _linearScaleUniformStep  = EditorPrefs.GetFloat    (PREFS_PREFIX + "linearScaleUniformStep",  _linearScaleUniformStep);

            // ── 随机 ──
            _randomCount       = EditorPrefs.GetInt            (PREFS_PREFIX + "randomCount",       _randomCount);
            _randomAreaMin     = LoadVector3                   (PREFS_PREFIX + "randomAreaMin",     _randomAreaMin);
            _randomAreaMax     = LoadVector3                   (PREFS_PREFIX + "randomAreaMax",     _randomAreaMax);
            _randomRotation    = EditorPrefs.GetBool           (PREFS_PREFIX + "randomRotation",    _randomRotation);
            _randomRotMin      = LoadVector3                   (PREFS_PREFIX + "randomRotMin",      _randomRotMin);
            _randomRotMax      = LoadVector3                   (PREFS_PREFIX + "randomRotMax",      _randomRotMax);
            _randomScale       = EditorPrefs.GetBool           (PREFS_PREFIX + "randomScale",       _randomScale);
            _randomScaleMin    = LoadVector3                   (PREFS_PREFIX + "randomScaleMin",    _randomScaleMin);
            _randomScaleMax    = LoadVector3                   (PREFS_PREFIX + "randomScaleMax",    _randomScaleMax);
            _randomScaleLockAspect = EditorPrefs.GetBool       (PREFS_PREFIX + "randomScaleLockAspect", _randomScaleLockAspect);
            _randomScaleUniformMin = EditorPrefs.GetFloat      (PREFS_PREFIX + "randomScaleUniformMin", _randomScaleUniformMin);
            _randomScaleUniformMax = EditorPrefs.GetFloat      (PREFS_PREFIX + "randomScaleUniformMax", _randomScaleUniformMax);
            _useSeed           = EditorPrefs.GetBool           (PREFS_PREFIX + "useSeed",           _useSeed);
            _randomSeed        = EditorPrefs.GetInt            (PREFS_PREFIX + "randomSeed",        _randomSeed);
            _randomAvoidOverlap = EditorPrefs.GetBool          (PREFS_PREFIX + "randomAvoidOverlap", _randomAvoidOverlap);
            _randomMinDistance = EditorPrefs.GetFloat          (PREFS_PREFIX + "randomMinDistance", _randomMinDistance);
            _randomMaxAttempts = EditorPrefs.GetInt            (PREFS_PREFIX + "randomMaxAttempts", _randomMaxAttempts);

            // ── UI 状态 ──
            _foldoutGeneral    = EditorPrefs.GetBool           (PREFS_PREFIX + "foldoutGeneral",    _foldoutGeneral);
            _foldoutMode       = EditorPrefs.GetBool           (PREFS_PREFIX + "foldoutMode",       _foldoutMode);
            _liveMode          = EditorPrefs.GetBool           (PREFS_PREFIX + "liveMode",          _liveMode);
        }

        /// Vector3 存档:用 "R" 格式(round-trip)保留完整浮点精度,逗号分隔
        private static void SaveVector3(string key, Vector3 v)
        {
            EditorPrefs.SetString(key,
                v.x.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                v.y.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "," +
                v.z.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// Vector3 读档:键不存在/格式错误都返回默认值,绝不抛异常
        private static Vector3 LoadVector3(string key, Vector3 defaultValue)
        {
            string s = EditorPrefs.GetString(key, null);
            if (string.IsNullOrEmpty(s)) return defaultValue;
            string[] parts = s.Split(',');
            if (parts.Length != 3) return defaultValue;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            if (!float.TryParse(parts[0], System.Globalization.NumberStyles.Float, inv, out float x) ||
                !float.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out float y) ||
                !float.TryParse(parts[2], System.Globalization.NumberStyles.Float, inv, out float z))
                return defaultValue;
            return new Vector3(x, y, z);
        }
    }
}
