# Resty 开发计划

**版本目标**: 0.1.2
**基准日期**: 2026-05-07
**状态**: 规划中（用户反馈整合）

---

## 1. 版本路线图

```
0.1.2 Release (用户反馈集成版)
│
├── Phase 1: 侧边栏交互优化（1.5 周）
│   ├── 工作区/环境模式下拉菜单（需求 2）
│   ├── 菜单项重组（需求 3-6）
│   └── 右键菜单完善
│
├── Phase 2: 请求历史增强（1 周）
│   ├── 历史记录详情展示（需求 7）
│   └── 请求摘要与文本展示
│
├── Phase 3: 质量提升（2 周）
│   ├── 补充单元测试（HttpRequestExecutor/CurlConverter）
│   ├── 代码重构优化（长文件拆分）
│   └── 日志基础设施
│
├── Phase 4: 功能补全（2 周）
│   ├── 动态变量实现（$uuid/$timestamp）
│   ├── 响应增强（JSON 高亮等）
│   └── cURL 导入 UI 集成
│
└── Phase 5: 发布准备（1 周）
    ├── 文档完善
    ├── 发布构建
    └── GitHub Release
```

---

## 2. 用户反馈需求分析（0.1.2 核心内容）

本版本基于用户实际使用反馈，重点优化侧边栏交互和历史记录功能。下面详细分析 7 个需求：

### 2.1 需求清单

| 需求 | 类别 | 优先级 | 说明 |
|------|------|--------|------|
| 需求1 | 版本号 | P0 | 版本定为 0.1.2（从 V2.0 改为） |
| 需求2 | 侧边栏操作菜单 | P0 | 工作区/环境模式分别有下拉菜单按钮 |
| 需求3 | 环境模式简化 | P0 | 移除"环境"标题和"+"按钮，统一用菜单按钮 |
| 需求4 | http文件菜单 | P1 | 右键菜单改为：新建请求、资源管理器打开、复制路径 |
| 需求5 | 请求菜单精简 | P1 | 右键菜单改为：仅"复制请求名称"（移除"打开"） |
| 需求6 | 环境变量菜单 | P1 | 移除"..."按钮，改为右键菜单包含"重命名" |
| 需求7 | 历史记录详情 | P0 | 选中历史项后右侧展示摘要和原始请求文本 |

### 2.2 UI 设计 - 侧边栏工作区模式

**当前状态**:
```
┌─────────────────────────────────┐
│ [工作区] [环境]                  │
│ ─────────────────────────────── │
│ [搜索请求…]                     │
│ ▾ users.http                    │
│   ├ GET  Get User               │
│   └ POST Create User            │
│ ▾ orders.http                   │
│   └ GET  List Orders            │
└─────────────────────────────────┘
```

**改进后**:
```
┌──────────────────────────────────┐
│ [工作区] [环境] [▼ 操作]          │
│ ────────────────────────────────│
│ [搜索请求…]                      │
│ ▾ users.http                    │
│   ├ GET  Get User               │
│   └ POST Create User            │
│ ▾ orders.http                   │
│   └ GET  List Orders            │
└──────────────────────────────────┘

操作按钮下拉菜单：
┌──────────────┐
│ ✓ 新建 HTTP  │  (添加新 .http 文件)
│ ☑ 跟随打开   │  (自动高亮当前激活的Tab)
└──────────────┘
```

**交互说明**:
- 右侧新增 "[▼]" 按钮（下拉菜单指示器）
- "新建 HTTP" - 点击后弹出输入框创建新 .http 文件（当前 BeginNewFile 的逻辑）
- "跟随打开" - Toggle 选项，启用时当 Tab 激活时自动高亮左侧对应的请求
  - 实现方式：RequestEditorView 激活时发出事件 → MainWindow 捕获 → Sidebar 高亮显示

### 2.3 UI 设计 - 侧边栏环境模式

**当前状态**:
```
┌─────────────────────────────────┐
│ [工作区] [环境]                  │
│ ─────────────────────────────── │
│ 环境                        [+]  │
│ ─────────────────────────────── │
│ ● dev                       […] │
│ ○ prod                      […] │
│ ○ staging                   […] │
└─────────────────────────────────┘
```

