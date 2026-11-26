param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$RepoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $RepoRoot

Write-Host "=== ekushey.org English (Bangladesh) [en-BD] + Taka Keyboard Build ===" -ForegroundColor Cyan

$vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
if (-not (Test-Path $vswhere)) {
    throw "Visual Studio Installer (vswhere.exe) not found."
}

$vsInstallDir = & $vswhere -latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath | Select-Object -First 1
if (-not $vsInstallDir) {
    throw "Visual Studio C++ build tools not found."
}

$vcvarsall = Join-Path $vsInstallDir "VC\Auxiliary\Build\vcvarsall.bat"
if (-not (Test-Path $vcvarsall)) {
    throw "vcvarsall.bat not found at $vcvarsall"
}

function Build-KbdDll {
    param(
        [string]$ArchLabel,
        [string]$VcVarsArch,
        [string]$ExtraCFlags = ""
    )

    Write-Host "`n--- Building kbdusbd.dll ($ArchLabel) ---" -ForegroundColor Yellow
    $outDir = Join-Path $RepoRoot "build\kbd\$ArchLabel"
    New-Item -ItemType Directory -Force -Path $outDir | Out-Null

    $srcC   = Join-Path $RepoRoot "src\kbdusbd\kbdusbd.c"
    $srcRc  = Join-Path $RepoRoot "src\kbdusbd\kbdusbd.rc"
    $srcDef = Join-Path $RepoRoot "src\kbdusbd\kbdusbd.def"
    $resOut = Join-Path $outDir "kbdusbd.res"
    $objOut = Join-Path $outDir "kbdusbd.obj"
    $dllOut = Join-Path $outDir "kbdusbd.dll"

    $cmd = "`"$vcvarsall`" $VcVarsArch >nul && " +
           "rc /nologo /fo `"$resOut`" `"$srcRc`" && " +
           "cl /nologo /O2 /W3 /GS- /DUNICODE /D_UNICODE $ExtraCFlags /c `"$srcC`" /Fo`"$objOut`" && " +
           "link /nologo /DLL /NOENTRY /SUBSYSTEM:NATIVE /OPT:REF /OPT:ICF /MERGE:.rdata=.data /DEF:`"$srcDef`" /OUT:`"$dllOut`" `"$objOut`" `"$resOut`""

    cmd.exe /c $cmd
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path $dllOut)) {
        throw "Failed to build kbdusbd.dll for $ArchLabel (exit code $LASTEXITCODE)"
    }
    Write-Host "Built: $dllOut" -ForegroundColor Green
}

Build-KbdDll -ArchLabel "x86"   -VcVarsArch "amd64_x86"
Build-KbdDll -ArchLabel "wow64" -VcVarsArch "amd64_x86" -ExtraCFlags "/DBUILD_WOW6432"
Build-KbdDll -ArchLabel "x64"   -VcVarsArch "amd64"
Build-KbdDll -ArchLabel "arm64" -VcVarsArch "amd64_arm64"

Write-Host "`n--- Building en-BD-Setup.exe (AnyCPU, .NET 4.8) ---" -ForegroundColor Yellow
$msbuild = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
if (-not $msbuild) {
    throw "MSBuild.exe not found."
}

$csproj = Join-Path $RepoRoot "src\EnBdSetup\EnBdSetup.csproj"
& $msbuild $csproj /restore /p:Configuration=$Configuration /v:minimal
if ($LASTEXITCODE -ne 0) { throw "MSBuild failed for EnBdSetup.csproj" }

$distDir = Join-Path $RepoRoot "dist"
New-Item -ItemType Directory -Force -Path $distDir | Out-Null

$exeSrc = Join-Path $RepoRoot "src\EnBdSetup\bin\$Configuration\net48\en-BD-Setup.exe"
$exeDst = Join-Path $distDir "en-BD-Setup.exe"
Copy-Item -Force $exeSrc $exeDst

$hash = (Get-FileHash $exeDst -Algorithm SHA256).Hash
Set-Content -Path (Join-Path $distDir "en-BD-Setup.exe.sha256") -Value "$hash  en-BD-Setup.exe"

Write-Host "`nBuild succeeded!" -ForegroundColor Green
Write-Host "Installer: $exeDst"
Write-Host "SHA256:    $hash"
