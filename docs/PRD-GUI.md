# PRD: Resty.Gui — 本地优先的 HTTP API 图形客户端

**Status**: Draft
**Author**: Alex (PM)
**Last Updated**: 2026-04-28
**Version**: 1.0
**依赖**: [PRD v1 (Resty.Core + Resty.Cli)](./PRD.md)
**Stakeholders**: 核心开发者

---

## 1. Problem Statement

CLI 解决了 CI/CD 和脚本化场景，但个人开发者在日常调试中需要更直观的交互：快速浏览集合中的请求、即时看到响应、在不离开工具的情况下编辑请求、对比多次响应。纯文本编辑器无法提供这些能力，而现有 GUI 工具（Postman/Insomnia）过重，依赖 Electron 和云服务。

**Resty.Gui 的目标：** 在保持文本/文件驱动核心的前提下，提供轻量、本地的 GUI 体验，底层直接复用 `Resty.Core`，不绕过 CLI。

---

## 2. 产品定位

> Resty.Gui 是 Resty.Core 的 GUI 前端，使用 MewUI (.NET) 构建，VS Code 式布局，支持集合管理、双模式请求编辑、结构化响应查看。

**设计原则：**
- **文件是真相源**：GUI 操作最终反映为 `.http` 文件的变更，不引入私有数据库
- **Core 是唯一执行层**：GUI 直接调用 `Resty.Core` 库，不通过 CLI 进程
- **布局参考 VS Code**：左侧文件树 + 右侧多标签编辑区，有经验的开发者零学习成本

---

## 3. Goals & Success Metrics

| Goal | Metric | Target |
|------|--------|--------|
| 从打开集合到发送第一个请求 | 操作步骤 | ≤ 3 步 |
| 轻量启动 | 冷启动时间 | < 1s（NativeAOT） |
| 文件双向同步 | 文件修改后 GUI 感知 | 外部编辑器保存后 ≤ 1s 内刷新 |
| 单 EXE 分发 | 可执行文件体积 | Windows x64 < 20MB |

---

## 4. Non-Goals（V2 明确不做）

- **历史记录**：响应历史对比是独立功能，延后到 V3
- **脚本引擎**：`> {% %}` 依然只支持简化断言 DSL，不执行 JavaScript
- **OAuth2 / API Key**：与 CLI V1 保持一致，V3 评估
- **多人协作**：无评论、共享、实时同步
- **插件系统**：不做扩展机制
- **Mac / Linux GUI**：与 CLI 同步，Windows 优先

---

## 5. 整体布局

参考 VS Code 布局，分为三个区域：

```
┌─────────────────────────────────────────────────────────┐
│  菜单栏  [文件] [视图] [运行] [帮助]                        │
├──────────────┬──────────────────────────────────────────┤
│              │  [Tab: GET /users] [Tab: POST /users] [+] │
│  集合侧边栏   ├──────────────────────────────────────────┤
│              │                                          │
│  ▶ 📁 users  │         请求编辑区                        │
│    ├ Get...  │                                          │
│    └ Post... ├──────────────────────────────────────────┤
│  ▶ 📁 orders │                                          │
│              │         响应面板                          │
│              │                                          │
├──────────────┴──────────────────────────────────────────┤
│  状态栏  [集合名称]  [环境: dev ▼]  [200 OK  45ms  1.2KB] │
└─────────────────────────────────────────────────────────┘
```

---

## 6. 用户故事与验收标准

### Story 1：管理视图 — 管理多个工作区

作为开发者，我想管理多个本地文件夹（工作区），并快速切换到某一个进入工作状态。

**触发入口：** 应用启动时无集合时显示；或菜单 `文件 → 管理工作区`

**Acceptance Criteria：**
- [ ] 显示已添加的工作区列表（名称 + 路径 + 最后打开时间）
- [ ] 可添加本地文件夹作为工作区（系统文件夹选择对话框）
- [ ] 可从列表中移除工作区（不删除本地文件，仅移除记录）
- [ ] 点击工作区 → 进入集合视图（同一时间只打开一个工作区）
- [ ] 工作区列表持久化到本地配置文件

