# 适配与配置解析证据

核实日期2026-10-26。资料描述不等于本机业务验收。实际版本见compatibility。

- [WorkBuddy官方系统设置](https://www.tencentcloud.com/document/product/1316/84502)：应用网络面板有直连、跟随系统、手动代理。官方页面未提供本机5.9.0磁盘字段结构；适配器只发现卸载元数据，不用CodeBuddy路径替代，也不开写入。
- [Claude Code官方网络文档](https://code.claude.com/docs/en/network-config)：CLI读取启动环境，大小写变量有优先级，不支持SOCKS；桌面托管连接与本机交互CLI不能混为一谈。读取用户settings.env代理白名单，不导出Key；项目/托管/进程覆盖仍未核实。
- [Codex配置参考](https://learn.chatgpt.com/docs/config-file/config-reference)：permissions/network和features/network_proxy涉及沙箱命令流量，不将proxy_url当作客户端上游代理。只读候选TOML，提供商只报告存在性，不改写模型/提供商。
- [Antigravity官方设置](https://antigravity.google/docs/settings)：当前文档含CLI与新架构，sandboxProxy只用于终端沙箱。旧IDE候选http.proxy不能据此推断覆盖新桌面/语言服务。旧配置缺失不等于未安装。
- Windows只读API：[当前连接用户配置](https://learn.microsoft.com/en-us/windows/win32/api/winhttp/nf-winhttp-winhttpgetieproxyconfigforcurrentuser)、[WinHTTP默认配置](https://learn.microsoft.com/en-us/windows/win32/api/winhttp/nf-winhttp-winhttpgetdefaultproxyconfiguration)。使用GlobalFree释放返回字符串；默认配置不代表各会话生效值，不主动解析PAC/WPAD。

## TOML复用决策

直接引用成熟组件Tomlyn解析器，自研诊断/白名单/修复门禁，不整库fork、不手写TOML语法。仓库查询快照：600 Star、46 Fork、6 Open Issues；未归档，最近push为2026-08-18。不是根据Star单独选型。

核实readme、2.10.1发布、许可、csproj/NuGet清单、近期Issue139/138和PR135/130。C#/.NET10匹配，net10.0依赖组为空；BSD-2-Clause可保留通知后分发。NuGet包指向提交6cc82dd23a2c0aefb858f92a51c1b184e2812103。

优点：完整解析/严格语义验证，避免正则解析注释/重复键。代价：新增一个运行依赖，需跟踪上游问题和版本，当前不保证目标App接受所有解析器接受的语法；已观察PR135在所选发布之后修复整数溢出边界，因此不据解析成功开放写入。只读状态与应用有效配置分开。

测试实际发现无类型反序列化即使设置重复键Error仍接受重复键；先ParseStrict(validate:true)再解析可拒绝本项目重复字段夹具。没有因此改测试掩盖问题。

## 安全边界

默认仅本地固定盘；UNC/设备/映射网络盘/重解析路径不自动读取，防止点击本机检查隐式触发SMB认证。包括原有本地junction也先明确未读取，尚未实现经过授权的安全跨路径读取。

备份用Windows桌面框架提供的ProtectedData(CurrentUser)，不增加另一NuGet密码学实现。目录当前用户专属ACL、GUID ID、目标/版本封套与hash校验；失败不降级明文。跨用户/干净VM仍未实测。
