param(
    [string]$BaseUrl = "http://127.0.0.1:5123",
    [switch]$StartServer
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$serverProcess = $null
$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [string]$Name,
        [bool]$Passed,
        [string]$Detail
    )

    $script:results.Add([pscustomobject]@{
        Case = $Name
        Result = if ($Passed) { "PASS" } else { "FAIL" }
        Detail = $Detail
    })

    if (-not $Passed) {
        throw $Detail
    }
}

function Invoke-Api {
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
        $json = $Body | ConvertTo-Json -Depth 8 -Compress
        $bodyFile = [System.IO.Path]::GetTempFileName()
        try {
            $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
            [System.IO.File]::WriteAllText($bodyFile, $json, $utf8NoBom)
            $raw = & curl.exe @curlArgs -H "Content-Type: application/json" --data-binary "@$bodyFile" $uri
        } finally {
            Remove-Item -LiteralPath $bodyFile -ErrorAction SilentlyContinue
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "curl failed for $Method $Path"
    }

    $result = $raw | ConvertFrom-Json
    if ($null -ne $result.value -and $null -ne $result.value.success) {
        return $result.value
    }

    return $result
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

    $health = Invoke-Api "/api/system/health"
    Add-Result "01 health endpoint returns P0.5" ($health.success -and $health.data.phase -eq "P0.5") "phase=$($health.data.phase)"

    $homeContent = & curl.exe -sS "$BaseUrl/"
    Add-Result "02 frontend shell is served" ($LASTEXITCODE -eq 0 -and ($homeContent -join "`n").Contains("NetMind")) "curl=$LASTEXITCODE"

    $maps = Invoke-Api "/api/mind-maps"
    Add-Result "03 seed maps are listed" ($maps.success -and $maps.data.Count -ge 3) "count=$($maps.data.Count)"

    $map = Invoke-Api "/api/mind-maps" "POST" @{ title = "P0.5 smoke map" }
    Add-Result "04 create mind map" ($map.success -and $map.data.id -gt 0) "id=$($map.data.id)"

    $renamedMap = Invoke-Api "/api/mind-maps/$($map.data.id)" "PUT" @{ title = "P0.5 smoke map updated"; rootNodeId = $null }
    Add-Result "05 rename mind map" ($renamedMap.success -and $renamedMap.data.title -eq "P0.5 smoke map updated") "title=$($renamedMap.data.title)"

    $root = Invoke-Api "/api/nodes" "POST" @{ mapId = $map.data.id; parentId = $null; title = "Root node"; content = "Smoke root"; orderNo = 1 }
    Add-Result "06 create root node" ($root.success -and $root.data.mapId -eq $map.data.id) "id=$($root.data.id)"

    $child = Invoke-Api "/api/nodes" "POST" @{ mapId = $map.data.id; parentId = $root.data.id; title = "Child node"; content = "Smoke child"; orderNo = 1 }
    Add-Result "07 create child node" ($child.success -and $child.data.parentId -eq $root.data.id) "id=$($child.data.id)"

    $nodes = Invoke-Api "/api/nodes/by-map/$($map.data.id)"
    $childFromList = $nodes.data | Where-Object { $_.id -eq $child.data.id } | Select-Object -First 1
    $nodePass = $nodes.success -and $nodes.data.Count -eq 2 -and $childFromList.parentId -eq $root.data.id
    Add-Result "08 list nodes by map preserves hierarchy" $nodePass "count=$($nodes.data.Count)"

    $updatedChild = Invoke-Api "/api/nodes/$($child.data.id)" "PUT" @{ parentId = $root.data.id; title = "Child node updated"; content = "Saved"; orderNo = 2 }
    Add-Result "09 update node" ($updatedChild.success -and $updatedChild.data.title -eq "Child node updated" -and $updatedChild.data.orderNo -eq 2) "title=$($updatedChild.data.title)"

    $relation = Invoke-Api "/api/node-relations" "POST" @{ mapId = $map.data.id; sourceId = $root.data.id; targetId = $child.data.id; relationType = "depends_on"; weight = 0.8 }
    Add-Result "10 create node relation" ($relation.success -and $relation.data.relationType -eq "depends_on") "id=$($relation.data.id)"

    $relations = Invoke-Api "/api/node-relations/by-map/$($map.data.id)"
    Add-Result "11 list relations by map" ($relations.success -and $relations.data.Count -eq 1) "count=$($relations.data.Count)"

    $deleteRelation = Invoke-Api "/api/node-relations/$($relation.data.id)" "DELETE"
    Add-Result "12 delete relation" ($deleteRelation.success -and $deleteRelation.data.deleted) "affected=$($deleteRelation.data.affectedCount)"

    $deleteMap = Invoke-Api "/api/mind-maps/$($map.data.id)/cascade" "DELETE"
    Add-Result "13 cascade delete mind map" ($deleteMap.success -and $deleteMap.data.affectedCount -eq 3) "affected=$($deleteMap.data.affectedCount)"

    $results | Format-Table -AutoSize
    Write-Host "P0.5 smoke passed: $($results.Count)/$($results.Count)"
} finally {
    if ($null -ne $serverProcess -and -not $serverProcess.HasExited) {
        Stop-Process -Id $serverProcess.Id -Force
    }

}
