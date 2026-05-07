# Resty 项目分析报告

**生成日期**: 2026-05-07
**分析范围**: 完整代码库
**版本基准**: develop 分支 (d70a8f2)

---

## 1. 项目概览

### 1.1 项目定位

Resty 是一个**本地优先、文本驱动**的 HTTP API 客户端，核心特点：

- **文件即真相源**: `.http` 文件（JetBrains HTTP 语法）是唯一数据源，Git 友好
- **三端合一**: Core 库 + CLI 工具 + GUI 应用，共享同一解析/执行引擎
- **NativeAOT 分发**: 单 EXE，无需 .NET Runtime，Windows x64 < 20MB

### 1.2 技术栈

| 层 | 技术选型 | 说明 |
|----|----------|------|
| 语言 | C# / .NET 10 | 最新稳定版 |
| GUI 框架 | MewUI 0.15.2 | 原生 Direct2D 渲染，非 Electron |
| HTTP 客户端 | System.Net.Http.HttpClient | 内置，AOT 友好 |
| JSON 处理 | System.Text.Json | 内置，高性能 |
| CLI | System.CommandLine | 微软官方 |
| 测试 | xUnit 2.9 | 主流框架 |
| 分发 | NativeAOT | 单文件可执行 |

---

## 2. 项目结构

```
Resty/
├── src/
│   ├── Resty.Core/          # 核心库（零依赖）
│   ├── Resty.Cli/           # CLI 工具
│   └── Resty.Gui/           # GUI 应用
├── tests/
│   └── Resty.Core.Tests/    # 单元测试
├── docs/
│   ├── PRD.md               # Core + CLI 产品规格
│   ├── PRD-GUI.md           # GUI 产品规格
│   └── UI-Design.md         # UI 设计系统
├── samples/                 # 示例 .http 文件
└── artifacts/               # 构建输出
```

### 2.1 代码统计

| 项目 | 文件数 | 代码行数（约） | 职责 |
|------|--------|----------------|------|
| Resty.Core | 14 | ~1,500 | 解析、执行、断言、报告 |
| Resty.Cli | 1 | ~200 | 命令行入口 |
| Resty.Gui | 12 | ~3,000 | 图形界面 |
| Resty.Core.Tests | 4 | ~600 | 单元测试 |
| **合计** | **31** | **~5,300** | - |

---

## 3. 核心模块分析

### 3.1 Resty.Core — 核心引擎

**设计原则**: 零外部依赖，纯 BCL，NativeAOT 友好

| 模块 | 关键类 | 完成度 | 说明 |
|------|--------|--------|------|
| Parsing | HttpFileParser | ✅ 完成 | 状态机解析，支持 `###`、`@变量`、Headers、Body、Assertions |
| Parsing | AssertionParser | ✅ 完成 | 断言 DSL 解析：status、body.$、header[]、responseTime |
| Parsing | CurlConverter | ✅ 完成 | curl 命令导入/导出 |
| Execution | HttpRequestExecutor | ✅ 完成 | HttpClient 封装，支持取消、超时 |
| Assertions | AssertionEngine | ✅ 完成 | 断言求值引擎 |
| Assertions | JsonPathHelper | ✅ 完成 | JSONPath 子集实现 ($.a.b, $[0]) |
| Environment | EnvironmentResolver | ✅ 完成 | 变量解析，支持 public/private 环境叠加 |
| Reporting | TextReporter | ✅ 完成 | ANSI 彩色控制台输出 |
| Reporting | JUnitReporter | ✅ 完成 | JUnit XML 格式（CI/CD 集成） |
| Reporting | JsonReporter | ✅ 完成 | JSON 机器可读格式 |

**代码模式**:
- 静态类用于无状态操作（Parser、Engine）
- `record` 类型用于不可变数据模型
- 枚举用于断言类型/操作符

### 3.2 Resty.Cli — 命令行工具

**完成度**: ✅ V1 功能完整