**改进后**:
```
┌──────────────────────────────────┐
│ [工作区] [环境] [▼ 操作]          │
│ ────────────────────────────────│
│ ● dev                           │
│ ○ prod                          │
│ ○ staging                       │
└──────────────────────────────────┘

操作按钮下拉菜单：
┌──────────────┐
│ + 新建环境   │
└──────────────┘
```

**变更说明**:
- 移除头部的"环境"标题和"+"按钮
- 操作统一通过右侧菜单按钮
- 环境变量行（dev/prod/staging）移除右侧"..."按钮，改为右键菜单

### 2.4 UI 设计 - 右键菜单重组

**http 文件右键菜单**（需求4）:
```
┌──────────────────────────────┐
│ 新建请求                      │ ← NEW
│ ─────────────────────────────│
│ 在资源管理器中打开            │
│ 复制路径                      │
└──────────────────────────────┘
```

**请求行右键菜单**（需求5）:
```
┌──────────────────────────────┐
│ 复制请求名称                  │
└──────────────────────────────┘
```
（移除"打开"，因为单击已实现打开功能）

**环境变量行右键菜单**（需求6）:
```
┌──────────────────────────────┐
│ 重命名                        │ ← NEW
│ ─────────────────────────────│
│ 删除                          │
└──────────────────────────────┘
```

### 2.5 UI 设计 - 历史记录详情面板

**当前状态**:
```
Activity Bar:
[☰] [⧗] [⊞] [⚗] [⚙]
     ↑
   点击历史

历史列表（左侧）:
[GET  200  2m]  api.example.com/users
 POST-New-User
────────────────────────────────
[POST 201  5m]  api.example.com/users
 Create-User
────────────────────────────────
```

**改进后** - 选中历史项后右侧展示详情：
```
左侧：历史列表（样式同上，支持选中状态高亮）

右侧：请求详情面板
┌─────────────────────────────────────┐
│ 请求摘要                             │
├─────────────────────────────────────┤
│ 文件夹: samples                      │
│ 文件:   users.http                  │
│ 请求:   Get User                    │
│ 时间:   2 分钟前                     │
│ 方法:   GET                         │
│ URL:    https://api.example.com/... │
│ 状态:   200 OK                      │
│ 耗时:   45ms                        │
│ 大小:   1.2KB                       │
├─────────────────────────────────────┤
│ 原始请求内容                         │
├─────────────────────────────────────┤
│ GET /api/users/1 HTTP/1.1            │
│ Host: api.example.com                │
│ Authorization: Bearer {{token}}      │
│ Accept: application/json             │
│                                      │
│ (No body)                            │
└─────────────────────────────────────┘
```

**交互说明**:
- 历史列表项点击激活（背景高亮 BgActive 色）
- 右侧面板实时显示选中项的详情
- 摘要部分显示关键信息：文件夹、文件、请求名、时间戳、方法、URL、状态码、耗时、响应大小
- 原始请求内容显示该历史项保存的请求文本（从 HistoryEntry 扩展字段获取）

---

## 3. Phase 1: 侧边栏交互优化（需求 2-6）

### 3.1 需求2：侧边栏操作菜单按钮

**改动文件**: `Resty.Gui/Views/SidebarView.cs`

**任务内容**:

1. **UI 组件**
   - 在 Tab 行（`_collectionTabBtn`、`_envTabBtn`）右侧添加下拉菜单按钮（`_operationsBtn`）
   - 按钮样式：图标为 `▼`（黑三角），宽度 28px，与 Tab 按钮高度一致 (32px)
   - 按钮位置：水平排列在 `tabRow` 右侧

2. **工作区模式菜单**
   - 菜单项："新建 HTTP"、"跟随打开"
   - "新建 HTTP" → 调用 `BeginNewFile()`
   - "跟随打开" → Toggle，设置内部标志 `_syncTabToSelection = bool`

3. **环境模式菜单**
   - 菜单项："新建环境"
   - "新建环境" → 调用 `BeginNewEnv()`

