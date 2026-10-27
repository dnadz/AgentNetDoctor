# Mac版实际实现与验收记录

目标：在原公开仓库新增中文Mac网络检查预览，不重写Windows界面，不将未验证的配置修改开放给用户。

## 已实现范围

新增Mac采集项目：固定路径scutil/ps/lsof命令、无shell拼接、10秒超时、取消与每输出流1MiB上限；解析系统代理/PAC、当前权限可见的进程/TCP和网卡/DNS配置。进程前后身份一致才关联端口；没有签名验证，仍是候选。只检查明确配置的数字回环代理入口，不扫描远端端口。

新增Avalonia简单界面：复用21项清单、Core结果摘要和分析。选择→开始→白话结果，高级选项默认收起。实际服务DNS/TCP/HTTP和可选HTTP代理路径需主动同意；无凭据、无重定向、不调用模型、不关闭TLS校验。配置为空/失败/未知不按正常处理。切换软件清空旧结果和联网同意，运行中锁定冲突控件，支持停止。

Mac建议使用独立说明，不套用Windows私有设置入口。Mac发行、CLI/共享宿主、应用内配置/工作区覆盖链仍未核实者明确未知；本版未扫描Agent私有配置或聊天。导出使用现有脱敏报告器，仅在用户选择位置保存，包含网络地址时提示分享前检查，不导出进程路径/环境原文，不自动上传。

Core提取HttpProbeRunner和ServiceDiagnostics供两平台共用；Windows原类保留兼容包装，WPF未替换。修正数字IPv6回环HTTP地址与端点解析器的一致性。没有数据库、对外API、凭据格式或真实修复权限变化。

## 依赖与路线依据

Avalonia、Desktop、Fluent固定12.1.4；测试单独引用Headless12.1.4，沿用xUnit2。曾尝试Headless.XUnit但该版本依赖xUnit3，与现有xUnit2产生CS0433；已改直接Headless会话，未升级原测试框架。依赖来自官方NuGet，遵循MIT及传递依赖许可。

开源路线调查见macos-proposal.md：复用Avalonia框架和自身Core，自研Mac适配；未forkLuLu/NetDoctor或复制其代码。WPF仅Windows、原生Swift双实现维护与框架许可是决策依据，不按Star断言适用。

## 本地已运行证据

- 解析夹具4项先实际NotImplemented失败后通过；采集器3项先有效运行失败后通过，覆盖命令失败与PID复用。最初未读参数编译错误不计业务红灯。
- 最新Mac测试19项通过：夹具、失败/身份变化、共享宿主不误认、端点凭据/查询/业务路径拒绝、本机端口边界、IPv6回环校验、真实合成回环HTTP不带认证/Cookie且不跟随302，以及Headless界面默认折叠/21项/联网门禁/切换清空同意。
- 共用探针提取后的Windows单元69项、正常用户上下文集成29项通过。完整Release构建0警告0错误。
- 没有在Windows执行真实Mac命令；Headless不是Mac真实GUI验收。后续远端Mac结果以GitHub Actions实际记录为准。

## 打包、审查与发布

Build-Mac.py只在Mac打包，分别osx-arm64/osx-x64自包含.app；验证Mach-O架构、ad-hoc签名、包结构，生成外置文件哈希清单与ZIP哈希。哈希清单不写入已签名.app，避免破坏资源封印。

许可放在.app/Contents/Resources中。独立只读审查发现运行时pack不在assets libraries，已补充显式复制.NET10.0.11运行时LICENSE.TXT及THIRD-PARTY-NOTICES.TXT，否则打包失败。其他审阅范围未发现可证实新增P1/P2；审查者未运行测试，不替代运行验证。

Mac CI分别运行测试、实际本机只读采集与打包；两个架构全部成功才发布独立Mac预览，已发布同名预览不覆盖。Windows CI照常运行。仅发布任务有contents:write，PR不发布，checkout不保留凭据，Action固定SHA。

## 差距、风险与优先级

没有Apple Developer ID签名/公证，只有临时ad-hoc签名，首次打开可能被系统阻止。不提供关闭Gatekeeper或绕过系统安全保护的步骤。Mac14+是目标，不宣称全部版本/机型兼容。

P0：真实Mac桌面交互、目标软件身份/私有配置覆盖链及业务、现有VPN/TUN切换时序仍需真实设备验收；自动修复继续锁定。P1：开发者签名、公证及普通用户实测；权限不足/共享宿主候选补充。P2：性能基准、更多OS覆盖。采集有时间/输出限额，未做负载基准；不能由TCP/HTTP回应推断业务健康。

## 远端实际完成记录（2026-10-27）

- 测试代码提交0986aad1c5a2914e2e05c7c5134f43cc33ac0e7b，文件树5f3ff95a4971b512c33d66951eb331d7cb4fe0b8与本地e322bbe完全一致，共163个受版本控制文件。
- Mac运行40534027554成功：macos-15 ARM64、macos-15-intel两个矩阵的Mac测试、核心测试、原生只读采集、架构/临时签名/打包检查及发布均成功。ARM64日志确认19项Mac、69项核心测试，原生采集proxy=Success、tools=Partial、tcp=Partial；Partial为可见候选/非完整采集边界，不包装为业务健康。
- Windows运行40534027851成功，原单元/集成、源码边界、Release构建与格式门禁通过，旧Windows预览不覆盖。
- 发布https://github.com/dnadz/AgentNetDoctor/releases/tag/v0.1.0-macos-preview.1 ，状态公开、非草稿、预览版；两个ZIP及两个SHA256附件均已上传。
- 两个公开ZIP已下载到忽略的artifacts目录：各自ZIP SHA256及包内299个文件逐一匹配，.NET运行时两份许可存在。ARM64 ZIP SHA256：5317a836bacc2474609026db414ebe600f9cba65b88c87c625a0f2bea5ba9197；Intel ZIP SHA256：052307c539746d37f53599fab1dfa5866bde74e1d68e63e9c84ae68ea4a56c2f。
- 未在本Windows机器执行Mac二进制；真实Mac桌面交互、目标Agent/VPN业务和Apple公证仍未验收。CI日志存在既有Action的Node20退役/强制Node24警告，任务本身成功，后续应独立维护Action版本，不混同为C#构建失败。
