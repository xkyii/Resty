# PRD: Resty — 本地优先的 HTTP API 客户端

**Status**: Approved  
**Author**: Alex (PM)  
**Last Updated**: 2026-04-28  
**Version**: 1.0  
**Stakeholders**: 核心开发者

---

## 1. Problem Statement

个人开发者在日常 API 调试和 CI/CD 集成中面临一个结构性矛盾：现有工具要么功能完整但过重（Postman/Insomnia，依赖云端、Electron、账号），要么轻量但缺少 GUI（curl、httpie），要么绑定 IDE（IntelliJ HTTP Client）。

**没有一个工具同时满足：**

- 纯本地文件存储，天然 Git 友好
- 人类可读的标准语法，可直接 `vi` 编辑
- CLI 作为一等公民，可无缝接入 CI/CD
- 轻量 GUI 辅助管理，不依赖 Electron/云服务

**Evidence：**

- JetBrains HTTP 语法已成为 `.http` 文件事实标准，但官方仅有 IDE 插件，无独立工具
- Bruno 有文件驱动的理念，但使用私有语法，无法与 JetBrains 生态互通
- CI/CD 场景中，现有 CLI 工具缺乏断言、报告、变量环境能力的统一方案

---

## 2. 产品定位

> Resty 是一个本地优先、文本驱动的 HTTP API 客户端，支持 CLI 和 GUI，使用 JetBrains HTTP 语法，单二进制分发，面向个人开发者和 CI/CD 场景。

**差异化对比：**

| 维度 | Postman | Bruno | IntelliJ HTTP | **Resty** |
|------|---------|-------|---------------|-----------|
| 存储 | 云端 | 本地文件 | 本地文件 | 本地文件 |
| 语法 | 私有 | 私有 | JetBrains HTTP | **JetBrains HTTP** |
| CLI | 弱 | 有 | 无 | **一等公民** |
| 分发 | 安装包 | 安装包 | IDE 插件 | **单二进制 (NativeAOT)** |
| GUI 框架 | Electron | Electron | Java Swing | **MewUI (.NET)** |

---

## 3. Goals & Success Metrics（V1）

| Goal | Metric | Target |
|------|--------|--------|
| CLI 可独立运行完整 HTTP 请求 | 核心功能覆盖率 | 覆盖 JetBrains HTTP 语法 90%+ |
| 可接入 CI/CD | 支持标准退出码 + 报告格式 | exit 0/1，支持 JUnit XML |
| 轻量分发 | 单 EXE 体积 | Windows x64 < 10MB（NativeAOT） |
| 开发者可上手 | 从安装到跑第一个请求 | < 2 分钟 |

---

## 4. Non-Goals（V1 明确不做）

- **GUI**：GUI 是独立子项目（Resty.Gui），不进入 V1 范围
- **Pre/Post 脚本执行**：`> {% %}` 块内只支持简化断言 DSL，不实现 JavaScript 脚本引擎
- **OAuth2 / API Key**：V1 只支持 Basic Auth 和 Bearer Token
- **团队协作功能**：无共享、评论、版本同步
- **Mac / Linux**：V1 Windows 优先，跨平台在 V2 评估
- **WebSocket / gRPC**：仅 HTTP/1.1 和 HTTP/2
- **分发渠道**：V1 不做 Scoop / WinGet，GitHub Releases 手动下载
- **Secret Store**：`private.env.json` V1 明文存储，用户自行加入 `.gitignore`

---

## 5. 用户画像与核心故事

### Persona A — 个人开发者「开发中的 API 调试」

李雷，后端开发，习惯在 VSCode/Neovim 里工作，希望 `.http` 文件和代码放在同一个 repo 里，用 Git 管理，不想打开 Postman。

### Persona B — CI/CD 流水线「自动化接口回归」

GitHub Actions / GitLab CI，在 PR 合并前跑一遍接口断言，失败则阻断合并。

---

### Story 1（核心）：运行请求

作为开发者，我想运行 `.http` 文件里的某一个请求，并在终端看到响应内容。

**Acceptance Criteria：**
- [ ] `resty run users.http` 运行文件内所有请求
- [ ] `resty run users.http --request "Get User"` 按名称运行单个请求
- [ ] 输出包含：状态码、耗时、响应头、响应体

### Story 2（环境变量）：多环境切换

作为开发者，我想在 dev / staging / prod 环境之间切换，不修改 `.http` 文件。

