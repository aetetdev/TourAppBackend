<#
.SYNOPSIS
    Türkiye OSM verisini indirir ve harvester'ın okuyacağı GeoJSONSeq dosyalarına çevirir.

.DESCRIPTION
    Üç çıktı üretir (hepsi ./data altında):
      poi.geojsonl         - turistik yer adayları (nokta ve alan geometrileriyle)
      provinces.geojsonl   - il sınırları  (admin_level=4)
      districts.geojsonl   - ilçe sınırları (admin_level=6)

    Aynı .pbf dosyası OSRM rota motoru tarafından da kullanılır, ikinci kez indirilmez.

.PARAMETER Force
    Var olan çıktı dosyalarını yeniden üretir.

.EXAMPLE
    ./scripts/prepare-osm-data.ps1
#>
[CmdletBinding()]
param(
    [switch]$Force,
    [string]$PbfUrl = "https://download.geofabrik.de/europe/turkey-latest.osm.pbf"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$dataDir = Join-Path $repoRoot "data"
$pbfName = "turkey-latest.osm.pbf"
$pbfPath = Join-Path $dataDir $pbfName

if (-not (Test-Path $dataDir)) {
    New-Item -ItemType Directory -Path $dataDir | Out-Null
}

function Invoke-Osmium {
    param([string[]]$Arguments)

    Push-Location $repoRoot
    try {
        & docker compose run --rm osmium @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "osmium komutu başarısız oldu: $($Arguments -join ' ')"
        }
    }
    finally {
        Pop-Location
    }
}

# --- 1. Ham veriyi indir ---

if ((Test-Path $pbfPath) -and -not $Force) {
    $sizeMb = [math]::Round((Get-Item $pbfPath).Length / 1MB, 1)
    Write-Host "[1/4] $pbfName zaten var ($sizeMb MB), indirme atlandı." -ForegroundColor DarkGray
}
else {
    Write-Host "[1/4] Türkiye OSM verisi indiriliyor (~500 MB, birkaç dakika sürebilir)..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $PbfUrl -OutFile $pbfPath
    $sizeMb = [math]::Round((Get-Item $pbfPath).Length / 1MB, 1)
    Write-Host "      indirildi: $sizeMb MB" -ForegroundColor Green
}

# --- 2. Turistik yer adaylarını süz ---
# Ham dosyada ~50 milyon nesne var; bu filtre onu on binler mertebesine indirir.
# Geniş tutuyoruz çünkü asıl eleme OsmCategoryMapper'da yapılıyor - orası test edilebilir,
# burası değil.

$poiPbf = Join-Path $dataDir "poi.osm.pbf"
$poiJson = Join-Path $dataDir "poi.geojsonl"

if ((Test-Path $poiJson) -and -not $Force) {
    Write-Host "[2/4] poi.geojsonl zaten var, atlandı." -ForegroundColor DarkGray
}
else {
    Write-Host "[2/4] Turistik yer adayları süzülüyor..." -ForegroundColor Cyan

    Invoke-Osmium @(
        "tags-filter", "--overwrite", "-o", "/data/poi.osm.pbf", "/data/$pbfName",
        "nwr/tourism",
        "nwr/historic",
        "nwr/natural=beach,cave_entrance,hot_spring,spring,valley,peak,cliff,arch,volcano,water",
        "nwr/waterway=waterfall",
        "nwr/leisure=park,garden,nature_reserve,water_park,beach_resort",
        "nwr/amenity=place_of_worship,theatre,arts_centre,marketplace,public_bath,monastery",
        "nwr/boundary=national_park,protected_area",
        "nwr/man_made=lighthouse,tower,obelisk,watermill,windmill",
        "nwr/place=island,islet,archipelago",
        "nwr/building=mosque,church,cathedral,chapel,synagogue,monastery,castle,palace"
    )

    Write-Host "      GeoJSONSeq'e çevriliyor..." -ForegroundColor Cyan
    Invoke-Osmium @(
        "export", "--overwrite", "/data/poi.osm.pbf",
        "-f", "geojsonseq",
        "--add-unique-id=type_id",
        "--geometry-types=point,polygon",
        "-o", "/data/poi.geojsonl"
    )

    $count = (Get-Content $poiJson | Measure-Object -Line).Lines
    Write-Host "      $count aday yer çıkarıldı." -ForegroundColor Green
}

# --- 3. İl ve ilçe sınırları ---
# osmium tags-filter tek çağrıda VEYA mantığı uygular; boundary=administrative VE
# admin_level=4 koşulu için iki aşamalı süzme gerekiyor.

$adminPbf = Join-Path $dataDir "admin.osm.pbf"

if (-not (Test-Path $adminPbf) -or $Force) {
    Write-Host "[3/4] İdari sınırlar süzülüyor..." -ForegroundColor Cyan
    Invoke-Osmium @(
        "tags-filter", "--overwrite", "-o", "/data/admin.osm.pbf", "/data/$pbfName",
        "r/boundary=administrative"
    )
}
else {
    Write-Host "[3/4] admin.osm.pbf zaten var, atlandı." -ForegroundColor DarkGray
}

foreach ($level in @(@{ Level = 4; Name = "provinces" }, @{ Level = 6; Name = "districts" })) {
    $outJson = Join-Path $dataDir "$($level.Name).geojsonl"

    if ((Test-Path $outJson) -and -not $Force) {
        Write-Host "      $($level.Name).geojsonl zaten var, atlandı." -ForegroundColor DarkGray
        continue
    }

    Write-Host "      admin_level=$($level.Level) ayrıştırılıyor -> $($level.Name).geojsonl" -ForegroundColor Cyan

    Invoke-Osmium @(
        "tags-filter", "--overwrite", "-o", "/data/$($level.Name).osm.pbf", "/data/admin.osm.pbf",
        "r/admin_level=$($level.Level)"
    )

    Invoke-Osmium @(
        "export", "--overwrite", "/data/$($level.Name).osm.pbf",
        "-f", "geojsonseq",
        "--add-unique-id=type_id",
        "--geometry-types=polygon",
        "-o", "/data/$($level.Name).geojsonl"
    )
}

# --- 4. Özet ---

Write-Host "[4/4] Hazır. Üretilen dosyalar:" -ForegroundColor Green
Get-ChildItem $dataDir -Filter "*.geojsonl" | ForEach-Object {
    $mb = [math]::Round($_.Length / 1MB, 1)
    Write-Host ("      {0,-24} {1,8} MB" -f $_.Name, $mb)
}

Write-Host ""
Write-Host "Sıradaki adım:" -ForegroundColor Cyan
Write-Host "  dotnet run --project src/Yolla.Harvester -- import-boundaries"
Write-Host "  dotnet run --project src/Yolla.Harvester -- import-places"
