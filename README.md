# 二批 CAD 图纸适配项目

当前开发主线是在现有版本上改善电气图、线束图分析与输出，按约定流程逐张验收二批 CAD 图纸。

## 克隆后运行

```powershell
git clone https://github.com/code-z26/DWG.git
cd DWG
```

双击根目录 **`启动软件.cmd`**。它使用独立部署的 Teigha/ODA 4.02.2 候选运行库构建并启动程序。旧版 Teigha 3.03 可通过根目录 **`启动旧版3.03.cmd`** 启动。

需要 64 位 Windows、.NET Framework 4.x 和 VC2008 SP1 x64 系统运行环境。启动时若提示缺少 VC2008，请按 [依赖说明](DEPENDENCIES.md) 中的微软官方地址安装一次，再重新启动。

运行中生成的中间文件和报表位于运行目录，不会混入跟踪的源码或依赖。CAD 打开/保存对话框中选择的路径仍以用户操作为准。

## 修改源码后运行

安装 Visual Studio / Build Tools、.NET 桌面编译组件及 v4.0 目标框架引用程序集，然后双击 **`编译并启动.cmd`**。它编译最新源码并使用 Teigha/ODA 4.02.2 候选运行库启动程序。

`启动软件.cmd` 和 `编译并启动.cmd` 都使用 4.02.2 候选构建；`启动旧版3.03.cmd` 运行保留的旧版程序。

也可以在 PowerShell 执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Build.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Start.ps1 -Build -Check
```

`-Check` 执行启动自检后退出，检查 x64、OpenCV 原生调用、XLSX 创建、默认 DWG 模板、CAD 渲染设备和关闭流程；这不等于业务矩阵验收。

已完成仅含 Git 文件的干净副本构建与启动验证，详见 [验证记录](docs/启动验证记录.md)。

负责人需要更新可直接运行的快照时，执行 `Build.ps1 -Publish`，再运行 `Start.ps1 -Check`，将 `app` 的更新与相应源码一起提交。

## 新成员从这里开始

先阅读 [AI 协作开发攻略](docs/team/AI协作开发攻略.md)，再领取具体任务。首次上手先完成一张统一样例图的启动、分析、导出和人工核对。

| 文档 | 用途 |
| --- | --- |
| [AI 协作开发攻略](docs/team/AI协作开发攻略.md) | 环境、项目入口、分工、Git 流程及 AI 提问模板 |
| [任务卡模板](docs/team/任务卡模板.md) | 记录复现步骤、业务依据、修改范围与验收条件 |
| [验证与提交清单](docs/team/验证与提交清单.md) | 规定实图验证和提交证据 |
| [PR 提交模板](docs/team/PR提交模板.md) | 汇报修复内容、验证结果和未验证项 |
| [AGENTS 示例](docs/team/AGENTS.example.md) | 供负责人完善后配置团队 AI 项目说明；当前仅为草案 |

## 打开工程

使用 Visual Studio 打开 [DWG.Development.sln](DWG.Development.sln)。它包含开发所需的三个源码项目和“团队协作文档”，不含安装包项目。原 [Teigha_Learn_Cad.sln](Teigha_Learn_Cad/Teigha_Learn_Cad.sln) 继续保留。

项目使用传统 .NET Framework 工程，运行依赖已归档到 `vendor/runtime`。直接构建应用工程即可，不需要先构建安装包项目。详见 [依赖说明](DEPENDENCIES.md)。

## 每个任务的开发流程

电气和线束各由两人共同负责，组内按具体问题分配主开发与复核者。每项任务覆盖输入到最终输出的完整链路；第一周每组共同完成一个小修复，熟悉后再并行。公共函数和规则变化提前协调。

领取具体问题 → 创建任务分支 → 复现错误 → AI 定位原因 → 最小修改 → 实图验证 → 同组复核 → 提交 PR → 负责人集成与全图回归。

输入图纸保持只读，输出保存到独立目录。编译成功、AI 宣称完成或成功生成 Excel，都不能代替业务结果核对。
