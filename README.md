# AgentNetDoctor

开发中的中文Windows Agent网络诊断工具。目标是启动Agent前检查直连/现有代理设置，配置后退出；不提供VPN、代理节点或后台守护。

**Mac版已新增源码与双架构检查/打包流程。** 复用C#核心，新增独立中文简单界面和Mac实际代理/进程/TCP采集；不改写系统网络。Mac预览是否可下载以[Releases](https://github.com/dnadz/AgentNetDoctor/releases)实际附件为准，Apple Silicon选osx-arm64、Intel选osx-x64。[使用与签名限制](docs/macos-guide.txt) · [实际验收与差距](docs/macos-audit.md)。

[下载预览版](https://github.com/dnadz/AgentNetDoctor/releases)（首次GitHub检查及打包通过后提供Windows x64 ZIP）。完整解压后双击AgentNetDoctor.App.exe。[使用与限制](docs/release-preview.4.md)。

**最新preview.4：普通用户简单模式。** 选择遇到问题的软件→点击“开始检查”→阅读白话结果和下一步。无需填写网址或端口；高级服务测试与报告默认收起。旧独立“连接对比”页及未开放修复入口已移除，底层检测能力仍保留。[简单模式说明](docs/simple-mode.md)。

当前提供0.1中文免安装试用包：默认简单流程，按需展开电脑整体检查、服务路径测试和技术报告。系统代理及真实Agent写入仍锁定，不能视为自动修复成品；XSUS TUN与目标Agent真实业务/退出时序尚未验收。以下preview.2/3说明为此前增量记录，实际入口以preview.4为准。

preview.2按用户更正，仅纳入截图20项软件及Codex，共21项，新增“软件清单”页（现在共五页）。支持精确进程名/卸载元数据候选、版本候选与逐项适配说明；未观察到不代表未安装。范围见[清单与验证](docs/screenshot-catalog.md)。不加入此前推荐的20款名单；Claude桌面与Claude Code、Qoder与QoderWake、TRAE CN与TRAE Work CN分开。

preview.3新增21款软件的独立网络检查窗口：软件清单选中一项→“检查并优化所选软件网络”。实际关联PID/启动身份/TCP远端，检查系统/环境/应用候选代理，测试已配置的本机回环代理端口；对用户指定的实际服务进行DNS、TCP、TLS/HTTP及代理路径检查，按证据生成逐软件优化步骤。不是只列名称，也不是已执行自动优化；未知应用配置改写仍锁定。[功能与验证边界](docs/per-tool-network.md)。

窗口中先点“检查本机”；需要联网时选择目标并确认，再点“对比连接路径”。代理地址仅接受无凭据HTTP入口，不猜测VPN端口。默认不联网，不发送API Key，不执行付费推理请求。无显式代理不能绕过TUN。探测采用无认证GET且不跟随重定向，收到401/403不代表业务成功。

内部已实现加密备份、字段保真写入、冲突感知回滚、恢复日志与短时启动；经过隔离文件/子进程测试，所有真实Agent能力注册仍为空。残留手动代理仅在静态回环地址、两次明确拒绝、无PAC/自动发现与可见策略影响时生成计划；原生写入口由发布门禁硬锁，待隔离Windows验收后解锁。没有完整路由/DNS可达性验收，不会创建服务、自启动或托盘守护。

读取候选配置时只导出代理白名单与提供商配置存在性，不导出密钥。默认拒绝UNC/映射网络盘/设备/重解析路径；即使重解析点通向本地，也会明确标为未读取，不要求改变原应用的数据位置。

## 开发

.NET 10 SDK；在Windows执行 `dotnet test`、`dotnet build -c Release`。测试使用合成数据/本地夹具，不需要真实API Key。

加密备份测试需要已加载当前用户资料的正常Windows上下文；沙箱/冒充用户上下文下DPAPI可能拒绝保护，此时产品拒绝继续，不降级明文。真实跨用户/干净VM仍待验证。

源码运行：`dotnet run --project src/AgentNetDoctor.App`。免安装包包含.NET 10.0.11 Windows x64运行时，解压整个文件夹后双击AgentNetDoctor.App.exe，不需要另装SDK。已在开发机实际运行，未做干净机验收；本包未签名。

打包：在仓库根目录运行 `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/Build-Portable.ps1`（只对该子进程生效，不改变全局执行策略）。输出在artifacts，附文件SHA256、ZIP校验、中文说明与运行时许可；构建输出不提交到源码仓库。[试用包说明](docs/portable-guide.txt)。

制作基线：[设计](docs/design.md)、[实施计划](docs/implementation-plan.md)、[实际进度](docs/STATUS.md)、[验证](docs/verification.md)。

配置解析引用Tomlyn 2.10.1（BSD-2-Clause），完整许可随源码与构建输出保留；Windows备份使用现有桌面框架的ProtectedData，不新增密码学NuGet实现。

[当前完整审计](docs/phase-5-audit.md)记录修改范围、依赖、验证与目标差距。

前期调查六个GitHub项目后选择自研小核心；参考成熟做法，未复制候选代码。调查是2026-10-25快照：[研究证据](docs/research/github-evidence-2026-10-25.json)。

## 隐私

原始日志、API Key、代理订阅和修复备份禁止提交。运行时数据独立保存；没有得到确认不修改系统代理、DNS或路由。