4. **跟随打开实现**
   - 新增事件：`public event Action<HttpFileNode, RequestNode>? SyncTabActive;`
   - RequestEditorView 激活时，由 MainWindow 捕获并调用 `SidebarView.SyncTabActive`
   - 实现方法：临时高亮目标请求行（1-2s 后恢复）

**工时估算**: 3 小时

### 3.2 需求3：环境模式头部简化

**改动文件**: `Resty.Gui/Views/SidebarView.cs`

**任务内容**:

1. **移除 BuildEnvHeader()**
   - 删除现有的 `BuildEnvHeader()` 方法（已移至下拉菜单）
   - 在 `SwitchMode(true)` 中移除头部渲染逻辑
   - DockPanel 直接包含 ScrollViewer，无需中间的头部 Border

2. **环境列表布局调整**
   - 移除头部（"环境"标题 + "+" 按钮）
   - 增加 padding，使列表与边界留有适当间距
   - 保持原有的环境行样式

**工时估算**: 1 小时

### 3.3 需求4：http 文件右键菜单 - 新建请求

**改动文件**: `Resty.Gui/Views/SidebarView.cs`

**任务内容**:

1. **BuildFileNode() 菜单更新**
   - 新增菜单项："新建请求"
   - 菜单顺序：新建请求 → 在资源管理器中打开 → 复制路径

2. **新建请求交互**
   - 点击后弹出对话框（类似 `BeginNewFile()` 的输入框）
   - 输入请求名称（如 "Get User"）
   - 确认后在文件末尾追加一个新请求模板：
     ```
     ###  {RequestName}
     GET https://example.com
     ```
   - 保存文件并打开新请求的编辑器标签
   - 触发事件：`RequestCreated?.Invoke(file, newRequest)`

3. **实现细节**
   - 新增方法：`CreateRequestInFile(string filePath, string requestName)`
   - 利用 WorkspaceService 的文件操作接口

**工时估算**: 2.5 小时

### 3.4 需求5：请求右键菜单精简

**改动文件**: `Resty.Gui/Views/SidebarView.cs`

**任务内容**:

1. **BuildRequestNode() 菜单更新**
   - 当前：`"打开"`, `"复制请求名称"`
   - 改为：仅保留 `"复制请求名称"`
   - 移除 `"打开"` 菜单项（单击已实现打开）

2. **验证单击打开**
   - 确保 `row.Click` 事件仍然正确触发 `RequestSelected?.Invoke(file, req)`

**工时估算**: 0.5 小时

### 3.5 需求6：环境变量右键菜单 - 重命名

**改动文件**: `Resty.Gui/Views/SidebarView.cs`

**任务内容**:

1. **移除"..."按钮**
   - 在 `BuildEnvRow()` 中移除 `menuBtn` 组件
   - 环境行布局调整：移除右侧的按钮空间

2. **添加右键菜单**
   - 环境行支持右键菜单（在 `rowBorder` 或 `btn` 上绑定 ContextMenu）
   - 菜单项："重命名"、"删除"

3. **重命名实现**
   - 点击"重命名"后，该环境行变成可编辑状态
   - 显示输入框，内容为当前环境名
   - 确认后调用 `RenameEnv(oldName, newName)`
   - 实现方法：更新两个 env.json 文件中的 key

4. **删除保持现状**
   - 原有的"删除"逻辑 `DeleteEnv()` 保持不变

**工时估算**: 2 小时

### 3.6 任务总结 - Phase 1

| 任务 | 工时 | 备注 |
|------|------|------|
| 需求2：操作菜单 | 3h | 新增按钮、菜单、跟随打开逻辑 |
| 需求3：环境头部简化 | 1h | 移除标题和按钮 |
| 需求4：新建请求 | 2.5h | 菜单项+对话框+文件操作 |
| 需求5：菜单精简 | 0.5h | 移除打开菜单项 |
| 需求6：重命名菜单 | 2h | 移除按钮、右键菜单、重命名逻辑 |
| **小计** | **9h** | **第1周** |

---

## 4. Phase 2: 请求历史增强（需求7）

