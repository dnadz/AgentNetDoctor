> 本文件保存批准的制作基线/实施计划，历史状态段不代表当前实现。实际完成度见 [STATUS](STATUS.md)。

# Windows Agent 网络与运行环境工具 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Recommended execution: the primary agent implements sequentially in this session; obtain an independent whole-change review before release. Do not assume authorization for remote publication or real machine writes from a test task.

**Goal:** 制作中文Windows工具，在启动Agent前检查和配置直连/现有代理共存，配置后退出，验证正常关闭VPN不影响可直连目标，并从首个提交准备GitHub交付。

**Architecture:** 小型C#核心、Windows采集/执行边界、工具适配器和WPF界面分离。使用系统能力，参考前期GitHub调查，按许可选择性复用小组件，不整库fork，不创建VPN、代理网关或常驻守护。

**Tech Stack:** C# / .NET 10 LTS / WPF / System.Text.Json；Windows API及固定JSON采集脚本；xUnit测试。SDK与可选解析依赖在Task 0核对并固定版本；生产代码不依赖云端LLM。

**Spec:** [制作基线1.2](./design.md)。本计划不是另一套产品定义，执行者必须同时阅读两份文件。

## Global Constraints

- Windows 11 x64 中文桌面程序，中文提示、帮助和报告，优先中国网络环境。
- 软件退出后不保留托盘进程、服务、计划任务、监听器或新的本地代理网关。
- Codex、Antigravity、WorkBuddy分别识别形态；Claude Code首版以本机交互CLI为主；DeepSeek是所选调用方的API提供商。
- 默认本机采集不主动外网探测；主动联网、配置写入和可能计费测试需要清楚显示作用。
- 未知版本只读；采集失败、权限不足、未安装、未配置不得混为一类。
- 无显式代理不等于绕过TUN；本工具探针通过不等于目标App业务通过。
- 修复前有差异、确认、备份和过期检查；不覆盖外部新修改，不强杀Agent。
- 安装向导、缓存清理、GPU、国家专业版和多平台不进入本计划。
- 本文中的命令均为将来执行步骤，目前未运行；复选框代表待办而非完成。

## Review Focus

1. VPN正常退出与崩溃/kill switch残留：前者在已支持组合必须实测，后者如不能恢复应明确未支持；Task 10负责。
2. 用户预览后客户端改写配置：写入/回滚应返回冲突而非覆盖；Task 7/8负责。
3. 单实例程序或Claude后台进程不继承启动环境：不能误称配置生效、不能强杀；Task 4/8负责。
4. 日志/URL/命令行包含凭据与恶意文本：不能泄漏、自动请求或执行；Task 1/5/6负责。
5. 仓库忽略规则漏掉已跟踪秘密、产品包误含用户数据：实际源码与历史白名单检查；Task 2/11负责。

## 执行位置、文档和接口约定

产品目录拟定为 `AgentNetDoctor/`，不把父目录建为产品仓库。下文文件路径均相对这个尚未创建的产品目录。

开工时把制作基线的可发布版本复制到 `docs/design.md`，实施计划到 `docs/implementation-plan.md`；移除只适合私人项目的聊天、个人环境和父目录链接。研究数据使用现有公开仓库快照并保留2026-10-25日期，不称新调查。`docs/STATUS.md`按任务记录实际命令、结果、未验证项和下一步。

源代码结构沿用基线四模块：`Core`、`Windows`、`Adapters`、`App`。测试分为Unit、Integration和人工WindowsAcceptance。先完成一个共享的数据模型，不为每层复制一套状态和DTO。

确切工程文件：根目录 `AgentNetDoctor.slnx`；`src/AgentNetDoctor.Core/AgentNetDoctor.Core.csproj`、`src/AgentNetDoctor.Windows/AgentNetDoctor.Windows.csproj`、`src/AgentNetDoctor.Adapters/AgentNetDoctor.Adapters.csproj`、`src/AgentNetDoctor.App/AgentNetDoctor.App.csproj`；`tests/Unit/AgentNetDoctor.UnitTests.csproj`、`tests/Integration/AgentNetDoctor.IntegrationTests.csproj`。WindowsAcceptance为人工记录而非假装可自动通过的测试程序集。Core保持不访问Windows全局状态，其余使用经M0确认的Windows目标框架。

