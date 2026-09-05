# UI Toolkit 落地设计文档

## 1. 目标

- 建立 UXML 规范约束，统一 UI 开发标准
- 实现 UXML → C# 绑定代码自动生成，消除手写 `Q<T>("name")` 查询
- 提供类型安全的 UI 元素引用，编译期发现命名错误
- 降低 AI 辅助开发门槛：AI 只需编写符合规范的 UXML，绑定代码自动生成

## 2. 目录结构

```
Assets/
├── UI/
│   ├── Documents/          # UXML 文件
│   │   ├── LoginView.uxml
│   │   └── SettingsView.uxml
│   ├── Styles/             # USS 样式文件
│   │   ├── Common.uss
│   │   └── LoginView.uss
│   └── Gen/                # 自动生成的绑定代码（勿手动修改）
│       ├── LoginView.Gen.cs
│       └── SettingsView.Gen.cs
├── Scripts/
│   └── UI/
│       ├── Base/
│       │   └── ViewBase.cs       # View 基类
│       ├── Views/
│       │   ├── LoginView.cs      # 业务逻辑（手写）
│       │   └── SettingsView.cs
│       └── UIManager.cs
└── Editor/
    └── UIToolkit/
        └── UxmlBindingGenerator.cs  # 代码生成器
```

## 3. 绑定生成判定规则

代码生成器遍历 UXML 所有节点时，依据以下规则决定是否为某个节点生成 C# 绑定代码：

### 3.1 核心判定：有 `name` 属性 = 生成绑定

```xml
<!-- 有 name → 生成绑定 -->
<ui:Button name="LoginBtn" text="登录" />        ✅ 生成 Button LoginBtn

<!-- 无 name → 不生成，纯布局/装饰用途 -->
<ui:VisualElement class="spacer" />               ❌ 跳过
<ui:Label text="版权所有" class="footer-text" />  ❌ 跳过
```

**唯一判定条件：元素是否设置了 `name` 属性。** 没有 `name` 的节点一律跳过，不会出现在生成代码中。

### 3.2 判定流程

```
遍历 UXML 所有节点
    │
    ▼
该节点有 name 属性？ ─── 否 ──→ 跳过，不生成
    │
    是
    ▼
name 以 _ 开头？ ─── 是 ──→ 生成可空绑定（Optional）
    │
    否
    ▼
生成强制绑定（Required）
```

### 3.3 三种节点分类

| 分类 | UXML 写法 | 生成结果 | 适用场景 |
|------|----------|---------|---------|
| **不绑定** | 不写 `name` | 不生成任何代码 | 纯布局容器、装饰性文本、间距占位 |
| **强制绑定** | `name="LoginBtn"` | `protected Button LoginBtn { get; private set; }` | 需要在代码中操作的交互元素 |
| **可选绑定** | `name="_DebugLabel"` | `protected Label DebugLabel { get; private set; }` (可为 null) | 可能动态添加/移除的调试元素 |

### 3.4 实际示例对照

```xml
<ui:VisualElement name="LoginView" class="view-root">

    <!-- 纯布局容器，不需要代码引用 → 不写 name → 不生成 -->
    <ui:VisualElement class="header">
        <!-- 需要代码修改文本 → 写 name → 生成 -->
        <ui:Label name="TitleLabel" text="登录" />
        <!-- 纯装饰图标 → 不写 name → 不生成 -->
        <ui:VisualElement class="icon-decoration" />
    </ui:VisualElement>

    <ui:VisualElement class="form">
        <!-- 需要读取输入值 → 写 name → 生成 -->
        <ui:TextField name="UsernameInput" label="用户名" />
        <ui:TextField name="PasswordInput" label="密码" />
    </ui:VisualElement>

    <!-- 需要注册点击事件 → 写 name → 生成 -->
    <ui:Button name="LoginBtn" text="登录" />

    <!-- 纯展示说明，不需要代码操作 → 不写 name → 不生成 -->
    <ui:Label text="忘记密码？" class="hint-text" />

    <!-- 调试用，可能被移除 → 以 _ 开头 → 生成可空绑定 -->
    <ui:Label name="_FpsLabel" />

</ui:VisualElement>
```