### 4.1 需求7：历史记录详情展示

**改动文件**:
- `Resty.Gui/Views/HistoryPanelView.cs` - 扩展历史记录模型、列表/详情显示
- `Resty.Gui/Models/HistoryEntry.cs` - 增加请求文本字段
- `Resty.Gui/MainWindow.cs` - 处理历史面板交互

**任务内容**:

1. **HistoryEntry 数据模型扩展**
   - 当前字段：RequestName, Method, Url, StatusCode, ElapsedMs, Timestamp
   - 新增字段：`FilePath` (文件路径)、`RequestText` (请求原始文本)、`FolderPath` (文件夹路径)
   - 修改方式：`record` 添加新字段，或创建子类

2. **HistoryPanelView UI 改造**
   - 改为左右两栏布局（SplitPanel）
   - **左栏（历史列表）**：保持当前样式，支持选中状态高亮
   - **右栏（详情面板）**：新增
     - 摘要部分：显示文件夹、文件名、请求名、时间、方法、URL、状态、耗时、大小
     - 原始请求文本：展示该请求的完整源文本（带语法高亮可选）

3. **交互流程**
   - 点击历史列表中的某一项 → 右侧面板显示详情
   - 支持复制请求文本按钮（Ctrl+C 或右键）
   - 支持"在编辑器中打开"按钮 → 在编辑器 Tab 中打开此请求

4. **数据来源**
   - 历史列表项添加时，由 MainWindow 在 SendRequested 后调用 AddEntry，传入完整信息
   - 需要在调用处补充 FilePath、RequestText 等字段

5. **存储格式**
   - 历史记录 JSON 文件（`.resty/history.json`）新增字段
   - 兼容旧版本（字段缺失时用默认值）

**实现步骤**:

| 步骤 | 内容 | 工时 |
|------|------|------|
| 5.1 | 扩展 HistoryEntry 模型 | 1h |
| 5.2 | 设计历史详情面板 UI | 1.5h |
| 5.3 | 实现左右分栏布局 | 1.5h |
| 5.4 | 摘要信息展示 | 1h |
| 5.5 | 原始文本展示 | 0.5h |
| 5.6 | 复制/打开按钮 | 1h |
| 5.7 | MainWindow 集成 | 1h |
| 小计 | | 7.5h |

---

## 5. Phase 3: 质量提升

### 5.1 补充单元测试

**目标**: 提升测试覆盖率至 80%+

| 模块 | 测试文件 | 测试内容 | 优先级 | 工时 |
|------|----------|----------|--------|------|
| HttpRequestExecutor | HttpRequestExecutorTests.cs | 成功请求、超时、取消、错误处理 | P0 | 4h |
| CurlConverter | CurlConverterTests.cs | 导入各种 curl 格式、导出验证 | P0 | 3h |
| Reporters | ReporterTests.cs | Text/JUnit/JSON 输出格式验证 | P1 | 2h |

**HttpRequestExecutor 测试用例**:
```
- ExecuteAsync_ValidRequest_ReturnsSuccess
- ExecuteAsync_Timeout_ThrowsTimeoutException
- ExecuteAsync_Cancelled_ThrowsOperationCanceledException
- ExecuteAsync_NetworkError_ThrowsHttpRequestException
- ExecuteAsync_WithHeaders_SendsCorrectHeaders
- ExecuteAsync_WithBody_SendsCorrectBody
```

**CurlConverter 测试用例**:
```
- Parse_SimpleGet_ReturnsCorrectDefinition
- Parse_WithHeaders_ReturnsCorrectHeaders
- Parse_PostWithBody_ReturnsCorrectBody
- Parse_WithAuth_ReturnsCorrectAuthHeader
- Export_GetRequest_GeneratesValidCurl
- Export_PostWithJson_GeneratesValidCurl
```

### 5.2 代码重构优化

| 重构项 | 文件 | 内容 | 工时 |
|--------|------|------|------|
| 拆分长文件 | RequestEditorView.cs | 拆分为 EditorCore、FormEditor、TextEditor | 4h |
| 拆分长文件 | ResponsePanelView.cs | 拆分为 BodyPanel、HeadersPanel、AssertionsPanel | 3h |
| 错误处理 | 全局 | 移除空 catch，添加具体异常处理 | 2h |
| 代码清理 | 全局 | 移除未使用的 using、死代码 | 1h |