测试用例按“输入→状态/副作用断言”实现，不写仅镜像实现的测试。例：`PermissionDeniedIsNotMissing`断言输出仍为PermissionDenied；`ChangedConfigRejectsApply`断言计划冲突且原文件不变；`ExplicitDirectIgnoresInheritedProxy`断言代理夹具命中数为0且目标夹具为1；`ExitDoesNotLeaveWatcher`断言无本工具守护/任务/新增监听端口。这些是未来测试契约，不是已运行结果。

公共接口均位于Core；操作系统实现位于Windows，产品实现位于Adapters，界面只调用接口。具体方法体交给实现者；下文签名与状态名是任务之间的契约。

## Task 0：M0事实与技术能力核实

**Files:** 创建 `docs/compatibility.md`、`docs/acceptance-environment.md`、`docs/STATUS.md`、`docs/decisions.md`。

**Interfaces:** 产出不含凭据的支持记录：产品、形态、版本、Windows版本/架构、VPN产品/版本、模式、代理协议、正常退出方式、已观察与未测试能力。没有运行就记录not_tested。

- [ ] 读取目录内AGENTS/README及两份基线，核对现有工作区，确定实际产品目录不覆盖其他项目。
- [ ] 检查可用.NET SDK、Git和Windows构建条件；缺少依赖时先记录必要项，安装遵守环境权限，不悄悄使用另一用户的配置。
- [ ] 在授权范围只读采集目标身份、版本和VPN模式，不获取APIKey、聊天或订阅；无法自行确认的事实仅在阻碍适配时问一个具体问题。
- [ ] 从官方资料与实际版本验证代理配置来源、覆盖链和单实例行为。用最小隔离验证核实，实际配置变更仍先展示差异。
- [ ] 锁定.NET SDK、测试和解析组件，写明许可证与必要性；库无法保真处理目标配置则该版本不开放写入。
- [ ] 固定第一组支持组合，记录端口/代理来源为观察值，不从默认端口猜。若需要驱动、常驻服务或全局路由修改才能满足主要目标，返回说明，不偷偷改路线。
- [ ] 本任务通过标准：支持记录可据此写适配器和真实验收；根因未确定不阻止只读诊断，但不能假定修复方案已成立。

## Task 1：核心证据模型、序列化与报告边界

**Files:** 创建四个src项目与解决方案、`global.json`、`Directory.Build.props`、`Directory.Packages.props`；Core中的 `Models/Observation.cs`、`Models/ToolInstance.cs`、`Models/NetworkSnapshot.cs`、`Models/ProbeResult.cs`、`Models/Finding.cs`、`Reporting/ReportWriter.cs`；创建 `tests/Unit/ReportWriterTests.cs`。

**Interfaces:**

- `ObservationStatus`: Success, Failed, NotTested, NotApplicable, PermissionDenied, UnsupportedVersion, Partial。
- `Attribution`: Confirmed, Probable, Unknown；归因与ObservationStatus分开。
- `ProcessIdentity(int Pid, DateTimeOffset StartedAt, string ExecutablePath)`。
- `Observation<T>(ObservationStatus Status, T? Value, string? ErrorCode, DateTimeOffset ObservedAt)`。
- `ToolInstance(string Id, string Form, string? Version, ProcessIdentity? Process)`。
- `NetworkSnapshot`包含Id、SchemaVersion、ObservedAt、结构化代理/接口/路由/端口Observation与完整性状态。
- `ProbePath`: NoExplicitProxy, ExplicitProxy, ProcessDefault。
- `ProbeResult`包含EndpointId、ProbePath、各阶段Observation、SnapshotId、耗时；不包含凭据。
- `Finding`包含Id、RuleId、ToolId、Attribution、EvidenceIds、中文说明与建议动作ID。
- `ReportWriter.WriteAsync(ReportData data, Stream destination, CancellationToken cancellationToken)`；ReportData汇总以上模型和固定schemaVersion=1，不接收原始密钥字段。

