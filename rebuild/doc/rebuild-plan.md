# Resty UI 重构执行清单（激进版）

## 基线

- 主设计依据：`Resty/doc/ui-2.md`
- 补充参考：`Resty/doc/ui-wireframes.md`
- 旧代码仅在行为不明确时参考

## 里程碑

### M1：框架与主题基座（已完成）

- 新建并行解决方案 `Resty.Rebuild.slnx`
- 创建四层项目：Desktop / Application / Domain / Infrastructure
- 接入 Semi 与 Ursa 主题链路
- 保证 Debug 构建通过

### M2：Shell 与双模式框架

- 实现无边框主窗口
- 标题栏统一：File/Help、模式切换、工作区名称、窗口按钮
- 中心模式切换：目录管理 / 工作区

### M3：目录管理页（按 ui-2.md）（已完成）

- 左侧：搜索 + 最近/目录树
- 右侧：工具栏（显示、移除、加入目录管理）
- 右侧：目录元信息区

本轮落地：

- 已实现最近/目录两组可展开列表与搜索过滤
- 已实现选中项驱动的工具栏动作显隐（最近项显示“加入目录管理”）
- 已实现目录元信息绑定（类型/名称/路径/最近打开时间）

### M4：工作区页（按 ui-2.md）

- 左侧：搜索 + 集合/历史切换 + 树列表
- 右侧：请求编辑区（Method/URL/Queries/Headers/Body/Auth）
- 右侧：响应区（code/time/size + Body/Headers/Cookies）

### M5：状态机与假数据联动

- 无工作区
- 空工作区
- 集合浏览
- 请求编辑
- 发送中/响应态

### M6：迁移真实逻辑

- 请求发送
- 集合持久化
- 历史记录
- Auth 策略

## 设计约束

- 保持无边框窗口
- 优先复用 Semi/Ursa 原生控件和样式体系
- 每个功能模块先做 ViewModel 状态，再做视图绑定
- 每个里程碑结束都可独立演示

## 验收标准（阶段性）

- 构建通过：`dotnet build Resty.Rebuild.slnx -c Debug`
- UI 可运行：`dotnet run --project src/Resty.Rebuild.Desktop/Resty.Rebuild.Desktop.csproj`
- 页面状态可切换且无崩溃
- 中英文资源切换不破版（后续阶段）
