# 目录管理页实施方案

## 一、功能边界

目录管理页负责：
- 管理"最近打开"目录列表（程序自动记录，上限20条）
- 管理"收藏目录"列表（用户手动添加，无数量上限）
- 持久化以上两个列表到磁盘
- 打开目录进入工作区（校验目录是否可达）
- 在右侧展示选中目录的详细信息（含状态检测结果）

**不包含**：
- 目录内文件的浏览和解析（属于工作区 P2）
- 创建新目录（操作系统职责）
- 文件级别的权限管理

---

## 二、信息架构

```
MainWindow
└── 目录管理模式（IsDirectoryManagerMode = true）
    ├── 左侧 NavMenu
    │   ├── 最近（RecentEntries）
    │   │   └── [目录项]  name / path
    │   └── 收藏（ManagedEntries）
    │       └── [目录项]  name / path
    └── 右侧
        ├── 顶部工具栏
        │   ├── 打开（→工作区）
        │   ├── 在资源管理器中显示
        │   ├── 加入收藏  （仅"最近"可用）
        │   └── 移除
        └── 详情面板
            ├── 名称
            ├── 路径
            ├── 最近打开时间
            ├── .http 文件数
            └── 状态（可访问 / 路径不存在 / 无权限）
```

---

## 三、状态模型

### DirectoryEntryItem 扩展字段

| 字段 | 类型 | 说明 |
|------|------|------|
| `Name` | string | 目录名（`Path.GetFileName`） |
| `Path` | string | 绝对路径 |
| `LastOpenedAt` | DateTime | 最近打开时间；从不赋 MinValue |
| `Kind` | enum | `Recent` / `Managed` |
| `IsAccessible` | bool | 目录存在且有读权限 |
| `HttpFileCount` | int | 目录内 `.http` 文件数量（含子目录） |
| `ValidationState` | enum | `Unknown / Accessible / NotFound / PermissionDenied` |

`IsAccessible` / `HttpFileCount` / `ValidationState` 在后台异步填充，不阻塞 UI。

### DirectoryValidationState 枚举

```csharp
public enum DirectoryValidationState { Unknown, Accessible, NotFound, PermissionDenied }
```

### 右侧详情面板派生属性

| 属性 | 来源 |
|------|------|
| `SelectedStatusText` | ValidationState → 对应文字 |
| `SelectedStatusColor` | ValidationState → 颜色字符串 |
| `SelectedHttpFileCountText` | HttpFileCount → 格式化字符串 |

---

## 四、关键交互规则

### 4.1 打开目录 → 工作区

1. 校验 `ValidationState`；若 `Unknown` 则先执行同步校验
2. `NotFound`：显示工具栏内联错误"路径不存在，请移除该记录"；不切换
3. `PermissionDenied`：显示错误"无读取权限"；不切换
4. `Accessible`：
   - 更新 `LastOpenedAt = DateTime.Now`
   - 若不在 `RecentEntries` 中则插入头部（上限20条）
   - 保存持久化
   - 触发 `OpenInWorkspaceRequested` 回调

### 4.2 双击左侧菜单项

等同于单击选中 + 执行"打开"操作（同上流程）。

### 4.3 加入收藏

1. 仅对 `Kind == Recent` 的项目可用
2. 不重复添加（Path 大小写不敏感去重）
3. 添加后保存持久化
4. "最近"中的同路径条目**保留**（不删除）

### 4.4 移除

1. 从对应列表（Recent 或 Managed）中删除
2. 保存持久化
3. 清空 `SelectedEntry`

### 4.5 在资源管理器中显示

- Windows：`explorer.exe /select,<path>`
- macOS：`open -R <path>`
- Linux：`xdg-open <parent_dir>`（不支持 `/select`）

---

## 五、持久化设计

### 存储位置

```
%APPDATA%/Resty/directories.json         (Windows)
~/.config/Resty/directories.json        (Linux)
~/Library/Application Support/Resty/directories.json  (macOS)
```

使用 `Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)` 跨平台获取根路径。

### JSON 结构

```json
{
  "version": 1,
  "recent": [
    { "path": "D:/workspace/demo-api", "lastOpenedAt": "2026-04-01T10:00:00" }
  ],
  "managed": [
    { "path": "D:/workspace/sandbox", "addedAt": "2026-03-20T08:00:00" }
  ]
}
```