---

### Story 2：集合视图 — 浏览集合

作为开发者，我想在侧边栏看到当前工作区内所有 `.http` 文件的树形结构，并快速定位到某个请求。

**Acceptance Criteria：**
- [ ] 侧边栏显示工作区文件树，仅展示 `.http` 文件和目录
- [ ] `.http` 文件节点可展开，显示文件内所有具名请求（`### RequestName`）
- [ ] 点击请求名称 → 在新标签页打开（若已打开则切换到该标签）
- [ ] 支持右键菜单：在资源管理器中打开、在外部编辑器中打开
- [ ] 外部修改文件后，侧边栏自动刷新（文件系统监听，≤ 1s）
- [ ] 无 `### Name` 的匿名请求显示为 `[未命名]`，可点击打开

---

### Story 3：多标签请求编辑区

作为开发者，我想同时打开多个请求，通过标签页快速切换，不丢失编辑状态。

**Acceptance Criteria：**
- [ ] 标签栏显示请求名称 + HTTP 方法标签（颜色区分：GET蓝、POST绿、PUT橙、DELETE红）
- [ ] 支持关闭单个标签（有未保存修改时提示确认）
- [ ] 支持关闭所有标签
- [ ] 标签有未保存修改时显示 `●` 标记
- [ ] 标签页内容在切换时保持状态（不重置）

---

### Story 4：双模式请求编辑器

作为开发者，我想根据场景选择用表单还是文本方式编辑请求，两种模式实时同步。

**编辑区上方有模式切换按钮：`[结构化] [文本]`**

#### 4a. 结构化模式（Form Mode）

| 区域 | 内容 |
|------|------|
| 请求行 | HTTP 方法下拉 + URL 输入框 + `发送` 按钮 |
| Params Tab | Query String 的 Key-Value 表格，勾选框控制启用/禁用 |
| Headers Tab | Header 的 Key-Value 表格，勾选框控制启用/禁用 |
| Auth Tab | 类型选择（None / Basic / Bearer）+ 对应输入框 |
| Body Tab | Content-Type 选择 + Body 输入区（raw text / JSON 编辑器） |
| Assertions Tab | 断言列表，可视化增删 `assert` 规则 |

**Acceptance Criteria（结构化模式）：**
- [ ] Method 下拉支持：GET / POST / PUT / PATCH / DELETE / HEAD / OPTIONS
- [ ] URL 支持 `{{variable}}` 高亮
- [ ] Header/Params 表格支持增行、删行、拖拽排序
- [ ] Auth 选择 Basic → 显示 Username/Password 输入框，自动生成 `Authorization` Header
- [ ] Auth 选择 Bearer → 显示 Token 输入框，自动生成 `Authorization: Bearer` Header
- [ ] Body 选择 JSON → 提供 JSON 语法高亮和简单格式校验

#### 4b. 文本模式（Text Mode）

直接显示 `.http` 文件中该请求的原始文本内容，可自由编辑。

**Acceptance Criteria（文本模式）：**
- [ ] 内容为当前请求在 `.http` 文件中对应的原始文本段（从 `###` 到下一个 `###`）
- [ ] 提供语法高亮：HTTP 方法、Header 名、变量 `{{...}}`、断言块
- [ ] 编辑内容实时写回 `.http` 文件（或标记 dirty，Ctrl+S 保存）

#### 4c. 双模式同步

**Acceptance Criteria：**
- [ ] 从结构化模式切换到文本模式：将表单状态序列化为 `.http` 语法展示
- [ ] 从文本模式切换到结构化模式：解析文本，填充表单字段
- [ ] 解析失败时（文本格式错误），提示错误，不强制切换，保留文本模式

---

### Story 5：发送请求与响应展示

