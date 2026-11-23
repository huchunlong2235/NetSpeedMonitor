# 打包与发布说明

本文件说明如何把本项目打包成两个 exe，并以规范的版本号发布到 GitHub Releases。

## 一、版本号规范

采用语义化版本 `MAJOR.MINOR.PATCH`，发布 Tag 前缀 `v`：

| 位 | 含义 | 示例 |
|---|---|---|
| MAJOR | 不兼容的架构/配置变更 | `v2.0.0` |
| MINOR | 新增功能（向后兼容） | `v1.1.0` |
| PATCH | Bug 修复、文案/样式微调 | `v1.0.1` |

当前版本：`v1.0.0`（首个公开发布版本）

## 二、本地打包

前置：安装 .NET 9 SDK（含 Windows Desktop 工作负载）。在 `src/NetSpeedMonitor/` 目录下执行。

### 方式 A：逐条命令

```bash
# 轻量版（framework-dependent，~236 KB，需系统已装 .NET 9）
dotnet publish -c Release -r win-x64 --self-contained false \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ../../publish-fd

# 独立版（self-contained，~71 MB，无需 .NET）
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o ../../publish
```

### 方式 B：一键脚本（PowerShell）

项目根目录已附带 `build.ps1`，直接运行即可（若执行策略拦截，用 `powershell -ExecutionPolicy Bypass -File build.ps1`）：

```powershell
.\build.ps1
```

脚本会自动结束正在运行的 NetSpeedMonitor 进程，再依次打包两个版本。

> 发布前必须先结束正在运行的 NetSpeedMonitor 进程，否则 exe 被占用会导致打包报 MSB4018（GenerateBundle 失败）。脚本已处理这一点。

## 三、发布到 GitHub

仓库里**只放源码 + 文档 + LICENSE**，两个 exe 作为 Release 附件上传，不进 Git 历史。

### 1. 准备本地仓库

```bash
git init
git add .          # .gitignore 会自动排除 bin/obj/publish/*.pdb
git commit -m "Initial commit: NetSpeedMonitor v1.0.0"
git branch -M main
```

### 2. 创建远端仓库并推送

在 GitHub 网页端 `New repository`（建议名 `NetSpeedMonitor`，不要勾选初始化 README/LICENSE，本地已有），拿到仓库地址后：

```bash
git remote add origin https://github.com/<你的用户名>/NetSpeedMonitor.git
git push -u origin main
```

### 3. 创建 Release 并上传 exe

网页端进入仓库 → 右侧 `Releases` → `Create a new release`：

| 字段 | 填写内容 |
|---|---|
| Tag | `v1.0.0`（与版本号一致） |
| Title | `v1.0.0 - 首个公开发布版本` |
| Description | 简要列出功能与本次变更（可直接复制 README 的功能列表） |
| Attachments | 拖入下面两个文件 |

**附件命名建议**（带版本号，便于用户区分）：

```
NetSpeedMonitor-v1.0.0-lightweight.exe   ← 来自 publish-fd/NetSpeedMonitor.exe（236 KB）
NetSpeedMonitor-v1.0.0-portable.exe      ← 来自 publish/NetSpeedMonitor.exe（71 MB）
```

上传后 `Publish release` 即可。用户从 Releases 页面下载，仓库本身保持轻量。

### 关于 gh CLI

本机 `git` 可用，但 `gh` 未安装。以上步骤用网页端即可完成，无需 gh。若后续想命令行创建 Release，可安装 gh 后执行：

```bash
gh release create v1.0.0 `
  NetSpeedMonitor-v1.0.0-lightweight.exe `
  NetSpeedMonitor-v1.0.0-portable.exe `
  --title "v1.0.0" --notes "首个公开发布版本"
```

## 四、版本更新流程

1. 修改源码，本地验证功能
2. 更新 `RELEASE.md` 顶部的「当前版本」与 README 功能列表
3. 运行 `build.ps1` 重新打包两个 exe
4. `git add` + `git commit`（附上变更说明）
5. `git tag v1.0.1` 并 `git push --tags`
6. 网页端创建新 Release，上传带新版本号的 exe

## 五、发布前检查清单

- [ ] 两个 exe 都能正常启动，悬浮窗/托盘/设置窗口无报错
- [ ] 配置文件无测试残留（`%APPDATA%\NetSpeedMonitor\config.json` 建议删除，让用户首次运行用默认值）
- [ ] README 中的体积数字与实际打包产物一致
- [ ] `.gitignore` 生效：`git status` 中不应出现 bin/obj/publish/pdb
- [ ] LICENSE 版权年份与所有者正确
