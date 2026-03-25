# ============================================================
#  Albion Online At Arayan Kelebekler Tools - Build Script
#  Kullanim: PowerShell'de calistirin -> .\build.ps1
# ============================================================

param(
    [string]$Version = "",
    [switch]$SkipInstaller,
    [switch]$UpdateVersionXml
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root         = $PSScriptRoot
$SrcDir       = Join-Path $Root "src\StatisticsAnalysisTool"
$ProjFile     = Join-Path $SrcDir "StatisticsAnalysisTool.csproj"
$AssemblyInfo = Join-Path $SrcDir "Properties\AssemblyInfo.cs"
$PublishDir   = Join-Path $Root "bin\Publish"
$InstallerDir = Join-Path $Root "installer"
$OutputDir    = Join-Path $InstallerDir "Output"
$UpdateXml    = Join-Path $SrcDir "ao-update-check.xml"
$GlobalJson   = Join-Path $Root "src\global.json"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "    OK: $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "    UYARI: $msg" -ForegroundColor Yellow }
function Write-Err($msg)  { Write-Host "`n    HATA: $msg" -ForegroundColor Red; exit 1 }

# ── PATH yenile (kurulum sonrasi icin) ───────────────────────
function Refresh-Path {
    $env:PATH = [System.Environment]::GetEnvironmentVariable("PATH","Machine") + ";" +
                [System.Environment]::GetEnvironmentVariable("PATH","User")
}

# ── .NET SDK bul ─────────────────────────────────────────────
function Find-DotnetWithSdk {
    # Bilinen SDK konumlari
    $candidates = @(
        "dotnet",
        "C:\Program Files\dotnet\dotnet.exe",
        "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe",
        "$env:ProgramFiles\dotnet\dotnet.exe"
    )
    foreach ($c in $candidates) {
        try {
            $sdks = & $c --list-sdks 2>&1
            if ($LASTEXITCODE -eq 0 -and ($sdks | Where-Object { $_ -match "^\d" })) {
                return $c
            }
        } catch {}
    }
    return $null
}

# ── .NET SDK 9 kur ───────────────────────────────────────────
function Install-DotNetSdk9 {
    Write-Warn ".NET SDK 9 bulunamadi. Kuruluyor..."

    # 1) winget dene
    $winget = Get-Command winget -ErrorAction SilentlyContinue
    if ($winget) {
        Write-Host "    winget ile kuruluyor..." -ForegroundColor Gray
        try {
            & winget install Microsoft.DotNet.SDK.9 --silent --accept-package-agreements --accept-source-agreements 2>&1 | Out-Null
            if ($LASTEXITCODE -eq 0) {
                Refresh-Path
                $found = Find-DotnetWithSdk
                if ($found) { Write-Ok ".NET SDK 9 winget ile kuruldu."; return $found }
            }
        } catch {}
    }

    # 2) Dogrudan Microsoft'tan indir
    Write-Host "    Microsoft'tan indiriliyor..." -ForegroundColor Gray
    $sdkInstaller = Join-Path $env:TEMP "dotnet-sdk9-installer.exe"
    $sdkUrl = "https://builds.dotnet.microsoft.com/dotnet/Sdk/9.0.112/dotnet-sdk-9.0.112-win-x64.exe"
    $sdkUrlFallback = "https://download.microsoft.com/download/dotnet/9.0/runtime/win-x64/dotnet-sdk-9.0-win-x64.exe"

    $downloaded = $false
    foreach ($url in @($sdkUrl, $sdkUrlFallback)) {
        try {
            Write-Host "    $url" -ForegroundColor DarkGray
            Invoke-WebRequest -Uri $url -OutFile $sdkInstaller -UseBasicParsing -TimeoutSec 120
            $downloaded = $true
            break
        } catch {
            Write-Host "    Bu URL denendi, sonraki..." -ForegroundColor DarkGray
        }
    }

    if (-not $downloaded) {
        Write-Host ""
        Write-Host "  .NET SDK 9 otomatik indirilemedi." -ForegroundColor Red
        Write-Host "  Lutfen asagidaki adresten manuel kurunuz:" -ForegroundColor Yellow
        Write-Host "  https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Cyan
        Write-Host "  Kurduktan sonra build.ps1'i tekrar calistirin." -ForegroundColor Yellow
        exit 1
    }

    Write-Host "    Kurulum baslatiliyor (bu birkaç dakika surebilir)..." -ForegroundColor Gray
    Start-Process -FilePath $sdkInstaller -ArgumentList "/install /quiet /norestart" -Wait
    Remove-Item $sdkInstaller -Force -ErrorAction SilentlyContinue

    Refresh-Path
    $found = Find-DotnetWithSdk
    if ($found) { Write-Ok ".NET SDK 9 basariyla kuruldu."; return $found }

    Write-Host ""
    Write-Host "  .NET SDK kuruldu ancak algılanamadi." -ForegroundColor Red
    Write-Host "  Lutfen bu PowerShell penceresini KAPATIP yeni acarak tekrar deneyin." -ForegroundColor Yellow
    exit 1
}

# ── global.json'u gecici kaldir ──────────────────────────────
function Remove-GlobalJson {
    if (Test-Path $GlobalJson) {
        Copy-Item $GlobalJson "$GlobalJson.bak" -Force
        Remove-Item $GlobalJson -Force
        Write-Warn "global.json gecici olarak devre disi birakildi."
        return $true
    }
    return $false
}

function Restore-GlobalJson {
    if (Test-Path "$GlobalJson.bak") {
        Copy-Item "$GlobalJson.bak" $GlobalJson -Force
        Remove-Item "$GlobalJson.bak" -Force
        Write-Ok "global.json geri yuklendi."
    }
}

# ────────────────────────────────────────────────────────────
# ADIM 1: Surum numarasi
# ────────────────────────────────────────────────────────────
Write-Step "Surum numarasi okunuyor..."
if ($Version -eq "") {
    $asmContent = Get-Content $AssemblyInfo -Raw
    if ($asmContent -match 'AssemblyFileVersion\("([0-9]+\.[0-9]+\.[0-9]+\.[0-9]+)"\)') {
        $Version = $matches[1]
    } else {
        Write-Err "AssemblyInfo.cs'ten surum okunamadi."
    }
}
Write-Ok "Surum: $Version"

# ────────────────────────────────────────────────────────────
# ADIM 2: .NET SDK bul veya kur
# ────────────────────────────────────────────────────────────
Write-Step ".NET SDK 9 kontrol ediliyor..."

# Once global.json olmadan dene (SDK versiyonu farklı olabilir)
$removedGlobal = Remove-GlobalJson
$dotnet = Find-DotnetWithSdk

if (-not $dotnet) {
    $dotnet = Install-DotNetSdk9
}

if (-not $dotnet) {
    Restore-GlobalJson
    Write-Err ".NET SDK bulunamadi ve kurulamadi."
}

# Hangi SDK'lar var?
$sdkList = & $dotnet --list-sdks 2>&1
Write-Ok "SDK'lar: $($sdkList -join ', ')"

# ────────────────────────────────────────────────────────────
# ADIM 3: Proje yayinla
# ────────────────────────────────────────────────────────────
Write-Step "Proje derleniyor (Release | x64 | win-x64 | SingleFile)..."
if (Test-Path $PublishDir) { Remove-Item $PublishDir -Recurse -Force }
New-Item -ItemType Directory -Path $PublishDir | Out-Null

$publishArgs = @(
    "publish", $ProjFile,
    "-c", "Release",
    "-p:Platform=x64",
    "-p:PublishDir=$PublishDir",
    "-p:TargetFramework=net9.0-windows",
    "-p:RuntimeIdentifier=win-x64",
    "-p:SelfContained=false",
    "-p:PublishSingleFile=true",
    "-p:PublishReadyToRun=false",
    "--nologo"
)

Write-Host "    Komut: $dotnet $($publishArgs -join ' ')" -ForegroundColor DarkGray
& $dotnet @publishArgs

$exitCode = $LASTEXITCODE
Restore-GlobalJson

if ($exitCode -ne 0) {
    Write-Err "Derleme basarisiz! (Exit code: $exitCode)"
}
Write-Ok "Yayinlama tamamlandi: $PublishDir"

# ────────────────────────────────────────────────────────────
# ADIM 3.5: Kelebek Tracker dosyalarini kopyala (lokal)
# ────────────────────────────────────────────────────────────
$TrackerSrc = "C:\Users\serka\Desktop\Dataset\dist"
$TrackerDst = Join-Path $PublishDir "Trackers"
if (Test-Path $TrackerSrc) {
    Write-Step "Kelebek Tracker dosyalari kopyalaniyor..."
    if (-not (Test-Path $TrackerDst)) { New-Item -ItemType Directory -Path $TrackerDst | Out-Null }
    Copy-Item "$TrackerSrc\*" $TrackerDst -Recurse -Force
    Write-Ok "Tracker dosyalari kopyalandi: $TrackerDst"
} else {
    Write-Warn "Kelebek Tracker klasoru bulunamadi: $TrackerSrc (atlanıyor)"
}

# ────────────────────────────────────────────────────────────
# ADIM 4: ao-update-check.xml guncelle (istege bagli)
# ────────────────────────────────────────────────────────────
if ($UpdateVersionXml) {
    Write-Step "ao-update-check.xml guncelleniyor..."
    $verShort = ($Version -split "\." | Select-Object -First 3) -join "."
    $releaseUrl  = "https://github.com/srknsrt/AlbionOnline-AAK/releases/download/v${verShort}/AlbionAAK-v${Version}-Setup.exe"
    $changelogUrl = "https://github.com/srknsrt/AlbionOnline-AAK/releases"
    $xmlContent = "<?xml version=""1.0"" encoding=""UTF-8""?>`n<item>`n`t<version>$Version</version>`n`t<url>$releaseUrl</url>`n`t<changelog>$changelogUrl</changelog>`n`t<mandatory>false</mandatory>`n</item>`n"
    Set-Content -Path $UpdateXml -Value $xmlContent -Encoding UTF8
    Write-Ok "ao-update-check.xml -> v$Version"
}

# ────────────────────────────────────────────────────────────
# ADIM 5: Installer olustur (istege bagli)
# ────────────────────────────────────────────────────────────
if ($SkipInstaller) {
    Write-Host "`n[BUILD TAMAMLANDI - Installer atlandi]" -ForegroundColor Green
    Write-Host "   Cikti: $PublishDir" -ForegroundColor White
    exit 0
}

Write-Step "Inno Setup aranıyor..."
$iscc = $null
$isccPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
)
foreach ($p in $isccPaths) { if (Test-Path $p) { $iscc = $p; break } }

