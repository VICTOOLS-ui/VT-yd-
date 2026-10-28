// VicToolsEditorCompat - Editor 侧跨 Unity 版本 API 兼容层
//
// 与 Runtime 侧的 VicToolsUnityCompat 配套，但只能放在 Editor 程序集：
// EditorUtility 属于 UnityEditor，在 Runtime asmdef 中不可见。
//
// 覆盖两处 error 级 obsolete（**分界版本不同，勿用同一个宏**）：
//   1. EditorUtility.InstanceIDToObject(int)  →  EditorUtility.EntityIdToObject(EntityId)
//      [Obsolete("InstanceIDToObject(int) is obsolete. Use EditorUtility.EntityIdToObject instead.", error: true)]
//      分界：6000.4 起（6000.0~6000.3 仍只有旧 API 且未被废弃）→ 用 UNITY_6000_4_OR_NEWER
//   2. SceneHandle → int 的隐式转换            →  SceneHandle.GetRawData()
//      [Obsolete("Implicit conversion from SceneHandle to int is deprecated. Use SceneHandle.GetRawData() instead", error: true)]
//      分界：6000.5 起（实测 6000.0.59f2 的 CoreModule 中既无 "GetRawData"
//      也无该 obsolete 消息字符串）→ 保守用 UNITY_6000_5_OR_NEWER
//
// ⚠ 已排除的错误方案（勿再尝试）：
//   用 `#pragma warning disable CS0619` 包住legacy API —— **无效**。
//   CS0619 是 error 级诊断而非 warning，pragma 压不住（Unity 6 实测仍编译失败）。
//   必须真正切到新 API。
//
// ⚠ EntityId 的取数限制：
//   `EntityId → int` 的 operator 自身也是 error 级 obsolete
//   （"EntityId will not be representable by an int in the future"），
//   故不可写 (int)entityId；Runtime 侧改用 entityId.GetHashCode() 取身份值，
//   Editor 侧反向重建改用 entityId.FromULong((ulong)(uint)id)。
//
// 关键：ID 语义与 Runtime 侧严格配对。
//   GetReversibleId(obj) 产出的 int，必须能用本文件的 IdToObject() 换回同一个对象。
//   切勿在这里改用 VicToolsUnityCompat.GetStableId()——那是 hash，不可反查。
//
// SceneHandle 说明：Unity 6 的 handle.GetRawData() 返回 **ulong**（底层 64 位 handle 数据），
//   而旧版隐式转换的目标是 int。所以 GetSceneHandle 统一返回 ulong，
//   调用方的去重集合也必须是 HashSet<ulong> —— 若强转 int 会截断，
//   两个不同场景的 handle 可能撞成同值，导致同一场景被重复加入 order。

using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Vic.Editor
{
    /// <summary>
    /// VicTools Editor 侧跨 Unity 版本 API 兼容层。
    /// </summary>
    public static class VicToolsEditorCompat
    {
        /// <summary>
        /// 把 <see cref="Vic.Runtime.VicToolsUnityCompat.GetReversibleId"/> 产出的 ID 换回对象。
        ///
        /// 对象已销毁 / ID 失效时返回 null。传入 0（空 ID）返回 null，不抛异常。
        ///
        /// ── Unity 6 实现说明 ────────────────────────────────────────────
        /// 不能写 <c>EditorUtility.EntityIdToObject((EntityId)id)</c>：
        /// int → EntityId 的隐式转换在 6000.4 的公开文档中尚不存在，
        /// 而 6000.5 起该方向又已被标为 error 级 obsolete
        /// （"EntityId will not be representable by an int in the future"），
        /// 两头都不可用。改用 <c>EntityId.FromULong</c> —— 它在 6000.4/6000.5
        /// 均是公开 API（"Creates an EntityId from raw ulong data"）。
        ///
        /// <c>(ulong)(uint)id</c> 而非 <c>(ulong)id</c>：
        /// 后者对负数做符号扩展（补全高 32 位），会把不存在的低位布局补进去；
        /// 先转 uint 保留原 32 位模式、再零扩展，才能与
        /// Runtime 侧 <c>GetEntityId().GetHashCode()</c> 的位精确对应。
        ///
        /// 若 Unity 后续连 FromULong 也废弃，则本方法签名需改为接收
        /// EntityId 本身（见 Runtime 侧 GetReversibleId 注释中的迁移方案）。
        /// </summary>
        public static Object IdToObject(int id)
        {
            if (id == 0) return null;

#if UNITY_6000_4_OR_NEWER
            return EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)(uint)id));
#else
            return EditorUtility.InstanceIDToObject(id);
#endif
        }

        /// <summary>
        /// 取 <see cref="Scene"/> 的原始 handle 值，用于 HashSet / Dictionary 去重与比较。
        ///
        /// 返回 <see cref="ulong"/> 而非 int：Unity 6 的 <c>GetRawData()</c> 实际返回
        /// <b>ulong</b>（底层 handle 是 64 位数据），若强转 int 会在数值超过 2^32 时截断，
        /// 导致两个不同场景的 handle 撞成同一个值 → 同一场景被重复加入 order。
        /// 2021~6000.4 无 GetRawData，其隐式转换目标是 int，
        /// 这里统一提升为 ulong，保证容器类型跨版本一致。
        ///
        /// ⚠ 两套数值语义<strong>不一定相同</strong>（legacy 是 SceneManager 运行时索引，
        /// GetRawData 是底层 64 位 handle 数据）。本方法只用于「单次调用内对已加载场景去重」，
        /// 只要<strong>同一版本内所有取值都经由本方法</strong>，混用就不会发生。
        /// 因此禁止把本方法结果与任何直接取自 <c>scene.handle</c> 的数值混存。
        /// </summary>
        public static ulong GetSceneHandle(Scene scene)
        {
#if UNITY_6000_5_OR_NEWER
            return scene.handle.GetRawData();
#else
            // 6000.0~6000.4：GetRawData 尚不存在，但旧的隐式 int 转换未被废弃，可直接用
            return (ulong)(int)scene.handle;
#endif
        }
    }
}