# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 项目

右键菜单管家（RightMenu Master）——Windows 右键上下文菜单管理器。C# / WPF / .NET 10（`net10.0-windows`），单项目，无依赖包。全部功能通过读写注册表实现，不注入 Shell、无后台常驻。

UI 文字、XML 注释、提交信息均为中文；新代码请沿用中文注释与中文界面文案。

## 常用命令

```bash
cd src/RightMenuMaster && dotnet build -c Debug
```

```bash
cd src/RightMenuMaster && dotnet run -c Release
```

单文件免安装发布：

```bash
cd src/RightMenuMaster && dotnet publish -c Release -r win-x64 --self-contained true
```

仓库中没有测试项目，也没有 lint 配置。验证靠实际运行 + UI Automation 脚本（见下文「验证与 E2E」）。

## 架构

### 分层

- `Models/` —— 纯数据：`MenuItemModel`（与注册表键一一对应）、`MenuCategory`（作用域枚举 + 注册表路径映射的扩展方法）、`BuiltinTemplates`（内置模板静态表）、`ExtensionInfo`。
- `Services/` —— 全部是 `static` 类，无 DI、无接口抽象：`RegistryService`（核心）、`ExportImportService`、`LlmService`、`DefaultProgramService`、`SystemTools`、`WindowTools`、`ElevationService`。
- `Imaging/` —— `BuiltinIcons`（用 `DrawingContext` 代码绘制的扁平图标库，非资源文件）、`IconService`（`ExtractIconEx` 提取 / 转 ICO 写盘）。
- `Views/` + `MainWindow` —— WPF 窗口，**代码后置（code-behind）事件驱动，不是 MVVM**。`ViewModels/` 下的 `ViewModelBase`/`RelayCommand` 基本未使用（仅 `MainWindow.ExtRow` 继承 `ViewModelBase`），不要以为项目是 MVVM 而去「补全」绑定。
- `Helpers/` —— `NativeMethods`（所有 P/Invoke 集中在此）、`AppPaths`、`GridSorter`、`Converters`。

### 注册表模型（改动前必读 `Services/RegistryService.cs`）

- 分类 → 注册表路径的唯一来源是 `MenuCategoryInfo.ShellPath()` / `ShellexPath()`。新增作用域必须同时改这两处。
- 不直接用 `HKEY_CLASSES_ROOT` 写入。读取时分别扫描 `HKCU\Software\Classes` 与 `HKLM\SOFTWARE\Classes` 并合并（**HKCU 覆盖同名 HKLM 项**，与资源管理器一致）；写入默认落在 HKCU。
- 每个分类有两类条目：`shell\*`（命令式菜单项，有 `command` 子键）与 `shellex\ContextMenuHandlers\*`（COM 处理程序，`Command` 字段里存的是 CLSID 而非命令行，`IsShellExtension=true`）。两者的启用/删除路径不同，凡是拼接路径的地方都要按 `IsShellExtension` 分支。
- 禁用手法不同：普通项写/删 `LegacyDisable`；shellex 把默认值换成空 CLSID `{000…000}`，原值备份在 `RightMenuMaster_OriginalClsid` 值中，启用时还原——**可逆是设计要求，不要改成直接删键**。
- 权限：写 HKLM 触发 `UnauthorizedAccessException`/`SecurityException` 时，Service 统一转成 `ElevationRequiredException`；UI 层捕获它并调用 `PromptElevation()`（提示以管理员重启，`ElevationService.RestartAsAdmin`）。新增的注册表写操作要保持这个包装约定。
- 任何写操作结束后调用 `NotifyShell()` → `SHChangeNotify(SHCNE_ASSOCCHANGED)`，否则资源管理器不刷新。
- 标题可能是 `@dll,-id` 间接资源串，用 `SHLoadIndirectString` 解析后再显示。

### 数据存放

`AppPaths` 把所有配置放在 **exe 所在目录**（绿色软件）：`llm.json`（AI 接口设置）、`Icons\`（用户选定/转换生成的 ICO）。不要改用 `%APPDATA%`。导出 JSON 会把 `Icons\` 目录下的图标 base64 内嵌，以便跨机导入。

### AI 填写

`LlmService` 直接 POST OpenAI 兼容的 `chat/completions`，system prompt 内联在 `BuildSystemPrompt()` 中，其字段定义必须与 `MenuDraft` 以及 `EditEntryDialog` 的控件一一对应；prompt 里的图标名列表由 `BuiltinIcons.All` 动态拼接。改编辑对话框的字段时三处要同步。命令类型 `program`/`cmd`/`powershell` 与 `EditEntryDialog.CommandKind` 对应，保存时由 `BuildCommand`/`DetectCommandKind` 在「命令行字符串」与「分栏控件」之间双向转换。

## WPF 约定与已知坑

- 样式集中在 `App.xaml`（`AccentBrush`、`RoundedButton`/`PrimaryButton`/`DangerButton`、`NavButton`、`SwitchCheckBox`、`Card`、`PageTitle`…）。新 UI 用现成资源键，不要内联颜色。
- 主窗口是「单窗口多页」：`PageMenus`/`PageTemplates`/`PageDefaults`/`PageTools` 四个 `Grid` 靠 `ShowPage()` 切换 `Visibility`，导航是带 `Tag` 的 `RadioButton`（`NavScope_Checked` / `NavPage_Checked`）。构造期间事件会先于字段初始化触发，处理器里保留了 `if (EntryList is null) return;` 一类的空检查，别删。
- **列表内开关（状态列）的绑定模式是刻意的**：`IsChecked="{Binding IsDisabled, Mode=OneTime, Converter=InvBool}"` + `Checked/Unchecked` 处理器。改成 TwoWay 会因「容器创建 → 触发 Checked → 刷新列表 → 新容器」造成启动即 `StackOverflowException`。处理器靠两道防线区分真实用户操作：与模型一致时直接 return，以及程序化刷新前后置 `_suppressStatusSwitch`。
- `GridSorter` 在排序时把列头 `Content` 换成「文字 + ▲/▼」的 `StackPanel`，复合内容会丢失可访问名称，因此显式 `AutomationProperties.SetName(header, "标题 ▲")`；带图标的按钮同理 UIA Name 为空，只能靠 `x:Name`（= AutomationId）定位。
- 行内按钮的 `Click` 会冒泡到 `GridViewColumnHeader.ClickEvent` 处理器，`GridSorter.OnHeaderClick` 已按 `e.OriginalSource` 过滤。

## 验证与 E2E

改动 UI 后请真正启动程序确认（`dotnet run`），必要时用 PowerShell + UIAutomation 断言。该应用的 UIA 细节（行是 `DataItem`、带 CheckBox 列的 ListView 暴露为 `DataGrid`、对话框窗口标题随新增/编辑模式变化、owned 窗口挂在 owner 的 Descendants 下、`TogglePattern.Toggle()` 不触发 `Click`、PasswordBox 无 ValuePattern 等）已记录在用户记忆 `uia-testing-quirks.md` 中，写测试脚本前先看它，避免重复踩坑。

注册表相关改动会**真实修改本机右键菜单**：调试时优先在 HKCU 下用自造的测试项验证，删除前先用「导出」备份。
