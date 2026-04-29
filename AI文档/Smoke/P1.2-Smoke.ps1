param(
    [string]$BaseUrl = "http://127.0.0.1:5129",
    [switch]$StartServer,
    [switch]$RunAiClean
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
    Add-Result "01 health endpoint returns P1.2" ($health.success -and $health.data.phase -eq "P1.2") "phase=$($health.data.phase)"

    $models = Invoke-JsonApi "/api/ai/models"
    Add-Result "02 AI model list is configured" ($models.success -and $models.data.Count -ge 2) "models=$($models.data.Count)"
    Add-Result "03 DeepSeek cloud is default" ($models.data[0].id -eq "deepseek-cloud" -and $models.data[0].isDefault) "default=$($models.data[0].id)"
    Add-Result "04 Ollama local fallback is listed" (($models.data | Where-Object { $_.id -eq "ollama-local" }).Count -eq 1) "fallback=ollama-local"

    $maps = Invoke-JsonApi "/api/mind-maps"
    Add-Result "05 mind maps query uses configured database" ($maps.success -and $null -ne $maps.data) "count=$($maps.data.Count)"

    if ($RunAiClean) {
        $clean = Invoke-JsonApi "/api/ai/clean" "POST" @{
            naturalLanguage = "Project planning map`n- collect requirements`n- confirm with users`n- import structured map"
        }
        Add-Result "06 AI clean returns standard schema" ($clean.success -and $clean.data.transfer.schemaVersion -eq "netmind.mindmap.v1") "schema=$($clean.data.transfer.schemaVersion)"
    }

    $results | Format-Table -AutoSize
    Write-Host "P1.2 smoke passed: $($results.Count)/$($results.Count)"
} finally {
    foreach ($path in $tempFiles) {
        Remove-Item -LiteralPath $path -ErrorAction SilentlyContinue
    }

    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
    }
}