**Acceptance Criteria：**
- [ ] `resty run users.http --env staging` 使用 staging 环境变量
- [ ] 读取同目录下 `http-client.env.json` 中对应 env 的变量
- [ ] 读取 `http-client.private.env.json` 中的私有变量（同名时覆盖 env.json）
- [ ] 变量引用语法：`{{variableName}}`

### Story 3（断言）：CI/CD 接口回归

作为 CI 工程师，我想对响应做断言，失败时 exit code 非零，阻断流水线。

**Acceptance Criteria：**
- [ ] `.http` 文件中用 `> {% %}` 块声明断言
- [ ] `resty test users.http --report junit` 输出 JUnit XML
- [ ] 任意断言失败，进程退出码为 1
- [ ] 报告中包含每个请求的断言通过/失败明细

### Story 4（认证）：凭证注入

作为开发者，我想在请求中使用 Basic Auth 和 Bearer Token，通过变量注入凭证，不在文件中硬编码。

**Acceptance Criteria：**
- [ ] 支持 `Authorization: Basic {{credentials}}` 标准 Header 写法
- [ ] 支持 `Authorization: Bearer {{token}}` 标准 Header 写法
- [ ] 凭证从环境变量文件中读取

---

## 6. 文件布局规范

遵循 JetBrains HTTP 语法，目录结构如下：

```
my-project/
├── http-client.env.json           # 环境变量（可提交 Git）
├── http-client.private.env.json   # 私有变量（加入 .gitignore）
├── users.http
├── orders.http
└── products/
    └── create.http
```

### `http-client.env.json` 格式

```json
{
  "dev": {
    "host": "localhost:8080",
    "token": "dev-token"
  },
  "staging": {
    "host": "api-staging.example.com",
    "token": "stg-token"
  }
}
```

### `http-client.private.env.json` 格式

```json
{
  "dev": {
    "token": "my-real-dev-token"
  }
}
```

> ⚠️ 此文件应加入 `.gitignore`，V1 明文存储。

### `.http` 文件格式

```http
### Get User
GET https://{{host}}/api/users/1
Authorization: Bearer {{token}}
Accept: application/json

> {%
assert status == 200
assert body.$.name == "John Doe"
assert header["Content-Type"] contains "application/json"
assert responseTime < 500
%}

###

### Create User
POST https://{{host}}/api/users
Content-Type: application/json

{
  "name": "John Doe"
}

> {%
assert status in [200, 201]
assert body.$.id != null
%}
```

---

## 7. 断言语法规范

断言写在请求后的 `> {% %}` 块内（与 JetBrains 语法头兼容，IntelliJ 可打开文件不报结构错误）。块内是 Resty 简化 DSL，**非 JavaScript**。

| 类型 | 语法示例 | 说明 |
|------|---------|------|
| 状态码精确匹配 | `assert status == 200` | 整数比较 |
| 状态码集合匹配 | `assert status in [200, 201]` | 数组包含 |
| 响应体字段（JSONPath） | `assert body.$.name == "John Doe"` | JSONPath 取值后比较 |
| 响应体字段非空 | `assert body.$.id != null` | 非空检查 |
| 响应头精确 | `assert header["Content-Type"] == "application/json"` | 精确匹配 |
| 响应头包含 | `assert header["Content-Type"] contains "application/json"` | 子串匹配 |
| 响应时间 | `assert responseTime < 500` | 毫秒，支持 `<` / `<=` |

**比较运算符：** `==` `!=` `<` `<=` `>` `>=` `in` `contains`

> **兼容说明：** JetBrains IntelliJ 会将 `> {% %}` 块解析为 JavaScript 响应处理器。Resty 的简化 DSL 不是合法的 JavaScript，IntelliJ 会有语法警告，但文件结构完整可打开。V2 可扩展为兼容 JetBrains JS API 的完整脚本引擎。

---

## 8. CLI 命令规范

```
resty run <target> [options]      # 发送请求，查看响应
resty test <target> [options]     # 运行断言，输出报告
```

### target 格式

| 格式 | 含义 |
|------|------|
| `users.http` | 单文件，所有请求 |
| `users.http#GetUser` | 单文件中名为 GetUser 的请求 |
| `./api/` | 目录下所有 `.http` 文件（递归） |

### 通用选项

