# Mac版制作方案（用户已确认的基线）

日期：2026-10-27。用户明确目标是苹果电脑macOS，不是iPhone/iPad，并确认Avalonia路线。继续使用公开仓库dnadz/AgentNetDoctor。本文是设计基线，实际验证见macos-audit.md。

## 目标与首版

沿用中文简单模式：选择软件→开始检查→查看白话结果与下一步，高级选项默认折叠。检测真实Mac信息，不把Windows探测结果或仅列名称当作Mac适配。

- 保留原21项清单。逐项核实Mac发行与进程/安装候选，不存在或尚未核实的Mac发行明确标注未知/不适用，不能假造已支持。
- 采集当前用户可观察的软件进程、TCP连接与监听、系统代理/PAC状态及网卡/DNS配置。权限不足、采集失败、快照不同步明确显示；不用管理员权限全盘扫描。
- 只对明确已有的本机代理入口检查监听情况，不猜VPN端口。没有显式代理不代表绕过VPN/TUN；端口监听不等于业务正常。
- 用户主动同意后，对实际服务地址进行DNS/TCP/HTTPS检测；不发送API Key，不调用付费模型，不关闭证书校验。地址/报告保留原有凭据与隐私边界。
- 给出逐软件建议；本版不自动改写代理、DNS、路由、VPN或Agent配置，不安装常驻服务。
- 导出脱敏报告；完成退出。分别构建Apple Silicon与Intel下载包，包含运行时、说明、许可和SHA256。

## 三条路线

1. 直接采用现有诊断/防火墙产品：尚未发现已验证能覆盖本项目21项清单、普通用户交互及证据边界的产品；不推荐直接替换产品。
2. 另写原生SwiftUI应用：Mac平台集成直接，但网络模型、安全校验、结果规则与测试会形成第二套实现，后续修改需同步两种语言。原生方案仍需Mac构建与验收。
3. 推荐：现有项目扩展，新增Avalonia Mac界面与Mac采集适配，复用现有C# Core和可移植测试。Windows WPF保持原样，不全仓迁移UI框架。增加Avalonia相关依赖及对应许可维护，代价是包体积和额外界面维护，优点是减少业务/安全逻辑重复。

该推荐属于平台技术路线选择，依据用户全局指令第3条，确认后才实施。时间和费用目前没有可靠估算，不承诺本轮即可达到完整兼容。

## 现有项目与资料调查

- Microsoft明确WPF只运行于Windows：https://learn.microsoft.com/en-us/dotnet/desktop/wpf/overview/
- Avalonia官方支持macOS ARM64与x64，可从Windows/Linux交叉编译，不需要标准.NET macOS workload；具体OS支持等级随版本变化：https://docs.avaloniaui.net/docs/supported-platforms
- AvaloniaUI/Avalonia，MIT，成熟跨平台界面框架。拟按固定版本引用，不fork框架，不复制第三方诊断产品代码。具体包版本、传递依赖与许可须在实施前核验：https://github.com/AvaloniaUI/Avalonia
- objective-see/LuLu，GPL-3.0，定位macOS防火墙。产品需要的流量控制/系统扩展与本轮只读诊断不同；不以其为fork基础，不复制代码，避免引入不必要的权限与维护范围：https://github.com/objective-see/LuLu
- 搜索发现ProfoundAstronomy/NetDoctor，Swift相关网络诊断候选，连接器核实默认分支v1.0.0。功能覆盖、许可、安全边界及维护活跃度尚未充分确认，不作为已验证可复用组件：https://github.com/ProfoundAstronomy/NetDoctor
- macOS系统代理接口调查入口：https://developer.apple.com/documentation/systemconfiguration
- GitHub标准macOS runner有Apple Silicon及Intel标签，实施时固定并复核：https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job

上述是路线调查，不是第三方项目安全审计；不凭Star数断言适合复用。

## 拟改动范围

新增Mac采集项目与Mac专用桌面入口、平台解析/策略测试、打包脚本及Mac CI。Core仅做必要平台无关抽取，禁止为方便复用直接引入Windows注册表、DPAPI或WPF依赖。保留Windows工程、下载、现有测试及原修复门禁。

采集来源通过Mac公开接口或固定路径系统命令获取；不得拼接用户输入为shell命令。命令需要超时、取消、输出上限和非零退出状态。配置读取依旧拒绝越界、网络路径、重解析/符号链接及超限数据，不能扩大为任意配置文件扫描。

## 验收和上传

先实现可测试的采集解析、端点校验与摘要，再实现Mac界面。Windows回归不退化；Mac runner分别测试/构建两个架构，验证.app包结构、Mach-O架构、许可与哈希。Mac上实际采集需单独运行，并标明CI机器未安装目标Agent时的验证边界。

远端测试通过后再发布Mac预览附件。签名、公证需要相应Apple身份与凭据；当前没有核实其可用性，不承诺无系统提示启动，不提供关闭Gatekeeper或绕过系统安全检查的方案。无签名能力时明确标注预览分发限制。

当前Windows工作机不能替代Mac真实GUI、用户Agent账户业务、VPN开关时序及旧macOS兼容验收。CI构建成功不等于这些场景通过。若关键能力受限，先报告证据，不悄悄缩成只有网址探测的版本。
