# Unity Novel Reader

[English](README.en.md)

Unity Novel Reader 是一个运行在 Unity Editor 内的本地文本阅读工具。默认界面采用 Console 伪装模式，包括 Console 标签标题、工具栏、Log 行、级别图标、搜索框和右侧计数器。

阅读器支持分页、章节、书签、进度保存和快捷键设置。老板键可以隐藏或恢复阅读器，并切换到 `Scene`、`Console`、`Profiler`、`Animator` 或 `Project` 窗口。

小说文件、阅读进度和书签都不进入真实项目；卸载工具、切换项目或 SVN 还原后，阅读数据仍然保留。

## Console 伪装模式

默认 Console 模式包含以下功能：

- 标签标题与图标、工具栏、交替深浅的 Log 行、级别图标、搜索框和右侧计数器采用近似 Unity Console 的布局。
- `Prev`、`Next`、`Mark` 提供翻页和书签操作；`Editor ▼` 集中提供 `Open TXT`、书库、面板、设置和阅读皮肤切换。
- `Clear` 用于隐藏或恢复当前正文，不改变页码、进度、书签和滚动位置。
- 右键任意 Log 行可添加一次性蓝色定位标记；右键另一行时标记会转移，翻页、跳章或换书后自动清除，不写入阅读数据。
- 章节标题使用 Error，段首使用 Log/Info，同段续行使用 Warning；右侧级别开关会同步弱化或恢复文字和对应图标，不过滤正文。
- Log 行会根据窗口宽度拆分或增高，保持正文完整显示。
- 目录/书签侧栏默认收起，需要时可以展开。
- 带时间戳的伪日志头可选，默认关闭。

Console 伪装只影响显示方式；原文字符、空白、偏移、阅读进度和书签保持不变。

## 功能

- 提供 Console 伪装模式，并保留 Classic Reader 传统阅读布局。
- 直接读取项目外的 `.txt`、`.text`、`.md`，不导入 `Assets`。
- 自动识别 UTF-8、UTF-16、GB18030 和 GBK。
- 识别常见中文、英文章节标题，提供搜索和跳转。
- 跨 Unity 项目保存书库、进度、书签和阅读设置。
- 分页、进度跳转、字号、每页字数和深浅背景可调。
- Console 模式下目录/书签侧栏默认收起，并提供明确的展开、收起控制。
- 提供可重绑的显示/隐藏快捷键、老板键和可选伪装窗口。
- 主窗口内置轻量 Settings 页面，可设置快捷键、阅读皮肤、伪日志头和伪装策略。
- 常驻 `Prev` / `Next` 翻页按钮；`Clear` 可反复隐藏/恢复正文，颜色开关不隐藏正文。
- 只有 Editor 程序集，不进入 Player 构建。

## 安装

### Release 拖拽包（推荐）

