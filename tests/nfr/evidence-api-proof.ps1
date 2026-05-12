<#
.SYNOPSIS
  Tạo nhiều POI + mô phỏng nhiều "người dùng" (deviceId), gọi API log / visit / movement,
  rồi xuất file .txt chứng minh API (kể cả queueDelayMs trên visit/start|end).

.DESCRIPTION
  - POST /api/Admin/poi-with-owner (cần X-Admin-Key) để tạo POI demo.
  - Với mỗi user: POST /api/Poi/log, visit/start, movement, visit/end (giống k6 concurrency).
  - GET /api/Admin/ops/ingress-queue và /api/Admin/analytics/paths để đối chiếu sau chạy.

.PARAMETER BaseUrl
  Ví dụ: https://localhost:7236 (không cần /api ở cuối).

.PARAMETER AdminApiKey
  Trùng appsettings Admin:ApiKey (mặc định dev key trong repo — đổi khi deploy).

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File tests/nfr/evidence-api-proof.ps1 -BaseUrl https://localhost:7236 -NumPois 3 -NumUsers 10
#>
[CmdletBinding()]
param(
    [string] $BaseUrl = "https://localhost:7236",
    [string] $AdminApiKey = "streetfood-admin-dev-key-change-me",
    [ValidateRange(2, 50)]
    [int] $NumPois = 3,
    [ValidateRange(1, 100)]
    [int] $NumUsers = 10,
    [string] $OutputDir = "tests/nfr/results"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Normalize-ApiRoot([string] $url) {
    $u = $url.TrimEnd("/")
    if ($u -match "/api$") { return $u }
    return "$u/api"
}

function Invoke-ApiJson {
    param(
        [string] $Method,
        [string] $Url,
        [hashtable] $Headers,
        [string] $Body = $null
    )
    $params = @{
        Uri             = $Url
        Method          = $Method
        Headers         = $Headers
        UseBasicParsing = $true
    }
    if ($null -ne $Body -and $Body.Length -gt 0) {
        $params["ContentType"] = "application/json; charset=utf-8"
        $params["Body"] = [System.Text.Encoding]::UTF8.GetBytes($Body)
    }
    try {
        $resp = Invoke-WebRequest @params
        return @{ Ok = $true; Status = [int]$resp.StatusCode; Content = $resp.Content }
    }
    catch {
        $r = $_.Exception.Response
        if ($null -eq $r) {
            return @{ Ok = $false; Status = 0; Content = $_.Exception.Message }
        }
        $reader = New-Object System.IO.StreamReader($r.GetResponseStream())
        $txt = $reader.ReadToEnd()
        return @{ Ok = $false; Status = [int]$r.StatusCode; Content = $txt }
    }
}

$tag = Get-Date -Format "yyyyMMdd-HHmmss"
$apiRoot = Normalize-ApiRoot $BaseUrl
$outDirAbs = Join-Path (Get-Location) $OutputDir
if (-not (Test-Path $outDirAbs)) { New-Item -ItemType Directory -Path $outDirAbs | Out-Null }
$reportPath = Join-Path $outDirAbs "api-evidence-$tag.txt"

$adminHeaders = @{
    "X-Admin-Key"                = $AdminApiKey
    "ngrok-skip-browser-warning" = "true"
}
$jsonHeaders = @{
    "ngrok-skip-browser-warning" = "true"
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("=== StreetFood — báo cáo chứng minh API (tự động) ===")
[void]$sb.AppendLine("Thời gian UTC: $((Get-Date).ToUniversalTime().ToString('o'))")
[void]$sb.AppendLine("Base URL: $apiRoot")
[void]$sb.AppendLine("Số POI tạo: $NumPois | Số thiết bị mô phỏng: $NumUsers")
[void]$sb.AppendLine("")

# --- Ingress settings (trước khi tải) ---
$ingressBefore = Invoke-ApiJson -Method GET -Url "$apiRoot/Admin/ops/ingress-queue" -Headers $adminHeaders
[void]$sb.AppendLine("--- GET /Admin/ops/ingress-queue (cấu hình ingress) ---")
[void]$sb.AppendLine("HTTP $($ingressBefore.Status)")
[void]$sb.AppendLine($ingressBefore.Content)
[void]$sb.AppendLine("")

# --- Tạo POI ---
$poiIds = [System.Collections.Generic.List[int]]::new()
$baseLat = 10.7765
$baseLng = 106.7010
for ($i = 1; $i -le $NumPois; $i++) {
    $ownerUser = "ev_${tag}_o$i"
    $ownerPass = "EvPass_${tag}_$i!"
    $bodyObj = @{
        ownerUsername = $ownerUser
        ownerPassword = $ownerPass
        ownerEmail    = "$ownerUser@evidence.local"
        poiName       = "Evidence POI $tag #$i"
        poiDescription = "Auto evidence run"
        latitude      = $baseLat + ($i * 0.00025)
        longitude     = $baseLng + ($i * 0.00025)
        radius        = 50
        address       = "Evidence $tag"
        imageUrl      = ""
        openingHours  = ""
        phone         = ""
    }
    $json = ($bodyObj | ConvertTo-Json -Compress)
    $url = "$apiRoot/Admin/poi-with-owner"
    $r = Invoke-ApiJson -Method POST -Url $url -Headers $adminHeaders -Body $json
    [void]$sb.AppendLine("--- Tạo POI #$i POST /Admin/poi-with-owner ---")
    [void]$sb.AppendLine("HTTP $($r.Status) | Owner=$ownerUser")
    [void]$sb.AppendLine($r.Content)
    if (-not $r.Ok) {
        [void]$sb.AppendLine("")
        [void]$sb.AppendLine("DỪNG: tạo POI thất bại. Kiểm tra API, DB, X-Admin-Key.")
        [System.IO.File]::WriteAllText($reportPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
        Write-Host "Đã ghi (lỗi): $reportPath"
        exit 1
    }
    try {
        $obj = $r.Content | ConvertFrom-Json
        if ($null -ne $obj.poiId) { $poiIds.Add([int]$obj.poiId) }
    }
    catch {
        [void]$sb.AppendLine("Không parse được poiId từ JSON.")
    }
    [void]$sb.AppendLine("")
}

if ($poiIds.Count -lt 2) {
    [void]$sb.AppendLine("Lỗi: cần ít nhất 2 POI để gọi movement A->B.")
    [System.IO.File]::WriteAllText($reportPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
    Write-Host $reportPath
    exit 1
}

[void]$sb.AppendLine("Danh sách poiId đã tạo: $($poiIds -join ', ')")
[void]$sb.AppendLine("")

# --- Mỗi "user": log + visit + movement + visit/end ---
$visitStartDelays = [System.Collections.Generic.List[int]]::new()
$visitEndDelays = [System.Collections.Generic.List[int]]::new()
$linesOk = 0
$linesFail = 0

for ($u = 1; $u -le $NumUsers; $u++) {
    $deviceId = "ev-$tag-u$u"
    $fromIdx = ($u - 1) % $poiIds.Count
    $toIdx = $u % $poiIds.Count
    $fromPoi = $poiIds[$fromIdx]
    $toPoi = $poiIds[$toIdx]
    $lat = $baseLat + ($u * 0.00003)
    $lng = $baseLng + ($u * 0.00003)
    $at0 = (Get-Date).ToUniversalTime().ToString("o")
    $at1 = (Get-Date).ToUniversalTime().AddSeconds(1).ToString("o")
    $at2 = (Get-Date).ToUniversalTime().AddSeconds(3).ToString("o")

    [void]$sb.AppendLine("========== User #$u | deviceId=$deviceId | POI $fromPoi -> movement -> $toPoi ==========")

    $logBody = (@{ deviceId = $deviceId; latitude = $lat; longitude = $lng } | ConvertTo-Json -Compress)
    $logR = Invoke-ApiJson -Method "POST" -Url "$apiRoot/Poi/log" -Headers $jsonHeaders -Body $logBody
    [void]$sb.AppendLine("POST /Poi/log -> HTTP $($logR.Status) $($logR.Content)")
    if ($logR.Ok) { $linesOk++ } else { $linesFail++ }

    $startBody = (@{ deviceId = $deviceId; poiId = $fromPoi; atUtc = $at0 } | ConvertTo-Json -Compress)
    $startR = Invoke-ApiJson -Method "POST" -Url "$apiRoot/Poi/visit/start" -Headers $jsonHeaders -Body $startBody
    [void]$sb.AppendLine("POST /Poi/visit/start -> HTTP $($startR.Status) $($startR.Content)")
    if ($startR.Ok) { $linesOk++ } else { $linesFail++ }
    if ($startR.Ok -and $startR.Content) {
        try {
            $sj = $startR.Content | ConvertFrom-Json
            if ($null -ne $sj.queueDelayMs) { $visitStartDelays.Add([int]$sj.queueDelayMs) }
        }
        catch { }
    }

    $moveBody = (@{ deviceId = $deviceId; fromPoiId = $fromPoi; toPoiId = $toPoi; atUtc = $at1 } | ConvertTo-Json -Compress)
    $moveR = Invoke-ApiJson -Method "POST" -Url "$apiRoot/Poi/movement" -Headers $jsonHeaders -Body $moveBody
    [void]$sb.AppendLine("POST /Poi/movement -> HTTP $($moveR.Status) $($moveR.Content)")
    if ($moveR.Ok) { $linesOk++ } else { $linesFail++ }

    $endBody = (@{ deviceId = $deviceId; poiId = $fromPoi; atUtc = $at2 } | ConvertTo-Json -Compress)
    $endR = Invoke-ApiJson -Method "POST" -Url "$apiRoot/Poi/visit/end" -Headers $jsonHeaders -Body $endBody
    [void]$sb.AppendLine("POST /Poi/visit/end -> HTTP $($endR.Status) $($endR.Content)")
    if ($endR.Ok) { $linesOk++ } else { $linesFail++ }
    if ($endR.Ok -and $endR.Content) {
        try {
            $ej = $endR.Content | ConvertFrom-Json
            if ($null -ne $ej.queueDelayMs) { $visitEndDelays.Add([int]$ej.queueDelayMs) }
        }
        catch { }
    }
    [void]$sb.AppendLine("")
}

# --- Đọc lại paths (Admin): tìm movement vừa ghi ---
$pathsR = Invoke-ApiJson -Method "GET" -Url "$apiRoot/Admin/analytics/paths" -Headers $adminHeaders
[void]$sb.AppendLine("--- GET /Admin/analytics/paths (mẫu 500 bản ghi gần nhất) — lọc theo deviceId ev-$tag ---")
[void]$sb.AppendLine("HTTP $($pathsR.Status)")
if ($pathsR.Ok -and $pathsR.Content) {
    try {
        $rows = $pathsR.Content | ConvertFrom-Json
        $match = @($rows | Where-Object { $_.deviceId -like "ev-$tag-*" })
        [void]$sb.AppendLine("Số bản ghi movement khớp prefix ev-${tag}: $($match.Count)")
        $match | Select-Object -First 15 | ForEach-Object {
            [void]$sb.AppendLine(("  deviceId={0} from={1} to={2} at={3}" -f $_.deviceId, $_.fromPoiId, $_.toPoiId, $_.createdAt))
        }
    }
    catch {
        [void]$sb.AppendLine($pathsR.Content.Substring(0, [Math]::Min(2000, $pathsR.Content.Length)))
    }
}
else {
    [void]$sb.AppendLine($pathsR.Content)
}
[void]$sb.AppendLine("")

# --- Tóm tắt chứng minh ---
[void]$sb.AppendLine("=== Tóm tắt (đưa vào báo cáo đồ án) ===")
[void]$sb.AppendLine("- Ghi nhận vị trí (heatmap ingest): POST /api/Poi/log trả 200 với accepted=true khi HTTP thành công.")
[void]$sb.AppendLine("- Phiên visit + hàng đợi ingress theo POI: POST /api/Poi/visit/start và /visit/end trả JSON có queueDelayMs (thời gian chờ khóa theo POI).")
[void]$sb.AppendLine("- Dịch chuyển giữa hai POI: POST /api/Poi/movement ghi movement_paths; có thể đối chiếu qua GET /api/Admin/analytics/paths.")
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Số lệnh HTTP 2xx (ước lượng từng bước): $linesOk | Lệnh không 2xx: $linesFail")
if ($visitStartDelays.Count -gt 0) {
    $avgS = [int](($visitStartDelays | Measure-Object -Average).Average)
    $maxS = ($visitStartDelays | Measure-Object -Maximum).Maximum
    [void]$sb.AppendLine("queueDelayMs (visit/start): mẫu=$($visitStartDelays.Count), trung bình~${avgS}ms, max=${maxS}ms")
}
else {
    [void]$sb.AppendLine("queueDelayMs (visit/start): không thu thập được (kiểm tra response JSON).")
}
if ($visitEndDelays.Count -gt 0) {
    $avgE = [int](($visitEndDelays | Measure-Object -Average).Average)
    $maxE = ($visitEndDelays | Measure-Object -Maximum).Maximum
    [void]$sb.AppendLine("queueDelayMs (visit/end): mẫu=$($visitEndDelays.Count), trung bình~${avgE}ms, max=${maxE}ms")
}
else {
    [void]$sb.AppendLine("queueDelayMs (visit/end): không thu thập được (kiểm tra response JSON).")
}
[void]$sb.AppendLine("")
[void]$sb.AppendLine("Gợi ý: đính kèm file này + screenshot dashboard heatmap/paths + cấu hình ingress ở đầu báo cáo.")

[System.IO.File]::WriteAllText($reportPath, $sb.ToString(), [System.Text.UTF8Encoding]::new($false))
Write-Host "Đã xuất chứng cứ: $reportPath"
