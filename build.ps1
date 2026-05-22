# build-all.ps1
Write-Host "=== 构建前端 ===" -ForegroundColor Cyan
Set-Location src\NetMind.Frontend
npm install
if ($LASTEXITCODE -ne 0) { throw "npm install 失败" }

npm run build
if ($LASTEXITCODE -ne 0) { throw "npm run build 失败" }
Set-Location ..\..

Write-Host "=== 发布后端 (win-x64 自包含) ===" -ForegroundColor Cyan
dotnet publish src\NetMind.WebApi\NetMind.WebApi.csproj -c Release -r win-x64 --self-contained true -o publish\netmind
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失败" }

Write-Host "=== 组装前端产物 ===" -ForegroundColor Cyan
New-Item -ItemType Directory -Force publish\NetMind.Frontend\dist | Out-Null
Copy-Item -Recurse -Force src\NetMind.Frontend\dist\* publish\NetMind.Frontend\dist\

Write-Host "=== 完成 ===" -ForegroundColor Green
Write-Host "后端目录: publish\netmind" -ForegroundColor Yellow
Write-Host "前端目录: publish\NetMind.Frontend\dist" -ForegroundColor Yellow