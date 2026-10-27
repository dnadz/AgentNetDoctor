# macOS implementation plan

> Execute inline with superpowers:executing-plans; one final independent review.

Goal: 中文Mac只读诊断、普通用户流程、双架构预览下载，保留Windows版本。
Spec: macos-proposal.md，用户已确认。Architecture: C# Core + Mac采集 + Avalonia独立入口，无系统配置写入。

- [ ] 1. 在src/AgentNetDoctor.Mac新增固定命令执行器、scutil/ps/lsof解析及MacSnapshotCollector，在tests/Mac用合成夹具先红后绿；子进程超时/取消/输出限额，权限失败保留未知。
- [ ] 2. 将可移植HTTP探针/服务DNS-TCP检查放入Core，Windows使用兼容包装，增加端点/拒绝重定向/取消等回归；不改变网络权限。
- [ ] 3. src/AgentNetDoctor.MacApp新增Avalonia中文简单界面，21项沿用Core，实际Mac观察为候选，不宣称每款均存在Mac发行；高级服务测试必须主动同意。报告只导出允许字段，不含进程完整路径和环境原文。
- [ ] 4. Mac打包脚本生成osx-arm64/osx-x64自包含.app、许可/说明/哈希。固定版本引用Avalonia，保留许可；不索取签名凭据，不关闭Gatekeeper。
- [ ] 5. GitHub Mac矩阵运行测试、实际只读采集和打包检查；Windows原CI回归；最后独立审查。通过后发布Mac预览附件，验证公开下载。真实GUI/Agent业务、VPN退出/签名公证未验证则明确说明。

Ruling: 不替换Windows UI。Mac只读采集API/固定命令分层，未知Mac软件形态和应用私有代理覆盖链保留未知。先以macOS14+为构建目标，实际支持范围受Avalonia和.NET固定版本约束，以CI及真实机器证据为准。

Validation ledger: pending.
