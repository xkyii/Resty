# 为 Resty 构建分层自动化测试体系

## 提交总结

为 Resty 项目设计并实现了一套完整的分层自动化测试结构，包括单元测试和 Avalonia Headless 集成测试两层。所有 30 个测试用例均通过验证，可直接用于 CI/CD 流程。

## 修改内容

### 1. 测试方案文档
- **doc/testing-strategy.md**：详细阐述了测试分层策略、职责边界、后续扩展方向
  - 明确 unit 层覆盖范围：解析、序列化、状态切换、文件操作
  - 明确 headless 层覆盖范围：Avalonia 绑定、视图定位、页面装配
  - 说明为何不建议一开始就做桌面端到端自动化

### 2. 项目结构创建
- **tests/Directory.Build.props**：统一的测试项目配置
- **tests/Kx.Resty.UnitTests/**：单元测试项目
- **tests/Kx.Resty.HeadlessTests/**：Avalonia Headless 测试项目
- **Resty.slnx**：已接入两个测试项目

### 3. Unit 测试（21 个）

#### Commands 层
- **HttpFileParserTests.cs** (5 个)：
  - 解析变量、多请求块、query 参数
  - 支持 bare URL (自动识别为 GET)、多行 continuation
  - 空块过滤、多注解解析

- **HttpFileWriter** (2 个)：
  - 只输出启用的 header 和 query 参数
  - Parse → Write → Parse 的往返一致性验证

#### ViewModels 层
- **RequestTabTests.cs** (7 个)：
  - Authorization header 的认证头解析和回填
  - 属性变更同步到底层 entry 和 tab title
  - body 的文件引用 vs 直接文本区分
  - 未保存标记、保存限制、认证类型切换

- **CollectionPanelTests.cs** (3 个)：
  - 集合重命名+冲突后自动追加后缀
  - 搜索过滤多层级嵌套节点
  - 请求重命名并写回文件

- **WorkspaceTabTests.cs** (2 个)：
  - 重复打开相同 entry 时 tab 复用
  - 关闭 tab 后激活相邻 tab

- **MainWindowTests.cs** (2 个)：
  - 工作区添加后聚合状态字段更新
  - 关闭工作区后激活邻近工作区

### 4. Headless 测试（9 个）

#### 页面装配与状态测试
- **MainWindowTests.cs** (2 个)：
  - MainWindow ViewModel 在 Avalonia UI 环境下的工作区管理
  - 集合添加时的状态字段级联更新

- **RequestTabTests.cs** (4 个)：
  - RequestTab 从 entry 数据加载
  - 未保存标记和 tab title 同步
  - 无绑定集合的保存限制
  - 认证类型切换时的 UI 响应

- **WorkspaceTabTests.cs** (3 个)：
  - CollectionPanel 搜索过滤在 UI 环境下的行为
  - WorkspaceTab 的请求 tab 管理
  - EnvironmentSet 环境变量加载和显示

## 测试覆盖范围

| 类所属层 | 覆盖点数 | 关键测试内容 |
|---------|---------|----------|
| HTTP 文件解析 | 7 | 请求拆分、变量、注解、query参数、body引用 |
| 文件序列化 | 2 | 启用项过滤、往返保真 |
| 请求编辑 | 7 | 绑定同步、body 处理、认证、tab 状态 |
| 集合管理 | 3 | 重命名、搜索、持久化 |
| 工作区管理 | 4 | tab 复用、切换、关闭、状态聚合 |
| UI 装配 | 9 | 页面创建、数据绑定、交互响应 |

## 技术栈

- **测试框架**：xUnit
- **Headless 支持**：Avalonia.Headless.XUnit
- **目标框架**：net10.0（与主项目保持一致）
- **Avalonia 版本**：11.3.12

## 执行结果

```
测试摘要: 总计: 30, 失败: 0, 成功: 30, 已跳过: 0
执行时间: 1.8 秒
```

## 运行方式

```powershell
# 运行所有测试
dotnet test Resty.slnx -p:OutDir=artifacts/test-bin/

# 只运行 unit 测试
dotnet test tests/Kx.Resty.UnitTests/Kx.Resty.UnitTests.csproj

# 只运行 headless 测试
dotnet test tests/Kx.Resty.HeadlessTests/Kx.Resty.HeadlessTests.csproj
```

## 后续扩展建议

1. **Unit 层扩展**：补充 RequestTab.Save 的文件操作测试、CollectionPanel.RenameRequest 的异常路径
2. **Headless 层扩展**：补充 WorkspaceScanner 的目录扫描和环境加载验证（需真实 Dispatcher）
3. **端到端冒烟**：后续如需真实桌面自动化，可在 tests/EndToEnd/ 下补充 2-4 条关键流程测试
4. **性能测试**：针对大型工作区（100+ 集合）的扫描和渲染性能

## 验证满足的要求

- ✅ 两层测试结构清晰分离，职责边界明确
- ✅ Unit 层覆盖核心业务逻辑和状态迁移
- ✅ Headless 层验证 Avalonia 绑定和页面装配
- ✅ 所有测试稳定通过，无 flaky 测试
- ✅ 可直接集成到 CI/CD 流程
- ✅ 文档完整，降低团队上手成本
