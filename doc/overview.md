# Kx.Resty — 项目概览

## 基本信息

| 项 | 值 |
|---|---|
| 项目名称 | Kx.Resty |
| 类型 | GUI 桌面应用（REST API 客户端） |
| 框架 | .NET 10、Avalonia 11.3.12 |
| MVVM | CommunityToolkit.Mvvm 8.4.0 |
| 命名空间根 | `Kx.Resty` |
| 解决方案 | `Resty.slnx` |
| 项目文件 | `src/Kx.Resty.csproj` |

---

## 目录结构

```
src/
├── App.axaml / App.axaml.cs      # 应用入口、全局命令、主题/语言切换
├── Converters/                   # 值转换器（静态类，SourceGit 风格）
│   ├── BrushConverters.cs        # MethodBrushConverter
│   ├── IntConverters.cs          # IndexToBoolConverter
│   └── StringConverters.cs      # ToLocaleConverter、ToThemeConverter
├── Models/
│   └── Locale.cs                 # Locale 数据类 + Supported 静态列表
├── Resources/
│   ├── Icons.axaml               # SVG 路径图标资源
│   ├── Styles.axaml             # 全局样式（按钮、输入框等）
│   ├── Themes.axaml             # 亮/暗主题颜色变量
│   └── Locales/
│       ├── en_US.axaml          # 英文字符串资源
│       └── zh_CN.axaml          # 简体中文字符串资源
├── ViewModels/
│   ├── Preferences.cs            # 应用偏好单例（持久化到 JSON）
│   ├── MainWindow.cs             # 主窗口 VM（Tab 管理）
│   ├── RequestTab.cs             # 单个请求标签页 VM（未实现网络逻辑）
│   ├── CollectionPanel.cs        # 左侧集合面板 VM（树形节点）
│   ├── KeyValueTableViewModel.cs # 键值对表格 VM（Params/Headers 用）
│   ├── Welcome.cs                # 欢迎页 VM
│   └── Popup.cs                  # 弹出提示 VM
└── Views/
    ├── ChromelessWindow.cs       # 自定义无边框窗口基类
    ├── MainWindow.axaml/.cs      # 主窗口（标题栏拖动、Tab 容器）
    ├── RequestTab.axaml/.cs      # 请求标签页视图
    ├── CollectionPanel.axaml/.cs # 左侧集合面板
    ├── CollectionNodeView.axaml/.cs # 集合树节点
    ├── KeyValueTable.axaml/.cs   # 键值对表格控件
    ├── Welcome.axaml/.cs         # 欢迎页
    ├── Preferences.axaml/.cs     # 偏好设置对话框（在 Views/ 根，非 Dialogs/）
    └── Converters.cs             # ← 已删除，职责已迁移到 src/Converters/
```

---

## 架构约定（SourceGit 风格）

### 命名约定
- 视图：`Kx.Resty.Views.*`
- ViewModel：`Kx.Resty.ViewModels.*`
- 转换器：`Kx.Resty.Converters.*`
- 模型：`Kx.Resty.Models.*`（目前只有 `Locale`）

### View ↔ ViewModel 自动映射
`App.CreateViewForViewModel(object data)` 通过反射将 `ViewModels.Foo` 映射到 `Views.Foo`。
`App.ShowDialog(object data)` 优先判断是否已是 `ChromelessWindow`，否则走映射。

### 全局命令
定义在 `App` 静态字段，AXAML 用 `{x:Static s:App.XxxCommand}` 绑定，ViewModel 不引用 View：

```csharp
public static ICommand OpenPreferencesCommand { get; } =
    new SimpleCommand(_ => ShowDialog(new Views.Preferences()));
```

### 偏好设置
- 单例 `ViewModels.Preferences.Instance`，启动时从 JSON 加载
- `Locale`/`Theme` setter 含副作用（立即调用 `App.SetLocale` / `App.SetTheme`）
- 每次 `PropertyChanged` 自动 `Save()`（在 `App.Initialize` 里注册）
- 偏好设置对话框直接以单例为 `DataContext`，改动实时生效，点 OK 显式 `Save()` 后关闭
- 数据目录：Windows `%AppData%\Kx.Resty`，macOS `~/Library/Application Support/Kx.Resty`，Linux `~/.kx.resty`

### 语言切换
`App.SetLocale(string key)` 用 `AvaloniaXamlLoader.Load(avares://...)` 加载 AXAML 资源并注入 `MergedDictionaries`，支持运行时热切换无需重启。

### 转换器
静态包含类 + `public static readonly` 实例，AXAML 用 `{x:Static c:FooConverters.Bar}`：

| 类 | 字段 | 用途 |
|---|---|---|
| `BrushConverters` | `MethodBrush` | HTTP 方法名 → 对应颜色画刷 |
| `IntConverters` | `IndexToBool` | 选中 Tab 索引 → RadioButton.IsChecked |
| `StringConverters` | `ToLocale` | locale key ↔ `Locale` 对象 |
| `StringConverters` | `ToTheme` | theme string ↔ `ThemeVariant` |

---

## 主界面布局

```
┌──────────────────── MainWindow ───────────────────────┐
│ [≡]           Kx.Resty            [─][□][×]           │  ← 标题栏（可拖动）
├──────┬────────────────────────────────────────────────┤
│      │  [+ New Request]  [Tab1 ×] [Tab2 ×]            │  ← Tab 栏
│ 集合 ├────────────────────────────────────────────────┤
│ 面板 │  GET ▾  https://...                   [Send]   │  ← 请求行
│      │  Params | Headers | Body | Auth                │  ← 请求编辑 Tabs
│      │  ─────────────────────────────────────────     │
│      │  [拖动分隔条]                                   │
│      │  200 OK  12ms  1.2KB      Body|Headers|Cookies │  ← 响应状态栏
│      │  { "result": ... }                             │  ← 响应体
└──────┴────────────────────────────────────────────────┘
```

---

## 当前实现状态

| 功能 | 状态 |
|---|---|
| 主窗口框架（标题栏、Tab、布局） | ✅ 完成 |
| 无边框窗口 + 拖动 | ✅ 完成 |
| 主题切换（Default/Dark/Light） | ✅ 完成 |
| 语言切换（en_US / zh_CN） | ✅ 完成 |
| 偏好设置对话框 | ✅ 完成 |
| 请求面板 UI（方法、URL、Tab） | ✅ UI 完成，逻辑未实现 |
| 响应面板 UI | ✅ UI 完成，逻辑未实现 |
| 左侧集合面板 UI（树形） | ✅ UI 完成，逻辑未实现 |
| 键值对表格（Params/Headers） | ✅ UI 完成，逻辑未实现 |
| HTTP 请求发送 | ❌ 未实现（`Send()` 是空方法） |
| 集合持久化（保存/加载） | ❌ 未实现 |
| 环境变量 | ❌ 未实现 |
| 历史记录 | ❌ 未实现 |
| 认证（Auth Tab） | ❌ 未实现 |

---

## 待实现（逻辑部分）

1. **HTTP 请求发送**：`ViewModels.RequestTab.Send()` — 使用 `HttpClient`，支持方法/URL/Headers/Body/Params，响应填充到 VM
2. **集合持久化**：集合树的 JSON 序列化/反序列化，加载到 `CollectionPanel`
3. **环境变量**：变量替换（`{{baseUrl}}` 等），环境管理 UI
4. **历史记录**：请求历史保存与回放
5. **Auth Tab**：Bearer Token、Basic Auth 等认证方式
6. **键值对表格逻辑**：行的增删改，绑定到请求的 Params/Headers