| 功能 | 状态 | 说明 |
|------|------|------|
| `resty run <target>` | ✅ | 执行请求，输出响应 |
| `resty test <target>` | ✅ | 执行断言，输出报告 |
| 目标格式 | ✅ | 文件 / 文件#请求 / 目录 |
| `--env` | ✅ | 环境切换 |
| `--report` | ✅ | text / junit / json |
| `--timeout` | ✅ | 请求超时 |
| 退出码 | ✅ | 0(成功) / 1(断言失败) / 2(网络错误) / 3(配置错误) |
| NativeAOT | ✅ | 单 EXE 发布 |

### 3.3 Resty.Gui — 图形界面

**完成度**: 🔄 V2 开发中，核心功能已完成

#### 布局架构（VS Code 风格）

```
┌─────────────────────────────────────────────────────┐
│ 标题栏（自定义 DWM chrome）                           │
├──────────┬──────────────────────────────────────────┤
│ Activity │ Sidebar  │  Main Area                    │
│ Bar      │          │  ┌─────────────────────────┐  │
│          │          │  │ Tab Bar                 │  │
│ 📁       │ File     │  ├─────────────────────────┤  │
│ 🔍       │ Tree     │  │ Editor / Response       │  │
│ ⚙️       │          │  │                         │  │
│          │          │  └─────────────────────────┘  │
└──────────┴──────────┴──────────────────────────────┘
```

#### 视图组件

| 组件 | 代码行数 | 完成度 | 说明 |
|------|----------|--------|------|
| MainWindow | 640 | ✅ | 窗口管理、标签生命周期 |
| NativeCustomWindow | 269 | ✅ | 自定义标题栏、DWM 边框 |
| SidebarView | 419 | ✅ | 文件树 + 环境列表 |
| RequestEditorView | 835 | ✅ | 双模式编辑器（文本/结构化） |
| ResponsePanelView | 685 | ✅ | 响应展示（Body/Headers/Assertions） |
| EnvManagerView | 242 | ✅ | 环境变量编辑 |
| WorkspacePanelView | 119 | ✅ | 最近工作区 |
| HistoryPanelView | 259 | ✅ | 请求历史 |
| SettingsView | 167 | ✅ | 设置面板 |

#### 服务层

| 服务 | 职责 |
|------|------|
| WorkspaceService | 文件扫描、FileSystemWatcher（600ms debounce）、缓存 |
| RecentWorkspacesService | 最近工作区持久化（%APPDATA%\Resty\） |
| SettingsService | 应用设置持久化 |

#### PRD-GUI 功能清单

| 里程碑 | 状态 | 说明 |
|--------|------|------|
| G0 — AOT Spike | ✅ 完成 | MewUI + NativeAOT 验证，1.5MB exe |
| G1 — 框架搭建 | ✅ 完成 | 窗口布局、侧边栏、标签页 |
| G2 — 请求执行 | ✅ 完成 | 文本编辑、发送、响应展示 |
| G3 — 结构化模式 | ✅ 完成 | Form Mode、双模式同步 |
| G4 — 完整功能 | ✅ 完成 | 断言展示、环境切换、JSON 树 |

| P 系列 | 状态 | 说明 |
|--------|------|------|
| P1-P9 | ✅ 完成 | 图标、快捷键、Activity Bar、环境面板等 |
| P10 | ⏳ 待定 | 新建 .http 文件 |
| P11 | ✅ 完成 | 请求历史 |
| P12 | ✅ 完成 | 设置页面 |
| P13 | 🔄 部分 | 环境变量注入（URL 预览可用） |
| P14 | ⏳ 待定 | 响应增强（语法高亮优化） |
| P15 | ✅ 完成 | 标签状态缓存 |
| P16 | 🔄 部分 | cURL 导入/导出（Core 完成，缺 UI） |

---

## 4. 测试覆盖

### 4.1 测试分布