- [ ] 写失败测试：序列化NotTested仍为NotTested；PermissionDenied不能成为“未安装”；合成凭据不能出现在可分享报告。
- [ ] 执行：`dotnet test tests/Unit --filter FullyQualifiedName~ReportWriterTests`，确认预期失败；仅环境故障不计作有效业务红灯。
- [ ] 实现最小模型/序列化与脱敏字段白名单；写入报告失败应可见，不吞异常。
- [ ] 同命令通过，再运行 `dotnet build -c Release`。
- [ ] 初始化独立本地Git仓库并提交本任务；不添加远端、不带入父目录或真实数据。

## Task 2：GitHub源码卫生与开发交付骨架

**Files:** 创建 `.gitignore`、`.gitattributes`、`README.md`、`LICENSE`、`THIRD_PARTY_NOTICES.md`、`SECURITY.md`、`CONTRIBUTING.md`、`CHANGELOG.md`、`.github/ISSUE_TEMPLATE/bug.yml`、`.github/pull_request_template.md`、`scripts/Verify-Source.ps1`、`tests/SourceAudit/Verify-Source.Tests.ps1`。

**Interfaces:** `Verify-Source.ps1 -RepositoryRoot <absolute-path>` 对实际跟踪文件检查，0表示未发现禁止项，非0表示失败；输出相对路径、规则ID和脱敏说明，不打印密钥原文。内部允许独立测试指定合成文件集，不扫描产品目录外文件。

- [ ] 用临时夹具先写失败测试：已跟踪的.env、用户报告/备份、带合成密钥的配置必须失败；合成示例明确标记后允许。
- [ ] 运行 `powershell.exe -NoProfile -File tests/SourceAudit/Verify-Source.Tests.ps1` 确认失败。
- [ ] 实现实际文件集审查；忽略bin/obj、artifacts、用户日志、备份、真实配置和凭据文件，但仍检查被强制跟踪的文件。
- [ ] README先写真实状态“开发中/尚未验收”，明确中文、Windows、使用前检查、退出限制；不放个人电脑路径或未通过的徽章。
- [ ] MIT作为原创建议许可，仅在实际复用组件兼容且权利确认后定稿；第三方清单逐项记录来源SHA/许可证/修改。SECURITY提供真实可用报告途径，未设渠道就标明尚未开放，不编造邮箱。
- [ ] 来源测试通过，并执行 `powershell.exe -NoProfile -File scripts/Verify-Source.ps1 -RepositoryRoot .`；提交。

## Task 3：Windows只读采集、进程身份和端口证据

**Files:** Core `Collection/ISnapshotCollector.cs`；Windows `Collection/WindowsSnapshotCollector.cs`、`Collection/ProxyReader.cs`、`Collection/ProcessPortReader.cs`；Unit `SnapshotCollectorTests.cs`、`PortEvidenceTests.cs`；Integration `WindowsReadOnlyTests.cs`。

**Interfaces:** `ISnapshotCollector.CollectAsync(CancellationToken cancellationToken) -> Task<NetworkSnapshot>`；采集器只读目标配置/网络，写文件由报告器负责；PortRecord使用ProcessIdentity和观察时间，不仅PID。

- [ ] 失败测试覆盖无权限、进程退出/PID重用、IPv4/IPv6、WinINET与WinHTTP不一致但没有错误证据、虚拟网卡存在却不一定是故障。
- [ ] 运行 `dotnet test tests/Unit --filter "FullyQualifiedName~SnapshotCollectorTests|FullyQualifiedName~PortEvidenceTests"` 确认失败。
- [ ] 实现Windows API和必要固定JSON采集；不同作用域分开，不导出完整进程命令行，不为发现失败强制提权。
- [ ] 对采集器记录动作审计，确保没有set/reset/kill操作；外网探测不在采集器中。
- [ ] 单元测试通过；在隔离Windows环境运行 `dotnet test tests/Integration --filter FullyQualifiedName~WindowsReadOnlyTests`，确认没有目标写入且诊断器不新增监听服务；提交。

## Task 4：四类目标适配、运行环境与能力白名单

