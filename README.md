# Resty

> **把 `.http` 文件作为唯一事实来源** —— 在编辑器里写请求，在终端里跑测试，GUI 和 CLI 共享同一套文件格式，零锁定，随时可迁移。

Resty 是一个探索性项目，目标是验证一个观点：**HTTP 请求不应该被锁进某个 GUI 工具的私有数据库**。请求文件是纯文本，可以 Git 追踪、可以 Code Review、可以直接用 CLI 在 CI 流水线里跑测试。

---

## 与主流工具的本质区别

| | Resty | Postman / Insomnia |
|---|---|---|
| 数据存储 | 普通 `.http` 文本文件，Git 友好 | 私有数据库 / 云端账号 |
| CI/CD 集成 | `resty test dir/` 一行命令，JUnit 报告 | 需要 Newman / Inso CLI，配置繁琐 |
| 协作方式 | PR + Code Review | 团队共享工作区（需付费） |
| 离线使用 | 完全本地，无需登录 | 部分功能需联网 |
| GUI 框架 | MewUI（NativeAOT 友好，极小体积） | Electron（100+ MB） |
| 断言定义 | 内联写在 `.http` 文件里 | 在工具界面单独配置 |

---

## 核心设计探索

### 1. GUI 与 CLI 共享文件格式

`.http` 文件既是 GUI 的编辑对象，也是 CLI 的执行对象。在 GUI 里编辑完按 Ctrl+S，立刻可以用 CLI 跑：

```powershell
resty test api/users.http --env prod --report junit --output report.xml
```

### 2. 断言内联在请求文件里

断言不在工具里单独配置，而是直接写在 `.http` 文件的请求块后面，随代码一起进入 Git：

```http
### 创建用户
POST {{baseUrl}}/users
Content-Type: application/json

{ "name": "Alice" }

> {%
assert status == 201
assert responseTime < 500
assert body.$.id != ""
assert header.Location contains /users/
%}
```

### 3. 文本模式 ↔ 结构化模式双向同步

不强迫用户选择"表单派"或"文本派"。两种模式实时互转，底层统一序列化回 `.http` 格式——探索在同一份源文件上提供不同粒度编辑体验的可能性。

### 4. NativeAOT CLI

CLI 支持 NativeAOT 单文件发布，启动时间 < 50 ms，适合作为 Git Hook 或 CI Step 内嵌到任意脚手架，无需安装 .NET 运行时。

---

## 快速上手

**前置要求：** .NET 10 SDK

```powershell
# 启动 GUI
dotnet run --project src/Resty.Gui/

# CLI：执行请求
dotnet run --project src/Resty.Cli/ -- run samples/smoke.http --env dev

# CLI：断言测试（失败时退出码 1）
dotnet run --project src/Resty.Cli/ -- test samples/smoke.http --env dev --report junit

# NativeAOT 单文件发布（需 VS Build Tools）
.\publish-aot.cmd
```

---

## GUI 功能

