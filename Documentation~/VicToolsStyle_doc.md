# VicToolsStyle 样式系统 — 使用方法详解

> 文件：`Packages/com.youdoo.victools/Editor/VicTools/VicToolsStyle.cs`（590 行）
> 命名空间：`VicTools`
> 作用：为整个 VicTools 包提供统一的 IMGUI 样式、配色、富文本辅助、对话框等公共能力。

---

## 一、文件结构总览

| 区域 | 行号 | 类型 | 职责 |
|------|------|------|------|
| `EditorStyle` 类 | 8 – 453 | `public class`（含静态/实例混合） | 所有面板/Inspector 共用的 GUIStyle 单例 + 富文本样式 + 折叠头辅助 |
| `HeaderStyle` 类 | 464 – 588 | `public static class` | ShaderGUI 共享的"功能类型配色"常量与着色辅助、读档对话框 |

> **注意**：类名是 `EditorStyle`（不是 `VicToolsStyle`），文件名与类名不一致，但 `using VicTools;` 后可直接 `EditorStyle.xxx` 访问。

---

## 二、`EditorStyle` — 通用样式与辅助

### 2.1 获取单例

```csharp
using VicTools; // 1. 引用命名空间

public class MyWindow : EditorWindow
{
    private void OnGUI()
    {
        var style = EditorStyle.Get; // 2. 通过 Get 属性拿到单例(懒加载)
        GUILayout.Label("标题", style.heading);
    }
}
```

> ✅ `EditorStyle.Get` 是**唯一推荐入口**，内部走懒加载，无需手动 `new`。
> ⚠️ 不要在静态字段初始化器里访问 `Get`（此时 `EditorStyles` 可能尚未就绪），用属性或方法内访问。

---

### 2.2 区域 / 布局样式

#### `area` —— 通用带填充区域

```csharp
var style = EditorStyle.Get;
EditorGUILayout.BeginVertical(style.area);   // 上下左右各 10px 内边距
GUILayout.Label("内容1");
GUILayout.Label("内容2");
EditorGUILayout.EndVertical();
```

#### `areaPadded` —— 更大填充区域

```csharp
var style = EditorStyle.Get;
EditorGUILayout.BeginVertical(style.areaPadded);  // 20px 内边距
GUILayout.Label("需要更多呼吸感的区块");
EditorGUILayout.EndVertical();
```

> 💡 两者都开启了 `wordWrap = true`，长文本会自动换行。

#### `link` —— 蓝色超链接文本

```csharp
var style = EditorStyle.Get;
if (GUILayout.Button("打开文档", style.link))
    Application.OpenURL("https://example.com/docs");
```

> 自动适配 Pro Skin（深色下用青色，浅色下用深蓝）。

---

### 2.3 按钮样式

所有按钮都基于 `miniButton`（或 `toolbarButton`），行高统一 24px，文字大小 14-15px。

| 属性 | 颜色 | 用途 |
|------|------|------|
| `normalButton` | 白色 | 普通按钮基类 |
| `normalButton_Y` | 黄 | 警告 / 提示类操作 |
| `normalButton_R` | 红（加粗） | 危险操作（删除/重置） |
| `normalButton_G` | 绿 | 确认 / 通过类操作 |
| `normalButton_B` | 蓝 | 信息 / 跳转类操作 |
| `menuButton` | 灰色 | 工具栏按钮（顶部 Tab） |
| `menuButtonSelected` | 金色高亮 | 当前选中的工具栏按钮 |

```csharp
var style = EditorStyle.Get;
EditorGUILayout.BeginHorizontal();
if (GUILayout.Button("应用", style.normalButton_G))   Apply();
if (GUILayout.Button("取消", style.normalButton))     Cancel();
if (GUILayout.Button("删除", style.normalButton_R))   Delete(); // 红色警示
EditorGUILayout.EndHorizontal();
```

**工具栏 Tab 用法**（`menuButton` / `menuButtonSelected`）：