if (-not $iscc) {
    Write-Warn "Inno Setup bulunamadi. winget ile kuruluyor..."
    try {
        & winget install JRSoftware.InnoSetup --silent --accept-package-agreements --accept-source-agreements 2>&1 | Out-Null
        Refresh-Path
        foreach ($p in $isccPaths) { if (Test-Path $p) { $iscc = $p; break } }
    } catch {}
}

if (-not $iscc) {
    Write-Warn "Inno Setup kurulamadi. Installer olusturma atlaniyor."
    Write-Warn "Kurulum: https://jrsoftware.org/isinfo.php"
    Write-Host "`n[BUILD TAMAMLANDI - Sadece publish]" -ForegroundColor Yellow
    Write-Host "   Cikti: $PublishDir" -ForegroundColor White
    exit 0
}
Write-Ok "Inno Setup: $iscc"

Write-Step "Installer olusturuluyor..."
$issFile = Join-Path $InstallerDir "setup.iss"
if (-not (Test-Path $issFile)) { Write-Err "installer\setup.iss bulunamadi!" }

if (Test-Path $OutputDir) { Remove-Item $OutputDir -Recurse -Force }
New-Item -ItemType Directory -Path $OutputDir | Out-Null

& $iscc $issFile "/DMyAppVersion=$Version" "/DPublishDir=$PublishDir" "/DOutputDir=$OutputDir"
if ($LASTEXITCODE -ne 0) { Write-Err "Installer olusturulamadi!" }

$setupExe = Get-ChildItem $OutputDir -Filter "*.exe" | Select-Object -First 1

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  BUILD TAMAMLANDI!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Surum     : v$Version" -ForegroundColor White
Write-Host "  Installer : $($setupExe.FullName)" -ForegroundColor White
Write-Host ""
Write-Host "  Sonraki adimlar:" -ForegroundColor Cyan
$verShort2 = ($Version -split "\." | Select-Object -First 3) -join "."
Write-Host "  1. git tag v$verShort2  &&  git push origin --tags" -ForegroundColor Gray
Write-Host "  2. GitHub Releases sayfasinda yeni Release olusturun" -ForegroundColor Gray
Write-Host "  3. $($setupExe.Name) dosyasini Release'e yukleyin" -ForegroundColor Gray
Write-Host "  4. .\build.ps1 -UpdateVersionXml  ile XML'i guncelleyin + push edin" -ForegroundColor Gray
Write-Host ""
