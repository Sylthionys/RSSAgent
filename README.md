# RssAgent

Windows desktop assistant that turns natural-language subscription requests into RSSHub routes and automates RSSBox subscription creation.

## 中文（CN）

### 简介
RssAgent 面向 Windows 桌面环境，基于自然语言生成 RSSHub 路由，并通过 Playwright 自动化写入 RSSBox 订阅。

### 功能（当前版本）
- 自然语言生成 RSSHub 路由（依赖 Gemini）
- Playwright 自动化创建 RSSBox 订阅
- 读取 RSSBox 选项并在新增订阅时应用
- 基础日志与自动化诊断信息

### 环境要求
- Windows 10/11
- .NET 8 SDK（开发构建）或 .NET 8 Desktop Runtime（运行）
- 可访问 RSSHub 与 RSSBox 的网络环境
- Playwright 浏览器依赖（Chromium，首次运行自动下载）

### 快速开始（开发构建与运行）

```powershell
dotnet restore
dotnet build -c Release
dotnet run --project src\RssAgent.App
```

### 配置
- 运行时配置路径：`%APPDATA%\RssAgent\appsettings.json`
- 仓库仅提供 `appsettings.example.json` 作为模板
- 应用内提供可视化设置，并支持导入/导出或手动导入配置文件
- 敏感字段请使用加密存储字段，不要提交到仓库

示例（仅展示少量字段）：

```json
{
  "RssBox": {
    "BaseUrl": "http://localhost:18000",
    "Username": "user"
  },
  "Gemini": {
    "Model": "gemini-3-flash-preview"
  },
  "Defaults": {
    "TargetLanguage": "Chinese Simplified"
  }
}
```

### 发布
推荐使用 `dotnet publish` 生成可分发目录，再打包为 zip：

```powershell
dotnet publish src\RssAgent.App -c Release -o artifacts\RssAgent
```

发布时请打包整个输出目录（含 dll/runtimeconfig/deps），不要只分发单个 exe。

### 常见问题
- 首次运行会下载 Playwright 浏览器依赖；离线环境可手动执行 `pwsh src\RssAgent.App\bin\Release\net8.0-windows\playwright.ps1 install chromium`
- 提示缺少运行时：请安装 .NET 8 Desktop Runtime
- 自动化失败：RSSBox 页面结构变更可能影响 Playwright 脚本

## English (EN)

### Overview
RssAgent is a Windows desktop app that converts natural-language subscription requests into RSSHub routes and automates RSSBox subscription creation via Playwright.

### Features
- Generate RSSHub routes from natural language (via Gemini)
- Automate RSSBox subscription creation with Playwright
- Load RSSBox options and apply them during subscription creation
- Basic logs and automation diagnostics

### Requirements
- Windows 10/11
- .NET 8 SDK (build) or .NET 8 Desktop Runtime (run)
- Network access to RSSHub and RSSBox
- Playwright browser dependency (Chromium, auto-downloaded on first run)

### Quick Start (build/run)

```powershell
dotnet restore
dotnet build -c Release
dotnet run --project src\RssAgent.App
```

### Configuration
- Runtime config path: `%APPDATA%\RssAgent\appsettings.json`
- Repository includes `appsettings.example.json` as a template only
- The UI supports visual settings and configuration import/export or manual import
- Store sensitive values in encrypted fields and keep them out of version control

Example (minimal fields only):

```json
{
  "RssBox": {
    "BaseUrl": "http://localhost:18000",
    "Username": "user"
  },
  "Gemini": {
    "Model": "gemini-3-flash-preview"
  },
  "Defaults": {
    "TargetLanguage": "Chinese Simplified"
  }
}
```

### Publishing
Use `dotnet publish` to produce a distributable folder, then zip the entire output:

```powershell
dotnet publish src\RssAgent.App -c Release -o artifacts\RssAgent
```

Do not ship a single exe only; distribute the full folder (dll/runtimeconfig/deps).

### FAQ
- Playwright browsers download on first run; for offline environments run `pwsh src\RssAgent.App\bin\Release\net8.0-windows\playwright.ps1 install chromium`
- Missing runtime: install the .NET 8 Desktop Runtime
- Automation failures: RSSBox UI changes may break Playwright scripts

## Security & Privacy
- Do not commit secrets (tokens, API keys, passwords, cookies, internal URLs).
- Diagnostics may include sensitive data (logs, traces, screenshots); sanitize before sharing.
- The repository includes templates only; local configuration is ignored by `.gitignore`.

## AI assistance disclosure
Initial implementation was generated with assistance from GPT-5.2-Codex. The project is maintained by the project owner(s).

## License
MIT License. See `LICENSE`.