以上 UXML 生成的绑定代码：

```csharp
// 只有 5 个带 name 的节点生成了绑定，其余全部跳过
protected VisualElement LoginViewRoot { get; private set; }  // name="LoginView"
protected Label TitleLabel { get; private set; }             // name="TitleLabel"
protected TextField UsernameInput { get; private set; }      // name="UsernameInput"
protected TextField PasswordInput { get; private set; }      // name="PasswordInput"
protected Button LoginBtn { get; private set; }              // name="LoginBtn"
protected Label FpsLabel { get; private set; }               // name="_FpsLabel" (可空)
```

### 3.5 命名即契约

`name` 属性在此方案中承担双重职责：

1. **UI Toolkit 运行时查询键**：`root.Q<Button>("LoginBtn")` 依赖此值定位元素
2. **代码生成的触发标记**：有 `name` = 需要代码引用 = 生成绑定

这意味着 **`name` 的取舍就是开发者声明"这个元素是否需要在代码中操作"的方式**。不需要操作的元素保持匿名，UXML 更干净，生成代码更精简。

## 4. UXML 规范约束

### 4.1 文件命名

| 类型 | 命名规则 | 示例 |
|------|---------|------|
| View（全屏页面） | `{Name}View.uxml` | `LoginView.uxml` |
| Panel（弹窗/面板） | `{Name}Panel.uxml` | `ConfirmPanel.uxml` |
| Widget（可复用组件） | `{Name}Widget.uxml` | `ItemSlotWidget.uxml` |
| USS 样式 | 与 UXML 同名或 `Common.uss` | `LoginView.uss` |

### 4.2 元素命名规则

所有需要在代码中引用的 VisualElement **必须** 设置 `name` 属性，命名使用 **大驼峰 + 类型后缀**：

| 元素类型 | 后缀 | 示例 |
|---------|------|------|
| `VisualElement`（容器） | `Container` / `Root` / `Group` | `ContentContainer` |
| `Label` | `Label` | `TitleLabel` |
| `Button` | `Btn` | `LoginBtn` |
| `TextField` | `Input` | `UsernameInput` |
| `Toggle` | `Toggle` | `RememberToggle` |
| `Slider` | `Slider` | `VolumeSlider` |
| `DropdownField` | `Dropdown` | `LanguageDropdown` |
| `ScrollView` | `ScrollView` | `ListScrollView` |
| `ListView` | `ListView` | `ItemListView` |
| `ProgressBar` | `ProgressBar` | `LoadingProgressBar` |
| `Image` / `VisualElement`（图片） | `Image` | `AvatarImage` |
| `Foldout` | `Foldout` | `AdvancedFoldout` |
| `GroupBox` | `Group` | `OptionsGroup` |
| 自定义元素 | 自定义 | 按实际语义命名 |

### 4.3 命名约束规则

1. **必须使用大驼峰**：`LoginBtn`，不允许 `login_btn`、`loginBtn`、`login-btn`
2. **必须包含类型后缀**：后缀用于代码生成器推断 C# 类型
3. **禁止重名**：同一 UXML 文件内 `name` 不得重复
4. **可选元素加 `_` 前缀**：`_DebugLabel` 表示该元素可能不存在，生成可空引用
5. **纯布局容器不命名**：不需要代码引用的纯布局元素不设置 `name`

### 4.4 UXML 模板约束

每个 UXML 根节点必须包含以下属性：

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements"
         xmlns:uie="UnityEditor.UIElements">
    <!-- 根容器必须命名，与文件名一致（去掉后缀） -->
    <ui:VisualElement name="LoginView" class="view-root">
        <!-- 内容 -->
    </ui:VisualElement>