### 5.3 日志基础设施

**方案**: 使用 Microsoft.Extensions.Logging.Abstractions（AOT 友好）

```csharp
// Core 层接口
public static class LoggerFactory
{
    public static ILogger CreateLogger(string categoryName);
}

// GUI 层实现
services.AddSingleton<ILogger>(new FileLogger("resty.log"));
```

**日志输出位置**: `%APPDATA%\Resty\logs\resty-{date}.log`

---

## 6. Phase 4: 功能补全

### 6.1 动态变量实现（$uuid, $timestamp）

**位置**: Resty.Core/Environment/DynamicVariables.cs

**支持的动态变量**:

| 变量 | 格式 | 示例 |
|------|------|------|
| `$uuid` | UUID v4 | `550e8400-e29b-41d4-a716-446655440000` |
| `$timestamp` | Unix 时间戳（秒） | `1715234567` |
| `$isoTimestamp` | ISO 8601 | `2026-05-07T10:30:00Z` |
| `$randomInt` | 0-999999 | `123456` |

**实现方案**:

```csharp
public static class DynamicVariables
{
    private static readonly Dictionary<string, Func<string>> Resolvers = new()
    {
        ["$uuid"] = () => Guid.NewGuid().ToString(),
        ["$timestamp"] = () => DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
        ["$isoTimestamp"] = () => DateTimeOffset.UtcNow.ToString("o"),
        ["$randomInt"] = () => Random.Shared.Next(0, 1000000).ToString()
    };

    public static string? Resolve(string variable)
        => Resolvers.TryGetValue(variable, out var resolver) ? resolver() : null;
}
```

**集成点**: EnvironmentResolver.Resolve() 中添加动态变量检测

### 6.2 P10 - 新建 .http 文件

**UI 设计**:

```
菜单栏: [文件] → [新建 .http 文件]
快捷键: Ctrl+N

对话框:
┌─────────────────────────────┐
│ 新建 HTTP 请求文件           │
├─────────────────────────────┤
│ 文件名: [________].http     │
│ 位置:   [当前工作区 ▼]       │
│ 模板:   [空文件 ▼]           │
│        - 空文件              │
│        - GET 请求模板        │
│        - POST JSON 模板      │
├─────────────────────────────┤
│         [取消]  [创建]       │
└─────────────────────────────┘
```

**实现步骤**:
1. MainWindow 添加 File → New 菜单项
2. 创建 NewFileDialog 对话框组件
3. WorkspaceService 添加 CreateFile 方法
4. 新建后自动打开标签页

### 6.3 P14 - 响应增强

**功能列表**:

| 功能 | 说明 | 优先级 |
|------|------|--------|
| JSON 语法高亮 | 关键字、字符串、数字着色 | P1 |
| 响应过滤 | jq 风格的 JSON 查询 | P2 |
| 响应搜索 | Ctrl+F 搜索响应内容 | P2 |
| 复制响应 | 右键复制 / Ctrl+C | P1 |
| 响应大小提示 | 大文件警告 | P3 |

**JSON 高亮方案**（自实现，避免依赖）:

```csharp
public static class JsonHighlighter
{
    public static string Highlight(string json)
    {
        // 使用正则匹配：字符串、数字、布尔、null、key
        // 返回带 ANSI 颜色码的文本
    }
}
```

### 6.4 P16 - cURL 导入 UI

**入口设计**:

```
RequestEditorView 工具栏:
[Import curl] [Export curl ▼]

Import 对话框:
┌─────────────────────────────────┐
│ 导入 cURL 命令                   │
├─────────────────────────────────┤
│ curl --location 'https://...'   │
│                                 │
│                                 │
├─────────────────────────────────┤
│           [取消]  [导入]         │
└─────────────────────────────────┘
```

**Export 功能**:
- 按钮 → 剪贴板（自动复制 curl 命令）
- 下拉 → 保存为 .sh 文件

