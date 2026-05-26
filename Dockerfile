# ── Stage 1: Build Frontend (Node.js) ────────────────────────────
FROM node:24-alpine AS frontend-build
WORKDIR /app
COPY src/NetMind.Frontend/package*.json ./
RUN npm ci --quiet
COPY src/NetMind.Frontend/ ./
RUN npm run build

# ── Stage 2: Build Backend (.NET SDK) ─────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS backend-build
WORKDIR /src
COPY src/ ./
RUN dotnet publish NetMind.WebApi/NetMind.WebApi.csproj \
    -c Release \
    -o /publish

# ── Stage 3: Runtime Image ────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /publish
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 ca-certificates \
    && rm -rf /var/lib/apt/lists/*
COPY --from=backend-build /publish ./

# Place frontend dist where Program.cs expects it:
#   ContentRootPath = /publish
#   Path.Combine(ContentRootPath, "..", "NetMind.Frontend", "dist")
#   → /NetMind.Frontend/dist
RUN mkdir -p /NetMind.Frontend/dist
COPY --from=frontend-build /app/dist/ /NetMind.Frontend/dist/
COPY publish/agent/ /agent/
RUN test -f /agent/src/agent_kernel.py

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 5120

# --urls takes precedence over the hardcoded fallback in Program.cs
# shell-form CMD expands the PORT env var (Render sets this)
CMD dotnet NetMind.WebApi.dll --urls "http://0.0.0.0:${PORT:-5120}"
