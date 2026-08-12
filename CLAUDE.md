# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目

右键菜单管家（RightMenu Master）——Windows 右键上下文菜单管理器。C# / WPF / .NET 10（`net10.0-windows`），零 NuGet 依赖（主项目），全部功能通过读写注册表实现。

UI 文字、XML 注释、提交信息均为中文；新代码请沿用中文注释与中文界面文案。

## 常用命令

```bash
dotnet build RightMenuMaster.slnx -c Debug
```

```bash
dotnet test RightMenuMaster.slnx
```

跑单个测试类或用例：

```bash
dotnet test --filter "FullyQualifiedName~CommandLineTests"
```

运行程序：

```bash
cd src/RightMenuMaster && dotnet run -c Release
```

单文件免安装发布：

```bash
cd src/RightMenuMaster && dotnet publish -c Release -r win-x64 --self-contained true
```

解决方案文件是新格式的 `RightMenuMaster.slnx`（不是 `.sln`）。**增删 XAML 文件后若某个配置报 `BG1002 找不到 .baml`，是该配置的 `obj/` 里残留了旧的中间产物**，删掉对应的 `obj/<配置>` 与 `bin/<配置>` 重新构建即可。

## 架构

### 分层

- `Models/` —— 纯数据：`MenuItemModel`（与注册表键一一对应，**带变更通知**）、`MenuCategory`（作用域枚举 + 注册表路径映射的扩展方法）、`BuiltinTemplates`、`ExtensionInfo`。
- `Services/` —— 全部是 `static` 类，无 DI、无接口抽象：`RegistryService`（核心）、`ExportImportService`、`LlmService`、`DefaultProgramService`、`SystemTools`、`WindowTools`、`ElevationService`。
- `Imaging/` —— `BuiltinIcons`（用 `DrawingContext` 代码绘制的扁平图标库，非资源文件）、`IconService`（图标提取 / 转 ICO / 带缓存的解析）。
- `Helpers/` —— `NativeMethods`（所有 P/Invoke 集中在此）、`AppPaths`、`DataProtection`、`CommandLine`、`GridSorter`、`Converters`。
- `Views/Pages/` + `MainWindow` —— **代码后置（code-behind）事件驱动，不是 MVVM**。`ViewModels/` 下只有 `ViewModelBase`（`MenuItemModel` 与 `ExtRow` 继承它）和一个未使用的 `RelayCommand`，不要以为项目是 MVVM 而去「补全」绑定。
- `tests/RightMenuMaster.Tests/` —— xUnit，覆盖命令行转义、图标转换、导出解析、DPAPI 等纯逻辑。

### 界面结构

`MainWindow` 只负责侧边导航、页面切换、权限状态与轻提示（约 150 行）。四个功能页各是一个 UserControl：

| 页面 | 职责 |
| --- | --- |
| `Views/Pages/MenuListPage` | 菜单列表、增删改、启禁用、导入导出、扩展名选择 |
| `Views/Pages/TemplatesPage` | 快捷模板与「运行看效果」预览 |
| `Views/Pages/DefaultProgramsPage` | 默认程序查看与修改 |
| `Views/Pages/ToolboxPage` | 置顶工具、密码速记框、explorer 重启、隐藏文件/扩展名开关 |

页面通过 `IShellHost` 接口（`Window.GetWindow(this) as IShellHost`）访问宿主能力：`ShowToast` / `PromptElevation` / `IsAdmin` / `MenuList`。**不要让页面直接依赖 `MainWindow` 具体类型**。模板页生成菜单项时复用 `MenuList.SaveViaDialog`，保证编辑对话框与错误处理只有一份。

样式集中在 `App.xaml`（含共用转换器 `BoolToVis`/`StrToVis`/`InvBool`）；只被单个页面使用的样式（`HoverCard`、`ToolCard`）下沉到该页面的 `UserControl.Resources`。

### 注册表模型（改动前必读 `Services/RegistryService.cs`）

- 分类 → 注册表路径的唯一来源是 `MenuCategoryInfo.ShellPath()` / `ShellexPath()`。新增作用域必须同时改这两处。
- 不直接用 `HKEY_CLASSES_ROOT` 写入。读取时分别扫描 `HKCU\Software\Classes` 与 `HKLM\SOFTWARE\Classes` 并合并（**HKCU 覆盖同名 HKLM 项**，与资源管理器一致）；写入默认落在 HKCU。
- 每个分类有两类条目：`shell\*`（命令式菜单项，有 `command` 子键）与 `shellex\ContextMenuHandlers\*`（COM 处理程序，`Command` 字段里存的是 CLSID 而非命令行，`IsShellExtension=true`）。去重字典的 key 是 `(isShellex, name)` 复合键——两处可能有同名子键，只用键名会互相覆盖导致列表丢项。凡是拼接路径或按键名查找的地方都要按 `IsShellExtension` 分支。
- 禁用手法不同：普通项写/删 `LegacyDisable`；shellex 把默认值换成空 CLSID `{000…000}`，原值备份在 `RightMenuMaster_OriginalClsid` 值中，启用时还原。**可逆是设计要求，不要改成直接删键**。
- 级联子菜单（`SubCommands`）一律只读：编辑、启禁用、删除三处都必须拦截。
- 权限：写 HKLM 触发 `UnauthorizedAccessException`/`SecurityException` 时，Service 统一转成 `ElevationRequiredException`；UI 层捕获它并调用 `Host.PromptElevation()`。新增的注册表写操作要保持这个包装约定，批量操作也不能把它吞掉。
- 任何写操作结束后调用 `NotifyShell()` → `SHChangeNotify(SHCNE_ASSOCCHANGED)`，否则资源管理器不刷新。
- 标题可能是 `@dll,-id` 间接资源串，用 `SHLoadIndirectString` 解析后再显示。

