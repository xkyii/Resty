# Resty.Rebuild

这是 Resty 的并行重构工程（激进路线），与现有 `src/` 实现隔离。

## 目标

- 以 `doc/ui-2.md` 为 UI 重构主依据
- 技术栈：`.NET 10` + `Avalonia 11` + `Semi.Avalonia` + `Irihi.Ursa` + `ReactiveUI`
- 先完成 UI 壳和状态机，再迁移业务逻辑

## 目录分层

- `src/Resty.Rebuild.Desktop`：UI 壳层（Shell + Features + Shared）
- `src/Resty.Rebuild.Application`：应用层（UseCases + Abstractions + State）
- `src/Resty.Rebuild.Domain`：领域层（Entities + ValueObjects）
- `src/Resty.Rebuild.Infrastructure`：基础设施层（Http + Persistence + FileSystem）
- `doc/`：重构计划、状态机和验收标准

## 启动命令

```powershell
cd d:\Code\.tmp\Resty\rebuild

dotnet restore Resty.Rebuild.slnx
dotnet build Resty.Rebuild.slnx -c Debug
dotnet run --project src/Resty.Rebuild.Desktop/Resty.Rebuild.Desktop.csproj
```

## 当前状态

- 并行解决方案已创建并可编译
- 已接入 `Semi.Avalonia`、`Irihi.Ursa`、`Irihi.Ursa.Themes.Semi`、`Irihi.Ursa.ReactiveUIExtension`
- `App.axaml` 已按官方示例启用 Semi + Ursa Semi 主题
- 分层目录骨架已初始化

详见 `doc/rebuild-plan.md`。