**Files:** Core `Adapters/IAgentAdapter.cs`、`Adapters/AdapterCapabilities.cs`；Adapters `Codex/CodexAdapter.cs`、`WorkBuddy/WorkBuddyAdapter.cs`、`Antigravity/AntigravityAdapter.cs`、`ClaudeCode/ClaudeCodeAdapter.cs`；Unit `AdapterIdentityTests.cs`、`ConfigurationReadTests.cs`、`RuntimeRequirementTests.cs`。

**Interfaces:** `IAgentAdapter.DiscoverAsync(CancellationToken) -> Task<IReadOnlyList<ToolInstance>>`；`ReadConfigurationAsync(ToolInstance tool, CancellationToken) -> Task<AgentConfiguration>`；`GetCapabilities(ToolInstance tool) -> AdapterCapabilities`。AgentConfiguration只含允许字段、来源/覆盖链、Observation状态与EndpointSpec，不返回密钥值；AdapterCapabilities分别列只读和可修复字段/机制及已验证版本。

- [ ] 先测试WorkBuddy不能因codebuddy命令存在就被确认安装；未知版本修复能力为空；配置语法错误不能变成“未配置”；可选Node/Python缺失不误报。
- [ ] 增加单实例/Claude supervisor夹具：终端新环境不代表已有后台进程已采用；首版不强制停止它们。
- [ ] 运行 `dotnet test tests/Unit --filter "FullyQualifiedName~AdapterIdentityTests|FullyQualifiedName~ConfigurationReadTests|FullyQualifiedName~RuntimeRequirementTests"` 确认失败。
- [ ] 根据Task 0实测版本实现发现和只读解析；DeepSeek配置检查隶属实际调用方，不新增同名本地服务。
- [ ] 使用合成配置测试UTF-8/BOM、重复字段、注释、路径空格与覆盖链；解析不能保真写回则Capabilities禁写。
- [ ] 同命令通过，更新支持矩阵并提交。真实凭据/日志不得进入夹具。

## Task 5：受控联网探针与本地代理测试夹具

**Files:** Core `Networking/IProbeRunner.cs`、`Networking/EndpointSpec.cs`、`Networking/ProbeRequest.cs`；Windows `Networking/ProbeRunner.cs`；Integration `Fixtures/LoopbackProxy.cs`、`ProbePathTests.cs`、`ProbeBoundaryTests.cs`。

**Interfaces:** EndpointSpec定义Id、Host、Port、允许路径、用途、来源与认证需求；`ProbeRequest(EndpointSpec Endpoint, ProbePath Path, Uri? ProxyUri, string SnapshotId)`不含凭据；`IProbeRunner.RunAsync(ProbeRequest request, CancellationToken) -> Task<ProbeResult>`。

- [ ] 先写测试：设置合成HTTP_PROXY后，NoExplicitProxy仍命中目标夹具而非代理；ExplicitProxy命中指定夹具；跨host重定向不发送认证；日志URL不会自动加入目标。
- [ ] 运行 `dotnet test tests/Integration --filter "FullyQualifiedName~ProbePathTests|FullyQualifiedName~ProbeBoundaryTests"` 确认失败。
- [ ] 实现独立handler/连接池与逐阶段结果；TCP3秒，TLS/HTTP阶段10—15秒预算；DNS和整体取消同样受控。
- [ ] 夹具只在回环动态端口监听，模拟401/403/407/429、超时、流式中断和取消。默认不调用公网，无真实Key。
- [ ] 明确NoExplicitProxy不能绕过TUN，系统解析与代理解析可观察性分别记录；不支持的代理协议返回UnsupportedVersion或明确能力不支持，不能偷偷换协议。
- [ ] 测试通过并提交。

## Task 6：规则归因、对照与中文报告

**Files:** Core `Diagnostics/DiagnosticEngine.cs`、`Diagnostics/RuleCatalog.cs`、`Reporting/Redactor.cs`、`Reporting/MarkdownReport.cs`；Unit `DiagnosticEngineTests.cs`、`RedactorTests.cs`。

**Interfaces:** `DiagnosticEngine.Analyze(NetworkSnapshot snapshot, IReadOnlyList<AgentConfiguration> configurations, IReadOnlyList<ProbeResult> probes) -> IReadOnlyList<Finding>`；`Redactor.Redact(string text) -> string`。规则ID按制作文档第9节，不能用未校准百分比。