```csharp
var style = EditorStyle.Get;
EditorGUILayout.BeginHorizontal();
if (GUILayout.Button("批量复制", _currentTab == 0 ? style.menuButtonSelected : style.menuButton)) _currentTab = 0;
if (GUILayout.Button("批量重命名", _currentTab == 1 ? style.menuButtonSelected : style.menuButton)) _currentTab = 1;
EditorGUILayout.EndHorizontal();
```

> 🔧 `menuButtonSelected` 内部创建了 2x2 像素的纯色纹理作为背景，**首次访问时会自动生成**，之后缓存复用。编辑器重启后 `[InitializeOnLoadMethod]` 会重建实例，避免纹理失效。

#### `smallSquareButton`

```csharp
var style = EditorStyle.Get;
style.smallSquareButton = new GUIStyle(EditorStyles.miniButton) { fixedWidth = 22, fixedHeight = 22 };
if (GUILayout.Button("×", style.smallSquareButton)) Close();
```

> ⚠️ 与其他样式不同，`smallSquareButton` 是**公开字段而非属性**，需要使用者自行初始化。

---

### 2.4 标题 / 文字样式

所有标题都基于 `Heading`（22px Bold），子标题往下递减：

| 属性 | 字号 | 用途 |
|------|------|------|
| `heading` | 22px Bold | 工具窗口顶部主标题 |
| `subheading` | 18px Bold | 二级标题（折叠头首选） |
| `subheading2` | 14px Bold | 三级标题（小型分组） |
| `normalfont` | 14px | 正文 |
| `normalfont_Hui` | 14px 灰色 | 次要说明文字 |
| `normalfont_Hui_Wrap` | 14px 灰色 + 自动换行 | 长说明文字 |
| `normalfont_Hui_Cen` | 14px 灰色 + 居中 + 换行 | 居中说明 |
| `boldLabelNoStretch` | 14px Bold | 不撑开宽度的粗体（按钮内 label 首选） |

```csharp
var style = EditorStyle.Get;
EditorGUILayout.LabelField("★ 工具主标题", style.heading);        // 22px
EditorGUILayout.LabelField("通用设置", style.subheading);          // 18px
EditorGUILayout.LabelField("次要分组", style.subheading2);         // 14px
EditorGUILayout.LabelField("普通正文", style.normalfont);
EditorGUILayout.LabelField("提示:此操作不可撤销", style.normalfont_Hui);
GUILayout.Label("这是一段长说明文字,会自动换行,适合放在狭窄面板里。", style.normalfont_Hui_Wrap);
GUILayout.Label("居中显示的版本号", style.normalfont_Hui_Cen);
```

> 💡 `subheading` 配合 `DrawSubheadingFoldout(...)`（见 2.7）可避免 `EditorGUILayout.Foldout` 对 18px 字体底部裁切的问题。

---

### 2.5 Toggle 样式

```csharp
var style = EditorStyle.Get;
_showHelp = GUILayout.Toggle(_showHelp, "显示帮助", style.toggle);
```

> 特点：`stretchWidth = false`，不会撑满整行，**多个 Toggle 横向排列时不互相挤压**。

---

### 2.6 富文本样式

Unity 内置的 `boldLabel` / `foldoutHeader` / `miniBoldLabel` 默认 `richText = false`，无法渲染 `<color>` / `<b>` 标签。下表三个属性已开启 `richText`：

| 属性 | 等价基础 | 用途 |
|------|---------|------|
| `BoldLabelRichStyle` | `boldLabel` | 普通粗体富文本 |
| `MiniBoldLabelRichStyle` | `miniBoldLabel` | 小型粗体富文本 |
| `FoldoutHeaderRichStyle` | `foldoutHeader` | 折叠组标题富文本 |

```csharp
var style = EditorStyle.Get;
// 直接传入已带 <color> 的富文本字符串即可
GUILayout.Label("<color=#4FC3F7><b>基础属性</b></color>", style.BoldLabelRichStyle);
```

> 这三个样式主要配合 `HeaderStyle.Rich(...)` / `HeaderStyle.ColoredHeader(...)`（见第三节）使用，**单独自用也可以**。

---

### 2.7 折叠头辅助（核心方法）

#### `DrawSubheadingFoldout(...)` —— 仿 ProjectTools 风格的可点击标题

