// VicToolsEditorUtility - VicTools 包内的 Editor-only 公共工具集合
// 由 Vic.Editor 程序集(Vic.Editor.asmdef)承载,与 Runtime 工具(Vic.Runtime)分离。
// 命名约定：每个工具单独一个 public static class,文件对外即"工具类清单"。
// 当前成员：
//   - VicToolsIconLoader：PNG 图标加载助手（解码原始字节,绕开 AssetDatabase/TextureImporter,
//                        避免平台压缩 / isReadable=0 / .meta 缺失等坑）
//   - VicToolsListConfirmWindow：通用【处理列表确认窗口】（非模态,列出待处理清单后由用户确认再执行,
//                        可在需要展示处理列表时共用该方式）
// 3.0.1（走兼容层取 ID，兼容 Unity 6000.4+ 下 `GetInstanceID` 被废弃的问题）

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using Vic.Runtime;

namespace Vic.Editor
{
    /// 通用 PNG 图标加载助手(Editor-only)。
    /// 用 <see cref="UnityEditor.ImageConversion"/> 直接解码原始 PNG 字节,绕过 AssetDatabase 与 TextureImporter。
    /// <remarks>
    /// 与 <see cref="AssetDatabase.LoadAssetAtPath{T}"/> 的区别:
    ///   ① 不依赖 .meta 与 TextureImporter,直接解码原始 PNG 字节
    ///   ② 强制 RGBA32 uncompressed + readable,避免平台压缩(BPTC/ETC2/DXT)与 isReadable=0 限制
    ///   ③ 创建的是独立 Texture2D 实例,Unity 不会自动回收,必须由调用方通过
    ///      <see cref="Release(Texture2D)"/> 或 <see cref="ReleaseAll(List{Texture2D})"/> 释放
    ///
    /// 典型用法(在 EditorWindow / Editor 类中):
    /// <code>
    ///     private readonly List&lt;Texture2D&gt; _ownedIcons = new();
    ///
    ///     private void LoadIcons()
    ///     {
    ///         var icon = VicToolsIconLoader.LoadPngIcon("Packages/.../foo.png");
    ///         if (icon != null) _ownedIcons.Add(icon);
    ///     }
    ///
    ///     private void OnDisable()
    ///     {
    ///         VicToolsIconLoader.ReleaseAll(_ownedIcons);
    ///         // 其余清理...
    ///     }
    /// </code>
    /// </remarks>
    public static class VicToolsIconLoader
    {
        /// 从绝对/相对路径加载 PNG 图标,返回可读 RGBA32 贴图。失败返回 null 并写日志。
        /// <param name="path">PNG 文件路径(支持绝对路径与 Unity 工程内相对路径)。</param>
        /// <returns>解码后的 Texture2D,或 null(文件不存在/读取失败/PNG 不合法)。</returns>
        public static Texture2D LoadPngIcon(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogWarning("[VicToolsIconLoader] 路径为空,跳过加载。");
                return null;
            }