- [ ] 用事实夹具写失败测试：共享远端443不是端口冲突；存在明确占用与对应bind失败才Confirmed；单次代理无响应仅Probable；403不直接归因为国家限制；高TIME_WAIT不等于端口耗尽。
- [ ] 测试未知、未测、无权限在报告中保持可见；恶意日志文本、代理URL凭据、密钥和个人路径不会成为执行指令或明文分享内容。
- [ ] 运行 `dotnet test tests/Unit --filter "FullyQualifiedName~DiagnosticEngineTests|FullyQualifiedName~RedactorTests"` 确认失败。
- [ ] 实现规则和中文说明，标明证据、替代原因和下一步；不把输入日志当成网络目标或配置命令。
- [ ] 测试通过，生成合成示例报告复核后提交。

## Task 7：修复计划、确认约束与受保护备份

**Files:** Core `Repair/RepairPlan.cs`、`Repair/RepairPlanner.cs`、`Repair/IRepairTargetStore.cs`；Windows `Repair/ProtectedBackupStore.cs`；Unit `RepairPlanTests.cs`、`BackupBoundaryTests.cs`。

**Interfaces:** ActionKind仅CreateLaunchProfile、PatchAppFields、RestoreUserManualProxy、LaunchApprovedTarget；`RepairPlanner.CreatePlan(Finding finding, AgentConfiguration configuration, RepairIntent intent, NetworkSnapshot snapshot) -> RepairPlan`；计划包含BeforeFingerprint、目标版本、精确差异、影响范围、验证目标、回滚策略和确认摘要。RepairIntent为用户选择，不是LLM命令。`IRepairTargetStore.ReadFingerprintAsync(RepairPlan plan, CancellationToken) -> Task<string>`。

- [ ] 先测试未知版本/企业策略/PAC不明/不受控路径不能产生可执行写动作；预览后hash变化计划过期；普通修复不能修改提供商、模型、账号或全局变量。
- [ ] 备份测试覆盖只允许当前用户、备份失败禁止写目标、重解析点拒绝、报告只出现备份ID不出现秘密。
- [ ] 运行 `dotnet test tests/Unit --filter "FullyQualifiedName~RepairPlanTests|FullyQualifiedName~BackupBoundaryTests"` 确认失败。
- [ ] 实现类型化计划、差异与用户级保护；计划不接受任意脚本/注册表路径；原始配置只进入受保护备份。
- [ ] 测试通过并提交。备份或保护机制无法证明有效时不得继续真实写入。

## Task 8：R1/R2执行、短时启动入口与冲突感知回滚

**Files:** Core `Repair/RepairExecutor.cs`、`Repair/RepairRecord.cs`、`Repair/VerificationResult.cs`；Windows `Repair/AppFieldPatcher.cs`、`Launch/LaunchProfileStore.cs`、`Launch/TargetLauncher.cs`；Integration `RepairRoundTripTests.cs`、`LaunchIsolationTests.cs`。

**Interfaces:** `RepairExecutor.ApplyAsync(RepairPlan plan, ConfirmedPlan confirmation, CancellationToken) -> Task<RepairRecord>`；`RollbackAsync(RepairRecord record, CancellationToken) -> Task<RollbackResult>`；ConfirmedPlan绑定计划ID/确认摘要，不能用一个布尔值批准新计划；`TargetLauncher.StartAsync(LaunchProfile profile, CancellationToken) -> Task<LaunchOutcome>`。LaunchProfile记录可信绝对路径、参数列表、模式及非秘密代理信息，不保存代理密码/APIKey。

