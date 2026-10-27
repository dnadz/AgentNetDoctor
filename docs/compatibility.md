# 支持矩阵

首版目标Windows11 x64。Codex、WorkBuddy、Antigravity和Claude Code的实际版本/代理机制尚未完成验证；目前不得开放未知版本配置写入。

XSUS/sing-box进程已观察到；用户确认使用TUN模式。具体版本、路由和DNS恢复能力尚未实测。尚无任何VPN退出时序被验收通过。

2026-10-26只读元数据核实（仅产品版本，不是代理能力验收）：

| 产品 | 观察版本 | 来源 | 业务/写入支持 |
|---|---|---|---|
| XSUS | 1.7.4+10704 | 可执行文件产品版本；TUN为用户确认 | 退出时序未测 |
| Codex桌面 | 26.1021.3705.0 | Windows应用包 | 未测，不推断CLI版本 |
| WorkBuddy | 5.9.0 | 当前用户卸载元数据 | 未测，禁止写入 |
| Antigravity | 2.26.0 | 当前用户卸载元数据 | 未测，禁止写入 |
| Claude桌面 | 3.6556.2.0 | Windows应用包 | 不等同Claude Code CLI |
| Claude Code CLI npm候选 | 2.1.296 | 本机@anthropic-ai/claude-code包manifest；engines.node为>=22.0.0 | 未执行CLI，运行版本/可信文件/业务未确认，禁止写入 |

本轮实际运行环境Windows 11 x64，系统版本10.0.22000。其他Windows版本/架构未验收。
