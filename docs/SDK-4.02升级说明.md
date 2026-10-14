# Teigha/ODA 4.02.2 候选升级说明

## 目标

在不替换默认 3.03 构建的前提下，增加一个可独立构建和运行的 4.02.2 候选入口。升级后的运行库可以读取当前程序中无法读取的 AC1027 图纸。

## 已验证范围

- `20.dwg`、`35.dwg`、`36.dwg`、`37.dwg`、`48.dwg`、`51.dwg`：Teigha 4.02.2 最小读取通过。
- `19.dwg`：仍返回 `eInvalidFileVersion`。4.02.2 不支持该 AC1032 文件，需单独转换为不高于 AC1024 的版本，或等待支持 AC1032 的正式 ODA SDK。
- 原有 `7.dwg` 和 `46.dwg`：4.02.2 最小读取通过。

## 使用前提

- 64 位 Windows。
- .NET Framework 4.5。
- Microsoft Visual C++ 2012 x64 运行库。
- 候选 SDK 文件放在：
  `artifacts\diagnostics\sdk402-candidate\runtime`

候选 SDK 不在 Git 中分发，必须由项目负责人按授权渠道提供。

## 构建和运行

关闭旧程序后，双击项目根目录：

`测试新版SDK.cmd`

脚本会：

1. 校验候选 SDK 文件哈希。
2. 使用 `TD_Mgd_4.02_11.dll` 和 .NET Framework 4.5 构建。
3. 将旧版 CAD 文件隔离，不复制到候选运行目录。
4. 输出实际 EXE 路径和 SDK/程序哈希到 `artifacts\sdk-candidate\...\deployment.json`。
5. 启动候选程序。

只做启动自检时执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\Start-CadSdkCandidate.ps1 -Check
```

## 当前默认行为

- `启动软件.cmd`、`编译并启动.cmd`：继续使用 Teigha 3.03。
- `测试新版SDK.cmd`：使用 Teigha 4.02.2。

在完整业务回归通过前，不把 4.02.2 替换为默认运行库。

## 原始文件备份

升级前的高版本原图位于：

`backups\dwg-version-originals`

这些文件使用 Git LFS 跟踪，并记录了文件头版本、SHA256 和字节数。
