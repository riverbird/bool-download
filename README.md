# BoolDownload

基于 **Avalonia 12** + **.NET 10** 构建的跨平台下载管理工具，采用 **Fluent Design** 风格界面，集成迅雷开放下载引擎（Xunlei Open Download SDK）与 MonoTorrent，支持 HTTP/FTP 直链、迅雷链接、磁力链接和 Torrent 种子等多种下载渠道，全部任务支持断点续传。

## 功能特性

- **多渠道下载**
  - 普通 HTTP / HTTPS / FTP 直链下载，内置迅雷引擎与系统原生下载双引擎，可自动或手动切换
  - 迅雷专用链接（`thunder://`）解析还原为普通直链后加速下载
  - 磁力链接（`magnet://`）与 Torrent 种子文件下载，基于 MonoTorrent 引擎，支持选择任务内文件
- **迅雷开放下载引擎**：集成 Xunlei Open Download SDK（覆盖 win-x64 / win-x86 / linux-x64 / osx-universal 原生库），充分利用迅雷 CDN 加速资源
- **任务管理**：下载列表按状态（进行中、已完成、失败等）分类筛选，实时显示进度、速度与剩余大小，支持暂停 / 继续 / 重试 / 删除，任务信息持久化保存，重启应用后可继续管理
- **文件加速**：内置 Github / SourceForge 链接加速工具，一键生成多节点加速链接并复制
- **跨平台支持**：桌面端支持 Windows、Linux（x64 / ARM64）与 macOS，同时保留 Android、iOS 与 Browser (WebAssembly) 项目结构
- **自动更新**：内置检查更新功能，支持 Windows 安装包及 Linux deb / rpm 包的版本检测与升级提示

## 技术栈

| 类别 | 技术 |
| --- | --- |
| UI 框架 | Avalonia 12 (Fluent Theme + FluentAvalonia + FluentIcons) |
| 运行时 | .NET 10 |
| MVVM | CommunityToolkit.Mvvm |
| 下载引擎 | Xunlei Open Download SDK、Downloader、MonoTorrent |

## 支持的平台与安装

| 平台 | 安装方式 |
| --- | --- |
| Windows x64 | `bool_download_setup_vX_X_X.exe` 安装包（Inno Setup 构建） |
| Linux x64 / ARM64 | `.deb` 包（Debian / Ubuntu / Mint 等）或 `.rpm` 包（Fedora 等） |
| macOS | 桌面端直接运行 |

## 从源码构建

### 环境要求

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Avalonia VS Code 扩展](https://marketplace.visualstudio.com/items?itemName=AvaloniaTeam.vscode-avalonia)（可选，用于 XAML 预览）

### 编译运行

```bash
# 桌面端（Windows / Linux / macOS）
dotnet run --project BoolDownload.Desktop
```

### 打包发布

Linux 下可使用 `scripts/` 目录中的构建脚本生成安装包：

```bash
./scripts/build_x64_deb_desktop.sh     # x64 .deb 包
./scripts/build_arm64_deb_desktop.sh   # ARM64 .deb 包
./scripts/build_x64_rpm_desktop.sh     # x64 .rpm 包
./scripts/build_arm64_rpm_desktop.sh   # ARM64 .rpm 包
```

Windows 安装包使用 `installer/install_script_laptop.iss` 通过 Inno Setup 构建。

## 项目结构

```
BoolDownload/            # 核心共享项目（UI、ViewModels、Services）
├── Services/            # 下载引擎封装（迅雷 SDK、原生下载、磁力/种子、任务存储）
├── ViewModels/          # 各视图模型（下载列表、新建任务、加速工具、属性等）
└── Views/               # Avalonia XAML 视图与对话框
BoolDownload.Desktop/    # 桌面端宿主（Windows / Linux / macOS）
BoolDownload.Android/    # Android 宿主
BoolDownload.iOS/        # iOS 宿主
BoolDownload.Browser/    # Browser (WebAssembly) 宿主
installer/               # Windows 安装包脚本
scripts/                 # Linux 打包脚本与更新配置
```

## 许可证

本项目基于 [GPL-3.0](LICENSE) 许可证开源。
