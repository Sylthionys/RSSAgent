<!--
README for RssAgent
Style goals: clean, modern, professional; friendly for both users & contributors.
-->

<div align="center">

# RssAgent

**RSSHub → RSSBox automation, in a Windows WPF desktop app**

自然语言描述你想订阅的内容，RssAgent 帮你生成 RSSHub 路由并用 Playwright 自动化写入 RSSBox。

<br/>

<!-- Badges -->
![Release](https://img.shields.io/github/v/release/Sylthionys/RssAgent?include_prereleases&sort=semver)
![License](https://img.shields.io/github/license/Sylthionys/RssAgent)
![Stars](https://img.shields.io/github/stars/Sylthionys/RssAgent?style=flat)
![Issues](https://img.shields.io/github/issues/Sylthionys/RssAgent)
![Downloads](https://img.shields.io/github/downloads/Sylthionys/RssAgent/total)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)
![RSSBox](https://img.shields.io/badge/RSSBox%20tested%20with-2025.12.13-blue)
![Status](https://img.shields.io/badge/status-alpha-orange)

<br/>

[Releases](https://github.com/Sylthionys/RssAgent/releases) ·
[Issues](https://github.com/Sylthionys/RssAgent/issues) ·
[Changelog](./CHANGELOG.md) ·
[License](./LICENSE)

</div>

---

> ⚠️ **Status: Alpha / Pre-release**  
> 可用但不稳定：升级可能存在 **Breaking Changes**，并且 **RSSBox UI 变更可能导致自动化失效**。  
> 如果你遇到问题，请提交 Issue（附复现步骤 + 环境信息 + 脱敏日志/截图）。

## 目录

- [亮点](#亮点)
- [兼容性与限制](#兼容性与限制)
- [快速开始（用户）](#快速开始用户)
- [快速开始（开发者）](#快速开始开发者)
- [配置说明](#配置说明)
- [常见问题与排障](#常见问题与排障)
- [Roadmap](#roadmap)
- [致谢](#致谢)
- [贡献指南](#贡献指南)
- [安全与隐私](#安全与隐私)
- [许可证](#许可证)
- [English](#english)

---

## 亮点

| 你想做的事 | RssAgent 怎么帮你 |
|---|---|
| “帮我订阅路透社科技新闻” | 生成候选 RSSHub 路由，并解释匹配原因 |
| “订阅某个网站/领域资讯” | 通过 LLM（Gemini）生成可用的订阅意图参数 |
| “把订阅直接加进 RSSBox” | Playwright 自动化打开 RSSBox，创建订阅并应用选项 |
| “希望更可控” | 允许你审阅候选、选择/修改后再添加 |

> 💡 设计目标：**少折腾**（不用到处找订阅源）+ **可控**（你来决定加哪个）+ **可复现**（可诊断、可提 Issue）。

---

## 兼容性与限制

### 支持的平台
- ✅ Windows 10 / 11
- ✅ .NET 8 Desktop Runtime（运行）
- ✅ .NET 8 SDK（开发）

### 关键依赖
- **RSSHub**：路由与可用性以 RSSHub 为准  
  - Repo: https://github.com/DIYgod/RSSHub  
  - Docs: https://docs.rsshub.app/
- **RSSBox**：用于管理订阅源  
  - Repo: https://github.com/versun/rssbox  
  - Docs: https://rssbox.app/
- **Gemini**：用于生成订阅意图/候选参数（需要 API Key）
- **Playwright**：用于自动化浏览器操作（RSSBox UI 自动化）

### 重要：RSSBox 版本说明（请务必阅读）
- ✅ 当前仅在 **RSSBox `2025.12.13`** 上验证可用  
- ⚠️ **暂不保证适配 RSSBox 最新版**（后续有时间再考虑适配）  
- 如果你使用更新版本出现自动化失败：  
  1) 可先降级到 `2025.12.13` 以确认问题  
  2) 再提交 Issue（附版本号与复现步骤）

---

## 快速开始（用户）

### 1) 下载与启动
1. 打开 Releases： https://github.com/Sylthionys/RssAgent/releases  
2. 下载最新 **pre-release** 的 Windows x64 压缩包  
3. 解压后运行（建议解压到无权限限制的目录，如 `D:\Apps\RssAgent\`）

### 2) 安装运行时（如提示缺失）
- 安装 **.NET 8 Desktop Runtime**（Windows）

### 3) 首次运行注意事项
- 第一次进行自动化时，Playwright 可能需要下载 Chromium（离线见下方排障）
- 在应用内完成以下信息配置：
  - RSSHub 地址（自建/公网）
  - RSSBox 地址与账号（自建/公网）
  - Gemini API Key（用于自然语言 → 意图参数）

---

## 快速开始（开发者）

### 环境要求
- Windows 10/11
- .NET 8 SDK
- Node.js（可选：仅在你需要对 Playwright 进行额外调试时）
- 能访问 RSSHub、RSSBox 的网络环境

### 构建与运行
```powershell
dotnet restore
dotnet build -c Release
dotnet run --project src\RssAgent.App
```

### 发布打包（建议）
```powershell
dotnet publish src\RssAgent.App -c Release -o artifacts\RssAgent
```

> 发布时请打包 **整个输出目录**（包含 `.dll` / `.deps.json` / `.runtimeconfig.json` 等），不要只分发单个 exe。

---

## 配置说明

### 配置文件位置
- 运行时配置：`%APPDATA%\RssAgent\appsettings.json`
- 仓库模板：`appsettings.example.json`

> ✅ 建议：优先在应用内完成配置，再导出/备份配置文件。  
> ❗️提醒：请勿将 Key、密码、Cookie、内网地址等敏感信息提交到仓库。

### 最小配置示例（示意）
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

<details>
<summary><b>（可选）示例：Docker 一键起 RSSHub + RSSBox</b></summary>

> 仅示例：不同环境下端口/环境变量可能需要调整。  
> 如果你已经有现成部署，请忽略本节。

```yaml
services:
  rsshub:
    image: diygod/rsshub:latest
    container_name: rsshub
    ports:
      - "1200:1200"
    restart: unless-stopped

  rssbox:
    image: versun/rssbox:latest
    container_name: rssbox
    ports:
      - "18000:80"
    restart: unless-stopped
```

</details>

---

## 常见问题与排障

### 1) Playwright 浏览器依赖下载失败（离线/受限网络）
首次运行可能会下载 Chromium。你可以手动安装：

```powershell
pwsh src\RssAgent.App\bin\Release\net8.0-windows\playwright.ps1 install chromium
```

> 如果你是从发布目录运行，请在你的发布输出目录中找到 `playwright.ps1` 再执行（路径会不同）。

### 2) 自动化失败 / 页面找不到元素 / 卡在 RSSBox
- 请先确认 RSSBox 版本：**是否为 `2025.12.13`**
- RSSBox 更新后页面结构变化，可能导致选择器失效（Alpha 阶段常见）
- 提交 Issue 前建议准备：
  - RSSBox 版本号
  - 复现步骤（越短越好）
  - 脱敏日志与截图（隐藏 token/用户名/内网地址）

### 3) 提示缺少 .NET 运行时
安装 **.NET 8 Desktop Runtime**（Windows），然后重启应用。

### 4) 我想提交 Issue，需要提供什么？
请尽量包含：
- 系统信息：Windows 版本、是否管理员运行
- 版本信息：RssAgent 版本、RSSBox 版本（尤其重要）、RSSHub 版本
- 复现步骤：从启动到出错的最短路径
- 证据：日志/截图（脱敏）

---

## Roadmap

> Roadmap 不是承诺，但会指导方向。

- [ ] 适配更新版本的 RSSBox（当前固定 `2025.12.13`）
- [ ] 自动化鲁棒性增强（更稳定的选择器 / 回退策略）
- [ ] 更完善的诊断：一键导出可脱敏日志包
- [ ] 候选路由解释更清晰（评分、匹配依据、参数可视化）
- [ ] 更友好的本地化与错误提示（中英一致）

---

## 致谢
- RSSHub: https://github.com/DIYgod/RSSHub
- RSSBox: https://github.com/versun/rssbox
- Playwright: https://playwright.dev/

---

## 贡献指南

欢迎贡献（Issue / PR）：

- 🐛 Bug：请附复现步骤 + 环境信息 + 脱敏日志
- ✨ Feature：描述你的使用场景与期望行为
- 🔧 PR：建议先开 Issue 对齐方向，避免白做

链接：
- Issues: https://github.com/Sylthionys/RssAgent/issues
- Pull requests: https://github.com/Sylthionys/RssAgent/pulls

---

## 安全与隐私

- 不要在仓库提交 secrets（API keys、密码、cookies、内网 URL）
- 日志/诊断信息可能包含敏感数据，分享前务必脱敏
- 如果你发现潜在安全问题，建议优先通过私密渠道联系维护者（可在 Issue 中说明“可私下提供细节”）

---

## 许可证
MIT License. See [`LICENSE`](./LICENSE).

---

## English

<details>
<summary><b>Show English README</b></summary>

### What is RssAgent?
RssAgent is a Windows WPF desktop app that converts natural-language subscription requests into RSSHub routes and automates adding them into RSSBox via Playwright.

### Status
⚠️ **Alpha / Pre-release** — usable but not stable. Breaking changes may happen, and RSSBox UI changes may break automation.

### Compatibility
- Windows 10/11
- .NET 8 Desktop Runtime (run), .NET 8 SDK (dev)
- **RSSBox tested with: `2025.12.13` (newer versions are not guaranteed)**

### Quick start (Users)
1. Download the latest **pre-release** zip from Releases:
   https://github.com/Sylthionys/RssAgent/releases
2. Extract and run
3. Install **.NET 8 Desktop Runtime** if prompted
4. Configure RSSHub / RSSBox / Gemini in the app

### Quick start (Developers)
```powershell
dotnet restore
dotnet build -c Release
dotnet run --project src\RssAgent.App
```

### Publishing
```powershell
dotnet publish src\RssAgent.App -c Release -o artifacts\RssAgent
```

### Troubleshooting (Playwright offline)
```powershell
pwsh src\RssAgent.App\bin\Release\net8.0-windows\playwright.ps1 install chromium
```

### Links
- Releases: https://github.com/Sylthionys/RssAgent/releases
- Issues: https://github.com/Sylthionys/RssAgent/issues
- RSSHub: https://github.com/DIYgod/RSSHub · Docs: https://docs.rsshub.app/
- RSSBox: https://github.com/versun/rssbox · Docs: https://rssbox.app/

### License
MIT. See `LICENSE`.

</details>
