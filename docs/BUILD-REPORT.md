# 本次构建记录

日期：2026-09-20。构建主机：macOS ARM64；SDK：.NET 8.0.425。

| 检查 | 结果 |
| --- | --- |
| 完整解决方案 Release 编译 | 通过，0 warning / 0 error |
| 核心场景测试程序 | 22/22 通过 |
| C# 素材提取 | 通过，PNG / 身体 / 叶子 / ICO 均已生成 |
| 素材透明度、白肚皮、内部像素保持 | 通过 |
| 素材 PNG 编解码往返 | 通过 |
| Windows x64 framework-dependent 发布 | 已生成主程序和 Guardian，需 .NET 8 Desktop Runtime x64 |
| Windows 实机 WPF 渲染、Hook 与点击穿透 | 未运行：当前主机不是 Windows |
| 多显示器混合 DPI、实际 CPU/GPU、崩溃恢复 | 未运行：见 WINDOWS-VALIDATION.md |
| GitHub Windows CI | 已提供配置，未上传或触发远程运行 |

依赖下载期间 NuGet 网络中断，已改用微软官方 WindowsDesktop targeting pack 和 Windows SDK 中的 apphost 完成交叉构建；没有向项目添加第三方包源或不必要的运行时依赖。

源码和运行时均不依赖 Python。最终素材由项目内 C# 工具重新生成。
