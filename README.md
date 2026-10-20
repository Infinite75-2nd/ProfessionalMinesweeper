# Professional Minesweeper

[English](README.en.md)

[GitHub 仓库](https://github.com/Infinite75-2nd/ProfessionalMinesweeper)

使用 **C#、.NET 10 与 Avalonia UI 12** 开发的多形状扫雷，支持 Windows 和 Android。

本项目代码由 **AI 辅助编写**，包括部分功能实现、重构、文档和测试；由作者组织开发与迭代。

## 项目来源

本项目是对原版 **Professional Minesweeper（`profmine.exe`）** 的复刻。棋盘排列、菜单顺序和预设研究基于此前对原游戏的逆向分析，包括运行进程中的棋盘数据与邻接关系。这里提供的是独立实现，不是原作者的官方版本；原游戏名称及第三方素材的权利属于各自权利人。

## 功能

- 18 种原版棋盘，外加一种自研拓扑「碎晶」：用黄金角螺旋撒点做 Voronoi 剖分，天然无缝、每格形状各异。
- 三档难度、延迟布雷、首击保护、旗帜/问号、连开、计时与胜负显示。
- Windows：滚轮垂直移动，Shift + 滚轮水平移动，Ctrl + 滚轮缩放，左键拖拽棋盘。
- Android：单指拖动、双指缩放、长按未翻开格标旗、长按数字预览邻格、双击数字连开；胜负结束后双击棋盘重开。
- Windows Native AOT 发布；Android Release 使用 Mono AOT。

## 构建

需要 .NET 10 SDK。Windows Native AOT 还需要 Visual Studio C++ 工具链与 Windows SDK；Android 需要 `android` 工作负载、兼容 JDK 与 Android SDK。

```powershell
dotnet build -c Release
dotnet test
dotnet publish src/ProfessionalMinesweeper.Desktop/ProfessionalMinesweeper.Desktop.csproj -c Release -r win-x64 --self-contained true
dotnet build src/ProfessionalMinesweeper.Android/ProfessionalMinesweeper.Android.csproj -c Release
```

Windows 静态链接使用 `2ndLAB.ANGLE.Static` 和 `2ndLAB.SkiaSharp.Static` NuGet 包，配置见 `NativeAOT.props`。发布目录为 Desktop 的 `bin/Release/net10.0/win-x64/publish/`；APK 位于 Android 的 `bin/Release/net10.0-android/`。

## 结构

- `src/ProfessionalMinesweeper.Core`：独立游戏引擎、多边形拓扑、棋盘生成与预设数据。
- `src/ProfessionalMinesweeper.Desktop`：Windows 界面、绘制与鼠标操作。
- `src/ProfessionalMinesweeper.Android`：Android 入口、触屏视口和移动界面。
- `tests/ProfessionalMinesweeper.Tests`：核心逻辑、覆盖、连通性与性能回归测试。
- `re/`：原游戏分析记录；原 EXE 和提取图集不纳入仓库。

## 作者与许可

布雷会参考集合约束推理验证候选布局（方法参考 [Simon Tatham 的 Mines](https://www.chiark.greenend.org.uk/~sgtatham/puzzles/doc/mines.html)），最多尝试128次、预算约200毫秒。验证通过的布局可由实现的逻辑规则完成；超过预算会保留普通布局，因此目前并非所有棋盘都保证无猜。核心实现为独立C#代码，未移植原项目源码。

**Infinite75** · [B站主页](https://space.bilibili.com/672619350)

本项目代码以 **GPL-3.0-only** 发布，完整协议见 [LICENSE](LICENSE)。依赖组件遵循各自许可。项目仍在迭代，构建通过不代表所有棋盘与触屏操作已完成实机验证。
