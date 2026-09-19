# Codex ↔ Unity 接入说明（isuzu UnityMCP）

## 已安装

| 组件 | 位置 / 版本 |
| --- | --- |
| UPM 包 | `jp.shiranui-isuzu.unity-mcp` 4.3.0，**embedded 在 `Packages/jp.shiranui-isuzu.unity-mcp`**（含 ToolCatalog 补丁，见下）。`Packages/manifest.json` 里仍保留 git 源作为回退声明 |
| 命令行 | `%LOCALAPPDATA%\Programs\isuzu-unity-cli\isuzu-unity-cli.exe`（4.3.0，已加入用户 PATH，需新开终端） |
| Codex 配置 | `~/.codex/config.toml` → `[mcp_servers.isuzu-unity]`，HTTP + Bearer token |
| Agent skill | `~/.codex/skills/isuzu-unity-cli` |
| 服务端点 | Editor 启动时自动监听 `http://127.0.0.1:<稳定端口>/mcp`，本机当前为 27252 |

同一时刻只能有一个 agent 独占操作编辑器：Codely Bridge（TCP 2544）与 UnityMCP 会互相抢场景状态。

## 工具组过滤

Codex 端 URL 带 `?group=` 参数，限制暴露给模型的工具面（工具定义每次请求都要发一遍）：

- `?group=authoring,code,diagnostics,input` —— 当前配置，77 个工具，约 2.9 万 token
- 去掉 `?group=` —— 全部 106 个工具，约 4.1 万 token
- `?group=diagnostics` —— 30 个工具，约 1.1 万 token
- 可选组：`diagnostics` `authoring` `rendering` `timeline` `build` `code` `input`

`code` 组里含 `execute_code`（在编辑器里直接跑任意 C#），所以裁掉 `rendering`/`build`/`timeline` 也不会失去能力。改完需重启 Codex 应用（见文末）。

## ToolCatalog 补丁（已固化进 Packages/）

**症状**：`isuzu-unity-cli health` 或任何工具调用返回
`error [internal_error]: Could not load type Microsoft.CodeAnalysis.SeparatedSyntaxList while decoding custom attribute: (null)`

**原因**：本工程同时装了 Codely Bridge（`cn.tuanjie.codely.bridge`），其 `Plugins/Codely.Roslyn.dll`
是改过命名空间的 Roslyn 5.0，内部仍残留指向 `Microsoft.CodeAnalysis.*` 的属性类型引用；
而工程里实际可加载的是 UnityMCP 自带的 Roslyn 3.7。isuzu 的工具目录会遍历整个 AppDomain
并对每个类型解码自定义属性（`Editor/Core/ToolCatalog.cs`），只按程序集名前缀跳过
`Microsoft.CodeAnalysis*`，漏掉了这个改名版，于是异常冒泡、106 个工具全部加载失败。

**补丁**：在 `Editor/Core/ToolCatalog.cs` 中把两处属性解码包进 try/catch，遇到无法解析的属性类型
就跳过该类型（`IsTestFixtureType` 的 `GetCustomAttributes`，以及 `BuildFromTypes` 里的
`GetCustomAttribute<McpToolAttribute>()`）。

**现状（2026-09-12）**：包已从 `Library/PackageCache` 迁到 `Packages/jp.shiranui-isuzu.unity-mcp`
（embedded，补丁随源码留在仓库里）。解析时 embedded 副本优先，`Library/PackageCache` 下那份已被
Unity 自动删除，`packages-lock.json` 里该包为 `"source": "embedded"`。之后清空包缓存、Unity
重新解包都不会再冲掉补丁。

**升级该包**：`isuzu-unity-cli update` 不会动 `Packages/` 下的包。要升级就手动来：用上游新版本覆盖
`Packages/jp.shiranui-isuzu.unity-mcp`，再按上面的「补丁」把那两处 try/catch 重新打一次。

**回退**：删掉 `Packages/jp.shiranui-isuzu.unity-mcp` 整个目录，Unity 会按 `Packages/manifest.json`
里的 git 源重新拉取（补丁回到「需要重打」的状态）。

**触发重新编译**：Unity 在窗口失焦时不会感知脚本改动，改完包源码/新增脚本后需要点击一次
Unity 窗口（或让它获得焦点）才会刷新并重编译。用 CLI 的话也可以先 `isuzu-unity-cli call execute_code`
跑一句 `UnityEditor.PackageManager.Client.Resolve();` 把包解析催出来。

## Codex 侧注意事项

- MCP 配置（`~/.codex/config.toml` 里的 `[mcp_servers.isuzu-unity]`）是 **Codex 应用启动时读一次**。
  改完配置要完全退出并重开 Codex 应用；只新建 / 重启会话不够。若 Codex 进程的启动时间早于
  `config.toml` 的修改时间，那一轮就抓不到 Unity 工具。
- Codex 的命令沙箱读不到 `%LOCALAPPDATA%\UnityMCP`（编辑器描述文件目录），沙箱内跑
  `isuzu-unity-cli` 会误报 `No running Unity Editor found`；沙箱外运行正常。需要 CLI 时让它以
  沙箱外权限调用，或者直接用 HTTP 打 `http://127.0.0.1:27252/mcp`（token 在 `~/.codex/config.toml` 里）。