- [ ] 先测试应用字段合并保留未知字段；目标被外部改写则写入/回滚冲突；执行中断后先核对状态；退出或取消不删除用户文件。
- [ ] 启动测试：子进程收到允许的环境，父进程和用户全局变量不变；直连配置不能只清变量却声称应用代理已禁；已有单实例占用时返回NeedsUserExit，不强杀。
- [ ] 运行 `dotnet test tests/Integration --filter "FullyQualifiedName~RepairRoundTripTests|FullyQualifiedName~LaunchIsolationTests"` 确认失败。
- [ ] 实现R1/R2；可复用快捷入口调用产品可执行文件的短时 `--launch <profile-id>` 模式，读取允许ID而非任意外部计划；启动后退出，不保留WPF/托盘/代理服务。
- [ ] 正常交互App缺省由用户保存退出；回滚只恢复本工具实际改动且仍匹配预期的对象。受保护备份无法恢复时显示失败，不覆盖空文件。
- [ ] 测试通过，检查退出后无新增常驻进程/监听端口，提交。

## Task 9：R3当前用户手动代理与R4正常重新启动

**Files:** Windows `Repair/UserManualProxyAction.cs`、`Repair/NormalRestartCoordinator.cs`；Core `Repair/CrossTargetVerifier.cs`；Integration `UserProxyActionTests.cs`、`RegressionGuardTests.cs`。

**Interfaces:** `CrossTargetVerifier.VerifyAsync(RepairRecord record, IReadOnlyList<VerificationTarget> targets, CancellationToken) -> Task<VerificationResult>`；VerificationTarget区分探针和人工业务确认；`UserManualProxyAction`只能消费Task7生成的RestoreUserManualProxy动作，不能接受任意注册表输入。

- [ ] 先测试仅当前用户静态手动代理且已复核失效才可执行；PAC/WPAD、企业策略、状态未知一律拒绝；不改WinHTTP、DNS/路由或全局环境变量。
- [ ] 测试一个目标恢复但此前正常目标回归会阻止“全部成功”；未验证目标仍为NotTested；未预授权回滚时提示而非自动反复切换。
- [ ] 运行 `dotnet test tests/Integration --filter "FullyQualifiedName~UserProxyActionTests|FullyQualifiedName~RegressionGuardTests"` 确认失败。
- [ ] 在隔离用户配置夹具/VM中实现精确字段变更与Windows设置通知；不能在开发者当前工作环境跑破坏性集成测试。
- [ ] 正常退出/重新启动要求用户保存工作；未退出就等待/取消。测试通过后提交，R3真实环境需单独展示作用范围再确认。

## Task 10：中文WPF工作流与VPN时序验收

**Files:** App `MainWindow.xaml`、`MainWindow.xaml.cs`、`ViewModels/MainViewModel.cs`、`Resources/Strings.zh-CN.xaml`；Unit `WorkflowStateTests.cs`；WindowsAcceptance `vpn-transition-checklist.md`、`acceptance-result.schema.json`。

**Interfaces:** MainViewModel状态：Idle、Collecting、AwaitingProbeConsent、Probing、ReviewingPlan、Applying、Verifying、Completed、Partial、Failed；只有Matching ConfirmedPlan才能从ReviewingPlan进入Applying。UI消费前述接口，不直接写系统设置。

- [ ] 先测试无联网确认不能探测、未确认差异不能执行；未知版本没有修复按钮；探针成功但无App业务证据只显示部分验证；退出不创建自启动/计划任务。
- [ ] 运行 `dotnet test tests/Unit --filter FullyQualifiedName~WorkflowStateTests` 确认失败，再实现最小中文界面并通过。
- [ ] 在人工验收中依次执行：VPN关闭直连可用；VPN开启代理/直连并行；退出本软件后正常关闭VPN，直连新请求可用；VPN关闭重新启动直连目标可用；重开VPN再启动代理目标可用。
- [ ] 改变四类目标启动顺序复测，包含DeepSeek实际调用方；认证/付费请求使用用户明确确认的最小任务，不导出Key。
- [ ] 异常VPN退出/kill switch残留作为边界记录，不把它包装成正常退出通过；如正常退出场景失败，主目标未完成，先报告原因和支持差距。
- [ ] 用支持矩阵标明VPN产品/版本/模式、目标版本、网络状态、自动/人工结果及未测项；敏感实测记录留本地，仓库只放脱敏摘要。
- [ ] 提交界面与合成测试，不提交原始用户报告。

## Task 11：GitHub CI、源码上传与draft Release准备

