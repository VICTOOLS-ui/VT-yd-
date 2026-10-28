// VicToolsUnityCompat - 跨 Unity 版本 API 兼容层
//
// 背景：Unity 6000.4 起，UnityEngine.Object.GetInstanceID() 被标记为
//   [Obsolete("GetInstanceID is deprecated. Use GetEntityId instead.", error: true)]
// 注意是 error: true —— 不是警告，而是**编译错误**（CS0619），
// 所以在新版 Unity 下整个包会直接编译失败，无法使用。
// 官方替代方案是 GetEntityId()，它返回 UnityEngine.EntityId 结构体；
// 但它在 6000.4 之前（含 6000.0 LTS ~ 6000.3）根本不存在，
// 那一段仍必须用 GetInstanceID() —— 这就是本文件存在的原因。
//
// 为什么需要这一层：
//   本包需同时支持 Unity 2021/2022/2023/6000.0~6000.3 与 Unity 6000.4+，
//   而这两段的可用 API 是**互斥**的：
//     · < 6000.4 ：只有 GetInstanceID（实测 6000.0.59f2 的 CoreModule 中
//                  不含 "Use GetEntityId instead" 字符串 → 未被 obsolete）
//     · >= 6000.4：GetInstanceID 被标 error 级 obsolete，改为 GetEntityId
//                  （官方文档 Object.GetEntityId 页 "Versions without this page"
//                    列出的正是 6.3 LTS / 6.0 LTS 及更早版本）
//   两个 API 无法在同一份代码里无条件下同时调用，
//   故用条件编译（UNITY_6000_4_OR_NEWER）分流到统一入口，
//   业务代码一律调 GetStableId() / GetReversibleId()，不再直接触碰版本相关 API。
//
// ⚠ 阈值来源（勿凭印象上调/下调）：
//   实测 Unity 6000.0.59f2 与 6000.5.10f1 的 UnityEngine.CoreModule.dll 字符串表：
//     6000.0.59f2：GetEntityId = 0 次、FromULong = 0 次、GetRawData = 0 次
//     6000.5.10f1：GetEntityId / FromULong / GetRawData 均存在
//   官方文档亦确认 Object.GetEntityId 与 EditorUtility.EntityIdToObject
//   自 6000.4 起才有页面，6000.0/6000.1/6000.2/6000.3 均为 404。
//   若日后 Unity 再改这两组的分界，只需改本文件与 VicToolsEditorCompat.cs
//   的这一处条件编译符号。
//
// ⚠ 已排除的错误方案（勿再尝试）：
//   用 `#pragma warning disable CS0619` 包裹 GetInstanceID() —— **无效**。
//   CS0619 是 error 级诊断，不是 warning，pragma 无法抑制
//   （实测 Unity 6 下仍报编译错误）。唯一正确做法是换用 GetEntityId()。
//
// ⚠ Unity 6 下 EntityId → int 的限制：
//   `EntityId → int` 的转换 operator 自身也被标记为
//     [Obsolete("EntityId will not be representable by an int in the future...", error: true)]
//   因此不能写 (int)obj.GetEntityId()。
//   当前用 GetEntityId().GetHashCode() 取值 —— 这是新版 Unity 下可用的取数方式
//   （官方 XML 文档："Returns a hash code for this EntityId. A 32-bit signed integer
//   hash code."），与同类型提供的 Equals(int) / int↔EntityId 隐式转换相互印证，
//   即 Unity 把 EntityId 的 32 位身份值视作原 InstanceID 的延续。
//   但官方已预告 EntityId 将来可能超出 int 范围，届时本文件需改为
//   类型化存储 EntityId（见 GetReversibleId 注释中的迁移方案）。
//
// 用法：
//   int id = VicToolsUnityCompat.GetStableId(someObject);        // 唯一标识用
//   int id = VicToolsUnityCompat.GetStableId(this);               // MonoBehaviour / Editor 内
//   string s = VicToolsUnityCompat.GetStableIdString(someObject); // 需要拼进名字/字符串时
//   int id = VicToolsUnityCompat.GetReversibleId(someObject);     // 需要之后换回对象时
//
// 语义保证：
//   - GetStableId：返回值恒为非负，仅保证「同一会话内稳定且互不相同」，
//     不可反查。适合 Dictionary key、控件名去重、Mesh 命名后缀。
//   - GetReversibleId：可能为负，但可用 VicToolsEditorCompat.IdToObject() 换回对象。
//   - 两者均不保证跨会话 / 跨编辑器重启的数值稳定（GetInstanceID 本身也不保证），
//     不可用于持久化存储。需要持久化请用 GlobalObjectId。

using UnityEngine;

