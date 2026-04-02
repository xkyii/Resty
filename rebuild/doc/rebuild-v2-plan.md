# Resty —— 重建计划 v2

参照 `Ursa.Avalonia/demo` 模式，从零新建项目，以 .NET 10 + ReactiveUI 重建，采用共享 UI + 平台入口分离的多平台架构。

---

## 一、现状问题

| 问题 | 现状 | 目标 |
|------|------|------|
| MVVM 库 | ReactiveUI（散乱在各 VM 中，无统一 base） | ReactiveUI（统一继承 `ViewModelBase : ReactiveObject`） |
| 导航 | 手写 bool 开关切换 View | `ViewLocator` + `IScreen` / `RoutingState` 内容导航 |
| 窗口 | 完全自定义 Chromeless Window + 手写标题栏 | `UrsaWindow`（内置标题栏扩展点，右侧插槽） |
| ViewModel 通信 | 属性直接引用 | `MessageBus.Current` 松耦合消息 |
| 多余层 | Application 层几乎为空 | 简化为 Domain + Infrastructure + 共享UI + 平台入口 |
| 样式覆盖 | App.axaml 大量手写样式补丁 | 直接使用 Semi + Ursa 主题，按需局部覆盖 |

---

## 二、新项目结构

参照 Ursa.Demo 的**共享 UI + 平台入口**分离模式：

```
Kx.Resty/                              ← 新解决方案根目录
├── Kx.Resty.slnx
├── global.json                        ← net10.0, rollForward: latestMajor
├── Directory.Build.props              ← Nullable enable, ImplicitUsings enable
├── Directory.Packages.props           ← Central package versioning
│
├── src/
│   │
│   │  ── 领域 / 基础设施（无 UI 依赖） ──
│   │
│   ├── Kx.Resty.Domain/               ← 纯领域模型（无 NuGet 依赖）
│   │   ├── Http/
│   │   │   ├── HttpRequestData.cs
│   │   │   └── HttpResponseData.cs
│   │   └── Workspace/
│   │       └── WorkspaceEntry.cs
│   │
│   ├── Kx.Resty.Infrastructure/       ← 技术实现（HTTP/文件/持久化）
│   │   ├── Http/
│   │   │   └── SystemHttpRequestExecutor.cs
│   │   ├── Workspace/
│   │   │   ├── HttpFileParser.cs
│   │   │   └── WorkspaceVariableResolver.cs
│   │   └── Persistence/
│   │       └── JsonWorkspaceStore.cs
│   │
│   │  ── 共享 UI（Views + ViewModels，不含平台启动代码） ──
│   │
│   ├── Kx.Resty/                      ← 共享 UI 项目（类库，无 OutputType）
│   │   ├── Kx.Resty.csproj            ← 引用 Semi/Ursa/ReactiveUI.Avalonia
│   │   ├── App.axaml / App.axaml.cs
│   │   ├── Assets/
│   │   │
│   │   ├── DataTemplates/
│   │   │   └── ViewLocator.cs         ← ViewModel → View 自动映射
│   │   │
│   │   ├── Views/
│   │   │   ├── MainWindow.axaml       ← UrsaWindow（Desktop 用）
│   │   │   ├── MainWindow.axaml.cs
│   │   │   ├── MainView.axaml         ← SingleView（Browser/Mobile 用）
│   │   │   ├── MainView.axaml.cs
│   │   │   └── TitleBarRightContent.axaml
│   │   │
│   │   └── Features/
│   │       ├── Shell/ViewModels/
│   │       │   └── MainWindowViewModel.cs
│   │       ├── DirectoryManager/
│   │       │   ├── Views/DirectoryManagerView.axaml
│   │       │   └── ViewModels/DirectoryManagerViewModel.cs
│   │       └── Workspace/
│   │           ├── Views/
│   │           │   ├── WorkspaceView.axaml
│   │           │   ├── NavigationView.axaml
│   │           │   ├── RequestEditorView.axaml
│   │           │   └── ResponseView.axaml
│   │           └── ViewModels/
│   │               ├── WorkspaceViewModel.cs
│   │               ├── NavigationViewModel.cs
│   │               ├── RequestEditorViewModel.cs
│   │               └── ResponseViewModel.cs
│   │
│   │  ── 平台入口项目（极简，仅含 Program.cs / MainActivity） ──
│   │
│   ├── Kx.Resty.Desktop/              ← Desktop 入口（Windows/macOS/Linux）
│   │   ├── Kx.Resty.Desktop.csproj   ← TargetFramework: net10.0, Avalonia.Desktop
│   │   ├── Program.cs                 ← BuildAvaloniaApp().StartWithClassicDesktopLifetime
│   │   └── app.manifest
│   │
│   ├── Kx.Resty.Browser/              ← Browser/WASM 入口（可选）
│   │   ├── Kx.Resty.Browser.csproj   ← TargetFramework: net10.0-browser
│   │   └── Program.cs
│   │
│   └── Kx.Resty.Android/              ← Android 入口（可选）
│       ├── Kx.Resty.Android.csproj   ← TargetFramework: net10.0-android
│       └── MainActivity.cs
│
└── tests/
    └── Kx.Resty.Tests/
```

