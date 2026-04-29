param(
    [string]$BaseUrl = "http://127.0.0.1:5127",
    [switch]$StartServer
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$serverProcess = $null
$results = New-Object System.Collections.Generic.List[object]
$tempFiles = New-Object System.Collections.Generic.List[string]

function Add-Result {
    param([string]$Name, [bool]$Passed, [string]$Detail)

    $script:results.Add([pscustomobject]@{
        Case = $Name
        Result = if ($Passed) { "PASS" } else { "FAIL" }
        Detail = $Detail
    })

    if (-not $Passed) {
        throw $Detail
    }
}

function New-TempJson {
    param([string]$Json)

    $path = [System.IO.Path]::GetTempFileName()
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($path, $Json, $utf8NoBom)
    $script:tempFiles.Add($path)
    return $path
}

function Invoke-JsonApi {
    param(
        [string]$Path,
        [string]$Method = "GET",
        [object]$Body = $null
    )

    $uri = "$BaseUrl$Path"
    $curlArgs = @("-sS", "-X", $Method, "-H", "Accept: application/json")
    if ($null -eq $Body) {
        $raw = & curl.exe @curlArgs $uri
    } else {
        $bodyFile = New-TempJson ($Body | ConvertTo-Json -Depth 20 -Compress)
        $raw = & curl.exe @curlArgs -H "Content-Type: application/json" --data-binary "@$bodyFile" $uri
    }

    if ($LASTEXITCODE -ne 0) {
        throw "curl failed for $Method $Path"
    }

    return $raw | ConvertFrom-Json
}

try {
    if ($StartServer) {
        $dllPath = Join-Path $repoRoot "src\NetMind.WebApi\bin\Release\net8.0\NetMind.WebApi.dll"
        if (-not (Test-Path $dllPath)) {
            throw "Release build output not found: $dllPath"
        }

        $webApiRoot = Join-Path $repoRoot "src\NetMind.WebApi"
        $env:ASPNETCORE_URLS = $BaseUrl
        $serverProcess = Start-Process -FilePath "dotnet" -ArgumentList @($dllPath) -WorkingDirectory $webApiRoot -WindowStyle Hidden -PassThru
        Start-Sleep -Seconds 3
    }

    $health = Invoke-JsonApi "/api/system/health"
    Add-Result "01 health endpoint returns P1.0" ($health.success -and $health.data.phase -eq "P1.0") "phase=$($health.data.phase)"

    $templatePath = [System.IO.Path]::GetTempFileName()
    $tempFiles.Add($templatePath)
    & curl.exe -sS -o $templatePath "$BaseUrl/api/mind-map-transfer/template"
    $template = Get-Content -Raw $templatePath | ConvertFrom-Json
    Add-Result "02 template download returns schema" ($template.schemaVersion -eq "netmind.mindmap.v1" -and $template.nodes.Count -gt 0) "nodes=$($template.nodes.Count)"

    $importRequest = @{
        mindMap = @{
            schemaVersion = "netmind.mindmap.v1"
            title = "P1 smoke structure"
            nodes = @(
                @{ clientId = "root"; title = "Root"; content = "Root content"; orderNo = 1 },
                @{ clientId = "child"; parentClientId = "root"; title = "Child"; content = "Child content"; orderNo = 1 }
            )
            relations = @(
                @{ sourceClientId = "root"; targetClientId = "child"; relationType = "supports"; weight = 0.7 }
            )
        }
    }

    $imported = Invoke-JsonApi "/api/mind-map-transfer/structure" "POST" $importRequest
    $mapId = $imported.data.structure.map.id
    Add-Result "03 structure import creates map" ($imported.success -and $mapId -gt 0) "map=$mapId"

    $structure = Invoke-JsonApi "/api/mind-map-transfer/$mapId/structure"
    Add-Result "04 structure export returns full map" ($structure.success -and $structure.data.transfer.nodes.Count -eq 2 -and $structure.data.transfer.relations.Count -eq 1) "nodes=$($structure.data.transfer.nodes.Count)"

    $exportPath = [System.IO.Path]::GetTempFileName()
    $tempFiles.Add($exportPath)
    & curl.exe -sS -o $exportPath "$BaseUrl/api/mind-map-transfer/$mapId/file"
    $exportedFile = Get-Content -Raw $exportPath | ConvertFrom-Json
    Add-Result "05 file export downloads JSON" ($exportedFile.schemaVersion -eq "netmind.mindmap.v1" -and $exportedFile.nodes.Count -eq 2) "nodes=$($exportedFile.nodes.Count)"

    $fileImportRaw = & curl.exe -sS -X POST -F "file=@$exportPath;type=application/json" -F "titleOverride=P1 smoke file import" "$BaseUrl/api/mind-map-transfer/file"
    if ($LASTEXITCODE -ne 0) {
        throw "curl failed for file import"
    }

    $fileImport = $fileImportRaw | ConvertFrom-Json
    Add-Result "06 file import creates map" ($fileImport.success -and $fileImport.data.structure.map.title -eq "P1 smoke file import") "map=$($fileImport.data.structure.map.id)"

    $results | Format-Table -AutoSize
    Write-Host "P1.0 smoke passed: $($results.Count)/$($results.Count)"
} finally {
    foreach ($path in $tempFiles) {
        Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue
    }

    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
    }
}