---

## 7. Phase 5: 体验优化

### 7.1 错误处理改进

| 场景 | 当前行为 | 改进方案 |
|------|----------|----------|
| 网络请求失败 | 空响应 | 显示错误面板（红色背景，错误信息） |
| 文件解析失败 | 静默失败 | 显示错误标记，悬停显示详情 |
| 变量未定义 | 原样显示 | 显示警告标记，悬停提示缺失变量 |
| JSON 格式错误 | 异常 | 显示格式错误位置 |

### 7.2 UI 细节打磨

| 项目 | 改进内容 |
|------|----------|
| 标签栏 | 添加中键关闭、右键菜单 |
| 侧边栏 | 添加文件搜索过滤 |
| 编辑器 | 添加行号、撤销/重做 |
| 响应面板 | 添加响应时间图表（历史） |
| 状态栏 | 添加请求队列指示器 |

### 7.3 性能优化

| 优化项 | 方法 |
|--------|------|
| 大文件加载 | 延迟解析、虚拟滚动 |
| 响应渲染 | 分块渲染、懒加载 JSON 树 |
| 文件监听 | 优化 debounce 策略 |
| 内存占用 | 标签页缓存 LRU 策略 |

---

## 8. Phase 6: 发布准备

### 8.1 文档完善

| 文档 | 内容 |
|------|------|
| README.md | 项目介绍、安装、快速开始 |
| CHANGELOG.md | 版本变更记录 |
| CONTRIBUTING.md | 贡献指南 |
| docs/UserGuide.md | 用户手册 |
| docs/ApiReference.md | .http 文件语法参考 |

### 8.2 发布构建

**构建脚本**: `build-release.cmd`

```batch
@echo off
dotnet publish src/Resty.Cli/ -c Release -r win-x64 --self-contained -p:PublishAot=true -p:StripSymbols=true -o artifacts/cli/
dotnet publish src/Resty.Gui/ -c Release -r win-x64 --self-contained -p:PublishAot=true -p:StripSymbols=true -o artifacts/gui/
```

**发布包结构**:
```
resty-v2.0.0-win-x64.zip
├── resty-cli.exe    (CLI 工具)
├── resty-gui.exe    (GUI 应用)
├── README.md
└── LICENSE
```

### 8.3 GitHub Release

- 版本标签: `v0.1.2`
- 发布说明: 功能列表、已知问题、升级指南
- 附件: ZIP 发布包、SHA256 校验和

---

## 9. 任务看板

### 9.1 Phase 1：侧边栏交互优化

| 任务 | 状态 | 负责人 | 预计工时 |
|------|------|--------|----------|
| 操作菜单按钮（需求2） | ⏳ 待开始 | - | 3h |
| 环境模式头部简化（需求3） | ⏳ 待开始 | - | 1h |
| http 文件新建请求菜单（需求4） | ⏳ 待开始 | - | 2.5h |
| 请求菜单精简（需求5） | ⏳ 待开始 | - | 0.5h |
| 环境变量重命名菜单（需求6） | ⏳ 待开始 | - | 2h |

**小计**: 9 小时

### 9.2 Phase 2：请求历史增强

| 任务 | 状态 | 负责人 | 预计工时 |
|------|------|--------|----------|
| HistoryEntry 数据模型扩展 | ⏳ 待开始 | - | 1h |
| 历史详情面板 UI 设计 | ⏳ 待开始 | - | 1.5h |
| 左右分栏布局（需求7） | ⏳ 待开始 | - | 1.5h |
| 摘要信息展示 | ⏳ 待开始 | - | 1h |
| 原始文本展示 | ⏳ 待开始 | - | 0.5h |
| 复制/打开按钮 | ⏳ 待开始 | - | 1h |
| MainWindow 集成 | ⏳ 待开始 | - | 1h |

**小计**: 7.5 小时

### 9.3 Phase 3：质量提升

