param(
    [string]$SdkDirectory = '',
    [string]$SdkAssemblyName = 'TD_Mgd_4.02_11',
    [string]$FrameworkVersion = 'v4.5',
    [ValidateSet('Debug','Release')][string]$Configuration = 'Debug',
    [switch]$Check
)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
if (!$SdkDirectory) { $SdkDirectory = Join-Path $taskRoot 'artifacts\diagnostics\sdk402-candidate\runtime' }
$SdkDirectory = [IO.Path]::GetFullPath($SdkDirectory)
if ($SdkAssemblyName -notmatch '^[A-Za-z0-9_.-]+$') { throw 'Invalid SDK assembly name' }
if ($FrameworkVersion -notmatch '^v4\.\d+(\.\d+)?$') { throw 'Expected a .NET Framework 4.x target' }
$sdkAssembly = Join-Path $SdkDirectory ($SdkAssemblyName + '.dll')
if (!(Test-Path -LiteralPath $sdkAssembly)) { throw "SDK assembly not found: $sdkAssembly" }
$assemblyIdentity = [Reflection.AssemblyName]::GetAssemblyName($sdkAssembly)
if ($assemblyIdentity.Name -ne $SdkAssemblyName) { throw 'SDK assembly identity does not match the selected name' }
$candidateManifest = Join-Path (Split-Path $SdkDirectory -Parent) 'download-manifest.json'
if (Test-Path -LiteralPath $candidateManifest) {
    $manifest = Get-Content -LiteralPath $candidateManifest -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($entry in $manifest.Files) {
        $selectedFile = Join-Path $SdkDirectory ([IO.Path]::GetFileName($entry.LocalPath))
        if (!(Test-Path -LiteralPath $selectedFile) -or (Get-FileHash -LiteralPath $selectedFile).Hash -ne $entry.SHA256) { throw "Candidate file changed: $selectedFile" }
    }
}
$vendorManifest = Get-Content -LiteralPath (Join-Path $taskRoot 'vendor\manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($entry in $vendorManifest.files) {
    $bundledFile = Join-Path (Join-Path $taskRoot 'vendor') $entry.path
    if (!(Test-Path -LiteralPath $bundledFile) -or (Get-FileHash -LiteralPath $bundledFile).Hash -ne $entry.sha256) { throw "Bundled file changed: $bundledFile" }
}
$vswhere = Join-Path ([Environment]::GetEnvironmentVariable('ProgramFiles(x86)')) 'Microsoft Visual Studio\Installer\vswhere.exe'
$msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (!$msbuild) { throw 'MSBuild is unavailable' }
$outputRoot = Join-Path $taskRoot ('artifacts\sdk-candidate\' + $SdkAssemblyName + '-' + $Configuration)
$buildDirectory = Join-Path $outputRoot 'build'
$runDirectory = Join-Path $outputRoot 'run'
New-Item -ItemType Directory -Path $buildDirectory,$runDirectory -Force | Out-Null
$project = Join-Path $taskRoot 'Teigha_Learn_Cad\Teigha_Learn_Cad\Teigha_Learn_Cad.csproj'
$buildArguments = @(
    $project, '/t:Build', "/p:Configuration=$Configuration", '/p:Platform=AnyCPU',
    "/p:TargetFrameworkVersion=$FrameworkVersion", "/p:CadSdkAssemblyName=$SdkAssemblyName",
    "/p:CadSdkRuntimeDirectory=$SdkDirectory", "/p:OutputPath=$buildDirectory\",
    '/nologo', '/v:minimal'
)
& $msbuild @buildArguments
if ($LASTEXITCODE -ne 0) { throw 'Candidate application build failed' }
# Old CAD binaries stay outside the isolated candidate deployment.
$nonCadFiles = Get-ChildItem -LiteralPath (Join-Path $taskRoot 'vendor\runtime') -File |
    Where-Object { $_.Name -notmatch '_3\.03_9\.(dll|tx|txv)$' }
Copy-Item -LiteralPath $nonCadFiles.FullName -Destination $runDirectory -Force
$sdkRuntimeFiles = Get-ChildItem -LiteralPath $SdkDirectory -File |
    Where-Object { $_.Extension -in @('.dll','.tx','.txv','.shx','.ttf','.ttc','.ini','.xml','.dat','.txt') }
Copy-Item -LiteralPath $sdkRuntimeFiles.FullName -Destination $runDirectory -Force
$templateDirectory = Join-Path $runDirectory 'Template'
New-Item -ItemType Directory -Path $templateDirectory -Force | Out-Null
Copy-Item -LiteralPath (Get-ChildItem -LiteralPath (Join-Path $taskRoot 'vendor\templates') -File).FullName -Destination $templateDirectory -Force
foreach ($name in @('Teigha_Learn_Cad.exe','Teigha_Learn_Cad.exe.config','TeighaCad_Bll.dll','ClassLibrary1.dll')) {
    Copy-Item -LiteralPath (Join-Path $buildDirectory $name) -Destination $runDirectory -Force
}
$configPath = Join-Path $runDirectory 'Teigha_Learn_Cad.exe.config'
$configText = [IO.File]::ReadAllText($configPath, [Text.Encoding]::UTF8)
$configText = $configText.Replace('.NETFramework,Version=v4.0', '.NETFramework,Version=' + $FrameworkVersion)
[IO.File]::WriteAllText($configPath, $configText, [Text.UTF8Encoding]::new($false))
$record = [pscustomobject]@{
    SdkAssembly = $assemblyIdentity.FullName; SdkDirectory = $SdkDirectory
    Framework = $FrameworkVersion; Configuration = $Configuration
    Executable = (Join-Path $runDirectory 'Teigha_Learn_Cad.exe')
    ApplicationSHA256 = [string](Get-FileHash -LiteralPath (Join-Path $runDirectory 'Teigha_Learn_Cad.exe')).Hash
    SdkSHA256 = [string](Get-FileHash -LiteralPath (Join-Path $runDirectory ($SdkAssemblyName + '.dll'))).Hash
}
$record | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath (Join-Path $outputRoot 'deployment.json') -Encoding UTF8
$executable = Join-Path $runDirectory 'Teigha_Learn_Cad.exe'
if ($Check) {
    $checkProcess = Start-Process -FilePath $executable -ArgumentList '--startup-check' -WorkingDirectory $runDirectory -WindowStyle Hidden -PassThru
    $null = $checkProcess.Handle
    if (!$checkProcess.WaitForExit(45000)) {
        Stop-Process -Id $checkProcess.Id -ErrorAction SilentlyContinue
        throw 'Candidate startup check timed out'
    }
    $checkProcess.Refresh()
    $report = Join-Path $runDirectory 'startup-check.txt'
    if ($checkProcess.ExitCode -ne 0 -or !(Test-Path -LiteralPath $report)) { throw "Candidate startup failed. See $report" }
    Get-Content -LiteralPath $report -Encoding UTF8
} else {
    Start-Process -FilePath $executable -WorkingDirectory $runDirectory
}
Write-Host "Candidate executable: $executable"