</ui:UXML>
```

### 4.5 UXML 示例

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement name="LoginView" class="view-root">

        <ui:VisualElement name="HeaderContainer" class="header">
            <ui:Label name="TitleLabel" text="登录" />
        </ui:VisualElement>

        <ui:VisualElement name="FormContainer" class="form">
            <ui:TextField name="UsernameInput" label="用户名" />
            <ui:TextField name="PasswordInput" label="密码" password="true" />
            <ui:Toggle name="RememberToggle" label="记住密码" />
        </ui:VisualElement>

        <ui:VisualElement name="ActionContainer" class="actions">
            <ui:Button name="LoginBtn" text="登录" class="btn-primary" />
            <ui:Button name="RegisterBtn" text="注册" class="btn-secondary" />
        </ui:VisualElement>

        <ui:Label name="ErrorLabel" class="error-text" />
        <ui:VisualElement name="LoadingContainer" class="loading hidden" />

    </ui:VisualElement>
</ui:UXML>
```

## 5. USS 样式规范

### 5.1 文件组织

| 文件类型 | 命名规则 | 作用域 | 示例 |
|---------|---------|--------|------|
| 全局样式 | `Common.uss` | 所有 View 共享的基础样式 | 颜色变量、字体、通用按钮样式 |
| 主题变量 | `Theme.uss` | CSS 自定义属性（变量）集中定义 | `--color-primary`, `--font-size-body` |
| View 样式 | 与 UXML 同名 | 仅在对应 View 内生效 | `LoginView.uss` |
| Widget 样式 | 与 Widget 同名 | 仅在对应 Widget 内生效 | `ItemSlotWidget.uss` |

USS 文件在 UXML 中通过 `<Style>` 标签引入：

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <Style src="../../Styles/Common.uss" />
    <Style src="../../Styles/LoginView.uss" />
    <ui:VisualElement name="LoginView" class="view-root">
        ...
    </ui:VisualElement>
</ui:UXML>
```

### 5.2 选择器命名规范

采用 **组件名-元素-状态** 的层级命名法（类似 BEM），全部使用 **小写短横线连接**：

```
.{component}-{element}--{state}
```

| 层级 | 说明 | 示例 |
|------|------|------|
| 组件名 | View/Widget 名称（小写短横线） | `.login-view` |
| 元素 | 组件内的子部件 | `.login-view-header` |
| 状态 | 交互或业务状态修饰 | `.login-view-btn--disabled` |

#### 具体规则

```css
/* ✅ 正确：组件级根样式 */
.login-view { }

/* ✅ 正确：子元素样式 */
.login-view-header { }
.login-view-form { }

/* ✅ 正确：状态修饰 */
.login-view-btn--primary { }
.login-view-btn--disabled { }
.login-view-input--error { }

/* ✅ 正确：通用/原子样式 */
.text-center { }
.hidden { }
.flex-row { }

/* ❌ 错误：大驼峰（大驼峰仅用于 name 属性，不用于 class） */
.LoginViewHeader { }

/* ❌ 错误：下划线连接 */
.login_view_header { }
```

### 5.3 USS 变量规范

所有可配置的视觉属性统一提取为 USS 变量，集中在 `Theme.uss` 中定义：

```css
/* Theme.uss — 主题变量 */
:root {
    /* 颜色 */
    --color-primary: #4A90D9;
    --color-primary-hover: #5BA0E9;
    --color-secondary: #6C757D;
    --color-danger: #DC3545;
    --color-success: #28A745;
    --color-bg: #1E1E2E;
    --color-bg-secondary: #2A2A3E;
    --color-text: #FFFFFF;
    --color-text-secondary: #AAAAAA;
    --color-border: #3A3A4E;

    /* 字号 */
    --font-size-h1: 28px;
    --font-size-h2: 22px;
    --font-size-body: 16px;
    --font-size-caption: 12px;

    /* 间距 */
    --spacing-xs: 4px;
    --spacing-sm: 8px;
    --spacing-md: 16px;
    --spacing-lg: 24px;
    --spacing-xl: 32px;

    /* 圆角 */
    --radius-sm: 4px;
    --radius-md: 8px;
    --radius-lg: 16px;
    --radius-full: 9999px;
}
```

业务 USS 中通过 `var()` 引用，禁止直接写硬编码值：

```css
/* LoginView.uss */
.login-view-btn--primary {
    background-color: var(--color-primary);      /* ✅ 引用变量 */
    border-radius: var(--radius-md);
    font-size: var(--font-size-body);
}