基于 [MewUI](https://github.com/Aprillz/MewUI)（非 Electron，原生 Windows 渲染）构建，VS Code 暗色风格。

| 功能 | 说明 |
|------|------|
| **工作区** | 打开本地文件夹，自动扫描 `.http` 文件，外部编辑自动刷新（600 ms 防抖） |
| **多标签** | 同时编辑多个请求，Dirty 状态 ● 实时标记未保存变更 |
| **双模式编辑** | 文本模式（原始 HTTP）↔ 结构化模式（Params / Headers / Auth / Body / Assertions）双向同步 |
| **语法提示** | 文本模式下实时解析并在提示栏展示 `METHOD URL`，快速确认语法正确性 |
| **环境变量** | 状态栏切换环境，`{{var}}` 在 URL 栏实时预览解析结果（已解析绿色 / 未知变量红色） |
| **发送 & 取消** | 异步执行，支持中途取消，响应摘要：状态码 + 耗时 + Body 体积 |
| **JSON 树视图** | 响应 Body 可在原始文本 / 可折叠 JSON 树之间切换 |
| **Ctrl+S 写回** | 编辑内容精确写回 `.http` 文件对应请求块，其他请求块不受影响 |
| **右键菜单** | 文件节点：在资源管理器中显示 / 复制路径；请求节点：打开 / 复制名称 |

---

## CLI 功能

```
resty run  <file|dir> [选项]    # 执行请求，打印响应
resty test <file|dir> [选项]    # 执行并校验断言，失败返回退出码 1
```

| 选项 | 说明 |
|------|------|
| `--env <name>` | 指定环境（读取 `http-client.env.json`） |
| `--request <name>` | 按名称过滤请求（部分匹配，不区分大小写） |
| `--report text\|json\|junit` | 输出格式（默认彩色文本） |
| `--output <file>` | 将报告写入文件 |
| `--timeout <ms>` | 超时毫秒数 |
| `--verbose` | 打印完整请求详情 |
| `--no-color` | 禁用 ANSI 彩色（适合重定向到文件） |

退出码：`0` 全部成功 / `1` 断言失败 / `2` 网络错误 / `3` 参数/解析错误

---

## .http 文件格式

完全兼容 JetBrains HTTP Client / VS Code REST Client。

```http
# 文件级变量
@baseUrl = https://api.example.com

### 获取用户信息
GET {{baseUrl}}/users/1
Authorization: Bearer {{token}}

> {%
assert status == 200
assert responseTime < 2000
assert body.$.data.name == "Alice"
assert header.Content-Type contains application/json
%}

### 创建用户
POST {{baseUrl}}/users
Content-Type: application/json

{
  "name": "Alice",
  "email": "alice@example.com"
}
```

**支持的断言主语：**

```
assert status == 200                        # HTTP 状态码
assert status in [200, 201]                 # 枚举匹配
assert responseTime < 500                   # 响应时间（ms）
assert body.$.items[0].id != ""             # JSONPath
assert header.Content-Type contains json    # 响应头
```

---

## 环境变量

```
项目目录/
├── http-client.env.json          # 提交到 Git（非敏感配置）
└── http-client.private.env.json  # 加入 .gitignore（敏感凭证）
```

```json
// http-client.env.json
{
  "dev":  { "baseUrl": "http://localhost:3000" },
  "prod": { "baseUrl": "https://api.example.com" }
}

// http-client.private.env.json
{
  "dev":  { "token": "dev-secret" },
  "prod": { "token": "prod-secret" }
}
```

优先级：`private.env.json` > `env.json` > 文件级 `@variable`

---

## 项目结构

```
Resty/
├── src/
│   ├── Resty.Core/      # 核心库（无 GUI 依赖，可独立引用）
│   │   ├── Parsing/     # .http 解析器
│   │   ├── Execution/   # HTTP 执行器
│   │   ├── Assertions/  # 断言引擎（status / responseTime / JSONPath / header）
│   │   ├── Environment/ # 环境变量加载与 {{var}} 替换
│   │   └── Reporting/   # 报告格式（Text / JSON / JUnit XML）
│   ├── Resty.Cli/       # CLI 入口（NativeAOT 可选）
│   └── Resty.Gui/       # Windows GUI（MewUI，非 Electron）
├── samples/
│   └── smoke.http       # 示例 + 断言
└── docs/                # 设计文档与 PRD
```

---

## 技术选型说明

| 组件 | 选择 | 原因 |
|------|------|------|
| GUI 框架 | MewUI 0.15.2 | 原生 Windows 渲染，NativeAOT 友好，非 Electron |
| 目标框架 | .NET 10 | 最新 LTS，AOT 成熟度最佳 |
| CLI 发布 | NativeAOT 单文件（可选） | 无运行时依赖，适合 CI 环境嵌入 |
| 文件格式 | JetBrains `.http` | 开放格式，工具生态广，避免私有锁定 |
