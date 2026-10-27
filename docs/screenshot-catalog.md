# 截图软件清单交付（preview.2）

2026-10-27。用户更正：不加入此前推荐20款，只加入两张截图内软件，再加入Codex。文件夹快捷方式不纳入。

## 精确范围：21项

Antigravity、AnythingLLM、Chatbox、Cursor、DeepSeek Harness、GitHub Copilot、GitHub Desktop、Google Cloud SDK Shell、Grok Bot、Obsidian、Ollama、Qoder、QoderWake、TRAE CN、TRAE Work CN、Visual Studio Code、WorkBuddy、豆包、ChatGPT、Claude桌面、Codex。

## 实现与决策

复用现有Core/Windows/Adapters/App分层；新增只读ToolCatalog和ToolInventory，扩展精确进程名/卸载名候选识别与UI清单、文本报告。Catalog静态集合及别名不可变；不按模糊前缀匹配，不把node/cmd或通用copilot宿主当作某产品。没有读取快捷方式目标、启动目标、读取会话/笔记/Key、改变网络、扩大写入权限或删除代码。

Claude截图按桌面版登记，主界面不再调用Claude Code用户配置检查；历史Claude Code适配器及测试保留，但不纳入本次清单。QoderWake、TRAE Work CN等没有已验证配置schema，加入清单及候选发现，不伪装代理适配完成。GitHub Desktop、Google Cloud SDK Shell、Obsidian、VS Code按配套工具显示，不全标Agent。分类仅为清单整理，不代表发行者身份验证。

开源路线沿用批准的小核心自研；复用本项目已有注册表读取、进程采集、脱敏、WPF和.NET运行时打包，不新增/升级/删除NuGet依赖，不新建数据库。查阅[DeepSeek Harness项目](https://github.com/deepseek-ai/deepseek-harness)、[QoderWake官方记录](https://docs.qoder.com/release-notes/qoderwake)核对名称独立性，没有复制其代码或把项目宣传作为本机身份依据。

## 验证及发现的问题

- 清单/精确匹配2例先实际失败，再实现后通过；随后增加状态/版本/完整性回归。Release单元62项通过。
- 首次全量集成22通过、1失败：旧LaunchIsolationTests使用GetProcessById后未持有真实句柄，读取退出码偶发失败。已在通知子进程退出前持有SafeHandle，重跑23项通过；不修改产品启动行为。
- 初次真实窗口启动失败：DataGrid.RowHeight设置Auto无法转换Double。新增真实WPF窗口构造回归先失败，移除非法设置后通过。此测试在STA线程构造并关闭窗口，不显示或操作桌面。
- 最终Release集成24/24通过；自包含包已实际打开，清单显示21项，本机只读采集后候选状态/版本更新，清单底部确认Codex和Claude桌面独立。包483文件SHA256通过；格式与源码检查记录见verification.md。

## 边界与后续

只证明清单、候选匹配和界面能力，不证明全部21项在本机已安装、联网正常或修复支持。商店版/便携版/共享宿主可能漏检；同名进程可能是假冒或不同形态。未知和读取不完整分开显示。元数据采集增加只读枚举工作，未做性能基准；没有新增网络请求、依赖或驻留进程。系统代理写入、真实Agent配置与启动仍锁定，所有业务与TUN退出验收仍未完成。没有上传GitHub。

P0逐产品可信身份/配置来源与代理语义核实；P1完善商店/便携/插件发现及真实业务验收；P2清单筛选与干净机测试。当前修改属于现有诊断范围的扩展，没有更换架构或增加费用。
