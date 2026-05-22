#!/usr/bin/env bash
set -euo pipefail

echo ""
echo "========================================"
echo "  Building NetMind Project (Linux)"
echo "========================================"
echo ""

# ── 1. Build Frontend ──────────────────────────────────────────────
echo "[1/3] Building Frontend..."
cd src/NetMind.Frontend

npm install
npm run build

cd ../..

# ── 2. Publish Backend ──────────────────────────────────────────────
echo ""
echo "[2/3] Publishing Backend..."
dotnet publish src/NetMind.WebApi/NetMind.WebApi.csproj \
    -c Release \
    -o publish/netmind

# ── 3. Assemble Frontend Artifacts ──────────────────────────────────
echo ""
echo "[3/3] Assembling Frontend..."
mkdir -p publish/NetMind.Frontend/dist
cp -r src/NetMind.Frontend/dist/* publish/NetMind.Frontend/dist/

echo ""
echo "========================================"
echo "  Build Completed Successfully!"
echo "========================================"
echo ""
echo "  Backend:  publish/netmind"
echo "  Frontend: publish/NetMind.Frontend/dist"
echo ""