.login-view-btn--primary:hover {
    background-color: var(--color-primary-hover); /* ✅ hover 状态变量 */
}

.login-view-error {
    color: #DC3545;                               /* ❌ 禁止硬编码颜色 */
    color: var(--color-danger);                    /* ✅ 正确写法 */
}
```

### 5.4 常用样式模式

```css
/* 隐藏元素 */
.hidden {
    display: none;
}

/* Flex 布局工具类 */
.flex-row {
    flex-direction: row;
}

.flex-column {
    flex-direction: column;
}

.flex-center {
    align-items: center;
    justify-content: center;
}

.flex-grow {
    flex-grow: 1;
}

/* View 根容器基础样式 */
.view-root {
    flex-grow: 1;
    background-color: var(--color-bg);
}
```

### 5.5 USS 选择器优先级

UI Toolkit 的 USS 选择器优先级从高到低：

| 优先级 | 选择器类型 | 示例 | 说明 |
|--------|-----------|------|------|
| 最高 | C# 内联样式 | `element.style.color = Color.red;` | 代码直接设置，覆盖一切 USS |
| 高 | `#name` 选择器 | `#LoginBtn { }` | 按 `name` 属性匹配 |
| 中 | `.class` 选择器 | `.btn-primary { }` | 按 `class` 属性匹配 |
| 低 | 类型选择器 | `Button { }` | 按元素类型匹配 |
| 最低 | 通配符 | `* { }` | 匹配所有元素 |

**同优先级时**：后加载的 USS 文件覆盖先加载的，同文件中后出现的规则覆盖先出现的。

**多个 class 时**：选择器匹配的 class 数量越多，优先级越高：

```css
.btn { color: white; }                  /* 1 个 class → 较低 */
.btn.btn-primary { color: blue; }       /* 2 个 class → 较高，生效 */
```

## 6. 样式调试与排查

UX / UI 开发者排查样式问题时，需要快速定位"某个节点上生效了哪些样式、来自哪里、为什么被覆盖"。以下是完整的排查工具链和方法。

### 6.1 UI Toolkit Debugger（首选工具）

Unity 内置的可视化调试器，功能等同于浏览器 DevTools：

**打开方式**：`Window → UI Toolkit → Debugger`

**核心功能**：

| 功能 | 操作 | 作用 |
|------|------|------|
| **Pick Element** | 点击左上角吸管图标，然后点击界面上的元素 | 直接选中要检查的节点 |
| **Hierarchy 面板** | 左侧树形结构 | 查看完整的 VisualElement 层级 |
| **Styles 面板** | 右侧样式列表 | 查看节点上所有生效的样式属性 |
| **Layout 面板** | 右侧布局信息 | 查看 margin/border/padding/content 盒模型 |
| **Matching Selectors** | Styles 面板顶部 | 显示所有匹配该节点的 USS 选择器及来源文件 |

**排查步骤**：

```
1. 打开 Debugger → 点击 Pick Element → 点击有问题的 UI 元素
                          │
                          ▼
2. 查看 Matching Selectors 区域
   → 列出所有匹配的选择器，按优先级排序
   → 被覆盖的属性显示删除线
   → 每条规则右侧显示来源 USS 文件名和行号
                          │
                          ▼
3. 找到目标属性
   → 如果属性值不符合预期：检查是否被更高优先级的选择器覆盖
   → 如果属性不存在：检查选择器是否正确匹配（class 拼写、层级关系）
   → 如果显示 "inline"：说明是 C# 代码直接设置的，优先级最高
```

### 6.2 快速定位节点的方法

