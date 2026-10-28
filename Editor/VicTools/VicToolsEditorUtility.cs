// VicToolsEditorUtility - VicTools 包内的 Editor-only 公共工具集合
// 由 Vic.Editor 程序集(Vic.Editor.asmdef)承载,与 Runtime 工具(Vic.Runtime)分离。
// 命名约定：每个工具单独一个 public static class,文件对外即"工具类清单"。
// 当前成员：
//   - VicToolsIconLoader：PNG 图标加载助手（解码原始字节,绕开 AssetDatabase/TextureImporter,
//                        避免平台压缩 / isReadable=0 / .meta 缺失等坑）
//   - VicToolsListConfirmWindow：通用【处理列表确认窗口】（非模态,列出待处理清单后由用户确认再执行,
//                        可在需要展示处理列表时共用该方式）

using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

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

            public Item(string text, Object target = null)
            {
                Text = text ?? string.Empty;
                Target = target;
            }
        }

        private string _summary = string.Empty;
        private string _confirmButtonText = "确定";
        private List<Item> _items = new List<Item>();
        private Action _onConfirm;
        private Action _onCancel;
        private Vector2 _scrollPos;

        /// 打开处理列表确认窗口(非模态)。
        /// </summary>
        /// <param name="title">窗口标题。</param>
        /// <param name="summary">顶部概要文字(可多行,如"待处理 N 个 / 跳过 M 个")。</param>
        /// <param name="items">处理清单;为 null 时显示空列表。</param>
        /// <param name="onConfirm">点击【确定】时执行的回调(传 null 则相当于只看不改)。</param>
        /// <param name="confirmButtonText">确认按钮文字,默认"确定"。</param>
        /// <param name="onCancel">点击【取消】/关窗时执行的回调(可选)。</param>
        public static VicToolsListConfirmWindow Show(
            string title,
            string summary,
            IReadOnlyList<Item> items,
            Action onConfirm,
            string confirmButtonText = "确定",
            Action onCancel = null)
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
            window.minSize = new Vector2(560, 360);
            window.ShowUtility();
            return window;
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(4);

            if (!string.IsNullOrEmpty(_summary))
            {
                // 逐行绘制,保证多行概要文本正确换行显示
                var lines = _summary.Split('\n');
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
            EditorGUILayout.LabelField($"处理清单（{_items.Count} 条）：", EditorStyles.miniBoldLabel);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scroll.scrollPosition;
                for (int i = 0; i < _items.Count; i++)
                {
                    var item = _items[i];
                    if (item == null) continue;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"{i + 1}. {item.Text}");
                    if (item.Target != null &&
                        GUILayout.Button("定位", GUILayout.Width(40), GUILayout.Height(18)))
                    {
                        EditorGUIUtility.PingObject(item.Target);
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
            if (GUILayout.Button(_confirmButtonText, GUILayout.Width(140), GUILayout.Height(24)))
            {
                // 先取出局部引用再 Close():Close 会触发 OnDestroy 清空字段
                var confirm = _onConfirm;
                Close();
                confirm?.Invoke();
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
        }

        private void OnDestroy()
        {
            // 关窗即视为取消,不执行任何处理
            _onConfirm = null;
            _onCancel = null;
            _items = new List<Item>();
        }
    }
}
