# 网速监控悬浮窗（NetSpeedMonitor）

Windows 桌面悬浮窗工具，实时显示上传/下载速率与物理内存占用率，支持深度样式定制。基于 C# + WPF（.NET 9）开发。

## 运行

提供两个版本，功能完全相同，区别只在体积和内存占用：

| 版本 | 路径 | exe 体积 | 独占内存 | 前置条件 | 推荐场景 |
|---|---|---|---|---|---|
| **轻量版（推荐）** | `publish-fd/NetSpeedMonitor.exe` | 236 KB | ~64 MB | 需本机已装 .NET 9 运行时 | 本机使用，内存占用低 |
| 独立版 | `publish/NetSpeedMonitor.exe` | 71 MB | ~140 MB | 无 | 拷到未装 .NET 的机器 |

> 轻量版之所以省一半内存：独立版把整个 .NET 运行时打包进 exe 并在启动时解压，运行时再加载一份，与系统已装的运行时重复；轻量版直接复用系统运行时。本机已装 .NET 9 运行时（含 WPF 的 WindowsDesktop.App），因此推荐轻量版。
>
> 任务管理器显示的"内存"通常为 Working Set，包含大量与系统其他程序共享的 WPF/DirectX/显卡驱动 DLL，会明显高于上表的独占内存。真实资源占用以"独占/专用内存"为准。

双击对应 exe 即可运行。程序启动后常驻系统托盘，悬浮窗默认显示在屏幕右上角。

命令行参数：
- `--settings`：启动时直接打开设置窗口
- `--history`：启动时直接打开历史曲线窗口

## 交互

| 操作 | 效果 |
|---|---|
| 左键拖动悬浮窗 | 移动位置（开启吸附时自动贴边） |
| 右键悬浮窗 | 菜单：历史曲线 / 设置 / 鼠标穿透 / 隐藏窗口 / 退出 |
| 双击悬浮窗 | 打开历史曲线 |
| 双击托盘图标 | 显示 / 隐藏悬浮窗 |
| 托盘右键 | 显示隐藏 / 历史曲线 / 设置 / **鼠标穿透** / 退出 |
| **Ctrl + Alt + M** | 切换鼠标穿透（穿透后用此热键或托盘菜单切回） |

### 鼠标穿透

开启后悬浮窗不接收任何鼠标事件，点击/悬停直接落到下层窗口，不会遮挡浏览器、编辑器等任何系统组件的点击。三种切换方式：

1. **托盘右键 → 鼠标穿透**（推荐，穿透后仍可操作托盘）
2. **Ctrl + Alt + M** 全局热键（任意界面下生效）
3. 设置窗口 → 显示项与行为 → 鼠标穿透；或非穿透时右键悬浮窗菜单

> 注意：穿透状态下悬浮窗本身无法点击（也拖不动），这是设计如此。需要恢复交互时用上面任一方式切回即可。全局热键若被其他软件占用，用托盘菜单照样能切。

## 功能

- **实时网速**：每秒采样网卡流量差分，指数滑动平均平滑，自动换算 B/KB/MB/GB 单位
- **虚拟网卡过滤**：Auto 模式自动排除 Clash vgate0、TAP/TUN、WSL、VMware、VPN 等虚拟网卡，避免速率虚高；也可在设置中手动指定网卡
- **内存占用**：通过 `GlobalMemoryStatusEx` 读取物理内存使用率，与任务管理器口径一致
- **内存进度条可定制**：进度条与百分比文字独立开关；可自定义填充色/底槽色、宽度（10–160px）、高度（2–16px）、相对数值的位置（之前/之后）；百分比前的标识文本可自定义（默认 `M:`，可改为 `MEM:`/`内存:` 或留空关闭）
- **鼠标穿透**：三种方式随时切换（托盘菜单 / Ctrl+Alt+M 热键 / 设置窗口），穿透后点击直达下层窗口，不遮挡其他程序
- **样式定制**：6 套预设主题 + 自定义背景色/文字色/上传色/下载色/内存色、透明度、字号、字体、圆角、内边距、横纵排列、显示项开关、数值精度、刷新间隔
- **历史曲线**：近 5 分钟速率曲线（上传/下载双线），每秒采样
- **当日流量**：累计上传/下载流量与峰值速率，跨天自动清零，配置持久化到 `%APPDATA%\NetSpeedMonitor\config.json`
- **开机自启**：设置中一键开关（写入当前用户启动项）

## 项目结构

```
NetSpeedMonitor/
├── publish-fd/NetSpeedMonitor.exe   # 轻量版：单文件 236 KB，需 .NET 9 运行时（推荐）
├── publish/NetSpeedMonitor.exe      # 独立版：单文件 71 MB，无需运行时
└── src/NetSpeedMonitor/             # 完整源码
    ├── Models/                      # 配置模型、主题预设
    ├── Services/                    # 采样、配置持久化、开机自启、历史缓冲
    ├── ViewModels/                  # 主窗口与设置窗口视图模型
    ├── Views/                       # 悬浮窗、设置窗口、历史曲线窗口、样式资源
    └── Helpers/                     # XAML 转换器、命令、Win32 穿透/全局热键
```

## 从源码构建

需要 .NET 9 SDK（含 Windows Desktop 工作负载）：

```bash
# 轻量版（复用系统运行时，推荐）
dotnet publish -c Release -r win-x64 --self-contained false \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o publish-fd

# 独立版（打包运行时，可拷到其他机器）
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o publish
```

更详细的版本号规范、一键打包脚本与 GitHub Releases 发布步骤见 [RELEASE.md](RELEASE.md)。

## 开源协议

MIT License —— 详见 [LICENSE](LICENSE)。可自由使用、修改、分发，商用需保留版权声明。
# NetSpeedMonitor
