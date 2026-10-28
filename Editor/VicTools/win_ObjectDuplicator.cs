using UnityEngine;
using UnityEditor;

namespace VicTools
{
    /// 提供一个独立的编辑器窗口来使用批量复制工具
    public class WinObjectDuplicator : EditorWindow
    {
        private ObjectDuplicator _analyzer;

        // 独立窗口的选中对象数量 - 不依赖主窗口
        public int StandaloneSelectedCount { get; set; }

        /// 显示批量复制独立窗口的菜单项
        [MenuItem("Tools/VicTools(YD)/[ ScenesTools ] 独立窗口 >/[批量复制]", false, 1804)]
        public static void ShowWindow()
        {
            var window = GetWindow<WinObjectDuplicator>("批量复制");
            window.minSize = new Vector2(470, 562);
            window.maxSize = new Vector2(1680, 1832);
        }

        /// 窗口启用时调用,初始化批量复制工具
        private void OnEnable()
        {
            try
            {
                // 注册选择变化事件
                Selection.selectionChanged += OnSelectionChanged;

                // 创建批量复制工具实例
                _analyzer = new ObjectDuplicator(ObjectDuplicator.WindowName, this);
                _analyzer.OnEnable();

                Debug.Log("批量复制独立窗口已初始化");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"初始化批量复制独立窗口失败: {ex.Message}");
            }
        }

        /// 窗口禁用时调用,清理资源
        private void OnDisable()
        {
            try
            {
                // 取消注册选择变化事件
                Selection.selectionChanged -= OnSelectionChanged;

                if (_analyzer != null)
                {
                    _analyzer.OnDisable();
                    Debug.Log("批量复制独立窗口已禁用");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"禁用批量复制独立窗口时出错: {ex.Message}");
            }
        }

        /// 窗口销毁时调用,清理资源
        private void OnDestroy()
        {
            try
            {
                // 确保取消注册选择变化事件
                Selection.selectionChanged -= OnSelectionChanged;

                if (_analyzer != null)
                {
                    _analyzer.OnDestroy();
                    _analyzer = null;
                    Debug.Log("批量复制独立窗口已销毁");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"销毁批量复制独立窗口时出错: {ex.Message}");
            }
        }

        /// 窗口获得焦点时调用
        private void OnFocus()
        {
            _analyzer?.OnFocus();
        }

        /// 窗口失去焦点时调用
        private void OnLostFocus()
        {
            _analyzer?.OnLostFocus();
        }

        /// 层级结构变化时调用
        private void OnHierarchyChange()
        {
            _analyzer?.OnHierarchyChange();
        }

        /// 处理选择变化事件 - 只更新独立窗口的选中对象数量
        private void OnSelectionChanged()
        {
            try
            {
                StandaloneSelectedCount = Selection.gameObjects.Length;
                Repaint();
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"处理选择变化事件时出错: {ex.Message}");
            }
        }

        /// 绘制窗口GUI界面
        private void OnGUI()
        {
            if (_analyzer != null)
            {
                try
                {
                    _analyzer.OnGUI();
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"绘制批量复制界面失败: {ex.Message}");
                    EditorGUILayout.HelpBox($"界面绘制出错:{ex.Message}\n请查看控制台获取详细信息", MessageType.Error);

                    if (GUILayout.Button("重新初始化"))
                    {
                        OnEnable();
                    }
                }
            }
            else
            {
                EditorGUILayout.LabelField("批量复制工具未初始化");

                if (GUILayout.Button("初始化批量复制工具"))
                {
                    OnEnable();
                }
            }
        }
    }
}
