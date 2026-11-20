# syntax=docker/dockerfile:1
# VehicleGo: Vue build → .NET publish → ASP.NET Core runtime (design §2.1, §8).
# Build through ./deploy.sh (it passes GIT_SHA); the Node major matches web/.nvmrc and the
# SDK satisfies global.json (rollForward: latestFeature).

# ---- 1. Frontend (web/ → web/dist) ----
FROM node:22.23-bookworm-slim AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY web/ ./
RUN npm run build

# ---- 2. Backend (dotnet publish VehicleGo.Web) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS api
WORKDIR /src
# Restore first so the NuGet layer is cached until a project file changes.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/VehicleGo.Domain/VehicleGo.Domain.csproj src/VehicleGo.Domain/
COPY src/VehicleGo.Application/VehicleGo.Application.csproj src/VehicleGo.Application/
COPY src/VehicleGo.Infrastructure/VehicleGo.Infrastructure.csproj src/VehicleGo.Infrastructure/
COPY src/VehicleGo.Web/VehicleGo.Web.csproj src/VehicleGo.Web/
RUN dotnet restore src/VehicleGo.Web/VehicleGo.Web.csproj
COPY src/ src/
RUN dotnet publish src/VehicleGo.Web/VehicleGo.Web.csproj --no-restore -c Release -o /app/publish /p:UseAppHost=false
# The Vue build becomes the published wwwroot (static files + SPA fallback).
COPY --from=web /web/dist/ /app/publish/wwwroot/

# ---- 3. Runtime (Ubuntu: includes tzdata for Asia/Taipei and ICU for NFKC) ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
ARG GIT_SHA=unknown
ENV GIT_SHA=${GIT_SHA} \
    ASPNETCORE_HTTP_PORTS=8080
WORKDIR /app
COPY --from=api /app/publish/ ./
# The image defaults to root; run as the non-root "app" user the base image provides.
USER $APP_UID
EXPOSE 8080
HEALTHCHECK --interval=10s --timeout=5s --start-period=30s --retries=5 \
  CMD ["dotnet", "VehicleGo.Web.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "VehicleGo.Web.dll"]