### 项目依赖关系

```
Kx.Resty.Domain
    ↑
Kx.Resty.Infrastructure
    ↑
Kx.Resty  (共享 UI，引用 Infrastructure + Domain)
    ↑
Kx.Resty.Desktop   (仅 Program.cs，引用 Kx.Resty)
Kx.Resty.Browser   (仅 Program.cs，引用 Kx.Resty)
Kx.Resty.Android   (仅 MainActivity，引用 Kx.Resty)
```

> 核心原则：**所有 UI 代码住在 `Kx.Resty`（共享项目），平台入口项目只有 5 行以内的启动代码。**

---

## 三、NuGet 包

### Directory.Packages.props（集中版本管理）

```xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <AvaloniaVersion>11.3.12</AvaloniaVersion>
  </PropertyGroup>
  <ItemGroup>
    <PackageVersion Include="Avalonia"                    Version="$(AvaloniaVersion)"/>
    <PackageVersion Include="Avalonia.Desktop"            Version="$(AvaloniaVersion)"/>
    <PackageVersion Include="Avalonia.Diagnostics"        Version="$(AvaloniaVersion)"/>
    <PackageVersion Include="ReactiveUI"                  Version="20.1.1"/>
    <PackageVersion Include="ReactiveUI.Avalonia"         Version="20.1.1"/>
    <PackageVersion Include="Semi.Avalonia"               Version="11.3.7.3"/>
    <PackageVersion Include="Irihi.Ursa"                  Version="1.15.0"/>
    <PackageVersion Include="Irihi.Ursa.Themes.Semi"      Version="1.15.0"/>
    <PackageVersion Include="Irihi.Ursa.ReactiveUIExtension" Version="1.1.0"/>
  </ItemGroup>
</Project>
```

### Kx.Resty.csproj（共享 UI）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <AvaloniaUseCompiledBindingsByDefault>true</AvaloniaUseCompiledBindingsByDefault>
  </PropertyGroup>
  <ItemGroup>
    <AvaloniaResource Include="Assets\**"/>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia"/>
    <PackageReference Include="Avalonia.Diagnostics">
      <IncludeAssets Condition="'$(Configuration)' != 'Debug'">None</IncludeAssets>
      <PrivateAssets Condition="'$(Configuration)' != 'Debug'">All</PrivateAssets>
    </PackageReference>
    <PackageReference Include="ReactiveUI.Avalonia"/>
    <PackageReference Include="Semi.Avalonia"/>
    <PackageReference Include="Irihi.Ursa"/>
    <PackageReference Include="Irihi.Ursa.Themes.Semi"/>
    <PackageReference Include="Irihi.Ursa.ReactiveUIExtension"/>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kx.Resty.Infrastructure\Kx.Resty.Infrastructure.csproj"/>
  </ItemGroup>
</Project>
```

### Kx.Resty.Desktop.csproj（平台入口，极简）

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Avalonia.Desktop"/>
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\Kx.Resty\Kx.Resty.csproj"/>
  </ItemGroup>
</Project>
```

> Domain 和 Infrastructure 均无 UI 依赖，仅引用 `Microsoft.NET.Sdk`。

---

## 四、核心架构模式（参照 Ursa.Demo）

### 4.1 ViewModelBase

```csharp
// ReactiveObject 是 ReactiveUI 的基类，等价于 Ursa.Demo 的 ObservableObject
public abstract class ViewModelBase : ReactiveObject { }
```

### 4.2 ViewLocator（DataTemplate 自动映射）

```csharp
// ViewModel 命名 → 对应 View 自动解析（与 ReactiveUI 的 ViewLocator 机制一致）
// DirectoryManagerViewModel → DirectoryManagerView
// WorkspaceViewModel        → WorkspaceView
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? data)
    {
        if (data is null) return null;
        var name = data.GetType().FullName!.Replace("ViewModel", "View");
        var type = Type.GetType(name);
        return type is not null ? (Control)Activator.CreateInstance(type)! : new TextBlock { Text = name };
    }
    public bool Match(object? data) => data is ViewModelBase;
}
```

```xml
<!-- App.axaml 中注册 -->
<Application.DataTemplates>
    <local:ViewLocator/>
</Application.DataTemplates>
```

### 4.3 ViewModel 导航（ReactiveUI MessageBus）

```csharp
// 发送方（标题栏按钮、菜单等）
MessageBus.Current.SendMessage("DirectoryManager", "Navigate");

// MainWindowViewModel 接收
public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase? _currentPage;
    public ViewModelBase? CurrentPage
    {
        get => _currentPage;
        set => this.RaiseAndSetIfChanged(ref _currentPage, value);
    }

    public MainWindowViewModel()
    {
        MessageBus.Current.Listen<string>("Navigate")
            .Subscribe(page => CurrentPage = page switch
            {
                "DirectoryManager" => new DirectoryManagerViewModel(),
                "Workspace"        => new WorkspaceViewModel(),
                _ => CurrentPage
            });

        CurrentPage = new DirectoryManagerViewModel(); // 默认页
    }
}
```

