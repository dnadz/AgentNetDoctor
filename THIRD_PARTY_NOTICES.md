# 第三方声明

目前产品代码未复制候选项目源码，研究参考不等于二次开发。使用.NET/WPF平台组件和NuGet测试包，发行时保留运行时第三方声明。测试依赖不随普通产品包交付。

实际复制组件前记录来源、版本/提交、许可证和改动；不把GPL/AGPL代码混入MIT产品。

## Tomlyn 2.10.1

NuGet运行依赖，用于只读解析Codex候选TOML；没有复制或修改其源码。BSD-2-Clause，版权Alexandre Mutel；完整许可随源码和程序输出保留于`third-party/Tomlyn-LICENSE.txt`。

来源：https://github.com/xoofx/Tomlyn/releases/tag/2.10.1；NuGet包仓库提交：`6cc82dd23a2c0aefb858f92a51c1b184e2812103`。net10.0目标无额外NuGet运行依赖。严格语义验证与反序列化分开执行，不能只依靠默认的无类型反序列化拒绝重复字段。

## 免安装包运行时

Windows x64自包含包包含Microsoft.NETCore.App和Microsoft.WindowsDesktop.App 10.0.11。打包脚本从官方NuGet运行时包复制.NET许可、第三方通知及WindowsDesktop许可到third-party目录；源码仓库不包含运行时二进制。Tomlyn许可仍单独随包保留。
# Mac界面依赖

Mac界面新增Avalonia、Avalonia.Desktop、Avalonia.Themes.Fluent 12.1.4（MIT），测试使用Avalonia.Headless 12.1.4。官方源码与许可：https://github.com/AvaloniaUI/Avalonia/blob/main/licence.md 。打包脚本收集固定解析版本的NuGet清单、许可表达式文本和许可/通知文件，另显式复制.NET运行时许可与第三方通知，放入.app/Contents/Resources/third-party；测试依赖不随产品分发。