**问题**：`EditorGUILayout.Foldout(_, _, _, style.subheading)` 因为 Foldout 内部行高固定 ≈16px，传入 18px 字体时**底部会被裁切**。

**方案**：用 `Label` 渲染标题（行高跟字体走），再叠一个 `GUIStyle.none` 透明按钮响应点击。

```csharp
var style = EditorStyle.Get;
_foldoutGeneral = true;  // 状态字段

// 调用方式 1:返回值即新状态,可链式 if
if (EditorStyle.DrawSubheadingFoldout(ref _foldoutGeneral, "通用设置", style.subheading))
{
    DrawGeneralSection();  // 展开时绘制内容
}

EditorGUILayout.Space();

// 调用方式 2:传 subheading2 用更小字号
_foldoutAdvanced = EditorStyle.DrawSubheadingFoldout(ref _foldoutAdvanced, "高级选项", style.subheading2);

// 调用方式 3:关闭悬停高亮
_foldoutSilent = EditorStyle.DrawSubheadingFoldout(ref _foldoutSilent, "静默折叠", style.subheading, hoverAlpha: 0f);
```

**方法签名**：

```csharp
public static bool DrawSubheadingFoldout(
    ref bool state,        // 折叠开关,ref 引用会被翻转
    string text,           // 标题文本(函数内部自动加 ▼ / ▶ 三角)
    GUIStyle labelStyle,   // 标签样式,通常传 subheading 或 subheading2
    float hoverAlpha = 0.06f  // 悬停高亮强度(0=禁用,1=纯白),默认 0.06
)
```

**关键特性**：
- ✅ `state` 为 `ref` 参数，调用后会自动翻转，原地修改
- ✅ 返回值就是新 `state`，可 `if (DrawSubheadingFoldout(...)) { ... }` 链式调用
- ✅ 文本前自动添加 `▼`（展开）/ `▶`（折叠）三角符
- ✅ 鼠标悬停时整行轻微高亮（`alpha = 0.06`）
- ✅ `labelStyle` 为 null 时自动 fallback 到 `EditorStyles.label`，不会 NRE
- ✅ 完美替代 `EditorGUILayout.Foldout(_, _, _, labelStyle)`，**大字体不再被裁切**

> 🔧 此方法为 `static`，无需访问 `EditorStyle.Get` 实例，可直接 `EditorStyle.DrawSubheadingFoldout(...)` 调用。

---

### 2.8 私有方法（通常不需要直接调用）

| 方法 | 签名 | 说明 |
|------|------|------|
| `MakeTex` | `private Texture2D MakeTex(int w, int h, Color c)` | 创建纯色纹理（基础 API） |
| `CreatePersistentTexture` | `private Texture2D CreatePersistentTexture(Color c, int w = 2, int h = 2)` | 在 `MakeTex` 基础上设 `HideFlags.HideAndDontSave`，避免编辑器断言 |

> 仅 `MenuButtonSelected` 内部使用，**业务代码不应直接调用**。

---

## 三、`HeaderStyle` — ShaderGUI 共享配色与辅助

### 3.1 功能类型配色常量

按"语义类型"分类，所有 ShaderGUI 脚本共用一套：

| 常量 | 颜色 (RGB) | 用途 |
|------|------------|------|
| `HeaderTitle` | (0.70, 0.90, 1.00) 浅青 | 顶部全局标题（带存档/读档按钮行） |
| `Base` | (0.31, 0.76, 0.97) 蓝 | 基础属性 / 主色 / 贴图 / 法线 / UV |
| `Lighting` | (1.00, 0.83, 0.31) 金 | 光照 / 高光 / 阴影 / 明暗 / PBR 参数 |
| `Sparkle` | (0.30, 0.82, 0.88) 青 | 闪烁 / 特效 / 动画 / 扰动 |
| `Fresnel` | (1.00, 0.54, 0.40) 橙 | 边缘光 / Rim / Kajiya-Kay |
| `Interaction` | (0.73, 0.41, 0.78) 紫 | 交互 / 物理 / 压痕 / 触摸 / 风力 |
| `Render` | (0.69, 0.74, 0.27) 黄绿 | 渲染设置 / 输出 / 剔除 |