1. 从 [最新 Release](https://github.com/yyyyyp233/UnityNovelReader/releases/latest) 下载 `UnityNovelReader-0.2.4.unitypackage`。
2. 将文件拖进 Unity，或使用 **Assets > Import Package > Custom Package...**。
3. 保持两个文件均被选中并导入。

导入后只有 `Assets/UnityNovelReader.Editor.dll` 和对应 `.meta`。如果装过早期源码版 `.unitypackage`，请先删除 `Assets/UnityNovelReader` 旧目录，再导入当前版本。

### Git URL

在 **Window > Package Manager** 中选择 **+ > Add package from git URL...**，输入：

```text
https://github.com/yyyyyp233/UnityNovelReader.git#v0.2.4
```

### 本地源码

1. 将本仓库放在 Unity 项目以外，例如 `C:\Tools\UnityNovelReader`。
2. Unity 打开 **Window > Package Manager**。
3. 点击 **+ > Add package from disk...**。
4. 选择本仓库根目录的 `package.json`。

也可以运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools~\Toggle-LocalPackage.ps1 -Action Install -ProjectPath C:\你的Unity项目
```

## 构建 `.unitypackage`

仓库的 `Releases~` 目录会生成可直接拖进 Unity 编辑器的传统资源包；目录名末尾的 `~` 可以避免 Unity 把发行文件当成项目资源扫描：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools~\Build-UnityPackage.ps1
```

也可以指定 Unity 和输出路径：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools~\Build-UnityPackage.ps1 `
  -UnityPath "C:\Program Files\Unity\Hub\Editor\2022.3.62f2\Editor\Unity.exe" `
  -OutputPath ".\Releases~\UnityNovelReader-0.2.4.unitypackage"
```

拖进 Unity 后只会得到 `Assets/UnityNovelReader.Editor.dll` 和对应 `.meta`，共 1 个资产路径、落盘 2 个文件，不产生额外目录。DLL 的导入配置强制为 Editor-only，不进入 Player；完整 MIT 声明嵌入程序集元数据，完整源码与可读许可证仍保留在 GitHub/UPM 仓库。请勿执行 SVN Add。删除这两个文件即可卸载，不影响项目外的小说、书签和进度。

可复现构建和发布检查详见[发布流程](Documentation~/RELEASING.md)。

## 卸载

在 Package Manager 中 Remove，或者运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\Tools~\Toggle-LocalPackage.ps1 -Action Uninstall -ProjectPath C:\你的Unity项目
```

也可以直接 SVN 还原 `Packages/manifest.json` 和 `Packages/packages-lock.json`。这些操作都不会删除阅读记录。

Windows 默认数据位置：

```text
%LOCALAPPDATA%\UnityNovelReader\state.json
```

Unity 菜单 **Tools > Unity Novel Reader > Open Data Folder** 可以直接打开。启动 Unity 前设置环境变量 `UNITY_NOVEL_READER_HOME` 可以改成自定义目录。

## 使用

从 **Tools > Unity Novel Reader > Open Reader** 打开窗口，然后选择本地小说。默认窗口标题和视觉语言接近 Unity Console：交替深浅的 Log 行、内置级别图标、搜索框和右侧计数开关。

Console 皮肤只改变呈现方式。默认不生成英文日志头，原小说正文直接从每条 Log 的第一行开始，并以约两行正文为目标，左侧使用 Unity 原生 32×32 级别图标。连续换行会附着到相邻正文的源文本中，显示层同时裁掉段首缩进与行首尾空白，使所有可见文字共用同一左边界。级别规则固定为：章节标题用 Error（红色），段首用 Log/Info（白色），同段续行用 Warning（黄色）。通过 `Editor ▼ > Synthetic Headers` 可以选择启用带时间戳的伪日志头，Settings 中也有同一个开关；Info、Warning、Error 分别提供 25、15、5 条候选消息，时间与候选消息会在翻页、跳章、换书等内容切换时重新生成，普通重绘、窗口缩放或 Reimport 不会反复刷新。源文本仍逐字符保留所有空白、偏移和原顺序。右侧三个级别按钮会同步弱化或恢复对应行的文字与图标，不会过滤或隐藏正文。

`Clear` 用同一个按钮切换正文隐藏和显示；再次点击会恢复同一页及原滚动位置，不关闭小说，也不改变进度、书签和持久化数据。翻页、章节跳转或重新打开小说也会恢复显示。

工具栏搜索框只搜索章节标题；输入内容时会自动展开章节面板。Console 模式下目录/书签侧栏默认收起，可以点击左侧窄条上的 `▶` 或通过 `Editor ▼ > Panels` 展开，再用 `◀` 收起。

`Editor ▼ > Settings` 会直接在当前阅读窗口内切到轻量设置页，不会额外打开插件窗口。这里可以修改快捷键、Console 外观、伪日志头和老板键目标；快捷键由 Unity 的用户级 Shortcut Manager 保存，不写入项目。

| 操作 | 控件或默认快捷键 |
| --- | --- |
| 打开本地小说 | `Editor ▼ > Open TXT` |
| 上一页 / 下一页 | 常驻 `Prev` / `Next` 按钮 |
| 隐藏 / 恢复可见正文 | `Clear` |
| 添加书签 | `Mark` |
| 目录、书签、书库、设置 | `Editor ▼`；目录也可用 `▶` / `◀` 控制 |
| 显示/隐藏阅读器 | `Ctrl+Alt+R` |
| 老板键：隐藏并切到所选伪装窗口 / 恢复 | `Ctrl+Alt+H` |
| 阅读窗口聚焦时快速隐藏 | `Esc` |
| 下一页 | `Space`、`PageDown`、`右方向键` |
| 上一页 | `PageUp`、`左方向键` |

可直接在阅读器 Settings 页或 Unity Shortcut Manager 里重新绑定。

通过 `Editor ▼ > Boss-key target` 选择老板键目标：`Scene`、`Console`、`Profiler`、`Animator` 或 `Project`。Console 模式可通过 `Editor ▼ > Classic Reader` 切换到传统布局；Classic 模式保留同一个 `Editor ▼` 菜单，并显示 `Console Reader` 用于切回伪装模式。如果当前 Unity 版本无法打开所选窗口，会自动回退到 `Scene`。

## 数据与项目隔离

- 不请求网络。
- 不把小说内容复制进项目或插件目录。
- 不使用项目内数据库，也不依赖 `EditorPrefs`。
- UPM 安装只产生正常需要的 `manifest.json` / `packages-lock.json` 本地差异。
- `.unitypackage` 安装只向 `Assets` 根目录导入一个 Editor-only DLL 及其 `.meta`；MIT 声明嵌入 DLL，不导入源码、小说或阅读数据。
- 包被移除后，用户数据仍保留在系统用户目录。

详见[隐私说明](Documentation~/PRIVACY.md)和[安装说明](Documentation~/INSTALLATION.md)。

## 开发

源码包支持 Unity 2020.3 或更高版本。EditMode 测试位于 `Tests/Editor`。`0.2.4` 已在 Unity `2022.3.62f2` 完成导入、编译和全部 30 项 EditMode 测试，其中真实 GB18030 长篇文本 smoke test 识别到 1,840 章。

## 许可证

[MIT](LICENSE)