namespace Vic.Runtime
{
    /// <summary>
    /// VicTools 跨 Unity 版本 API 兼容层。
    /// 目前仅封装 GetInstanceID / GetEntityId 的版本差异，后续 Unity 版本迁移可在此集中扩展。
    /// </summary>
    public static class VicToolsUnityCompat
    {
        /// <summary>
        /// 获取 <paramref name="obj"/> 的稳定唯一数值 ID（恒为非负，<strong>不可反查</strong>）。
        ///
        /// 仅用于「同一进程内唯一即可」的用途：Dictionary key、去重集合、Mesh 名 / 控件名后缀。
        /// 需要「换回对象」请用 <see cref="GetReversibleId"/>。
        ///
        /// 取 <c>&amp; 0x7FFFFFFF</c> 抹掉符号位，而非 <c>Math.Abs()</c>：
        /// <c>Math.Abs(int.MinValue)</c> 在 .NET 中返回 int.MinValue 本身（溢出），
        /// 会让返回值为负 —— 而调用点会把它直接拼进 Mesh 名 / 控件名，
        /// 负号与超长数字都可能触发 Unity 的 64 字符名截断。
        /// 也正因抹了符号位，本方法结果<strong>不能</strong>喂给 IdToObject 反查。
        /// </summary>
        /// <param name="obj">目标对象，允许为 null（此时返回 0）。</param>
        public static int GetStableId(Object obj)
        {
            if (obj == null) return 0;

#if UNITY_6000_4_OR_NEWER
            return obj.GetEntityId().GetHashCode() & 0x7FFFFFFF;
#else
            return obj.GetInstanceID() & 0x7FFFFFFF;
#endif
        }

        /// <summary>
        /// <see cref="GetStableId"/> 的字符串版本，用于拼接对象名 / 控件名。
        /// null 返回 <c>"0"</c>（与非 null 的 "0" 撞名概率可忽略，
        /// 且所有调用点在使用前都已判空，不构成实际问题）。
        /// </summary>
        public static string GetStableIdString(Object obj)
        {
            return GetStableId(obj).ToString();
        }

        /// <summary>
        /// 获取<strong>可反查</strong>的原始 ID —— 存入后必须能用它换回同一个对象
        /// （配套 <c>VicToolsEditorCompat.IdToObject</c>）。
        ///
        /// 与 <see cref="GetStableId"/> 的区别：
        /// <list type="bullet">
        /// <item>本方法返回<strong>未加工的原始索引</strong>，可反查。</item>
        /// <item>返回值<strong>可能为负</strong>，<strong>不可做绝对值 / 位与</strong>——
        /// 抹掉符号位会让 ID 与原生不匹配，反查必然失败。</item>
        /// <item><see cref="GetStableId"/> 抹了符号位，只保证「唯一」不保证「可反查」，
        /// 仅适合 Dictionary key、Mesh 名后缀等一次性用途。</item>
        /// </list>
        ///
        /// 选型规则：
        ///   ID 只在本进程内当唯一标识（去重 / 分组 / 命名）→ GetStableId
        ///   ID 需存下来并换回对象（存档 / 反查 / ID 比较）→ GetReversibleId
        ///
        /// ── Unity 6 实现说明 ────────────────────────────────────────────
        /// 不能写 <c>(int)obj.GetEntityId()</c>：EntityId → int 的转换 operator
        /// 自身是 error 级 obsolete（"EntityId will not be representable by an int
        /// in the future"）。改用 <c>GetEntityId().GetHashCode()</c>，
        /// 其返回值即 EntityId 的 32 位身份值（官方 XML 文档："A 32-bit signed
        /// integer hash code"），且不做任何位运算，可原样喂回 <c>IdToObject</c>。
        ///
        /// ⚠ 可逆性依赖 EntityId 的 int 视图与内部身份值一致
        ///   （同类型还提供了 <c>Equals(int)</c> 与 int↔EntityId 隐式转换，
        ///    说明 Unity 本身就把 EntityId 当作原 InstanceID 的整数身份延续）。
        ///   若 Unity 未来变更该布局，<c>IdToObject</c> 会静默返回 null，
        ///   表现为"资源箱灰色对象恢复不回来"，而非报错 —— 届时需回归验证。
        ///
        /// 未来 Unity 让 EntityId 超出 int 范围时，本方法的 int 返回值将失效。
        /// 届时两种迁移方向：
        ///   A. 条件编译类型别名（Unity 6 下存 EntityId，旧版存 int），
        ///      改动面较大：ScenesTools 的 _resourceBoxInstanceIDs /
        ///      _instanceIDToDisplayName / newInstanceIDs / existingInstanceIDs 等集合
        ///      及其字符串序列化格式都要一并调整。
        ///   B. 改用 GlobalObjectId（字符串、Unity 6 未废弃、跨会话稳定），
        ///      ScenesTools 已有 GLOBALID: 主路径，INSTANCE: 仅作旧格式兜底。
        /// </summary>
        public static int GetReversibleId(Object obj)
        {
            if (obj == null) return 0;

#if UNITY_6000_4_OR_NEWER
            // 注意：不可加 & 0x7FFFFFFF，也不可用 Math.Abs —— 会破坏可反查性
            return obj.GetEntityId().GetHashCode();
#else
            return obj.GetInstanceID();
#endif
        }
    }
}