### 命令行处理

`Helpers/CommandLine` 是命令行拼装/解析的唯一实现（编辑对话框与模板预览共用）：

- 转义严格遵循 `CommandLineToArgvW` 规则（引号前反斜杠翻倍、结尾反斜杠翻倍）。不要退回成简单的 `Replace("\"", "\\\"")`——脚本以反斜杠结尾时会把收尾引号吃掉。
- PowerShell 分两条路径：**不含** `%1`/`%V`/`%L` 时用 `-EncodedCommand`（UTF-16LE base64），彻底免疫引号问题；**含**占位符时必须保持明文，否则资源管理器无法替换占位符。改这里前先看 `CommandLineTests` 里的往返用例。

### 数据存放与敏感信息

`AppPaths` 默认把配置放在 **exe 所在目录**（绿色软件）：`llm.json`、`Icons\`。程序目录不可写时（放进 Program Files）自动回退 `%LOCALAPPDATA%\RightMenuMaster`。

API Key 用 DPAPI 按当前用户加密后写入 `ProtectedApiKey`，明文不落盘；`LlmSettings.Key` 是内存中的明文（`[JsonIgnore]`），旧版本的明文 `ApiKey` 字段仅为兼容读取而保留。导出 JSON 会把 `Icons\` 下的图标 base64 内嵌，以便跨机导入。

### 导入流程

导入的命令会写进右键菜单并在点击时执行，因此**必须**先给用户过目：`ExportImportService.Parse` 只读解析（顺带查重名），`ImportPreviewDialog` 逐条勾选 + 选择同名项跳过/覆盖，确认后才 `Apply` 写注册表。不要把它简化回「选文件即导入」。

## WPF 约定与已知坑

- **列表内开关（状态列）**：`IsChecked` 是 `OneWay` 绑定到 `IsDisabled`，`MenuItemModel` 带变更通知，所以切换单项后**只更新这一行，不要调用整表刷新**。处理器靠「UI 状态与模型是否已一致」区分真实用户操作（一致 = 容器复用/初始绑定触发，直接 return）；失败回弹用 `GetBindingExpression(...).UpdateTarget()`。历史上用 TwoWay + 处理器内整表刷新会造成「容器创建 → Checked → 刷新 → 新容器」的启动即 `StackOverflowException`，别绕回去。
- `MenuItemModel.Clone()` 必须 `ClearPropertyChangedSubscribers()`：`MemberwiseClone` 会把事件订阅者一起复制过去。
- 注册表枚举与图标提取都在后台线程（`Task.Run`）完成，图标是冻结的 `BitmapSource` 可跨线程；填充列表回 UI 线程前要检查用户是否已切换分类（过期结果作废）。
- 图标解析走 `IconService.ResolveIconCached`，手动「刷新」时才 `ClearIconCache()`。没设置图标的项保持 `Icon = null`（留空），不要塞默认图标。
- `GridSorter` 排序时把列头 `Content` 换成「文字 + ▲/▼」的 `StackPanel`，复合内容会丢失可访问名称，因此显式 `AutomationProperties.SetName(header, "标题 ▲")`；带图标的按钮同理 UIA Name 为空，只能靠 `x:Name`（= AutomationId）定位。**改动 `x:Name` 会破坏 UIA 测试**。
- 行内按钮的 `Click` 会冒泡到 `GridViewColumnHeader.ClickEvent` 处理器，`GridSorter.OnHeaderClick` 已按 `e.OriginalSource` 过滤。

## 验证与 E2E

单元测试覆盖纯逻辑（`dotnet test`）。UI 改动请用 PowerShell + UIAutomation 实测，脚本模式见用户记忆 `uia-testing-quirks.md`——里面记录了本机的关键限制，写脚本前务必先看：

- 本机 UIA **枚举不到 `#32770` 的子树**（MessageBox 按钮、系统文件对话框的文件名框都取不到），`FindAll` 与 `TreeWalker` 都为空。MessageBox 用 `SendKeys {ENTER}` 确认默认按钮；系统文件/保存对话框无法可靠自动化，相关逻辑靠单元测试覆盖。
- 对话框窗口标题随模式变化：新增 = `新增菜单项 - 右键菜单管家`，编辑 = `编辑「<标题>」 - 右键菜单管家`。
- PowerShell 变量名不区分大小写，`$EDIT` 常量会被 `$edit` 变量覆盖——脚本里给元素变量起名注意避让。

注册表相关改动会**真实修改本机右键菜单**：调试时在 HKCU 下用自造的测试项验证（脚本里统一用 `_RMM` 前缀并在 finally 里清理），删除前先用「导出」备份。