```csharp
using VicTools;

// 1. 着色字符串(纯文本)
string title = HeaderStyle.Rich("基础属性", HeaderStyle.Base);
// → "<color=#4FC2F8><b>基础属性</b></color>"

GUILayout.Label(title, EditorStyle.Get.BoldLabelRichStyle);
```

---

### 3.2 字符串生成辅助

#### `Rich(text, color)` —— 纯字符串（给 `GUILayout.Label` / `EditorGUILayout.LabelField` 用）

```csharp
var style = EditorStyle.Get;
GUILayout.Label(HeaderStyle.Rich("光照参数", HeaderStyle.Lighting), style.BoldLabelRichStyle);
```

#### `ColoredHeader(text, color)` —— `GUIContent`（给 `EditorGUILayout.BeginFoldoutHeaderGroup` 用）

```csharp
var style = EditorStyle.Get;
_foldoutLight = EditorGUILayout.BeginFoldoutHeaderGroup(
    _foldoutLight,
    HeaderStyle.ColoredHeader("光照参数", HeaderStyle.Lighting),
    style.FoldoutHeaderRichStyle);

if (_foldoutLight) {
    // ... 光照相关属性
}
EditorGUILayout.EndFoldoutHeaderGroup();
```

> 两者底层都生成 `<color=#XXXXXX><b>text</b></color>` 字符串，区别仅是返回类型。

---

### 3.3 控件着色辅助

> **原理**：`MaterialEditor.ShaderProperty` / `EditorGUILayout.Toggle` 内部用 `EditorStyles.label` 画 label，临时开启 `richText` 后用 `Rich(...)` 包裹文字即可上色，绘制完立即还原。**不会污染全局样式**。

#### `ShaderProperty(editor, prop, label, color)` —— 给 Shader 属性行上色

```csharp
public class MyShaderGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor editor, MaterialProperty[] props)
    {
        var mainTex = FindProperty("_BaseMap", props);
        var color   = FindProperty("_Color",   props);

        // 主色属性用 Base 蓝色
        HeaderStyle.ShaderProperty(editor, color, "主色", HeaderStyle.Base);

        // 发光属性用 Lighting 金色
        var emission = FindProperty("_EmissionColor", props);
        HeaderStyle.ShaderProperty(editor, emission, "自发光", HeaderStyle.Lighting);
    }
}
```

#### `Toggle(label, value, color, options)` —— 给 Toggle 控件上色

```csharp
bool _showFresnel = true;
_showFresnel = HeaderStyle.Toggle("启用边缘光", _showFresnel, HeaderStyle.Fresnel);
```

> 与 `EditorGUILayout.Toggle` 签名完全兼容，**可直接替换**。

---

### 3.4 主纹理属性判定

#### `MainTexturePropertyNames` —— 共享主纹理名集合

```csharp
using System.Linq;
// 公开常量,内置 _BaseMap (URP) 和 _MainTex (内置/Standard)
HashSet<string> mains = HeaderStyle.MainTexturePropertyNames;  // { "_BaseMap", "_MainTex" }
```

#### `IsMainTexture(propertyName)` —— 判断是否主纹理

```csharp
// 在"读档"循环里跳过主纹理,保留当前值
foreach (var prop in sourceProps)
{
    if (HeaderStyle.IsMainTexture(prop.name)) continue;  // 跳过 _BaseMap / _MainTex
    destMat.SetColor(prop.name, prop.colorValue);
}
```

---

### 3.5 读档对话框（ShaderGUI 共用）

#### `LoadTextureChoice` —— 弹窗结果结构

```csharp
public struct LoadTextureChoice
{
    public bool LoadTextures;  // true=读所有纹理, false=跳过所有纹理只读数值
    public bool LoadMainTex;   // true=主纹理也读, false=保留当前主纹理(仅 LoadTextures=true 时有效)
}
```

#### `ShowLoadTextureDialog(...)` —— 弹出三选项对话框

