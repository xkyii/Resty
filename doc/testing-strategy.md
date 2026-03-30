# Resty 测试分层方案

本文档定义 Resty 的自动测试结构、职责边界和首批落地范围。

## 目标

Resty 采用两层测试结构：

1. unit：覆盖解析、序列化、状态切换、文件操作这类稳定且反馈快的逻辑
2. headless：覆盖 Avalonia 绑定、视图定位、页面装配和少量关键交互，不依赖真实桌面环境

暂不把真实桌面端到端自动化作为主线。端到端测试只保留为后续少量冒烟流程。

## 目录结构

```text
tests/
  Directory.Build.props
  Kx.Resty.UnitTests/
    Kx.Resty.UnitTests.csproj
    Commands/
    ViewModels/
  Kx.Resty.HeadlessTests/
    Kx.Resty.HeadlessTests.csproj
    HeadlessTestApp.cs
    Views/
```

## 分层职责

### unit

放在 `tests/Kx.Resty.UnitTests`。

适合覆盖的对象：

1. `HttpFileParser`
2. `HttpFileWriter`
3. `WorkspaceScanner` 中不依赖 watcher 时序的扫描和环境合并逻辑
4. `CollectionPanel`、`WorkspaceTab`、`MainWindow`、`RequestTab` 中不依赖真实 Avalonia 窗口的状态逻辑

建议规则：

1. 单测不依赖真实文件选择器、真实窗口和真实网络
2. 涉及文件操作时用临时目录隔离
3. 重点验证输入输出、状态迁移和边界条件

首批建议：

1. `HttpFileParser` 的请求块拆分、变量解析、注解解析、query 参数拆分
2. `HttpFileWriter` 的写回格式和 round-trip
3. `MainWindow` 的聚合状态字段
4. `WorkspaceTab` 的请求 tab 复用
5. `RequestTab` 的 model 与 view state 映射

### headless

放在 `tests/Kx.Resty.HeadlessTests`。

适合覆盖的对象：

1. ViewModel 到 View 的定位规则
2. 主窗口和关键页面的装配是否正常
3. 绑定和模板能否在 Headless Avalonia 环境中创建
4. 少量关键显示状态，例如空态、集合页、请求页切换

建议规则：

1. 使用 `Avalonia.Headless.XUnit`
2. Headless 测试只验证 UI 装配和绑定，不承担全部业务逻辑覆盖
3. 只覆盖少量关键页面，避免把大量业务断言堆到 UI 层

首批建议：

1. `App.CreateViewForViewModel()` 能正确返回 `MainWindow` 视图
2. 主窗口在 headless 环境下可实例化
3. 请求页在绑定 `RequestTab` 后可创建

## 解决方案接入

当前解决方案中加入两个测试项目：

1. `tests/Kx.Resty.UnitTests/Kx.Resty.UnitTests.csproj`
2. `tests/Kx.Resty.HeadlessTests/Kx.Resty.HeadlessTests.csproj`

两者都直接引用主项目 `src/Kx.Resty.csproj`，确保测试基线与产品代码一致。

## 运行方式

在 Resty 根目录执行：

```powershell
dotnet test Resty.slnx
```

只跑 unit：

```powershell
dotnet test tests/Kx.Resty.UnitTests/Kx.Resty.UnitTests.csproj
```

只跑 headless：

```powershell
dotnet test tests/Kx.Resty.HeadlessTests/Kx.Resty.HeadlessTests.csproj
```

## 后续扩展顺序

1. 先补齐 parser 和 writer 的 round-trip 测试
2. 再补 `RequestTab` 与 `WorkspaceTab` 的状态迁移测试
3. 然后再补少量 headless 页面装配测试
4. 最后如果需要，再增加真实桌面冒烟自动化