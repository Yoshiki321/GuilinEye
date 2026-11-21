param(
    [string]$ProjectPath = "D:\web\AICamera\桂林之眼\仓库\GuilinEye\unity\GuilinEye",
    [string]$EditorPath = ""
)

$ErrorActionPreference = "Stop"

function Find-UnityEditor {
    param([string]$Hint)
    $candidates = @()
    if ($Hint) { $candidates += $Hint }
    $candidates += @(
        "C:\Program Files\Unity\Hub\Editor\6000.3.29f1\Editor\Unity.exe",
        "C:\Program Files\Unity\Hub\Editor\6000.3.0f1\Editor\Unity.exe",
        "D:\Unity\6000.3.29f1\Editor\Unity.exe",
        "D:\Unity\6000.3.0f1\Editor\Unity.exe"
    )
    foreach ($p in $candidates) {
        if (Test-Path -LiteralPath $p) { return (Resolve-Path -LiteralPath $p).Path }
    }
    $roots = @("C:\Program Files\Unity\Hub\Editor", "D:\Unity")
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root)) { continue }
        $found = Get-ChildItem -LiteralPath $root -Recurse -Filter Unity.exe -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -match '\\Editor\\Unity\.exe$' } |
            Sort-Object LastWriteTime -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return $null
}

if (-not (Test-Path -LiteralPath $ProjectPath)) { throw "工程不存在：$ProjectPath" }
$editor = Find-UnityEditor -Hint $EditorPath
if (-not $editor) { throw "未找到 Unity.exe。请先安装 6000.3.29f1，或传 -EditorPath。" }

$version = (Get-Item -LiteralPath $editor).VersionInfo.FileVersion
Write-Host "[Unity] $editor ($version)"

$logDir = Join-Path $ProjectPath "Logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$buildLog = Join-Path $logDir "batch_scene_build.log"
$smokeLog = Join-Path $logDir "batch_smoke.log"

Write-Host "[1/3] 批处理生成 18 模块场景 + 4 组合场景 ..."
$p = Start-Process -FilePath $editor -ArgumentList @(
    "-batchmode", "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", "GuilinEye.Editor.SceneBootstrapper.BuildAll",
    "-logFile", $buildLog
) -Wait -PassThru -NoNewWindow
if ($p.ExitCode -ne 0) {
    Write-Host "[FAIL] 场景生成退出码 $($p.ExitCode)，日志：$buildLog"
    Get-Content -LiteralPath $buildLog -Tail 40 -ErrorAction SilentlyContinue
    exit 1
}

Write-Host "[2/3] 检查场景文件 ..."
$sceneRoot = Join-Path $ProjectPath "Assets\_Project\Modules"
$scenes = @(Get-ChildItem -LiteralPath $sceneRoot -Recurse -Filter *.unity -ErrorAction SilentlyContinue)
Write-Host "[OK] 模块场景 $($scenes.Count) 个"
if ($scenes.Count -lt 18) {
    Write-Host "[FAIL] 场景不足 18，日志：$buildLog"
    Get-Content -LiteralPath $buildLog -Tail 60 -ErrorAction SilentlyContinue
    exit 2
}

Write-Host "[3/3] 批处理冒烟 ..."
$p2 = Start-Process -FilePath $editor -ArgumentList @(
    "-batchmode", "-quit",
    "-projectPath", $ProjectPath,
    "-executeMethod", "GuilinEye.Editor.VenueSmokeTest.RunBatch",
    "-logFile", $smokeLog
) -Wait -PassThru -NoNewWindow
if ($p2.ExitCode -ne 0) {
    Write-Host "[FAIL] 冒烟退出码 $($p2.ExitCode)，日志：$smokeLog"
    Get-Content -LiteralPath $smokeLog -Tail 40 -ErrorAction SilentlyContinue
    exit 3
}

$smoke = Get-Content -LiteralPath $smokeLog -Raw -Encoding utf8 -ErrorAction SilentlyContinue
if ($smoke -match "SMOKE OK") { Write-Host "[OK] SMOKE OK" }
else { Write-Host "[WARN] 未在日志中找到 SMOKE OK，请查看 $smokeLog" }

Write-Host "[DONE] Unity 场景生成与冒烟完成。"
Write-Host "打开工程：& \"$editor\" -projectPath \"$ProjectPath\""