```csharp
// 默认参数版本:标题"读取纹理",3 个按钮"全部读取"/"仅参数"/"排除主纹理"
var choice = HeaderStyle.ShowLoadTextureDialog();

if (choice.LoadTextures)            // 全部读取 OR 排除主纹理
{
    if (choice.LoadMainTex) { /* 同步 _BaseMap / _MainTex 贴图引用与 tiling */ }
    else                     { /* 只同步非主纹理的贴图引用与 tiling */ }
    // 同步其他数值参数(color / float / vector)
}
else                                  // 仅参数
{
    // 只读数值参数,所有贴图保持当前引用
}
```

**自定义文案版本**：

```csharp
var choice = HeaderStyle.ShowLoadTextureDialog(
    title: "应用预设 - 贴图处理",
    message: "检测到预设里包含贴图,如何处理?\n\n• 全部应用\n• 仅参数\n• 排除主贴图",
    ok: "全部应用",
    cancel: "仅参数",
    alt: "排除主贴图");
```

> 返回值与 `DisplayDialogComplex` 按钮位置一一对应：
> - 按钮 0（OK / "全部读取"）→ `LoadTextureChoice(true, true)`
> - 按钮 1（Cancel / "仅参数"）→ `LoadTextureChoice(false, false)`
> - 按钮 2（Alt / "排除主纹理"）→ `LoadTextureChoice(true, false)`

---

## 四、综合示例 —— 完整面板骨架

```csharp
using UnityEngine;
using UnityEditor;
using VicTools;

public class MyToolWindow : EditorWindow
{
    private Vector2 _scroll;
    private int _currentTab;
    private bool _foldoutBasic = true;
    private bool _foldoutAdvanced;
    private bool _enableFresnel = true;
    private Color _tint = Color.white;

    [MenuItem("Tools/My Tool")]
    public static void Open() => GetWindow<MyToolWindow>("My Tool");

    private void OnGUI()
    {
        var style = EditorStyle.Get;
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        EditorGUILayout.BeginVertical(style.areaPadded);

        // ── 主标题(22px) ──
        EditorGUILayout.LabelField("★ 我的工具", style.heading);

        // ── 工具栏 Tab ──
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("基础", _currentTab == 0 ? style.menuButtonSelected : style.menuButton)) _currentTab = 0;
        if (GUILayout.Button("高级", _currentTab == 1 ? style.menuButtonSelected : style.menuButton)) _currentTab = 1;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // ── 折叠头(18px, 大字体不被裁切) ──
        EditorStyle.DrawSubheadingFoldout(ref _foldoutBasic, "基础设置", style.subheading);
        if (_foldoutBasic)
        {
            _tint = EditorGUILayout.ColorField("主色", _tint);
            _enableFresnel = HeaderStyle.Toggle("启用边缘光", _enableFresnel, HeaderStyle.Fresnel);
        }

        EditorGUILayout.Space();

        EditorStyle.DrawSubheadingFoldout(ref _foldoutAdvanced, "高级选项", style.subheading2);
        if (_foldoutAdvanced)
        {
            GUILayout.Label("这是一段长说明,会按面板宽度自动换行。", style.normalfont_Hui_Wrap);
        }

        EditorGUILayout.Space();

        // ── 操作按钮 ──
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("应用",   style.normalButton_G)) Apply();
        if (GUILayout.Button("取消",   style.normalButton))   Cancel();
        if (GUILayout.Button("重置",   style.normalButton_R)) Reset();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
    }

    private void Apply()  { /* ... */ }
    private void Cancel() { /* ... */ }
    private void Reset()  { /* ... */ }
}
```

---

## 五、常见问题 & 注意事项

### 5.1 字体被裁切？

**症状**：`EditorGUILayout.Foldout(_, "标题", true, style.subheading)` 文字底部缺失。
**解决**：改用 `EditorStyle.DrawSubheadingFoldout(ref state, "标题", style.subheading)`。

### 5.2 富文本不生效？

**症状**：`GUILayout.Label("<color=red>...</color>", style.boldLabel)` 文本原样显示。
**解决**：把 `boldLabel` 换成 `BoldLabelRichStyle`（已开启 `richText`）。

### 5.3 菜单按钮高亮失效？