| 任务 | 状态 | 负责人 | 预计工时 |
|------|------|--------|----------|
| HttpRequestExecutor 测试 | ⏳ 待开始 | - | 4h |
| CurlConverter 测试 | ⏳ 待开始 | - | 3h |
| Reporters 测试 | ⏳ 待开始 | - | 2h |
| RequestEditorView 重构 | ⏳ 待开始 | - | 4h |
| ResponsePanelView 重构 | ⏳ 待开始 | - | 3h |
| 错误处理改进 | ⏳ 待开始 | - | 2h |
| 日志基础设施 | ⏳ 待开始 | - | 3h |

**小计**: 21 小时

### 9.4 Phase 4：功能补全

| 任务 | 状态 | 负责人 | 预计工时 |
|------|------|--------|----------|
| 动态变量实现 | ⏳ 待开始 | - | 3h |
| P10 新建文件 UI | ⏳ 待开始 | - | 4h |
| P14 响应增强 | ⏳ 待开始 | - | 5h |
| P16 cURL 导入 UI | ⏳ 待开始 | - | 3h |

**小计**: 15 小时

### 9.5 Phase 5：体验优化

| 任务 | 状态 | 负责人 | 预计工时 |
|------|------|--------|----------|
| 错误处理 UI | ⏳ 待开始 | - | 3h |
| 标签栏增强 | ⏳ 待开始 | - | 2h |
| 侧边栏搜索 | ⏳ 待开始 | - | 2h |
| 性能优化 | ⏳ 待开始 | - | 4h |

**小计**: 11 小时

### 9.6 Phase 6：发布准备

| 任务 | 状态 | 负责人 | 预计工时 |
|------|------|--------|----------|
| 文档编写 | ⏳ 待开始 | - | 4h |
| 发布构建脚本 | ⏳ 待开始 | - | 1h |
| GitHub Release | ⏳ 待开始 | - | 1h |

**小计**: 6 小时

**总计工时**: 69.5 小时

---

## 10. 里程碑时间线

```
Week 1（9h）:     Phase 1 - 侧边栏交互优化
Week 2（7.5h）:   Phase 2 - 请求历史增强
Week 3-4（21h）:  Phase 3 - 质量提升
Week 5-6（15h）:  Phase 4 - 功能补全
Week 7（11h）:    Phase 5 - 体验优化
Week 8（6h）:     Phase 6 - 发布准备
```

**建议排期**: 8 周（每周 6-8 小时）

---

## 11. 用户需求映射

| 用户需求 | 对应 Phase | 优先级 | 状态 |
|----------|-----------|--------|------|
| 需求1：版本 0.1.2 | 发布 | P0 | 已采纳 |
| 需求2：操作菜单 | Phase 1 | P0 | 已采纳 |
| 需求3：环境模式简化 | Phase 1 | P0 | 已采纳 |
| 需求4：新建请求菜单 | Phase 1 | P1 | 已采纳 |
| 需求5：菜单精简 | Phase 1 | P1 | 已采纳 |
| 需求6：重命名菜单 | Phase 1 | P1 | 已采纳 |
| 需求7：历史详情展示 | Phase 2 | P0 | 已采纳 |

---

## 12. 任务看板（原有内容移至文档底部）

### Phase 1 旧内容（参考）

---

## 13. 风险与应对

| 风险 | 可能性 | 影响 | 应对措施 |
|------|--------|------|----------|
| MewUI API 变更 | 中 | 高 | 锁定版本 0.15.2，推迟升级 |
| 历史记录 JSON 兼容性 | 低 | 中 | 旧版本字段缺失时用默认值 |
| 跟随打开功能稳定性 | 低 | 低 | 充分测试 Tab 切换逻辑 |
| 功能蔓延 | 高 | 中 | 严格按需求范围，新功能延后 |

---

## 14. 后续规划（0.1.3+ 以后）

0.1.2 发布后可考虑的功能方向：

- **历史记录对比**: 响应 diff、趋势图表
- **脚本引擎**: 支持 JavaScript pre/post 脚本
- **OAuth2 支持**: 授权码流程、自动刷新 Token
- **跨平台**: macOS / Linux GUI（评估 AvaloniaUI）
- **插件系统**: 自定义断言、报告器扩展
- **团队协作**: 工作区共享、环境模板