| 模块 | 测试文件 | 测试用例数 | 覆盖评估 |
|------|----------|------------|----------|
| HttpFileParser | HttpFileParserTests.cs | ~10 | ✅ 良好 |
| AssertionParser | AssertionParserTests.cs | ~14 | ✅ 良好 |
| AssertionEngine | AssertionEngineTests.cs | ~16 | ✅ 良好 |
| EnvironmentResolver | EnvironmentResolverTests.cs | ~10 | ✅ 良好 |
| HttpRequestExecutor | - | 0 | ❌ 缺失 |
| CurlConverter | - | 0 | ❌ 缺失 |
| Reporters | - | 0 | ❌ 缺失 |

**总计**: ~50 个测试用例

### 4.2 测试建议

1. **HttpRequestExecutor**: 需要 HttpClient 抽象层或 Mock
2. **CurlConverter**: 复杂解析逻辑，建议添加边界用例
3. **Reporters**: 输出格式验证，可添加快照测试

---

## 5. 代码质量评估

### 5.1 优点

- ✅ 现代化 C# 特性：record、nullable、pattern matching
- ✅ 清晰的分层架构：Core → Cli / Gui
- ✅ 零外部依赖的 Core 库
- ✅ NativeAOT 友好设计（InvariantGlobalization、Source Generator）
- ✅ 完善的 PRD 文档

### 5.2 待改进

- ⚠️ 部分文件较长（RequestEditorView.cs 835 行）
- ⚠️ 错误处理有空 catch 块
- ⚠️ 缺少日志基础设施
- ⚠️ HttpRequestExecutor 和 CurlConverter 缺少测试
- ⚠️ 动态变量（$uuid, $timestamp）未实现

---

## 6. 构建与发布

### 6.1 构建命令

```bash
# 开发运行
dotnet run --project src/Resty.Gui/

# CLI 运行
dotnet run --project src/Resty.Cli/ -- run samples/smoke.http --env dev

# 测试
dotnet test tests/Resty.Core.Tests/

# NativeAOT 发布
.\publish-aot.cmd
```

### 6.2 发布产物

| 项目 | 目标框架 | 输出类型 | 预期体积 |
|------|----------|----------|----------|
| Resty.Cli | net10.0 | NativeAOT EXE | < 10MB |
| Resty.Gui | net10.0-windows | NativeAOT WinExe | < 20MB |

---

## 7. 风险与建议

### 7.1 技术风险

| 风险 | 等级 | 缓解措施 |
|------|------|----------|
| MewUI API 变更 | 高 | 版本锁定，封装隔离层 |
| 测试覆盖不足 | 中 | 优先补充 Executor/Converter 测试 |
| JSONPath 功能受限 | 低 | 当前子集满足基本断言需求 |

### 7.2 功能缺口

| 缺口 | 优先级 | 建议 |
|------|--------|------|
| 动态变量 $uuid/$timestamp | 中 | Core 层实现，CLI/GUI 共享 |
| 新建 .http 文件 UI | 中 | GUI 层添加 File → New 菜单 |
| cURL 导入 UI | 低 | 添加 Import 按钮，调用 CurlConverter |
| 响应语法高亮 | 低 | 可引入轻量级高亮库或自实现 |
| 日志系统 | 中 | 添加 ILogger 抽象，支持诊断 |

---

## 8. 总结

Resty 项目是一个**架构清晰、文档完善、核心功能完整**的 HTTP API 客户端。

**当前状态**:
- Core + CLI 已达到 V1 发布标准
- GUI 核心功能完成，处于 V2 迭代阶段
- 测试覆盖需要补充（Executor、Converter、Reporters）

**推荐下一步**:
1. 补充 HttpRequestExecutor 和 CurlConverter 的单元测试
2. 完成 P10（新建文件）、P14（响应增强）、P16（cURL UI）
3. 实现动态变量（$uuid、$timestamp）
4. 添加日志基础设施
5. 准备 V2 正式发布