| 方法 | 适用场景 | 操作 |
|------|---------|------|
| **Pick Element（吸管工具）** | 能看到元素、不知道在树中的位置 | Debugger 左上角吸管 → 点击元素 |
| **按 name 搜索** | 知道元素的 `name` | Debugger 搜索框输入 name 值 |
| **按 class 搜索** | 知道元素的 class | Debugger 搜索框输入 `.class-name` |
| **Hierarchy 展开** | 排查层级嵌套问题 | 在 Debugger 左侧手动展开树 |

### 6.3 常见问题排查表

| 现象 | 可能原因 | 排查方法 |
|------|---------|---------|
| 样式完全不生效 | USS 文件未被引入 | 检查 UXML 中是否有 `<Style src="...">` 引用该 USS |
| 样式被覆盖 | 更高优先级的选择器存在 | Debugger → Matching Selectors 查看优先级排序 |
| 颜色/字号不对 | 变量值被覆盖或拼写错误 | 检查 `Theme.uss` 中变量定义，Debugger 中查看 computed value |
| 元素不可见 | `display: none` 或 `visibility: hidden` 或 `opacity: 0` | Debugger → Styles → 检查 display/visibility/opacity |
| 布局位置不对 | Flex 属性设置错误 | Debugger → Layout 面板查看盒模型数值 |
| hover 不生效 | `:hover` 伪类选择器优先级不够 | 确认没有更高优先级的选择器覆盖 hover 状态 |
| 样式只在某平台不生效 | C# 代码运行时覆盖了样式 | 搜索代码中 `.style.xxx =` 的赋值，inline 样式优先级最高 |

### 6.4 C# 运行时样式调试

当 USS 排查不出问题时，可能是 C# 代码在运行时修改了样式。使用以下代码查看节点的实际计算样式：

```csharp
#if UNITY_EDITOR
[ContextMenu("Dump Style Info")]
private void DumpStyleInfo()
{
    var element = LoginBtn; // 替换为需要检查的元素
    Debug.Log($"=== Style Debug: {element.name} ===");
    Debug.Log($"  display: {element.resolvedStyle.display}");
    Debug.Log($"  visibility: {element.resolvedStyle.visibility}");
    Debug.Log($"  opacity: {element.resolvedStyle.opacity}");
    Debug.Log($"  color: {element.resolvedStyle.color}");
    Debug.Log($"  backgroundColor: {element.resolvedStyle.backgroundColor}");
    Debug.Log($"  fontSize: {element.resolvedStyle.fontSize}");
    Debug.Log($"  width: {element.resolvedStyle.width}");
    Debug.Log($"  height: {element.resolvedStyle.height}");
    Debug.Log($"  classes: {string.Join(", ", element.GetClasses())}");
    Debug.Log($"  inline style count: {element.style.color.keyword}");
}
#endif
```

### 6.5 样式来源标识速查

在 Debugger 的 Styles 面板中，每条属性右侧都会标注来源：

| 来源标识 | 含义 | 修改方式 |
|---------|------|---------|
| `Common.uss:42` | 来自 USS 文件第 42 行 | 修改对应 USS 文件 |
| `LoginView.uss:15` | 来自 View 专属 USS | 修改对应 USS 文件 |
| `inline` | C# 代码 `element.style.xxx = ...` 设置 | 搜索代码中对该元素的 `.style` 赋值 |
| `inherited` | 从父节点继承（如 color、font-size） | 检查父节点的样式 |
| `initial` | USS 未设置，使用默认值 | 在 USS 中为该选择器添加属性 |

## 7. 自动生成绑定代码

### 7.1 生成规则

代码生成器读取 UXML 文件，为每个带 `name` 属性的元素生成强类型引用：