            if (!File.Exists(path))
            {
                Debug.LogWarning($"[VicToolsIconLoader] 文件不存在: {path}");
                return null;
            }

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VicToolsIconLoader] 读取失败 {path}: {e.Message}");
                return null;
            }

            // 初始 2x2 占位即可,LoadImage 内部会按 PNG 实际尺寸重新分配
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            tex.hideFlags = HideFlags.HideAndDontSave;

            // markNonReadable=false 保留 CPU 端可读;若不需要 GetPixels,可改为 true 省一份内存
            if (!ImageConversion.LoadImage(tex, bytes, markNonReadable: false))
            {
                Object.DestroyImmediate(tex);
                Debug.LogWarning($"[VicToolsIconLoader] PNG 解码失败(可能不是合法 PNG): {path}");
                return null;
            }

            // 关键:把 alpha 通道当透明度,否则 GUI/DockTab 区域会把 alpha=0 的像素当成
            // RGB 实色显示(对带透明背景的设计稿 PNG 来说,会"填满整个图标槽"导致形状看不清)
            // PNG 不带 alpha 时 alpha 默认为 255(全不透明),此开关无副作用
            tex.alphaIsTransparency = true;

            return tex;
        }

        /// 释放单个由 <see cref="LoadPngIcon"/> 创建的贴图。null 安全。
        public static void Release(Texture2D tex)
        {
            if (tex == null) return;
            Object.DestroyImmediate(tex);
        }

        /// 释放列表中所有由 <see cref="LoadPngIcon"/> 创建的贴图,然后清空列表。null 安全。
        /// <remarks>
        /// 推荐在 EditorWindow.OnDisable / Editor.OnDisable 中调用,
        /// 防止域重载或窗口关闭时贴图泄漏。
        /// </remarks>
        public static void ReleaseAll(List<Texture2D> ownedIcons)
        {
            if (ownedIcons == null) return;
            for (int i = 0; i < ownedIcons.Count; i++)
                if (ownedIcons[i] != null) Object.DestroyImmediate(ownedIcons[i]);
            ownedIcons.Clear();
        }
    }

    /// 通用【处理列表确认窗口】(Editor-only,非模态)。
    /// 适用于任何"先扫描 / 生成处理清单 → 用户确认 → 再执行"的批量处理场景:
    /// 资源箱批量添加场景、批量替换资源、批量修复引用等。
    ///
    /// 特性:
    ///   · 非模态(<see cref="EditorWindow.ShowUtility"/>),不阻塞编辑器事件循环,
    ///     用户可在窗口打开期间自由切到 Console 查看完整日志,确认后再回来点【确定】。
    ///   · 清单可滚动、支持任意条数,每项可选带【定位】按钮(Ping 到 Project 窗口)。
    ///   · 传入 <c>onAddSingle</c> 时,每项【定位】按钮右侧额外显示【添加】按钮,
    ///     可不等批量【确定】就把单个对象立即加入资源箱(添加成功后该行按钮置灰)。
    ///   · 同一时间只保留一个实例,重复调用会自动关闭旧窗口,避免叠加。
    ///   · 点【取消】或直接关窗 = 放弃处理,不会执行确认回调。
    ///
    /// 典型用法:
    /// <code>
    ///     var items = new List&lt;VicToolsListConfirmWindow.Item&gt;();
    ///     foreach (var scene in scenes)
    ///         items.Add(new VicToolsListConfirmWindow.Item(AssetDatabase.GetAssetPath(scene), scene));
    ///
    ///     VicToolsListConfirmWindow.Show(
    ///         "添加项目场景",
    ///         $"待添加 {scenes.Count} 个场景",
    ///         items,
    ///         () =&gt; DoAdd(scenes),          // 确认回调
    ///         $"确定添加（{scenes.Count}）");   // 确认按钮文字
    /// </code>
    /// </remarks>
    public class VicToolsListConfirmWindow : EditorWindow
    {
        /// 清单条目:显示文本 + 可选的定位目标对象(用于【定位】按钮 Ping 到 Project 窗口)。
        public class Item
        {
            /// 列表中显示的文本(一般是资源路径或处理对象描述)。
            public string Text { get; }

            /// 可选的定位目标(资源资产等);为 null 时不显示【定位】按钮。
            public Object Target { get; }

            /// 可选的文本颜色(如标记被当前场景引用的资源);为 null 时使用默认颜色。
            public Color? TextColor { get; }

            /// 是否为「被当前场景引用」的资源(用于清单内的引用对象批量操作)。
            public bool IsReferenced { get; }

            public Item(string text, Object target = null, Color? textColor = null, bool isReferenced = false)
            {
                Text = text ?? string.Empty;
                Target = target;
                TextColor = textColor;
                IsReferenced = isReferenced;
            }
        }

        /// 动态状态快照:由 <see cref="Show"/> 的 dynamicStateProvider 提供。
        /// <remarks>
        /// 窗口每次绘制(OnGUI)都会重新调用 provider 求值一次,用于"单项添加后待添加数量变化"
        /// 时让【概要文字】与【确定按钮文字】实时同步,而不是停留在打开窗口那一刻的快照。
        /// </remarks>
        public struct DynamicState
        {
            /// 概要文字(可多行);为空时沿用 <see cref="Show"/> 传入的静态 summary。
            public string Summary;

            /// 确定按钮文字(如"确定添加（3）");为空时沿用 Show 传入的静态 confirmButtonText。
            public string ConfirmButtonText;

            /// 是否禁用确定按钮。默认 false = 启用;用于"待添加数量已归零"时阻止无效的批量提交。
            public bool ConfirmDisabled;
        }

        private string _summary = string.Empty;
        private string _confirmButtonText = "确定";
        private List<Item> _items = new List<Item>();
        private Action _onConfirm;
        private Action _onCancel;
        private Vector2 _scrollPos;

        // 单项添加回调(非 null 时显示每行的【添加】按钮);返回 true = 添加成功
        private Func<Object, bool> _onAddSingle;

        // 动态状态提供者(非 null 时每次绘制重新求值概要与按钮文字);见 DynamicState
        private Func<DynamicState> _dynamicStateProvider;

        // 引用对象批量添加回调(可选):把清单中标记为「被当前场景引用」的资源一次性加入目标集合;
        // 参数为待添加对象数组,返回 true = 添加成功
        private Func<Object[], bool> _onAddReferenced;

        // 已通过【添加】/【添加引用对象】按钮成功添加的对象 ID(用于把对应按钮置灰)
        private readonly HashSet<int> _addedReferencedIds = new HashSet<int>();

        // 【添加引用对象】是否已成功执行过(执行后按钮置灰)
        private bool _referencedAdded;

        // 批量提交前的二次确认门(可选):点击【确定】时先调用,返回 false 则不关窗、不提交
        private Func<bool> _submitGuard;

        // 已通过【添加】按钮成功添加的对象 ID,用于把该行按钮置灰
        // 用 VicToolsUnityCompat.GetStableId() 取值:Unity 6000.4+ 下 GetInstanceID 已是 error 级废弃,
        // 统一走兼容层可在 2021/2022/2023/6000.x 各版本下编译通过
        private readonly HashSet<int> _addedTargetIds = new HashSet<int>();

        /// 打开处理列表确认窗口(非模态)。
        /// <param name="title">窗口标题。</param>
        /// <param name="summary">顶部概要文字(可多行,如"待处理 N 个 / 跳过 M 个")。</param>
        /// <param name="items">处理清单;为 null 时显示空列表。</param>
        /// <param name="onConfirm">点击【确定】时执行的回调(传 null 则相当于只看不改)。</param>
        /// <param name="confirmButtonText">确认按钮文字,默认"确定"。</param>
        /// <param name="onCancel">点击【取消】/关窗时执行的回调(可选)。</param>
        /// <param name="onAddSingle">
        /// 单项添加回调(可选)。传入后,每行【定位】按钮右侧会显示【添加】按钮,
        /// 点击即把该行的 <see cref="Item.Target"/> 单独加入目标集合(不等批量【确定】)。
        /// 返回 true 表示添加成功,窗口会置灰该行按钮并弹出提示;返回 false 表示失败(如已在集合中)。
        /// </param>
        /// <param name="dynamicStateProvider">
        /// 动态状态提供者(可选)。传入后,窗口每次绘制都会重新调用它刷新【概要文字】与【确定按钮文字】,
        /// 便于"单项添加"后待添加数量变化时保持显示同步(例如"确定添加（10）"随单项添加递减为"确定添加（7）")。
        /// 为 null 时使用 <c>Show()</c> 传入的静态 summary / confirmButtonText。
        /// </param>
        /// <param name="submitGuard">
        /// 批量提交前的二次确认门(可选)。点击【确定】时先调用它:返回 true 才关闭窗口并执行
        /// <c>onConfirm</c>;返回 false 则保留窗口、不做任何提交(用于数量过大时弹确认框)。
        /// 为 null 时不做额外校验,直接提交。
        /// </param>
        /// <param name="onAddReferenced">
        /// 引用对象批量添加回调(可选)。传入后,窗口底部显示蓝色【添加引用对象】按钮,
        /// 点击即把清单中标记为「被当前场景引用」(<see cref="Item.IsReferenced"/>)的资源一次性加入目标集合。
        /// 返回 true 表示添加成功,窗口会置灰该按钮并弹出提示。
        /// </param>
        public static VicToolsListConfirmWindow Show(
            string title,
            string summary,
            IReadOnlyList<Item> items,
            Action onConfirm,
            string confirmButtonText = "确定",
            Action onCancel = null,
            Func<Object, bool> onAddSingle = null,
            Func<DynamicState> dynamicStateProvider = null,
            Func<bool> submitGuard = null,
            Func<Object[], bool> onAddReferenced = null)
        {
            // 关闭已存在的旧实例,避免叠加多个预览窗口
            var existed = Resources.FindObjectsOfTypeAll<VicToolsListConfirmWindow>();
            foreach (var w in existed)
            {
                if (w != null) w.Close();
            }

            var window = CreateInstance<VicToolsListConfirmWindow>();
            window.titleContent = new GUIContent(string.IsNullOrEmpty(title) ? "确认" : title);
            window._summary = summary ?? string.Empty;
            window._confirmButtonText = string.IsNullOrEmpty(confirmButtonText) ? "确定" : confirmButtonText;
            window._items = items != null ? new List<Item>(items) : new List<Item>();
            window._onConfirm = onConfirm;
            window._onCancel = onCancel;
            window._onAddSingle = onAddSingle;
            window._dynamicStateProvider = dynamicStateProvider;
            window._submitGuard = submitGuard;
            window._onAddReferenced = onAddReferenced;
            window.minSize = new Vector2(560, 360);
            window.ShowUtility();
            return window;
        }

        // 行文本样式缓存：清单可能上千行,避免每帧为每行新建 GUIStyle
        private static readonly Dictionary<Color, GUIStyle> _itemLabelStyleCache = new Dictionary<Color, GUIStyle>();

        private static GUIStyle GetItemLabelStyle(Color? color)
        {
            if (!color.HasValue) return EditorStyles.label;
            if (_itemLabelStyleCache.TryGetValue(color.Value, out GUIStyle cached)) return cached;
            var style = new GUIStyle(EditorStyles.label);
            style.normal.textColor = color.Value;
            _itemLabelStyleCache[color.Value] = style;
            return style;
        }

        // Ctrl+点击【定位】：单选定位目标对象（替换当前选择，不做累加多选）
        private static void SelectSingleObject(Object target)
        {
            if (!target) return;
            Selection.activeObject = target;
        }

        // 收集清单中标记为「被当前场景引用」且尚未添加的对象（按对象去重）
        private List<Object> CollectReferencedTargets()
        {
            var result = new List<Object>();
            var seen = new HashSet<int>();
            for (int i = 0; i < _items.Count; i++)
            {
                var item = _items[i];
                if (item == null || !item.IsReferenced || !item.Target) continue;
                int id = VicToolsUnityCompat.GetStableId(item.Target);
                if (!seen.Add(id)) continue;
                // 已通过单行【添加】或本按钮添加过的对象不再重复
                if (_addedTargetIds.Contains(id) || _addedReferencedIds.Contains(id)) continue;
                result.Add(item.Target);
            }
            return result;
        }

        private void OnGUI()
        {
            // 求值动态状态:单项添加后待添加数量会变,这里每次绘制都重新取一次,
            // 保证概要与【确定】按钮上的数量实时递减
            var dyn = _dynamicStateProvider != null ? _dynamicStateProvider() : default;
            string curSummary = !string.IsNullOrEmpty(dyn.Summary) ? dyn.Summary : _summary;
            string curConfirmText = !string.IsNullOrEmpty(dyn.ConfirmButtonText)
                ? dyn.ConfirmButtonText
                : _confirmButtonText;

            EditorGUILayout.Space(4);

            if (!string.IsNullOrEmpty(curSummary))
            {
                // 逐行绘制,保证多行概要文本正确换行显示
                var lines = curSummary.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    EditorGUILayout.LabelField(lines[i], EditorStyles.wordWrappedLabel);
                }
            }

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "本窗口不锁定焦点，可随时切到 Console 查看完整处理日志；确认后再执行。",
                MessageType.Info);

            EditorGUILayout.Space(4);
            string listTitle = $"处理清单（{_items.Count} 条）";
            if (_addedTargetIds.Count > 0) listTitle += $"，已单独添加 {_addedTargetIds.Count} 条";
            EditorGUILayout.LabelField(listTitle + "：", EditorStyles.miniBoldLabel);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scroll.scrollPosition;
                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    if (item == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{i + 1}. {item.Text}", GetItemLabelStyle(item.TextColor));
                    // 【定位】按钮沿用行标记色：被当前场景引用的资源，按钮同样标为蓝色
                    GUI.backgroundColor = item.TextColor.HasValue ? item.TextColor.Value : Color.white;
                    bool ctrlHeld = Event.current != null && Event.current.control;
                    if (item.Target != null &&
                        GUILayout.Button(
                            new GUIContent("定位", ctrlHeld
                                ? "Ctrl+点击：单选定位该资源"
                                : "在 Project 窗口中定位该资源"),
                            GUILayout.Width(40), GUILayout.Height(18)))
                    {
                        if (ctrlHeld)
                        {
                            // Ctrl + 点击：单选定位目标对象（替换当前选择），并 Ping 提示
                            SelectSingleObject(item.Target);
                            EditorGUIUtility.PingObject(item.Target);
                        }
                        else
                        {
                            EditorGUIUtility.PingObject(item.Target);
                        }
                    }

                    GUI.backgroundColor = new Color(0.9f, 0.49f, 0.5f);
                    // ── 【添加】按钮:单独把该条对象加入资源箱,不等批量【确定】──
                    if (_onAddSingle != null && item.Target != null)
                    {
                        bool alreadyAdded = _addedTargetIds.Contains(VicToolsUnityCompat.GetStableId(item.Target));
                        using (new EditorGUI.DisabledScope(alreadyAdded))
                        {
                            if (GUILayout.Button(
                                    new GUIContent("添加", alreadyAdded
                                        ? "该对象已添加到资源箱"
                                        : "把该对象单独添加到资源箱(不等批量添加)"),
                                    GUILayout.Width(40), GUILayout.Height(18)))
                            {
                                bool ok = _onAddSingle(item.Target);
                                if (ok) _addedTargetIds.Add(VicToolsUnityCompat.GetStableId(item.Target));
                                ShowNotification(new GUIContent(ok
                                    ? $"已添加: {item.Target.name}"
                                    : "添加失败(可能已在资源箱中)"));
                                // 主动重绘:让动态概要与【确定添加（N）】的数量立刻递减
                                Repaint();
                            }
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                }
            }

            EditorGUILayout.Space(6);
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("取消", GUILayout.Width(90), GUILayout.Height(24)))
            {
                var cancel = _onCancel;
                Close();
                cancel?.Invoke();
                return;
            }
            GUI.backgroundColor = Color.red;
            using (new EditorGUI.DisabledScope(dyn.ConfirmDisabled))
            {
                if (GUILayout.Button(curConfirmText, GUILayout.Width(140), GUILayout.Height(24)))
                {
                    // 二次确认门(可选):返回 false 时保留窗口,不关闭、不提交
                    var guard = _submitGuard;
                    if (guard != null && !guard.Invoke()) return;

                    // 先取出局部引用再 Close():Close 会触发 OnDestroy 清空字段
                    var confirm = _onConfirm;
                    Close();
                    confirm?.Invoke();
                }
            }
            // ── 【添加引用对象】:一键把清单中被当前场景引用的资源加入资源箱(置于最右侧) ──
            if (_onAddReferenced != null)
            {
                var referencedTargets = CollectReferencedTargets();
                GUI.backgroundColor = new Color(0.35f, 0.65f, 1f);
                using (new EditorGUI.DisabledScope(referencedTargets.Count == 0 || _referencedAdded))
                {
                    if (GUILayout.Button(
                            new GUIContent($"添加引用对象（{referencedTargets.Count}）",
                                "一键把清单中被当前场景引用（蓝色标记）的资源加入资源箱"),
                            GUILayout.Width(170), GUILayout.Height(24)))
                    {
                        bool ok = _onAddReferenced(referencedTargets.ToArray());
                        if (ok)
                        {
                            _referencedAdded = true;
                            foreach (var t in referencedTargets)
                            {
                                _addedReferencedIds.Add(VicToolsUnityCompat.GetStableId(t));
                            }
                        }
                        ShowNotification(new GUIContent(ok
                            ? $"已添加 {referencedTargets.Count} 个引用对象到资源箱"
                            : "添加引用对象失败"));
                        Repaint();
                    }
                }
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void OnDestroy()
        {
            // 关窗即视为取消,不执行任何处理
            _onConfirm = null;
            _onCancel = null;
            _onAddSingle = null;
            _dynamicStateProvider = null;
            _submitGuard = null;
            _onAddReferenced = null;
            _addedTargetIds.Clear();
            _addedReferencedIds.Clear();
            _items = new List<Item>();
        }
    }
}