- `Name` 由程序从 `Path.GetFileName(path)` 实时计算，不存储
- `recent` 按 `lastOpenedAt` 降序排列，上限20条
- 写操作：内存修改后异步写入，用 `SemaphoreSlim(1,1)` 防并发覆盖

### 服务接口（Application 层）

```csharp
public interface IDirectoryStore
{
    Task<DirectoriesData> LoadAsync();
    Task SaveAsync(DirectoriesData data);
}

public record DirectoriesData(
    List<RecentDirectoryRecord> Recent,
    List<ManagedDirectoryRecord> Managed
);

public record RecentDirectoryRecord(string Path, DateTime LastOpenedAt);
public record ManagedDirectoryRecord(string Path, DateTime AddedAt);
```

### 实现（Infrastructure 层）

`JsonDirectoryStore` —— 使用 `System.Text.Json`，存到上述路径。

---

## 六、目录校验策略

```csharp
public static DirectoryValidationState Validate(string path)
{
    if (!Directory.Exists(path))
        return DirectoryValidationState.NotFound;
    try
    {
        Directory.GetFiles(path); // 触发权限异常
        return DirectoryValidationState.Accessible;
    }
    catch (UnauthorizedAccessException)
    {
        return DirectoryValidationState.PermissionDenied;
    }
}
```

- 启动时对所有持久化条目执行异步后台校验（不阻塞启动）
- 每次打开前执行即时同步校验（轻操作，可接受）

---

## 七、错误信息标准

| 场景 | 显示位置 | 文字 |
|------|----------|------|
| 路径不存在 | 工具栏内联 Banner | `⚠ 路径不存在：{path}` |
| 无读取权限 | 工具栏内联 Banner | `⚠ 无读取权限：{path}` |
| 打开成功 | （无提示，直接切换到工作区） | - |
| 可用性未知 | 详情面板状态行 | `检测中…` |

Banner 在下次操作（选择新项、再次打开）时自动消失。

---

## 八、实施阶段

### D1 — 持久化 + 校验（基础设施）

**涉及文件：**
- `Domain/DirectoryStore/DirectoriesData.cs`（记录类型）
- `Application/Abstractions/IDirectoryStore.cs`
- `Infrastructure/DirectoryStore/JsonDirectoryStore.cs`
- `Desktop/Features/DirectoryManager/ViewModels/DirectoryManagerViewModel.cs`（注入 IDirectoryStore，启动加载，变更保存）
- `App.axaml.cs`（构造 JsonDirectoryStore 并注入）

**验收：**
- 启动时从磁盘加载，左侧列表显示持久化数据
- 添加/移除/加入收藏后磁盘文件更新
- 关闭再启动数据不丢失

### D2 — 校验联动打开流程

**涉及文件：**
- `DirectoryManagerViewModel.cs`（`ValidateAndOpen` 方法，内联错误属性）
- `MainWindow.axaml`（工具栏 Banner 绑定）

**验收：**
- 打开不存在路径 → Banner 显示，不切换
- 打开有效路径 → 切换工作区，Banner 不出现
- 后台校验完成后详情面板状态行更新

### D3 — 右侧详情面板增强

**涉及文件：**
- `DirectoryManagerViewModel.cs`（`SelectedStatusText/Color`，`SelectedHttpFileCountText`）
- `MainWindow.axaml`（详情面板绑定）

**验收：**
- 状态行显示"可访问"（绿）/ "路径不存在"（红）/ "无权限"（橙）/ "检测中…"（灰）
- .http 文件数显示正确（首次打开异步统计后更新）

### D4 — 联通工作区真实路径

**涉及文件：**
- `DirectoryEntryItem.cs`（确保 Path 字段传递下去）
- `MainWindowViewModel.cs`（`OpenDirectoryEntryInWorkspace` 改用真实路径而非 Name）
- `WorkspaceNavigationViewModel.cs`（`LoadWorkspace` 接收路径，真实扫描 .http 文件）

**验收：**
- 切换到工作区后，左侧集合树显示真实 .http 文件列表
- 文件以"文件名"为集合名，内部请求为 `### block` 解析结果

---

## 九、验收标准（整体）

1. 冷启动：持久化目录正常加载显示
2. 添加：打开新目录后自动出现在"最近"
3. 收藏：从"最近"加入"收藏"并持久化
4. 移除：移除条目后磁盘文件同步更新
5. 错误路径：打开无效路径显示内联错误，不崩溃
6. 真实路径传递：点"打开"后工作区左侧可见真实文件结构
7. 跨平台：Windows / macOS / Linux 路径存储均正常