| 选项 | 默认值 | 说明 |
|------|--------|------|
| `--env <name>` | `dev` | 指定环境 |
| `--request <name>` | 无（全部） | 按请求名称过滤（run 模式） |
| `--report <format>` | `text` | `text` / `junit` / `json` |
| `--output <file>` | stdout | 报告输出文件路径 |
| `--timeout <ms>` | `30000` | 请求超时毫秒数 |
| `--no-color` | false | 禁用颜色输出（CI 环境友好） |
| `--verbose` | false | 输出完整请求头和响应头 |

### 退出码

| 码 | 含义 |
|----|------|
| `0` | 全部成功 |
| `1` | 至少一个断言失败 |
| `2` | 请求执行错误（网络超时、TLS 错误等） |
| `3` | 配置/文件错误（文件不存在、JSON 解析失败等） |

---

## 9. 技术架构

### 项目分层

```
Resty/
├── src/
│   ├── Resty.Core/       # 核心引擎库（解析、执行、断言、报告）
│   └── Resty.Cli/        # CLI 入口，NativeAOT 编译目标
└── tests/
    └── Resty.Core.Tests/ # 单元测试
```

> `Resty.Gui` 作为独立子项目，在 V2 阶段启动，基于 MewUI (.NET) 框架。

### Resty.Core 职责

- `.http` 文件解析（JetBrains 语法兼容）
- 环境变量解析与注入（`{{variable}}` 替换）
- HTTP 请求执行（`HttpClient`）
- Basic Auth / Bearer Token 处理
- `> {% %}` 断言块解析与执行
- 报告生成（text / JUnit XML / JSON）

### 技术选型

| 组件 | 选型 | 原因 |
|------|------|------|
| 语言 | C# / .NET 9+ | MewUI 强约束，NativeAOT 支持 |
| HTTP 执行 | `System.Net.Http.HttpClient` | 内置，NativeAOT 友好 |
| JSON 解析 | `System.Text.Json` + Source Generator | NativeAOT 反射限制规避 |
| JSONPath | `JsonPath.Net` 或自实现子集 | 用于 `body.$.*` 断言 |
| CLI 解析 | `System.CommandLine` | 微软官方，NativeAOT 支持 |
| 单元测试 | `xUnit` | 主流，工具链完整 |

### 风险登记

| 风险 | 可能性 | 影响 | 缓解措施 |
|------|--------|------|---------|
| MewUI API 破坏性变更 | High（Experimental v0.15） | High | V1 不引入 MewUI，GUI 独立隔离 |
| JetBrains 语法边缘 case 覆盖不全 | Medium | Medium | 以 JetBrains 官方 `.http` 示例文件作为集成测试用例 |
| NativeAOT 下第三方库裁剪不兼容 | Medium | Medium | 提前验证 JSONPath 库的 AOT 兼容性 |

---

## 10. 发布里程碑

| 里程碑 | 目标 | 通过标准 |
|--------|------|---------|
| **M1 — 核心引擎** | 解析 + 执行 + 变量替换 | 能跑通基础 GET / POST，变量正确替换 |
| **M2 — 断言 + 报告** | 完整 `resty test` 命令 | JUnit XML 可被 GitHub Actions 读取，断言失败 exit 1 |
| **M3 — 认证 + 打包** | Basic/Bearer + NativeAOT 单 EXE | Windows EXE < 10MB，无需安装 .NET Runtime |
| **V2 — GUI** | Resty.Gui 子项目启动 | 评估 MewUI 稳定性后制定计划 |

---

## 11. 决策记录

| # | 决策 | 日期 | 理由 |
|---|------|------|------|
| D1 | 断言使用 `> {% %}` 语法包裹 | 2026-04-28 | 与 JetBrains 文件格式兼容，IntelliJ 可打开不报结构错误 |
| D2 | `private.env.json` V1 明文 | 2026-04-28 | 避免过早引入 secret store 复杂度，用户自行 .gitignore |
| D3 | V1 不做分发渠道 | 2026-04-28 | 验证核心功能后再做 Scoop/WinGet |
| D4 | GUI 作为独立子项目 | 2026-04-28 | 隔离 MewUI Experimental 风险，保证 V1 核心交付 |
| D5 | Windows 优先，V2 跨平台 | 2026-04-28 | MewUI 目前 Windows 最成熟，降低 V1 复杂度 |

---

## 12. 参考资料

- [JetBrains HTTP Client 语法文档](https://www.jetbrains.com.cn/en-us/help/idea/exploring-http-syntax.html)
- [MewUI GitHub](https://github.com/aprillz/MewUI)
