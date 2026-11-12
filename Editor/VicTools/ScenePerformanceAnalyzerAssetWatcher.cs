using UnityEditor;

namespace VicTools
{
    /// 工程资源变更监听器。
    /// 工程内新增 / 删除 / 移动资源（如导入 mp4、wav）不会触发 OnHierarchyChange，
    /// 需通过 AssetPostprocessor 感知，否则「重要资源占比」不会自动刷新。
    public static class ScenePerformanceAnalyzerAssetWatcher
    {
        /// 自上次消费后，是否发生过资源导入 / 删除 / 移动
        public static bool HasChanges { get; internal set; }

        /// 消费掉当前的变更标记
        public static void ConsumeChanges()
        {
            HasChanges = false;
        }
    }

    /// 资源导入后置处理器：仅做变更标记，不修改任何资源
    public class ScenePerformanceAnalyzerAssetImporter : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (importedAssets.Length > 0 || deletedAssets.Length > 0 || movedAssets.Length > 0)
            {
                ScenePerformanceAnalyzerAssetWatcher.HasChanges = true;
            }
        }
    }
}
