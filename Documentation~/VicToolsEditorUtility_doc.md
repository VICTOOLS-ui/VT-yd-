**VicToolsEditorUtility 使用文档**

> 包：`com.youdoo.victools`
> 源文件：`Editor/VicTools/VicToolsEditorUtility.cs`
> 程序集：`Vic.Editor`（`Editor/Vic.Editor.asmdef`）
> 命名空间：`Vic.Editor`
> 类型：公共工具集合（静态工具类 + 通用 EditorWindow 组件）
> 适用对象：本包及同程序集（`Vic.Editor`）内的 Editor 工具作者

---

## 目录

1. [概述](#1-概述)
2. [当前成员一览](#2-当前成员一览)
3. [VicToolsIconLoader — PNG 图标加载助手](#3-victoolsiconloader--png-图标加载助手)
   - 3.1 [设计动机](#31-设计动机)
   - 3.2 [API 总览](#32-api-总览)
   - 3.3 [LoadPngIcon — 加载 PNG 图标](#33-loadpngicon--加载-png-图标)
   - 3.4 [Release — 释放单张贴图](#34-release--释放单张贴图)
   - 3.5 [ReleaseAll — 释放列表中所有贴图](#35-releaseall--释放列表中所有贴图)
   - 3.6 [完整使用模式](#36-完整使用模式)
   - 3.7 [与 AssetDatabase.LoadAssetAtPath 的对比](#37-与-assetdatabaseloadassetatpath-的对比)
   - 3.8 [常见问题 / FAQ](#38-常见问题--faq)
4. [VicToolsListConfirmWindow — 通用处理列表确认窗口](#4-victoolslistconfirmwindow--通用处理列表确认窗口)
   - 4.1 [设计动机](#41-设计动机)
   - 4.2 [API 总览](#42-api-总览)
   - 4.3 [Show — 打开确认窗口](#43-show--打开确认窗口)
   - 4.4 [Item — 清单条目](#44-item--清单条目)
   - 4.5 [单项添加 —【添加】按钮](#45-单项添加--添加按钮)
   - 4.5.1 [数量递减 — 动态状态（v1.3）](#451-数量递减--动态状态v13)
   - 4.6 [窗口行为与生命周期](#46-窗口行为与生命周期)
   - 4.7 [完整使用示例（资源箱批量添加项目场景）](#47-完整使用示例资源箱批量添加项目场景)
   - 4.8 [其他复用范式](#48-其他复用范式)
   - 4.9 [与 EditorUtility.DisplayDialog 的对比](#49-与-editorutilitydisplaydialog-的对比)
   - 4.10 [常见问题 / FAQ](#410-常见问题--faq)
5. [扩展规范](#5-扩展规范)（新增功能迭代用）
6. [变更日志](#6-变更日志)

---

## 1. 概述

`VicToolsEditorUtility.cs` 是 VicTools 包内 **Editor-only 公共工具集合**。它的定位是"包内所有 Editor 工具共享的、最底层、与业务无关的工具代码"。

### 核心特征

| 项 | 说明 |
|---|---|
| 承载程序集 | `Vic.Editor`（独立 asmdef，与 `Vic.Runtime` 分离，避免 Runtime 包污染） |
| 类型 | 以 `public static class` 为主；通用 UI 组件允许为 `public class`（继承 `EditorWindow`） |
| 命名 | 每个工具单独一个类，类名清晰表达职责（当前：`VicToolsIconLoader` / `VicToolsListConfirmWindow`） |
| 线程 | 全部在 Unity Editor 主线程使用，不保证线程安全 |
| 依赖 | 仅依赖 `UnityEditor` / `UnityEngine` / `System.IO` |

### 当前成员

- **VicToolsIconLoader** — PNG 图标加载助手（解码原始字节，绕开 `AssetDatabase` / `TextureImporter`）
- **VicToolsListConfirmWindow** — 通用【处理列表确认窗口】（非模态，列出待处理清单后由用户确认再执行；见 [§4](#4-victoolslistconfirmwindow--通用处理列表确认窗口)）

后续新增的工具类都应追加到本文件，并在本文档「变更日志」与对应章节同步更新。

---

## 2. 当前成员一览

| 类名 | 类型 | 职责 | 公开 API | 文档章节 |
|---|---|---|---|---|
| `VicToolsIconLoader` | 静态类 | PNG 图标加载 + 释放 | `LoadPngIcon` / `Release` / `ReleaseAll` | [§3](#3-victoolsiconloader--png-图标加载助手) |
| `VicToolsListConfirmWindow` | EditorWindow | 通用【处理列表确认窗口】（非模态，确认后才执行） | `Show` / `Item` / `DynamicState` | [§4](#4-victoolslistconfirmwindow--通用处理列表确认窗口) |

---

## 3. VicToolsIconLoader — PNG 图标加载助手

### 3.1 设计动机

在 Editor 工具中加载 PNG 图标，最直观的写法是 `AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/.../foo.png")`。但这条路径有 4 个常见坑：

| 坑 | 表现 |
|---|---|
| **依赖 `.meta` 文件** | 文件刚从外部工具拷入、SVN 同步后 .meta 未生成 → `LoadAssetAtPath` 静默返回 `null` |
| **依赖 `TextureImporter` 设置** | 设计师把 `isReadable` 勾掉后 → `GetPixels` 抛异常、且贴图被压缩到 DXT/BPTC/ETC2 影响锐利度 |
| **平台压缩** | Android 平台下 PNG 会被压成 ETC2，UI 边缘出现色块/模糊 |
| **路径写死 `Assets/`** | 包内文件路径为 `Packages/...`，需要拼接根路径，易出错 |

`VicToolsIconLoader.LoadPngIcon` 的解法：**用 `ImageConversion.LoadImage` 直接解码原始 PNG 字节**，完全绕开 `AssetDatabase` 与 `TextureImporter`：

- 强制 `RGBA32` uncompressed，CPU 端 readable
- 走 `File.ReadAllBytes`，路径只要文件存在就能加载
- 设置 `alphaIsTransparency = true`，透明背景的 PNG 在 GUI 中正确显示
- 创建的是**独立 `Texture2D` 实例**，Unity 不会自动回收，必须由调用方显式释放

### 3.2 API 总览

| 方法 | 签名 | 用途 | 必须持有引用？ |
|---|---|---|---|
| `LoadPngIcon` | `Texture2D LoadPngIcon(string path)` | 从路径加载 PNG，返回 RGBA32 贴图 | **是**（必须由 `Release` / `ReleaseAll` 释放） |
| `Release` | `void Release(Texture2D tex)` | 释放单张贴图 | — |
| `ReleaseAll` | `void ReleaseAll(List<Texture2D> ownedIcons)` | 释放列表中所有贴图并清空列表 | — |

### 3.3 LoadPngIcon — 加载 PNG 图标

#### 签名

```csharp
public static Texture2D LoadPngIcon(string path)
```

#### 参数

| 参数 | 类型 | 说明 |
|---|---|---|
| `path` | `string` | PNG 文件路径。**支持绝对路径与 Unity 工程内相对路径**（如 `"Packages/com.youdoo.victools/Editor/Icons~/foo.png"`） |

#### 返回值

- 成功：解码后的 `Texture2D`（`RGBA32`、`alphaIsTransparency=true`、`HideAndDontSave`）
- 失败：`null`，并写 `Debug.LogWarning`，日志形如 `[VicToolsIconLoader] 文件不存在: <path>`

#### 失败场景

| 触发条件 | 日志 | 返回值 |
|---|---|---|
| `path` 为 null/空 | `路径为空,跳过加载` | `null` |
| 文件不存在 | `文件不存在: <path>` | `null` |
| `File.ReadAllBytes` 抛异常（权限等） | `读取失败 <path>: <异常消息>` | `null` |
| PNG 不合法 | `PNG 解码失败(可能不是合法 PNG): <path>` | `null` |

#### 关键行为

1. **格式强制**：`Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false)`，后由 `LoadImage` 重新分配到 PNG 实际尺寸
2. **可读性**：`markNonReadable=false`，保留 CPU 端可读。如不需要 `GetPixels`，可改 `true` 省内存
3. **alpha 透明**：`tex.alphaIsTransparency = true`。对带透明背景的设计稿 PNG 至关重要，否则 GUI/DockTab 区域会把 alpha=0 的像素当成 RGB 实色显示，导致图标"填满整个图标槽"
4. **隐藏标志**：`HideFlags.HideAndDontSave`，避免被场景序列化、不被 `Resources.UnloadUnusedAssets` 误回收（但仍需调用方显式销毁）

#### 使用示例

```csharp
// 推荐：用 '~' 后缀目录,Unity 不处理该目录(不生成 .meta),File.ReadAllBytes 不受影响
var tex = VicToolsIconLoader.LoadPngIcon(
    "Packages/com.youdoo.victools/Editor/Icons~/myIcon.png");

if (tex != null) {
    GUI.DrawTexture(rect, tex);
    // ... 用完后调用 Release
}
```

#### ⚠️ 重要：必须持有引用并释放

`LoadPngIcon` 返回的 `Texture2D` 是独立实例，**Unity 不会自动回收**。如果只 `GUI.DrawTexture` 后丢弃引用，会导致**贴图内存泄漏**。参见 [§3.4](#34-release--释放单张贴图) / [§3.5](#35-releaseall--释放列表中所有贴图)。

### 3.4 Release — 释放单张贴图

#### 签名

```csharp
public static void Release(Texture2D tex)
```

#### 行为

- 内部调用 `Object.DestroyImmediate(tex)`
- **`null` 安全**：传 `null` 不抛异常

#### 适用场景

- 临时加载、即用即弃（如一次性弹窗的图标）
- 单张图标的精确释放

#### 使用示例

```csharp
var tex = VicToolsIconLoader.LoadPngIcon("...");
GUI.DrawTexture(rect, tex);
VicToolsIconLoader.Release(tex);   // 用完即释放
```

### 3.5 ReleaseAll — 释放列表中所有贴图

#### 签名

```csharp
public static void ReleaseAll(List<Texture2D> ownedIcons)
```

#### 行为

- 遍历列表，对每张非 `null` 的 `Texture2D` 调用 `DestroyImmediate`
- 调用结束后**自动 `Clear()` 清空列表**
- **`null` 列表安全**：传 `null` 不抛异常

#### 适用场景

- **推荐场景**：在 `EditorWindow.OnDisable` / `Editor.OnDisable` / `OnDestroy` 中统一释放窗口持有的一批图标
- 防止域重载（domain reload）或窗口关闭时贴图泄漏

#### 使用示例

```csharp
public class MyWindow : EditorWindow
{
    // 持有本窗口加载的所有图标,窗口关闭时统一释放
    private readonly List<Texture2D> _ownedIcons = new();

    private void LoadIcons()
    {
        var icon = VicToolsIconLoader.LoadPngIcon(
            "Packages/com.youdoo.victools/Editor/Icons~/myIcon.png");
        if (icon != null) _ownedIcons.Add(icon);
    }

    private void OnDisable()
    {
        // 一次性销毁所有,避免域重载/窗口关闭时的内存泄漏
        VicToolsIconLoader.ReleaseAll(_ownedIcons);
    }
}
```

### 3.6 完整使用模式

#### 模式 A：按路径缓存（推荐用于下拉菜单 / 工具栏图标集）

适合**多窗口共享同一批图标** + **同一路径只解码一次**的场景：

```csharp
public class VicToolsWindow : EditorWindow
{
    // 按路径缓存 → 同名路径复用同一 Texture2D 实例,避免重复解码
    private static readonly Dictionary<string, Texture2D> _iconCache = new();

    // 持有列表 → OnDestroy / OnDisable 中统一释放
    private static readonly List<Texture2D> _ownedIcons = new();

    public static Texture2D LoadItemIcon(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)) return null;
        if (_iconCache.TryGetValue(assetPath, out var cached) && cached != null)
            return cached;

        var tex = VicToolsIconLoader.LoadPngIcon(assetPath);
        if (tex != null)
        {
            _iconCache[assetPath] = tex;
            _ownedIcons.Add(tex);   // 持引用
        }
        return tex;
    }

    private void OnDestroy()
    {
        VicToolsIconLoader.ReleaseAll(_ownedIcons);
        _iconCache.Clear();
    }
}
```

> 参考实现：`Editor/VicTools/VicToolsWindow.cs` 的 `_itemIconCache` + `_ownedDropdownIcons`

#### 模式 B：实例级缓存（推荐用于 SubWindow / 单窗口持有固定几个图标）

适合 SubWindow 场景（每个窗口实例独立管理图标，避免跨实例互相持有）：

```csharp
public class MySubWindow : SubWindow
{
    private Texture2D[] _iconCache;                       // 实例级
    private readonly List<Texture2D> _ownedIcons = new(); // 实例级持有列表

    public override void OnEnable()
    {
        base.OnEnable();
        _iconCache = new Texture2D[_modeCount];
        for (int i = 0; i < _modeCount; i++)
        {
            var tex = VicToolsIconLoader.LoadPngIcon(_iconPaths[i]);
            if (tex != null) _ownedIcons.Add(tex);   // 持引用
            _iconCache[i] = tex;
        }
    }

    public override void OnDisable()
    {
        VicToolsIconLoader.ReleaseAll(_ownedIcons);  // 释放
        _iconCache = null;
    }
}
```

> 参考实现：`Editor/VicTools/ObjectDuplicator.cs` 的 `_modeIconCache` + `_ownedModeIcons`

#### 模式 C：即用即弃

适合临时工具提示、一次性弹窗：

```csharp
void ShowTooltip()
{
    var tex = VicToolsIconLoader.LoadPngIcon(path);
    if (tex != null)
    {
        GUI.DrawTexture(rect, tex);
        VicToolsIconLoader.Release(tex);
    }
}
```

### 3.7 与 AssetDatabase.LoadAssetAtPath 的对比

| 维度 | `AssetDatabase.LoadAssetAtPath` | `VicToolsIconLoader.LoadPngIcon` |
|---|---|---|
| 路径形式 | 必须是 `"Assets/..."` 或 `"Packages/..."` 且已纳入 AssetDatabase | 任意 `File.Exists` 可访问的路径 |
| 依赖 `.meta` | **是**，未生成则返回 `null` | **否**，直接读字节 |
| 依赖 `TextureImporter` | **是**，受 `isReadable` / 平台压缩设置影响 | **否**，强制 `RGBA32` + readable |
| 平台压缩 | 走平台压缩（DXT/ETC2/BPTC…） | **不会**，uncompressed RGBA32 |
| alpha 透明 | 默认按 TextureImporter 设置 | **强制** `alphaIsTransparency = true` |
| 内存所有权 | 共享资产，Unity 自动管理 | **独立实例**，必须显式 `Release` |
| 失败表现 | 静默返回 `null` | 写 `Debug.LogWarning` 后返回 `null` |
| 加载性能 | 快（已有缓存） | 每次都重新解码（建议按路径缓存） |
| 适用场景 | 已规范导入工程的资源 | **包内原始 PNG**、用户刚放入的图标、跨平台一致性 |

### 3.8 常见问题 / FAQ

#### Q1：为什么不用 `AssetDatabase.LoadAssetAtPath` 更快？
**A**：`AssetDatabase` 走的是已导入的 Texture2D 资源（有缓存），确实更快。但它依赖 .meta + TextureImporter 链路，在**包内 `Icons~/` 这种不让 Unity 处理的目录**下无法工作。本助手设计目标就是绕开这条链路。

#### Q2：每次都重新解码，性能会不会有问题？
**A**：单张 PNG 解码通常 < 5ms。**强烈建议按路径缓存**（参见模式 A / B），避免重复解码。已缓存后等价于内存指针读取。

#### Q3：忘记调用 `Release` 会怎样？
**A**：贴图内存泄漏。每次 `LoadPngIcon` 都新建 `Texture2D` 实例，Unity 的 GC **不会**回收 `UnityEngine.Object` 子类（即使没有引用）。在 `OnDisable` / `OnDestroy` 中调用 `ReleaseAll` 是硬性要求。

#### Q4：`alphaIsTransparency = true` 有什么副作用？
**A**：对**不带 alpha 的 PNG 无副作用**（alpha 默认 255）。对**带 alpha 的 PNG** 是必须的：否则 GUI / DockTab 会把透明像素当成 RGB 实色，导致图标"填满整个图标槽"。

#### Q5：可以加载 JPG / EXR 吗？
**A**：`ImageConversion.LoadImage` 支持 PNG / JPG（EXR 走另一套 API）。本助手类名限定 PNG，如需 JPG 可扩 `LoadJpgIcon`，但要遵守"独立实例 + 显式释放"约定。

#### Q6：`HideAndDontSave` 与 `HideInHierarchy` 区别？
**A**：`HideAndDontSave = HideInHierarchy | HideInInspector | DontSave | NotEditable`。本助手使用 `HideAndDontSave`，确保贴图**不会**被场景序列化、不会出现在 Inspector，**但**仍受调用方手动 `DestroyImmediate` 控制（这不是自动回收，只是"避免误序列化"）。

#### Q7：域重载（Domain Reload）会清掉静态缓存吗？
**A**：会。域重载后所有静态字段清零。`LoadPngIcon` 不会在 `[InitializeOnLoad]` 中自动预热——缓存是 lazy 的（首次访问时解码）。如果必须在域重载后保持图标，需要在窗口 `OnEnable` / `OnAfterDeserialize` 中重新加载。

#### Q8：路径可以是网络 URL / `http://...` 吗？
**A**：不能。`File.ReadAllBytes` 只读本地文件。如需远程图标请自行用 `UnityWebRequestTexture`，但**那是另一个职责**，不应放入本助手。

---

## 4. VicToolsListConfirmWindow — 通用处理列表确认窗口

### 4.1 设计动机

Editor 里凡是"**先扫描 / 生成处理清单 → 让用户确认 → 再真正执行**"的场景，都需要弹一个确认框。最常见的写法是 `EditorUtility.DisplayDialog`，但它有 3 个硬伤：

| 痛点 | 表现 |
|---|---|
| **模态阻塞** | `DisplayDialog` 打开期间编辑器事件循环被阻塞，鼠标无法点开 Console / Project 窗口 |
| **文本长度受限** | 对话框只能显示纯文本，条目多时要么超长要么只能截断显示"前 20 条" |
| **无法定位资源** | 清单只是一行文字，用户看到"Assets/a/b.unity"也没法直接跳过去看 |

`VicToolsListConfirmWindow` 用**独立 `EditorWindow`（`ShowUtility`）**替代模态对话框：

- **不锁定焦点** → 窗口打开期间可以自由切到 Console 看完整日志、也可以在 Project 窗口里核对资源
- **清单可滚动、支持任意条数** → 逐条完整展示，不截断
- **每条可带【定位】按钮** → 一键 `PingObject` 到 Project 窗口
- **职责单一、与业务无关** → 任何"要展示处理列表"的工具都能直接复用

### 4.2 API 总览

| 类型 / 方法 | 签名 | 用途 |
|---|---|---|
| `VicToolsListConfirmWindow` | `public class ... : EditorWindow` | 通用处理列表确认窗口（非模态） |
| `Show` | `static VicToolsListConfirmWindow Show(string title, string summary, IReadOnlyList<Item> items, Action onConfirm, string confirmButtonText = "确定", Action onCancel = null, Func<Object, bool> onAddSingle = null, Func<DynamicState> dynamicStateProvider = null, Func<bool> submitGuard = null)` | 打开窗口并注册确认 / 取消 / 单项添加 / 批量提交二次确认回调 |
| `Item` | `public class Item { string Text; Object Target; }` | 清单条目：显示文本 + 可选定位目标 |
| `DynamicState` | `public struct DynamicState { string Summary; string ConfirmButtonText; bool ConfirmDisabled; }` | **动态状态快照**（v1.3 新增）：由 `dynamicStateProvider` 每次绘制时求值，用于让概要与【确定】按钮上的数量随单项添加实时递减 |

### 4.3 Show — 打开确认窗口

#### 签名

```csharp
public static VicToolsListConfirmWindow Show(
    string title,
    string summary,
    IReadOnlyList<Item> items,
    Action onConfirm,
    string confirmButtonText = "确定",
    Action onCancel = null,
    Func<Object, bool> onAddSingle = null,
    Func<DynamicState> dynamicStateProvider = null,
    Func<bool> submitGuard = null)
```

#### 参数

| 参数 | 类型 | 说明 |
|---|---|---|
| `title` | `string` | 窗口标题（显示在 Unity 窗口标签上）。为 null/空时回退为 `"确认"` |
| `summary` | `string` | 顶部概要文字，**支持多行**（内部按 `\n` 逐行绘制，如"待处理 N 个 / 跳过 M 个 / 失败 K 个"）。为 null/空时不绘制 |
| `items` | `IReadOnlyList<Item>` | 处理清单。`null` 安全（视作空清单） |
| `onConfirm` | `Action` | 点击【确定】按钮时执行。**传 null 则相当于"只看不改"**（窗口仍可打开） |
| `confirmButtonText` | `string` | 确认按钮文字，默认 `"确定"`；为 null/空时回退为 `"确定"` |
| `onCancel` | `Action` | 点击【取消】/直接关窗时执行（可选） |
| `onAddSingle` | `Func<Object, bool>` | **单项添加回调**（可选，v1.2 新增）。传入后清单每行在【定位】右侧多出【添加】按钮，点击即把该行 `Item.Target` 单独加入目标集合。返回 `true` = 成功（该行按钮置灰）。传 `null`（默认）= 不显示【添加】按钮。详见 [§4.5](#45-单项添加--添加按钮) |
| `dynamicStateProvider` | `Func<DynamicState>` | **动态状态提供者**（可选，v1.3 新增）。窗口**每次绘制**都会重新调用它，用返回值刷新【概要文字】与【确定按钮文字】，从而让"确定添加（N）"这类数量在单项添加后实时递减。传 `null`（默认）= 使用上面传入的静态 `summary` / `confirmButtonText`。详见 [§4.5.1](#451-数量递减--动态状态v13) |
| `submitGuard` | `Func<bool>` | **批量提交二次确认门**（可选，v1.4 新增）。点击【确定】时先调用它：返回 `true` 才关闭窗口并执行 `onConfirm`；返回 `false` 则**保留窗口、不做任何提交**（用于待处理数量过大时先弹确认框）。传 `null`（默认）= 直接提交。详见 [§4.5.2](#452-批量二次确认--提交门v14) |

#### 返回值

返回已创建并显示的窗口实例（一般无需接收，保留给需要进一步定制标题/尺寸的场景）。

#### 关键行为

1. **单实例**：调用前会先 `Close()` 掉已存在的同类窗口，避免重复点击按钮导致窗口叠加
2. **非模态**：`ShowUtility()` 浮出，不阻塞编辑器事件循环
3. **最小尺寸**：`minSize = (560, 360)`
4. **清单完整**：可滚动，**不限制条数**（与旧版弹窗"最多显示 20 条"不同）
5. **定位按钮**：仅当 `Item.Target != null` 时显示，点击执行 `EditorGUIUtility.PingObject(Target)`
6. **添加按钮**（v1.2）：仅当 `onAddSingle != null` 且 `Item.Target != null` 时显示，点击执行 `onAddSingle(Target)`（详见 [§4.5](#45-单项添加--添加按钮)）
7. **动态状态**（v1.3）：`dynamicStateProvider != null` 时，概要与【确定】按钮文字改为每次绘制重新求值；`DynamicState.ConfirmDisabled = true` 时【确定】按钮置灰（详见 [§4.5.1](#451-数量递减--动态状态v13)）
8. **提交二次确认门**（v1.4）：`submitGuard != null` 时，点击【确定】先执行它；返回 `false` 时窗口保持打开、`onConfirm` 不执行（详见 [§4.5.2](#452-批量二次确认--提交门v14)）

### 4.4 Item — 清单条目

```csharp
public class Item
{
    public string Text { get; }          // 显示文本（一般是资源路径或对象描述）
    public Object Target { get; }        // 可选定位目标；为 null 时不显示【定位】按钮
    public Item(string text, Object target = null);
}
```

| 场景 | 构造方式 | 效果 |
|---|---|---|
| 纯文本条目 | `new Item("说明文字")` | 只显示文字，无【定位】按钮 |
| 可定位条目 | `new Item(AssetDatabase.GetAssetPath(asset), asset)` | 显示文字 + 【定位】按钮（Ping 到 Project） |

> `Text` 为 null 时会自动回退为空字符串，不会抛异常。

### 4.5 单项添加 —【添加】按钮

> 参考实现：`Editor/VicTools/VicToolsEditorUtility.cs`（按钮绘制）；`Editor/VicTools/ScenesTools.cs`（`AddObjectToResourceBox()` / `AddSingleObjectToResourceBox()`）

#### 能力

`Show()` 传入 `onAddSingle` 回调后，清单每一行在【定位】按钮**右侧**多出一个【添加】按钮：

```text
1. Assets/Scenes/Main.unity        [定位] [添加]
2. Assets/Scenes/UI.unity          [定位] [添加]
```

点击【添加】→ **立即**把该行的 `Item.Target` 单独加入目标集合（资源箱），**不等**批量【确定添加】。

#### 参数

| 参数 | 类型 | 说明 |
|---|---|---|
| `onAddSingle` | `Func<Object, bool>` | 单项添加回调（可选）<br>• **入参**：被点击那一行的 `Item.Target`<br>• **返回值**：`true` = 添加成功（该行按钮置灰）；`false` = 失败（如已在集合中）<br>• 传 `null`（默认）= **不显示**【添加】按钮，完全保持 v1.1 行为 |

#### 关键行为

1. **显示条件**：`_onAddSingle != null` **且** `Item.Target != null` —— 与【定位】按钮条件一致；纯文本条目（`new Item("说明文字")`）不会出现【添加】按钮
2. **成功后置灰**：窗口内部用 `HashSet<int>`（`VicToolsUnityCompat.GetStableId()`）记录已添加对象，用 `EditorGUI.DisabledScope` 禁用该行按钮，避免重复点击（走兼容层取 ID，兼容 Unity 6000.4+ 下 `GetInstanceID` 被废弃的问题）
3. **即时反馈**：点击后 `ShowNotification` 弹出结果气泡（`已添加: XXX` / `添加失败(可能已在资源箱中)`）
4. **不触发批量回调**：点【添加】**不会**执行 `onConfirm`（批量写入），两个通道完全独立
5. **关窗即失效**：`OnDestroy` 清空 `_onAddSingle` 与 `_addedTargetIds`

#### 调用侧：资源箱的公开 API

窗口本身**不认识**"资源箱"，只认识 `Func<Object, bool>`。业务侧（`ScenesTools`）负责提供实现：

```csharp
/// 把单个对象添加到资源箱（公开 API，供确认窗口【添加】按钮等外部工具调用）
public bool AddObjectToResourceBox(Object obj)
{
    if (!obj) return false;                       // null / 已被销毁
    if (_resourceBox.Contains(obj)) return false; // 去重：同一实例只保留一份

    // 场景对象默认带 DontSaveInEditor，不清掉会存不进存档
    if (((int)obj.hideFlags & (int)HideFlags.DontSaveInEditor) != 0)
    {
        obj.hideFlags &= ~HideFlags.DontSaveInEditor;
    }

    InsertToResourceBoxByType(obj);           // 按类型插入 + 生成 displayName
    RebuildIndexMappings();                   // 重建 InstanceID→索引 映射
    UpdateSelectedObjectsInResourceBox();      // 刷新列表项上的选中态图标
    if (Parent) Parent.Repaint();             // 重绘场景工具界面
    SaveResourceBox(false);                   // 立即落盘（不弹确认框）
    return true;
}

/// 单项添加回调：把确认窗口里某一行【添加】按钮对应的对象单独加入资源箱
private bool AddSingleObjectToResourceBox(Object obj)
{
    if (!obj) return false;

    bool ok = AddObjectToResourceBox(obj);
    if (ok)
    {
        Debug.Log($"[ResourceBox] 已添加 {obj.name}");
        if (Parent) Parent.ShowNotification(new GUIContent($"已添加: {obj.name}"));
    }
    else
    {
        Debug.Log($"[ResourceBox] {obj.name} 已在资源箱中，跳过");
        if (Parent) Parent.ShowNotification(new GUIContent($"{obj.name} 已在资源箱中"));
    }
    return ok;
}
```

#### 接入示例

```csharp
VicToolsListConfirmWindow.Show(
    "添加项目场景",
    summary,
    confirmItems,
    () => AddScenesToResourceBox(newScenes, capturedSkipped, capturedFailed),  // 批量通道
    $"确定添加（{newScenes.Count}）",
    () => Debug.Log("[ResourceBox] 已取消添加项目场景"),
    obj => AddSingleAndRemove(obj, newScenes),                                // 单项通道（成功后从待添加列表移除）
    () => new VicToolsListConfirmWindow.DynamicState                           // 动态状态：数量实时递减
    {
        Summary = BuildScenesSummary(newScenes.Count, skippedCount, failedCount),
        ConfirmButtonText = $"确定添加（{newScenes.Count}）",
        ConfirmDisabled = newScenes.Count <= 0,
    });
```

> `AddSingleAndRemove` 与 `DynamicState` 的说明见 [§4.5.1 数量递减 — 动态状态](#451-数量递减--动态状态v13)。

#### 已在使用的调用点

| 调用点 | 窗口标题 | 单项添加对象 | 待添加列表（数量递减来源） |
|---|---|---|---|
| `AddAllProjectScenesToResourceBox()` | 添加项目场景 | `SceneAsset` | `newScenes`（`List<SceneAsset>`） |
| `AddCurrentSceneMainLightToResourceBox()` | 添加当前场景主光 | 主光 `GameObject` | `pendingLights`（`List<GameObject>`） |
| `AddCurrentSceneCamerasToResourceBox()` | 添加当前场景摄像机 | 摄像机 `GameObject` | `pendingCameras`（`List<GameObject>`） |
| `AddRenderPipelineConfigsToResourceBox()` | 添加渲染管线配置 | `RenderPipelineAsset` / `RendererData` | `pendingAssets`（`List<Object>`） |

### 4.5.1 数量递减 — 动态状态（v1.3）

> 需求背景：单项添加把某个对象单独加入资源箱后，【确定添加（N）】上的数量若仍停留在打开窗口那一刻的快照（如 10），会误导用户以为还有 10 个待添加。

#### 问题根因

`summary` 与 `confirmButtonText` 都是 `Show()` 调用时**一次性构造好的字符串**，窗口内部只负责把它们显示出来。单项添加并不会回头修改这两个字符串，因此数量不会自动递减。

#### 解决方案：动态状态提供者

`Show()` 增加末位可选参数 `Func<DynamicState> dynamicStateProvider`，窗口在 `OnGUI` 里**每次绘制都重新求值一次**：

```csharp
/// 动态状态快照：由 Show() 的 dynamicStateProvider 提供
public struct DynamicState
{
    /// 概要文字（可多行）；为空时沿用 Show 传入的静态 summary
    public string Summary;

    /// 确定按钮文字（如"确定添加（3）"）；为空时沿用 Show 传入的静态 confirmButtonText
    public string ConfirmButtonText;

    /// 是否禁用确定按钮。默认 false = 启用；用于"待添加数量已归零"时阻止无效的批量提交
    public bool ConfirmDisabled;
}
```

| 字段 | 类型 | 说明 |
|---|---|---|
| `Summary` | `string` | 顶部概要文字。为 null/空时沿用 `Show()` 传入的静态 `summary` |
| `ConfirmButtonText` | `string` | 【确定】按钮文字。为 null/空时沿用 `Show()` 传入的静态 `confirmButtonText` |
| `ConfirmDisabled` | `bool` | 是否禁用【确定】按钮。默认 `false` = 启用；数量归零时置 `true` 可避免空提交 |

#### 关键行为

1. **每次绘制求值**：`OnGUI` 开头调用一次 `dynamicStateProvider()`，据此决定概要文字、按钮文字与按钮可用状态
2. **空值回退**：任一字段为空时回退到 `Show()` 传入的静态值 —— 两者可混合使用，不必二选一
3. **即时刷新**：单项添加成功后窗口主动 `Repaint()`，数量立刻递减（无需手动刷新）
4. **数量递减的实现前提**：**调用侧**必须把"已单独添加"的对象从"待添加列表"中移除（见下方 `AddSingleAndRemove`），否则列表 `Count` 不变，动态状态取到的数字自然也不变
5. **关窗即失效**：`OnDestroy` 清空 `_dynamicStateProvider`

#### 调用侧：待添加列表 + 移除

业务侧（`ScenesTools`）维护一个"待添加列表"（只含**尚未入箱、批量操作会写入**的对象），单项添加成功后从中移除：

```csharp
/// 通用单项添加回调:添加成功后再把该对象从"待添加列表"移除,使全局待添加数量递减
/// <typeparam name="T">待添加列表的元素类型(须为 UnityEngine.Object 派生类)。</typeparam>
private bool AddSingleAndRemove<T>(Object obj, List<T> pendingList) where T : Object
{
    bool ok = AddSingleObjectToResourceBox(obj);
    // 从待添加列表移除 → 待添加数量递减
    // （UnityEngine.Object 的 Equals 按 InstanceID 比较，Remove 可正确命中）
    if (ok && obj is T typed && pendingList != null) pendingList.Remove(typed);
    return ok;
}
```

#### 接入示例（项目场景）

```csharp
var summary = BuildScenesSummary(newScenes.Count, skippedCount, failedCount);

VicToolsListConfirmWindow.Show(
    "添加项目场景",
    summary,
    confirmItems,
    () => AddScenesToResourceBox(newScenes, capturedSkipped, capturedFailed),  // 批量通道
    $"确定添加（{newScenes.Count}）",
    () => Debug.Log("[ResourceBox] 已取消添加项目场景"),
    // 单项通道：添加成功后从 newScenes 移除 → newScenes.Count 递减
    obj => AddSingleAndRemove(obj, newScenes),
    // 动态状态：每次绘制重新求值 → 概要与【确定添加（N）】实时递减
    () => new VicToolsListConfirmWindow.DynamicState
    {
        Summary = BuildScenesSummary(newScenes.Count, skippedCount, failedCount),
        ConfirmButtonText = $"确定添加（{newScenes.Count}）",
        ConfirmDisabled = newScenes.Count <= 0,
    });
```

效果：

```text
打开窗口：  待添加场景：10 个        [确定添加（10）]
单点 3 个【添加】后：
            待添加场景：7 个         [确定添加（7）]
全部单独添加完：待添加场景：0 个    [确定添加（0）]（按钮置灰）
```

#### 批量通道与单项通道的关系

- 单项添加**不会**触发 `onConfirm`（批量写入），两个通道依旧完全独立
- 批量写入时业务侧仍有**二次去重**（`_resourceBox.Contains(...)`），因此已单独添加的对象不会被重复入箱
- 主光窗口的【确定】按钮**始终可用**（`ConfirmDisabled = false`）：主光已在资源箱时点击仍会定位并选中它

### 4.5.2 批量二次确认 — 提交门（v1.4）

> 需求背景：待处理数量很大时（例如往资源箱批量加入上百项资源），用户点【确定添加（N）】容易误触，且无法在弹窗中核对数量。

#### 解决方案：提交门 `submitGuard`

`Show()` 增加末位可选参数 `Func<bool> submitGuard`，窗口在【确定】按钮处理里**先执行它**：

```csharp
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
```

| 情况 | 行为 |
|---|---|
| `submitGuard == null` | 直接关闭窗口并执行 `onConfirm`（与 v1.3 行为一致） |
| `submitGuard()` 返回 `true` | 关闭窗口并执行 `onConfirm` |
| `submitGuard()` 返回 `false` | **窗口保留**、`onConfirm` 不执行，用户可继续逐项添加或再次点击 |

#### 调用侧示例（数量超阈值时弹确认框）

```csharp
// 批量加入资源箱前的一次性确认：待添加数量超过阈值时弹确认框
private static bool ShouldConfirmBatchAdd(ResourceCategoryStat stat, int pendingCount)
{
    if (pendingCount <= ResourceBoxBatchConfirmThreshold) return true;

    bool confirmed = EditorUtility.DisplayDialog(
        "确认批量添加到资源箱",
        $"即将向资源箱添加 {pendingCount} 项资源（已超过 {ResourceBoxBatchConfirmThreshold} 项）。\n" +
        "资源箱是工具的对象工作清单，不会改变打包内容，请确认数量无误。",
        "确定添加", "取消");
    return confirmed;
}

// 传入窗口
VicToolsListConfirmWindow.Show(
    /* ... */,
    () => new VicToolsListConfirmWindow.DynamicState { /* ... */ },
    () => ShouldConfirmBatchAdd(stat, pendingAssets.Count));   // ← 提交门
```

### 4.6 窗口行为与生命周期

```
用户点击功能按钮
      │
      ▼
Show(...)  ──► 关闭旧窗口 → 创建新窗口 → ShowUtility()（非模态，立即返回）
      │
      │   ← 此刻调用方函数已结束，编辑器恢复交互（可切 Console / Project）
      │
      ├─ 用户点【确定】 → submitGuard? → 通过则取出 onConfirm 局部引用 → Close() → 执行 onConfirm()
      │                    └─ 拒绝则窗口保留，不提交
      │
      └─ 用户点【取消】或直接关窗 → 取出 onCancel 局部引用 → Close() → 执行 onCancel()
```

| 事件 | 行为 |
|---|---|
| 确认按钮 | 先执行 `submitGuard`（可选，v1.4）→ 通过后把 `onConfirm` 存入局部变量 → `Close()` → 再执行回调（**顺序很重要**：`Close()` 会触发 `OnDestroy` 清空字段）；`DynamicState.ConfirmDisabled = true` 时按钮置灰、不可点击 |
| 取消按钮 / 关窗 | 同上，执行 `onCancel`（可为 null） |
| 单项添加 | 执行 `onAddSingle(Target)` → 成功则记录到 `_addedTargetIds` 置灰该行 → `ShowNotification` 提示 → `Repaint()` 让动态数量立即刷新（**不触发** `onConfirm`） |
| `OnDestroy` | 清空 `_onConfirm` / `_onCancel` / `_onAddSingle` / `_dynamicStateProvider` / `_submitGuard`，重置 `_items`；**不做任何业务处理** |
| 重复调用 `Show` | 旧窗口的 `onCancel` 会被触发（因为旧窗口被 `Close()`），新窗口接管 |

> ⚠️ 注意：重复点击功能按钮时，上一次的窗口会被关闭并触发其 `onCancel` 回调。若 `onCancel` 里有"用户已取消"之类的日志，注意区分场景。

### 4.7 完整使用示例（资源箱批量添加项目场景）

> 参考实现：`Editor/VicTools/ScenesTools.cs` → `AddAllProjectScenesToResourceBox()` / `AddScenesToResourceBox()`
> 功能：扫描项目 `Assets` 下所有场景，列出清单让用户确认后再放进资源箱。

#### 步骤 1：扫描（只读，不改数据）

```csharp
/// v3.4 新增：一键将【项目中 Assets 目录下的所有场景】放入资源箱
private void AddAllProjectScenesToResourceBox()
{
    // ① AssetDatabase 检索（只保留 Assets/ 下，排除 Packages 内场景）
    var scenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var guid in AssetDatabase.FindAssets("t:Scene"))
    {
        var path = AssetDatabase.GUIDToAssetPath(guid);
        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
            scenePaths.Add(path);
    }

    // ② 文件系统兜底扫描（兼容团结引擎 .scene / Unity .unity）
    foreach (var ext in new[] { ".unity", ".scene" })
    {
        var files = System.IO.Directory.GetFiles(
            Application.dataPath, "*" + ext, System.IO.SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var relative = file.Substring(Application.dataPath.Length).Replace('\\', '/');
            scenePaths.Add("Assets" + relative);
        }
    }

    if (scenePaths.Count == 0)
    {
        Debug.LogWarning("[ResourceBox] 项目中未找到任何场景");
        if (Parent) Parent.ShowNotification(new GUIContent("项目中未找到任何场景"));
        return;
    }

    // ③ 扫描并分类（只读，不修改资源箱）
    var newScenes = new List<SceneAsset>();   // 待添加的场景
    var skippedCount = 0;                     // 已在资源箱中的场景
    var failedCount = 0;                      // 加载失败的场景

    var sortedPaths = new List<string>(scenePaths);
    sortedPaths.Sort(StringComparer.OrdinalIgnoreCase);   // 顺序稳定

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
        Debug.Log($"[ResourceBox][扫描] {msg}");
        if (Parent) Parent.ShowNotification(new GUIContent(msg));
        return;
    }
```

#### 步骤 2：组装清单 + 打开公共确认窗口

```csharp
    // ---------- ④ 扫描结果预览窗口（非模态，确认后才开始添加） ----------
    int capturedSkipped = skippedCount;
    int capturedFailed = failedCount;

    // 组装通用【处理列表确认窗口】的清单项（带定位目标）
    var confirmItems = new List<VicToolsListConfirmWindow.Item>(newScenes.Count);
    foreach (var sceneAsset in newScenes)
    {
        if (!sceneAsset) continue;
        confirmItems.Add(new VicToolsListConfirmWindow.Item(
            AssetDatabase.GetAssetPath(sceneAsset), sceneAsset));   // 文本 + 定位目标
    }

    // 概要文字：多行会逐行显示
    var summary = BuildScenesSummary(newScenes.Count, skippedCount, failedCount);

    // 公共窗口（Vic.Editor.VicToolsListConfirmWindow）：非模态，用户确认后才执行添加
    VicToolsListConfirmWindow.Show(
        "添加项目场景",                                     // 窗口标题
        summary,                                           // 顶部概要
        confirmItems,                                      // 完整清单（带【定位】）
        () => AddScenesToResourceBox(newScenes, capturedSkipped, capturedFailed), // 批量通道
        $"确定添加（{newScenes.Count}）",                    // 确认按钮文字（静态）
        () => Debug.Log("[ResourceBox] 已取消添加项目场景"),   // 取消回调
        // 单项通道：每行【添加】按钮，成功后从 newScenes 移除 → newScenes.Count 递减
        obj => AddSingleAndRemove(obj, newScenes),
        // 动态状态：每次绘制重新求值 → 概要与【确定添加（N）】随单项添加实时递减
        () => new VicToolsListConfirmWindow.DynamicState
        {
            Summary = BuildScenesSummary(newScenes.Count, skippedCount, failedCount),
            ConfirmButtonText = $"确定添加（{newScenes.Count}）",
            ConfirmDisabled = newScenes.Count <= 0,
        });
    // 实际写入由预览窗口的【确定添加】按钮触发；此处直接返回
}
```

#### 步骤 3：确认回调里才真正写入

```csharp
/// 把确认后的场景列表写入资源箱（由预览窗口的【确定添加】按钮回调）
private void AddScenesToResourceBox(List<SceneAsset> scenes, int skippedCount, int failedCount)
{
    if (scenes == null || scenes.Count == 0) return;

    var addedCount = 0;
    foreach (var sceneAsset in scenes)
    {
        if (!sceneAsset) continue;                    // 资源被删除等异常
        if (_resourceBox.Contains(sceneAsset)) continue; // 二次校验，避免重复添加
        InsertToResourceBoxByType(sceneAsset);        // 按类型插入
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
```

#### 关键点小结

| 关键点 | 做法 |
|---|---|
| 扫描与写入分离 | 扫描阶段只产出 `List<SceneAsset>` + 统计数字，**不碰资源箱**；写入只发生在确认回调里 |
| 参数闭包快照 | `capturedSkipped` / `capturedFailed` 先取局部副本，避免回调里读到被后续修改的局部变量（闭包捕获的是变量本身而非值） |
| 二次校验 | 确认窗口可能停留较久，回调里再 `Contains` 一次，防止期间资源被手动加进资源箱造成重复 |
| 完成提示不模态 | 用 `Parent.ShowNotification(...)` 气泡 + `Debug.Log`，避免"添加完成"弹窗再次卡住编辑器 |
| 可定位清单 | `Item` 的 `Target` 传 `SceneAsset`，用户在窗口里点【定位】即可跳到 Project 窗口核对 |

### 4.8 其他复用范式

#### 范式 A：纯文本清单（无资源定位）

适合"批量重命名 / 批量改属性 / 生成报告"等没有具体资源对象的场景：

```csharp
var items = new List<VicToolsListConfirmWindow.Item>();
foreach (var asset in assets)
    items.Add(new VicToolsListConfirmWindow.Item($"重命名 {asset.name} → {asset.name}_new"));

VicToolsListConfirmWindow.Show(
    "批量重命名",
    $"共 {items.Count} 个资源将被重命名",
    items,
    () => DoRename(assets));
```

#### 范式 B：带自定义取消处理的清单

```csharp
VicToolsListConfirmWindow.Show(
    "批量修复引用",
    summary,
    items,
    onConfirm: () => DoFix(items),
    confirmButtonText: "确定修复",
    onCancel: () => Debug.Log("[MyTool] 用户取消修复，未做任何改动"));
```

#### 范式 C：只看不改（纯展示）

`onConfirm` 传 `null`，窗口只作为"结果清单"展示器：

```csharp
VicToolsListConfirmWindow.Show(
    "扫描结果",
    $"共发现 {items.Count} 条",
    items,
    onConfirm: null);
```

### 4.9 与 EditorUtility.DisplayDialog 的对比

| 维度 | `EditorUtility.DisplayDialog` | `VicToolsListConfirmWindow` |
|---|---|---|
| 模态 | **是**（阻塞编辑器事件循环） | **否**（`ShowUtility` 浮出，可切 Console / Project） |
| 清单容量 | 纯文本，条目多时只能截断 | 可滚动，**完整展示任意条数** |
| 资源定位 | 不支持 | 每条可选【定位】按钮（`PingObject`） |
| 确认前能否继续操作 | 不能 | 能（窗口不独占交互） |
| 回调形式 | 返回 `bool` | `onConfirm` / `onCancel` 两个 `Action`，可携带上下文 |
| 窗口实例 | 系统弹窗 | 独立窗口，可自定义标题、最小尺寸、按钮文字 |
| 适用场景 | 简单的是/否询问 | **需要展示处理清单的批量操作**（推荐） |

> 简单的"确定/取消"仍可直接用 `DisplayDialog`；**只要涉及"让用户核对一份清单"**，统一用本窗口。

### 4.10 常见问题 / FAQ

#### Q1：为什么用 `ShowUtility()` 而不是 `Show()`？
**A**：`Show()` 会把窗口作为独立主窗口（带完整标题栏、可分离标签页），在 Unity 里通常用于"长期打开的工具窗口"；`ShowUtility()` 是浮动工具窗口，更适合"一次性的确认/预览"，且用户更容易一眼看出它可以随时关掉。

#### Q2：窗口打开期间数据被外部改动了怎么办？
**A**：本窗口只负责"展示 + 收集确认"，**不保证数据一致性**。因此约定：**扫描与写入分离**，并在写入回调里做二次校验（参见 [§4.7](#47-完整使用示例资源箱批量添加项目场景) 的"二次校验"）。

#### Q3：`onConfirm` 里可以再弹窗 / 再开窗口吗？
**A**：可以。`onConfirm` 是在 `Close()` 之后执行的，此时窗口已销毁，弹窗或再开窗口不会被自身遮挡。

#### Q4：为什么确认按钮是红色、取消按钮是绿色？
**A**：沿用资源箱工具既有的按钮配色约定（取消=绿色、确定=红色），并且在按钮绘制后会把 `GUI.backgroundColor` 复位为 `Color.white`，避免颜色泄漏到其它 GUI。

#### Q5：清单里某项的 `Target` 已经失效了怎么办？
**A**：`Target` 为 Unity 已销毁对象时，`Item.Target != null` 走 Unity 的重载判定为 `false`，【定位】按钮不会出现，条目文本照常显示。

#### Q6：可以传 `List<string>` 吗？
**A**：不能直接传，需要先转成 `List<Item>`（`new Item(text)`）。这是为了保留"可选定位目标"的能力。

#### Q7：能不能改成模态（必须先处理完才能用编辑器）？
**A**：技术上可以换成 `ShowModalUtility()`，但那样就失去了"切到 Console 看明细"的意义——这正是本窗口存在的理由，故保持非模态。

---

## 5. 扩展规范

后续若在本文件新增工具类，请遵守以下约定，并在本文档中追加对应章节。

### 5.1 新增工具类流程

1. **新增 `public static class`**：类名清晰表达职责（如 `VicToolsJsonHelper` / `VicToolsPathUtil`）；若为通用 UI 组件（EditorWindow 等），可用 `public class` 并继承对应基类
2. **顶部注释**：说明类用途、典型用法、与 `AssetDatabase` / `UnityEngine` 原生 API 的差异（若适用）
3. **每个公开方法写 `<summary>` + `<param>` + `<returns>` + `<remarks>`**（如有坑）
4. **失败处理**：失败必须返回 `null` / `false` / 默认值，并写 `Debug.LogWarning`，**不抛异常**（编辑器工具应宽容）
5. **内存所有权**：如果创建 `UnityEngine.Object` 子类（贴图、Mesh、Material…），必须提供对应的 `Release` 方法，并在 XML 注释中明确"调用方必须持有引用 + 显式释放"
6. **线程安全**：不保证线程安全，必须在主线程调用
7. **更新本文档**：
   - 在 [§2 当前成员一览](#2-当前成员一览) 表格追加一行
   - 新增 `### §X.Y <类名>` 主章节
   - 在 [§6 变更日志](#6-变更日志) 追加日期 + 简述

### 5.2 命名约定

| 元素 | 约定 | 示例 |
|---|---|---|
| 类名 | `VicTools<职责>Util` 或 `VicTools<职责>Loader` / `Helper` / `Window` | `VicToolsIconLoader` / `VicToolsListConfirmWindow` |
| 静态方法 | 动词 + 名词，清晰表达行为 | `LoadPngIcon` / `ReleaseAll` / `Show` |
| 日志前缀 | `[VicToolsIconLoader] ...` | `[VicToolsJsonHelper] 解析失败: ...` |

### 5.3 文档同步

**新增功能必须同步更新本文档**——这是硬性要求。每个工具类的"使用文档"是这个文件唯一对外的真相源（Source of Truth），不维护两份。

### 5.4 不应放入本文件的代码

- **业务相关**：如 `ObjectDuplicator`、`CustomHairGUI` 的具体业务逻辑
- **依赖具体类型**：如某个工具的私有 helper（应放回该工具类内）
- **Runtime 可复用**：如 `Mathf` / `Vector3` 扩展（应放入 `Vic.Runtime`）

---

## 6. 变更日志

| 日期 | 版本 | 变更 | 作者 |
|---|---|---|---|
| 2026-11-11 | 1.4 | `VicToolsListConfirmWindow` 新增**批量提交二次确认门**：`Show()` 增加 `submitGuard`（`Func<bool>`）参数，点击【确定】时先执行它，返回 `false` 则保留窗口、不执行 `onConfirm`；新增 [§4.5.2 批量二次确认 — 提交门](#452-批量二次确认--提交门v14)；`ScenePerformanceAnalyzer` 接入，待添加超过 30 项时弹数量确认框 | — |
| 2026-10-29 | 1.3 | `VicToolsListConfirmWindow` 新增**动态状态**能力：`Show()` 增加 `dynamicStateProvider`（`Func<DynamicState>`）参数与嵌套结构 `DynamicState`（`Summary` / `ConfirmButtonText` / `ConfirmDisabled`），窗口每次绘制重新求值，**单项添加后【确定添加（N）】与概要中的数量实时递减**、数量归零时【确定】按钮置灰；新增 [§4.5.1 数量递减 — 动态状态](#451-数量递减--动态状态v13)；`ScenesTools` 四个确认窗口（项目场景 / 主光 / 摄像机 / 渲染管线配置）接入，新增 `AddSingleAndRemove<T>()` 与 `Build*Summary()` | — |
| 2026-10-29 | 1.2 | `VicToolsListConfirmWindow` 新增**单项添加**能力：`Show()` 增加 `onAddSingle`（`Func<Object, bool>`）参数，传入后清单每行【定位】右侧显示【添加】按钮，可不等批量【确定】就把单个对象单独加入资源箱；新增 [§4.5 单项添加](#45-单项添加--添加按钮)；`ScenesTools` 配套新增公开 API `AddObjectToResourceBox()` | — |
| 2026-10-26 | 1.1 | 新增成员：`VicToolsListConfirmWindow`（通用处理列表确认窗口，非模态；`Show` / `Item`）；补充 [§4](#4-victoolslistconfirmwindow--通用处理列表确认窗口) 使用说明与资源箱批量添加场景的完整示例 | — |
| 2026-10-12 | 1.0 | 初始文档；当前成员：`VicToolsIconLoader`（`LoadPngIcon` / `Release` / `ReleaseAll`） | — |

> 后续追加新工具类时，请在表格上方插入新行（保持倒序），并在 [§2](#2-当前成员一览) / 对应章节同步更新。
