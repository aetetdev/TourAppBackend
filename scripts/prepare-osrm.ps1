<#
.SYNOPSIS
    Rota motoru (OSRM) için Türkiye yol grafiğini hazırlar.

.DESCRIPTION
    İki profil üretir:
      osrm/car   - şehirlerarası yolculuk (araç)
      osrm/foot  - şehir içi gezi (yürüme)

    Her profil kendi klasöründe hazırlanır, çünkü osrm-extract çıktı dosyalarını
    girdiyle aynı ada yazar ve iki profil birbirinin üzerine yazardı.

    Adımlar: extract (yol ağını çıkarır) -> partition -> customize (MLD algoritması).
    Türkiye verisi için toplam 20-40 dakika sürer ve 4-8 GB bellek ister.

.PARAMETER Which
    Yalnızca tek profil hazırlamak için: car veya foot.

.PARAMETER Force
    Hazır dosyalar varsa bile yeniden üretir.

.EXAMPLE
    ./scripts/prepare-osrm.ps1
#>
[CmdletBinding()]
param(
    # "Profile" adı kullanılamaz: PowerShell'in kendi otomatik değişkeniyle çakışıyor
    [ValidateSet("car", "foot", "all")]
    [string]$Which = "all",
    [switch]$Force
)

$ErrorActionPreference = "Stop"
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

$repoRoot = Split-Path -Parent $PSScriptRoot
$pbfName = "turkey-latest.osm.pbf"
$sourcePbf = Join-Path (Join-Path $repoRoot "data") $pbfName
$osrmImage = "ghcr.io/project-osrm/osrm-backend:latest"

if (-not (Test-Path $sourcePbf)) {
    throw "Ham veri bulunamadı: $sourcePbf`nÖnce ./scripts/prepare-osm-data.ps1 çalıştırılmalı."
}

# Türkiye yol grafiğini kurmak yoğun bellek ister; yetersizse işlem sessizce
# öldürülür ve nedeni logda görünmez. Baştan uyarmak saatlerce süren bir
# yanlış teşhisi önlüyor.
$dockerMemoryBytes = [int64](& docker info --format "{{.MemTotal}}" 2>$null)
$requiredGb = 9

if ($dockerMemoryBytes -gt 0) {
    $dockerMemoryGb = [math]::Round($dockerMemoryBytes / 1GB, 1)

    if ($dockerMemoryGb -lt $requiredGb) {
        Write-Warning @"
Docker'a ayrılan bellek $dockerMemoryGb GB; Türkiye grafiği için en az $requiredGb GB önerilir.
İşlem bellek yetersizliğinden yarıda kesilebilir.

Çözüm: %USERPROFILE%\.wslconfig dosyasına aşağıdakini yazıp
'wsl --shutdown' çalıştırın ve Docker Desktop'ı yeniden başlatın:

    [wsl2]
    memory=11GB
    swap=4GB
"@
    }
}

function Invoke-Osrm {
    param(
        [string]$ProfileDir,
        [string[]]$Arguments
    )

    $mount = "$($ProfileDir):/data"

    & docker run --rm -t -v $mount $osrmImage @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "OSRM komutu başarısız: $($Arguments -join ' ')"
    }
}

function Build-Profile {
    param(
        [string]$Name,
        [string]$LuaProfile
    )

    $profileDir = Join-Path (Join-Path $repoRoot "osrm") $Name
    $targetPbf = Join-Path $profileDir $pbfName
    $graphFile = Join-Path $profileDir "turkey-latest.osrm.mldgr"

    if ((Test-Path $graphFile) -and -not $Force) {
        Write-Host "[$Name] Grafik zaten hazır, atlandı." -ForegroundColor DarkGray
        return
    }

    if (-not (Test-Path $profileDir)) {
        New-Item -ItemType Directory -Path $profileDir -Force | Out-Null
    }

    if (-not (Test-Path $targetPbf)) {
        Write-Host "[$Name] Ham veri kopyalanıyor..." -ForegroundColor Cyan
        Copy-Item $sourcePbf $targetPbf
    }

    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

    Write-Host "[$Name] 1/3 extract - yol ağı çıkarılıyor (en uzun adım)..." -ForegroundColor Cyan
    Invoke-Osrm -ProfileDir $profileDir -Arguments @("osrm-extract", "-p", $LuaProfile, "/data/$pbfName")

    Write-Host "[$Name] 2/3 partition..." -ForegroundColor Cyan
    Invoke-Osrm -ProfileDir $profileDir -Arguments @("osrm-partition", "/data/turkey-latest.osrm")

    Write-Host "[$Name] 3/3 customize..." -ForegroundColor Cyan
    Invoke-Osrm -ProfileDir $profileDir -Arguments @("osrm-customize", "/data/turkey-latest.osrm")

    $stopwatch.Stop()

    # Ham veri artık gerekmiyor; profil başına 600 MB yer kaplıyor
    Remove-Item $targetPbf -Force -ErrorAction SilentlyContinue

    Write-Host "[$Name] Hazır ($([math]::Round($stopwatch.Elapsed.TotalMinutes, 1)) dk)." -ForegroundColor Green
}

if ($Which -eq "car" -or $Which -eq "all") {
    Build-Profile -Name "car" -LuaProfile "/opt/car.lua"
}

if ($Which -eq "foot" -or $Which -eq "all") {
    Build-Profile -Name "foot" -LuaProfile "/opt/foot.lua"
}

Write-Host ""
Write-Host "Rota motorunu başlatmak için:" -ForegroundColor Cyan
Write-Host "  docker compose up -d osrm-car osrm-foot"
