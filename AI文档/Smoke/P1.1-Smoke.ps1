param(
    [string]$BaseUrl = "http://127.0.0.1:5128",
    [switch]$StartServer
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$serverProcess = $null
$tempFiles = New-Object System.Collections.Generic.List[string]
$results = New-Object System.Collections.Generic.List[object]

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
    Add-Result "01 health endpoint returns P1.1" ($health.success -and $health.data.phase -eq "P1.1") "phase=$($health.data.phase)"

    $models = Invoke-JsonApi "/api/ai/models"
    Add-Result "02 AI model list returns default first" ($models.success -and $models.data.Count -ge 2 -and $models.data[0].isDefault) "default=$($models.data[0].id)"

    $clean = Invoke-JsonApi "/api/ai/clean" "POST" @{
        modelId = $models.data[0].id
        naturalLanguage = "Project planning map`n- collect requirements`n- confirm with users`n- import structured map"
    }
    Add-Result "03 AI clean returns standard schema" ($clean.success -and $clean.data.transfer.schemaVersion -eq "netmind.mindmap.v1") "schema=$($clean.data.transfer.schemaVersion)"
    Add-Result "04 AI clean expands nodes" ($clean.data.transfer.nodes.Count -ge 4) "nodes=$($clean.data.transfer.nodes.Count)"
    Add-Result "05 AI clean creates relations" ($clean.data.transfer.relations.Count -ge 3) "relations=$($clean.data.transfer.relations.Count)"

    $imported = Invoke-JsonApi "/api/mind-map-transfer/structure" "POST" @{ mindMap = $clean.data.transfer }
    Add-Result "06 cleaned structure can be imported" ($imported.success -and $imported.data.structure.nodes.Count -eq $clean.data.transfer.nodes.Count) "map=$($imported.data.structure.map.id)"

    $results | Format-Table -AutoSize
    Write-Host "P1.1 smoke passed: $($results.Count)/$($results.Count)"
} finally {
    foreach ($path in $tempFiles) {
        Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue
    }

    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
    }
}