**Files:** 创建 `.github/workflows/ci.yml`、`.github/workflows/draft-release.yml`、`scripts/Prepare-Source.ps1`、`scripts/Package-Windows.ps1`、`docs/release-checklist.md`、`docs/verification.md`、`docs/github-upload-checklist.md`。

**Interfaces:** Prepare-Source仅复制固定白名单到新artifacts/source目录，绝不移动/删除用户文件；Package-Windows发布完整win-x64目录、包和SHA256，不声称未验收的安装器正常；所有scripts非0表示失败，失败不能继续推送或发版。

- [ ] 先用合成私密文件测试来源白名单与实际Git文件不一致会失败；准备源包后解包检查文件清单。
- [ ] 在本地运行来源检查、`dotnet test tests/Unit -c Release`、`dotnet test tests/Integration -c Release`、`dotnet build -c Release`、`dotnet format --verify-no-changes`；Windows集成测试必须使用隔离夹具，不改真实系统代理。实际结果写入verification，不预填通过。
- [ ] 配置Windows CI：默认contents:read，第三方Actions固定复核后的SHA，PR不使用真实API/VPN凭据，不进行公网业务请求，不把运行日志整体上传。
- [ ] 生成自包含Windows包，记录实际依赖/NOTICE/SBOM可用性；安装包在真正完成安装/升级/卸载验证后才作为正式资产。缺签名如实说明，不要求关闭保护。
- [ ] 复核实际跟踪文件和提交历史；发现真实秘密时先处理泄漏与凭据，不仅添加.gitignore或删除当前文件就继续push。
- [ ] 核对GitHub连接身份、最终所有者、仓库名及可见性；暂建议agent-net-doctor，名称/目标未确认前不建库、不推送。不要把其他项目历史或父proposals目录带入。
- [ ] 首次上传前展示最终文件列表、LICENSE/NOTICE、已运行检查、未验证项和目标URL；目标已明确后按授权执行普通源码上传，不反复确认常规提交。
- [ ] draft-release仅手动或已审核版本触发，默认draft，不由普通push正式发布；写权限只限该工作流。核对源码commit、包、校验值和支持矩阵后，正式公开Release另行确认。
- [ ] GitHub页面与资产实际核验后，才能报告“已上传/已发布”；本地commit或打包不算上传。

## 依赖关系、进度与停止条件

顺序：Task0 → 1 → 2 → 3/4 → 5 → 6 → 7 → 8 → 9 → 10 → 11。3/4虽然可独立设计，本轮按单会话顺序做，不默认增加多Agent上下文费用。界面可在核心契约稳定后逐步验证，不等到所有功能完工才看真实使用流程。

每任务通过相关测试后做小提交，写STATUS和未验证项。首次执行先做Task0及只读功能；若VPN/客户端能力不允许主要退出时序，不用常驻服务或全局路由重构偷换目标。

原3—4工作周只是旧范围估算；新增Claude和退出时序后，Task0结束依据实际支持组合给出更新排期。目前不承诺固定完成日或资助获批时间。

## 制作与上传的完成定义

- 源码：用户目标完整实现，未知/不支持边界可见，测试/构建实际通过。
- 修复：至少指定组合完成目标App业务与跨工具、VPN退出时序；无证据不能称完成。
- 安装：干净安装/升级/卸载实测，程序退出无守护，目标Agent数据不被破坏。
- GitHub：代码与许可/说明/测试可复现，敏感数据与原始日志不入仓库，实际远端文件和资产核验完成。
- 发布：支持矩阵、已知限制、SHA256、资产和源码commit一致；draft不算正式发布，正式发布须用户确认。

## 本计划自审与本轮状态

覆盖映射：基线1—2/5/8由Task0/4/10；第6节由Task3；第7节由Task5；第9节由Task6；第10—11节由Task7—9；第12—14节由Task1/2/7—10；第15—16节由各任务测试和Task10；第17—21节由Task0/11及完成定义。风险条件均有任务归属，不把未测项藏在报告中。

本轮只创建/更新文档，未执行本文命令、初始化产品仓库、安装依赖、编写/测试程序、改网络或推送GitHub。具体OS、VPN及客户端事实在Task0核实；API/账号资格和服务端可用性不能由本工具保证。