| UXML 元素 | 生成的 C# 类型 |
|-----------|---------------|
| `<ui:VisualElement name="X">` | `VisualElement X` |
| `<ui:Label name="X">` | `Label X` |
| `<ui:Button name="X">` | `Button X` |
| `<ui:TextField name="X">` | `TextField X` |
| `<ui:Toggle name="X">` | `Toggle X` |
| `<ui:Slider name="X">` | `Slider X` |
| `<ui:SliderInt name="X">` | `SliderInt X` |
| `<ui:DropdownField name="X">` | `DropdownField X` |
| `<ui:ScrollView name="X">` | `ScrollView X` |
| `<ui:ListView name="X">` | `ListView X` |
| `<ui:ProgressBar name="X">` | `ProgressBar X` |
| `<ui:Foldout name="X">` | `Foldout X` |
| `<ui:GroupBox name="X">` | `GroupBox X` |
| `<ui:MinMaxSlider name="X">` | `MinMaxSlider X` |
| `<ui:RadioButton name="X">` | `RadioButton X` |
| `<ui:RadioButtonGroup name="X">` | `RadioButtonGroup X` |
| 其他 / 未识别 | `VisualElement X` |

### 7.2 生成的代码示例

输入 `LoginView.uxml`（上文示例），生成 `LoginView.Gen.cs`：

```csharp
// <auto-generated>
// 由 UxmlBindingGenerator 自动生成，请勿手动修改
// Source: Assets/UI/Documents/LoginView.uxml
// </auto-generated>

using UnityEngine.UIElements;

public partial class LoginView
{
    #region Auto-Generated Bindings

    protected VisualElement LoginViewRoot { get; private set; }
    protected VisualElement HeaderContainer { get; private set; }
    protected Label TitleLabel { get; private set; }
    protected VisualElement FormContainer { get; private set; }
    protected TextField UsernameInput { get; private set; }
    protected TextField PasswordInput { get; private set; }
    protected Toggle RememberToggle { get; private set; }
    protected VisualElement ActionContainer { get; private set; }
    protected Button LoginBtn { get; private set; }
    protected Button RegisterBtn { get; private set; }
    protected Label ErrorLabel { get; private set; }
    protected VisualElement LoadingContainer { get; private set; }

    protected void AutoBind(VisualElement root)
    {
        LoginViewRoot = root.Q<VisualElement>("LoginView");
        HeaderContainer = root.Q<VisualElement>("HeaderContainer");
        TitleLabel = root.Q<Label>("TitleLabel");
        FormContainer = root.Q<VisualElement>("FormContainer");
        UsernameInput = root.Q<TextField>("UsernameInput");
        PasswordInput = root.Q<TextField>("PasswordInput");
        RememberToggle = root.Q<Toggle>("RememberToggle");
        ActionContainer = root.Q<VisualElement>("ActionContainer");
        LoginBtn = root.Q<Button>("LoginBtn");
        RegisterBtn = root.Q<Button>("RegisterBtn");
        ErrorLabel = root.Q<Label>("ErrorLabel");
        LoadingContainer = root.Q<VisualElement>("LoadingContainer");
    }

    #endregion
}
```

### 7.3 可选元素（以 `_` 前缀命名）

对于 `name="_DebugLabel"` 的元素，生成可空查询且不做断言：

```csharp
protected Label DebugLabel { get; private set; } // 可能为 null
```

绑定时不会报错，调用方自行判空。

### 7.4 根元素命名冲突处理

当根容器 `name` 与类名相同时，生成属性名自动加 `Root` 后缀，避免与类名冲突。

## 8. View 基类设计

```csharp
using UnityEngine;
using UnityEngine.UIElements;

public abstract class ViewBase
{
    public VisualElement Root { get; private set; }
    public bool IsVisible => Root != null && Root.resolvedStyle.display == DisplayStyle.Flex;

    protected abstract string UxmlPath { get; }

    public void Create(VisualElement parent)
    {
        var tree = Resources.Load<VisualTreeAsset>(UxmlPath);
        Root = tree.Instantiate();
        parent.Add(Root);

        OnBind(Root);
        OnCreated();
    }

    public void Show()
    {
        Root.style.display = DisplayStyle.Flex;
        OnShow();
    }

    public void Hide()
    {
        Root.style.display = DisplayStyle.None;
        OnHide();
    }

    public void Destroy()
    {
        OnDestroy();
        Root.RemoveFromHierarchy();
        Root = null;
    }

    protected abstract void OnBind(VisualElement root);
    protected virtual void OnCreated() { }
    protected virtual void OnShow() { }
    protected virtual void OnHide() { }
    protected virtual void OnDestroy() { }
}
```