作为开发者，我想点击发送后立即看到请求结果，结果按类型分区展示。

**Acceptance Criteria：**
- [ ] 点击 `发送` 后，按钮变为 `取消`，可中止请求
- [ ] 请求发送中显示 loading 状态
- [ ] 响应区分为三个 Tab：**Body** | **Headers** | **Assertions**

**Body Tab：**
- [ ] 子切换：`原始` / `JSON 树`
- [ ] 原始模式：等宽字体，完整响应体文本，支持滚动
- [ ] JSON 树模式：可折叠/展开的树形结构，叶子节点显示类型和值
- [ ] 非 JSON 响应时，JSON 树 Tab 置灰不可用

**Headers Tab：**
- [ ] 显示响应头的 Key-Value 表格
- [ ] 包含伪头：`:status`（状态码）、`:time`（耗时 ms）、`:size`（响应体字节数）

**Assertions Tab：**
- [ ] 仅在请求含 `> {% %}` 断言块时激活
- [ ] 每条断言显示：规则文本 + 通过/失败图标 + 失败时的实际值
- [ ] 全部通过显示绿色汇总；任意失败显示红色汇总 + 失败条数

**状态栏更新：**
- [ ] 请求完成后状态栏显示：状态码（颜色标记 2xx绿/4xx橙/5xx红）+ 耗时 + 响应体大小

---

### Story 6：环境切换

作为开发者，我想在 GUI 中切换当前工作区的环境变量，立即生效于后续发送的请求。

**Acceptance Criteria：**
- [ ] 状态栏右侧显示当前环境名称，点击弹出下拉菜单
- [ ] 下拉列表来源：当前工作区 `http-client.env.json` 的顶层 key
- [ ] 切换环境后，URL 和编辑区内 `{{variable}}` 占位符实时更新预览值（工具提示）
- [ ] 变量无法解析时（当前环境无该变量），`{{variable}}` 显示警告色

---

## 7. 技术架构

### 项目结构

```
Resty/
├── src/
│   ├── Resty.Core/          # 核心引擎库（V1 已定义）
│   ├── Resty.Cli/           # CLI（V1 已定义）
│   └── Resty.Gui/           # GUI 项目（本 PRD 范围）
│       ├── App.cs           # Application 入口
│       ├── Views/
│       │   ├── MainWindow.cs
│       │   ├── ManageWorkspacesView.cs
│       │   └── RequestTabView.cs
│       ├── ViewModels/      # 状态管理（ObservableValue）
│       └── Services/
│           ├── WorkspaceService.cs    # 工作区持久化
│           ├── FileWatcherService.cs  # 文件系统监听
│           └── CoreBridge.cs         # Resty.Core 调用封装
└── tests/
    ├── Resty.Core.Tests/
    └── Resty.Gui.Tests/
```

### GUI 与 Core 的关系

```
Resty.Gui
  └── 直接引用 Resty.Core（项目引用，非进程调用）
      ├── IHttpFileParser      → 解析 .http 文件
      ├── IHttpRequestExecutor → 执行请求
      ├── IAssertionEngine     → 执行断言
      └── IEnvironmentResolver → 解析变量
```

### 状态管理

使用 MewUI 的 `ObservableValue<T>` 进行绑定，无反射，NativeAOT 友好：

```csharp
// 示例：环境选择绑定
var currentEnv = new ObservableValue<string>("dev");
var envDropdown = new ComboBox()
    .BindSelectedItem(currentEnv);
var urlPreview = new Label()
    .BindText(currentEnv, env => resolver.Resolve(rawUrl, env));
```

### 工作区配置持久化

工作区列表存储在用户配置目录，与工作区文件本身分离：

```
%APPDATA%\Resty\workspaces.json
```

```json
{
  "workspaces": [
    {
      "name": "My API Project",
      "path": "D:\\Code\\my-api",
      "lastOpened": "2026-04-28T10:00:00Z"
    }
  ],
  "lastActiveWorkspace": "D:\\Code\\my-api"
}
```

