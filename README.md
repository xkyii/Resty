# Resty

本地优先的 HTTP API 客户端，同时提供桌面 GUI 和命令行两种使用方式，完全兼容 JetBrains `.http` 文件格式。

---

## 功能概览

### GUI（`resty-gui`）

基于 [MewUI](https://github.com/Aprillz/MewUI) 构建的 Windows 桌面客户端，VS Code 暗色主题风格。

| 功能 | 说明 |
|------|------|
| **工作区管理** | 打开本地文件夹，自动扫描所有 `.http` 文件 |
| **侧边栏** | 文件树 + 请求列表，支持搜索过滤、右键菜单（资源管理器定位、复制路径/名称） |
| **多标签编辑** | 同时打开多个请求，标签栏切换，Dirty 状态（●）实时提示 |
| **文本模式** | 原始 HTTP 语法编辑，语法摘要提示栏（方法 + URL 实时解析） |
| **结构化模式** | Params / Headers / Auth（None/Basic/Bearer）/ Body / Assertions 分 Tab 填写 |
| **环境变量** | 读取 `http-client.env.json` / `http-client.private.env.json`，状态栏切换环境，URL 变量 `{{var}}` 实时预览 |
| **发送与取消** | 异步执行，30 秒超时，支持中途取消（`■ 取消` 按钮 + CancellationToken） |
| **响应展示** | 状态码 + 耗时 + Body 大小摘要；Body 原始/JSON 树双视图（可折叠展开） |
| **响应断言** | 断言结果内联展示（✓ / ✗） |
| **写回文件** | Ctrl+S 保存，将编辑内容写回 `.http` 文件对应请求块 |
| **文件监听** | 工作区文件变更（外部编辑）自动刷新侧边栏（600 ms 防抖） |

### CLI（`resty`）

零依赖命令行工具，支持 NativeAOT 单文件发布。

```
resty run  <file|dir> [options]   # 执行请求，打印响应
resty test <file|dir> [options]   # 执行并校验断言，失败返回退出码 1
```

**常用选项：**

| 选项 | 说明 |
|------|------|
| `--env <name>` | 指定环境名（读取 env.json） |
| `--request <name>` | 按名称过滤请求（大小写不敏感，支持部分匹配） |
| `--report text\|json\|junit` | 输出格式（默认彩色文本） |
| `--output <file>` | 将报告写入文件 |
| `--timeout <ms>` | 超时毫秒数 |
| `--verbose` | 打印请求详情 |
| `--no-color` | 禁用 ANSI 彩色输出 |

**退出码：**

| 码 | 含义 |
|----|------|
| `0` | 全部成功 |
| `1` | 存在失败断言（`test` 模式） |
| `2` | 网络/传输错误 |
| `3` | 参数错误或文件解析失败 |

---

## .http 文件格式

与 JetBrains HTTP Client / VS Code REST Client 兼容。

```http
# 文件级变量
@baseUrl = https://api.example.com

### 获取用户信息
GET {{baseUrl}}/users/1
Authorization: Bearer {{token}}
Accept: application/json

> {%
assert status == 200
assert responseTime < 2000
assert body.$.data.id == "1"
%}

### 创建用户
POST {{baseUrl}}/users
Content-Type: application/json

{
  "name": "Alice",
  "email": "alice@example.com"
}
```

**断言语法：**

```
assert status == 200
assert status in [200, 201]
assert responseTime < 1000
assert body.$.items[0].name == "Alice"    # JSONPath
assert header.Content-Type contains application/json
```

---

## 环境变量

在 `.http` 文件同级目录创建：

**`http-client.env.json`**（提交到版本库）
```json
{
  "dev":  { "baseUrl": "http://localhost:3000", "token": "dev-token" },
  "prod": { "baseUrl": "https://api.example.com" }
}
```

**`http-client.private.env.json`**（加入 `.gitignore`，存放敏感凭证）
```json
{
  "dev":  { "token": "my-real-token" },
  "prod": { "token": "prod-secret" }
}
```

优先级：`private.env.json` > `env.json` > 文件级 `@variable`

---

## 项目结构

```
Resty/
├── src/
│   ├── Resty.Core/          # 核心库（解析、执行、断言、环境、报告）
│   │   ├── Parsing/         # .http 文件解析器
│   │   ├── Execution/       # HTTP 请求执行器
│   │   ├── Assertions/      # 断言引擎（status/responseTime/body/header）
│   │   ├── Environment/     # 环境变量加载与 {{var}} 替换
│   │   └── Reporting/       # 输出格式（Text / JSON / JUnit XML）
│   ├── Resty.Cli/           # 命令行入口（resty）
│   └── Resty.Gui/           # Windows GUI（resty-gui）
│       ├── Views/           # 各视图组件
│       └── Services/        # WorkspaceService（文件扫描、保存、文件监听）
├── samples/
│   └── smoke.http           # 示例请求文件
└── docs/                    # 设计文档
```

---

## 构建与运行

**前置要求：** .NET 10 SDK

```powershell
# GUI
dotnet run --project src/Resty.Gui/

# CLI（开发模式）
dotnet run --project src/Resty.Cli/ -- run samples/smoke.http

# CLI NativeAOT 单文件发布（需 Visual Studio Build Tools）
.\publish-aot.cmd
```

---

## 技术栈

| 组件 | 技术 |
|------|------|
| GUI 框架 | [Aprillz.MewUI.Windows](https://github.com/Aprillz/MewUI) 0.15.2 |
| 目标框架 | .NET 10 / net10.0-windows |
| CLI 发布 | NativeAOT 单文件（可选） |
| 文件格式 | JetBrains HTTP Client `.http` |