## 9. 业务层使用示例

手写的 `LoginView.cs`（与 `LoginView.Gen.cs` 组成 partial class）：

```csharp
public partial class LoginView : ViewBase
{
    protected override string UxmlPath => "UI/Documents/LoginView";

    protected override void OnBind(VisualElement root)
    {
        AutoBind(root);
    }

    protected override void OnCreated()
    {
        LoginBtn.clicked += OnLoginClicked;
        RegisterBtn.clicked += OnRegisterClicked;
        ErrorLabel.style.display = DisplayStyle.None;
    }

    protected override void OnDestroy()
    {
        LoginBtn.clicked -= OnLoginClicked;
        RegisterBtn.clicked -= OnRegisterClicked;
    }

    private void OnLoginClicked()
    {
        string user = UsernameInput.value;
        string pass = PasswordInput.value;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
        {
            ShowError("用户名和密码不能为空");
            return;
        }

        // 业务逻辑...
    }

    private void OnRegisterClicked()
    {
        // 跳转注册...
    }

    private void ShowError(string message)
    {
        ErrorLabel.text = message;
        ErrorLabel.style.display = DisplayStyle.Flex;
    }
}
```

## 10. 代码生成器工作流

### 10.1 触发方式

1. **保存 UXML 时自动触发**：通过 `AssetPostprocessor` 监听 `.uxml` 文件变更
2. **手动触发**：菜单 `Tools/UI Toolkit/Generate All Bindings`
3. **单文件生成**：右键 UXML 文件 → `Generate Binding Code`

### 10.2 生成流程

```
UXML 文件变更
    │
    ▼
解析 XML，提取所有 name 属性的元素
    │
    ▼
校验命名规范（大驼峰、后缀、无重复）
    │
    ├── 校验失败 → Console 输出警告，标注违规元素
    │
    ▼
根据元素类型映射 C# 类型
    │
    ▼
生成 .Gen.cs partial class
    │
    ▼
写入 Gen/ 目录，触发编译
```

### 10.3 校验规则

| 规则 | 级别 | 说明 |
|------|------|------|
| 大驼峰命名 | Error | 首字母必须大写，不含 `-` `_`（`_` 前缀除外） |
| 类型后缀匹配 | Warning | 后缀应与元素类型对应（`Button` 用 `Btn`） |
| 名称唯一性 | Error | 同一文件内不得重名 |
| 根容器存在 | Error | 根 `VisualElement` 必须有 `name` |
| 文件名匹配 | Warning | 根元素 `name` 应与文件名（去后缀）一致 |

## 11. .gitignore 策略

生成的 `*.Gen.cs` 文件 **应该提交到版本控制**，理由：
- 确保 CI/CD 和其他开发者无需运行生成器即可编译
- 代码审查时可以看到 UI 结构变化
- 生成代码量小且稳定，不会产生无意义 diff

## 12. AI 协作工作流

```
AI 根据需求编写 UXML + USS
        │
        ▼
保存后自动生成 .Gen.cs
        │
        ▼
AI 编写 partial class 业务逻辑
（直接使用强类型属性，无需手写 Q 查询）
        │
        ▼
编译验证 → 运行测试
```

AI 只需关注：
1. **UXML**：结构和布局（类似 HTML，AI 擅长）
2. **USS**：样式（类似 CSS，AI 擅长）
3. **业务 C#**：使用 `AutoBind` 后的强类型属性编写逻辑

AI **不需要关注**：
- `Q<T>("name")` 查询代码
- 元素类型拼写错误
- 绑定遗漏