### 风险登记

| 风险 | 可能性 | 影响 | 缓解措施 |
|------|--------|------|---------|
| MewUI 控件 API 变更 | High（Experimental） | High | 封装 `ViewFactory` 层，隔离 MewUI 直接调用，降低变更波及范围 |
| 文本↔结构化双模式同步 bug | Medium | Medium | 单独建集成测试：给定 `.http` 文本，验证往返转换（text→form→text）无损 |
| 文件系统监听在 Windows 上的性能 | Low | Low | 使用 `FileSystemWatcher`，限制监听范围为工作区根目录 |
| NativeAOT 下 MewUI 渲染异常 | Medium | High | V2 早期做 AOT spike，确认 MewUI Gallery demo 能 AOT 编译通过 |

---

## 8. 发布里程碑

| 里程碑 | 目标 | 通过标准 |
|--------|------|---------|
| **G0 — AOT Spike** ✅ | 验证 MewUI + NativeAOT 可行性 | MewUI Gallery 能 AOT 编译并运行，无崩溃。**实测：resty-gui.exe 1.5 MB，窗口正常显示（2026-04-28）** |
| **G1 — 框架搭建** | MainWindow 布局 + 侧边栏 + 标签页空壳 | 能打开工作区，侧边栏显示文件树 |
| **G2 — 请求执行** | 文本模式编辑 + 发送 + 响应展示（Raw + Headers） | 能发送 GET/POST，看到响应 |
| **G3 — 结构化模式** | Form Mode + 双模式同步 | 结构化↔文本往返转换无损 |
| **G4 — 完整功能** | 断言展示 + 环境切换 + JSON 树 | 所有 Story 验收标准通过 |
| **V2 GA** | NativeAOT 单 EXE < 20MB | Windows 冷启动 < 1s |

---

## 9. 决策记录

| # | 决策 | 日期 | 理由 |
|---|------|------|------|
| D1 | GUI 直接调用 Core 库，不通过 CLI 进程 | 2026-04-28 | 响应体验好，避免进程间通信、输出解析复杂度 |
| D2 | 布局参考 VS Code（侧边栏 + 多标签） | 2026-04-28 | 目标用户熟悉 VS Code，零学习成本 |
| D3 | 双模式编辑器（结构化 + 文本） | 2026-04-28 | 快速调试用结构化，精细控制用文本，互不排斥 |
| D4 | 文件是真相源，不引入私有数据库 | 2026-04-28 | 保持 Git 友好，与 CLI 共享同一套文件格式 |
| D5 | 历史记录延后到 V3 | 2026-04-28 | 避免 V2 范围过大，优先验证核心体验 |
| D6 | 现在启动，接受 MewUI Experimental 风险 | 2026-04-28 | 用 G0 AOT Spike 提前暴露风险，封装层隔离变更 |
| D7 | 工作区配置存 `%APPDATA%\Resty\` | 2026-04-28 | 与工作区文件本身解耦，不污染项目目录 |

---

## 10. 与 V1 的边界

| 层 | V1（已定义） | V2（本 PRD） |
|----|-------------|-------------|
| `Resty.Core` | 解析、执行、断言、报告 | **不变**，GUI 直接复用 |
| `Resty.Cli` | CLI 命令 | **不变**，独立可用 |
| `Resty.Gui` | 不存在 | **新增**，本 PRD 全部范围 |

---

## 11. 参考资料

- [PRD v1 (Resty.Core + CLI)](./PRD.md)
- [MewUI GitHub](https://github.com/aprillz/MewUI)
- [MewUI C# Markup 文档](https://github.com/aprillz/MewUI/blob/main/docs/CSharpMarkup.md)
- [MewUI Binding 文档](https://github.com/aprillz/MewUI/blob/main/docs/Binding.md)
- [JetBrains HTTP Client 语法](https://www.jetbrains.com.cn/en-us/help/idea/exploring-http-syntax.html)