**症状**：编辑器重启后 `menuButtonSelected` 背景变透明。
**原因**：静态纹理被 Unity GC。
**解决**：无需手动处理，文件内 `[InitializeOnLoadMethod] InitializeStyles()` 会在编辑器启动/重编译时**自动重建**所有样式实例。

### 5.4 `EditorStyle.Get` 返回 null？

**不要**在类的静态字段初始化阶段访问 `Get`：

```csharp
// ❌ 错误:此时 EditorStyles 可能尚未初始化
private static GUIStyle _myStyle = EditorStyle.Get.subheading;

// ✅ 正确:在 OnGUI 内懒加载,或用属性封装
private static GUIStyle _myStyle;
public static GUIStyle MyStyle => _myStyle ?? (_myStyle = EditorStyle.Get.subheading);
```

### 5.5 同一类文件里有两个类？

`VicToolsStyle.cs` 包含 `EditorStyle`（line 8）和 `HeaderStyle`（line 464）。
- `EditorStyle` 既有实例成员（`Get` 单例）也有静态成员（`DrawSubheadingFoldout`）
- `HeaderStyle` **完全 static**，所有方法都直接 `HeaderStyle.Rich(...)` 调用

---

## 六、API 速查表

| 类别 | 名称 | 类型 | 一句话说明 |
|------|------|------|-----------|
| **单例** | `EditorStyle.Get` | `static` | 获取唯一实例 |
| **区域** | `area` / `areaPadded` | `instance` | 10px / 20px 内边距垂直区 |
| **超链接** | `link` | `instance` | 蓝色（Pro Skin 自适应） |
| **按钮** | `normalButton` / `_Y` / `_R` / `_G` / `_B` | `instance` | 白/黄/红/绿/蓝 |
| **按钮** | `menuButton` / `menuButtonSelected` | `instance` | 工具栏 Tab / 选中态 |
| **按钮** | `smallSquareButton` | `instance` | 22×22 小方块按钮（需自初始化） |
| **标题** | `heading` / `subheading` / `subheading2` | `instance` | 22 / 18 / 14px 粗体 |
| **文字** | `normalfont` / `_Hui` / `_Hui_Wrap` / `_Hui_Cen` | `instance` | 14px 普通/灰/灰+换行/灰+居中 |
| **文字** | `boldLabelNoStretch` | `instance` | 不撑宽的粗体 |
| **Toggle** | `toggle` | `instance` | 不撑宽 Toggle |
| **富文本** | `BoldLabelRichStyle` / `MiniBoldLabelRichStyle` / `FoldoutHeaderRichStyle` | `instance` | 三种开启 richText 的内置样式变体 |
| **静态方法** | `DrawSubheadingFoldout(ref, string, GUIStyle, float)` | `static` | 仿 ProjectTools 风格的可点击标题 |
| **配色** | `HeaderTitle` / `Base` / `Lighting` / `Sparkle` / `Fresnel` / `Interaction` / `Render` | `static` | 7 种功能类型配色 |
| **字符串** | `HeaderStyle.Rich(text, color)` | `static` | 生成 `<color><b>...</b></color>` 字符串 |
| **GUIContent** | `HeaderStyle.ColoredHeader(text, color)` | `static` | 同上,返回 GUIContent |
| **着色** | `HeaderStyle.ShaderProperty(editor, prop, label, color)` | `static` | 给 ShaderProperty 整行着色 |
| **着色** | `HeaderStyle.Toggle(label, value, color, options)` | `static` | 给 Toggle label 着色 |
| **主纹理** | `HeaderStyle.MainTexturePropertyNames` | `static` | `{ "_BaseMap", "_MainTex" }` 集合 |
| **主纹理** | `HeaderStyle.IsMainTexture(name)` | `static` | 判断是否主纹理属性 |
| **对话框** | `HeaderStyle.ShowLoadTextureDialog(...)` | `static` | 弹出"读档 - 是否读纹理"三选项框 |
| **结构** | `HeaderStyle.LoadTextureChoice` | `static struct` | 对话框返回值(LoadTextures, LoadMainTex) |

---

> 维护者：VicTools
> 最近更新：2026-10-10