```xml
<!-- MainWindow.axaml 中 ContentControl 自动走 ViewLocator -->
<ContentControl Content="{Binding CurrentPage}"/>
```

### 4.4 UrsaWindow 标题栏

```xml
<!-- MainWindow.axaml -->
<u:UrsaWindow ...>
    <u:UrsaWindow.RightContent>
        <views:TitleBarRightContent/>
    </u:UrsaWindow.RightContent>
    <ContentControl Content="{Binding CurrentPage}"/>
</u:UrsaWindow>
```

`TitleBarRightContent` 放主题切换、"打开文件夹"等按钮，无需手写最小化/最大化/关闭逻辑。

### 4.5 ViewModel 属性写法（ReactiveUI）

```csharp
// 属性
private string? _foo;
public string? Foo
{
    get => _foo;
    set => this.RaiseAndSetIfChanged(ref _foo, value);
}

// 命令（ReactiveCommand）
public ReactiveCommand<Unit, Unit> OpenFolderCommand { get; }
public ReactiveCommand<Unit, Unit> SaveCommand { get; }

public MyViewModel()
{
    OpenFolderCommand = ReactiveCommand.CreateFromTask(OpenFolderAsync);

    var canSave = this.WhenAnyValue(x => x.Foo, f => !string.IsNullOrEmpty(f));
    SaveCommand = ReactiveCommand.Create(Save, canSave);
}

private async Task OpenFolderAsync() { ... }
private void Save() { ... }
```

---

## 五、各层迁移对应关系

| 现 Resty.Rebuild | 新 Kx.Resty | 变化说明 |
|---|---|---|
| `Resty.Rebuild.Domain` | `Kx.Resty.Domain` | 基本不变，清理 Class1.cs |
| `Resty.Rebuild.Application` | _删除_ | 合并到 Infrastructure（当前为空层） |
| `Resty.Rebuild.Infrastructure` | `Kx.Resty.Infrastructure` | 保留 Http/Persistence/Workspace 子目录 |
| `Resty.Rebuild.Desktop` | `Kx.Resty` + `Kx.Resty.Desktop` | 共享 UI 拆出；手写 Chromeless 标题栏 → UrsaWindow；ReactiveUI 继续使用 |
| Features/DirectoryManager | Features/DirectoryManager | ViewModel 继续继承 `ReactiveObject`（通过 `ViewModelBase`） |
| Features/Workspace | Features/Workspace | 拆分为 Navigation/Editor/Response 三个子 ViewModel |
| MainWindow（自定义 Chromeless） | MainWindow（UrsaWindow） | 去掉 SystemDecorations=None 和手写 caption_btn |

---

## 六、执行阶段

| 阶段 | 内容 | 交付物 |
|------|------|--------|
| **P0** | 创建新解决方案骨架 | `Kx.Resty.slnx`，三个 csproj，`global.json`，`Directory.Packages.props` |
| **P1** | Desktop 项目：App + MainWindow（UrsaWindow）+ ViewLocator + ViewModelBase | 能运行的空壳，显示 UrsaWindow 标题栏 |
| **P2** | DirectoryManager 迁移 | `DirectoryManagerViewModel`（`ReactiveObject`）+ `DirectoryManagerView.axaml` |
| **P3** | Workspace 迁移 | Navigation / Editor / Response ViewModel + View |
| **P4** | 导航 | `MessageBus.Current` 在 MainWindow 和 DirectoryManager/Workspace 之间切换 |
| **P5** | 标题栏右侧插槽 | `TitleBarRightContent`（打开文件夹、主题切换） |
| **P6** | Infrastructure 迁移 | HttpFileParser / VariableResolver / HttpRequestExecutor / JsonWorkspaceStore |
| **P7** | 收尾 | 样式清理（去掉大量 App.axaml 补丁），单元测试补充 |

---

## 七、关键约定

1. **命名空间**：`Kx.Resty.*`，与旧 `Resty.Rebuild.*` 完全隔离，两个目录可并存。
2. **编译绑定**：`AvaloniaUseCompiledBindingsByDefault=true`，所有绑定加 `x:DataType`。
3. **MVVM 库**：继续使用 `ReactiveUI`（`ReactiveObject` / `ReactiveCommand` / `MessageBus`），无需 `partial class`。
4. **主题**：`Semi.Avalonia` + `Irihi.Ursa.Themes.Semi`，不再叠加大量全局 Button 样式补丁。
5. **对话框**：使用 Ursa 的 `MessageBox.ShowOverlayAsync` / `Dialog.ShowCustomModal`，废弃手写 Window 弹窗。
6. **ReactiveUI 扩展**：引用 `Irihi.Ursa.ReactiveUIExtension` 以获得 Ursa 控件的 ReactiveUI 绑定支持。
