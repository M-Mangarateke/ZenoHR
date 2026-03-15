# REQ-OPS-009: Multi-stage Dockerfile for ZenoHR.Api + ZenoHR.Web (Blazor Server embedded in API).
# Build stage uses .NET SDK 10; runtime stage uses .NET ASP.NET runtime (smaller image).
# Target: Azure Container Apps, Linux/amd64.
# POPIA: image runs in southafricanorth — no data residency concern in the image itself.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first (layer caching for NuGet restore)
COPY ZenoHR.sln ./
COPY ZenoHR.slnx ./
COPY global.json ./
COPY Directory.Build.props ./
COPY Directory.Build.targets ./
COPY NuGet.config ./

COPY src/ZenoHR.Api/ZenoHR.Api.csproj src/ZenoHR.Api/
COPY src/ZenoHR.Web/ZenoHR.Web.csproj src/ZenoHR.Web/
COPY src/ZenoHR.Domain/ZenoHR.Domain.csproj src/ZenoHR.Domain/
COPY src/ZenoHR.Infrastructure/ZenoHR.Infrastructure.csproj src/ZenoHR.Infrastructure/
COPY src/ZenoHR.Module.Employee/ZenoHR.Module.Employee.csproj src/ZenoHR.Module.Employee/
COPY src/ZenoHR.Module.TimeAttendance/ZenoHR.Module.TimeAttendance.csproj src/ZenoHR.Module.TimeAttendance/
COPY src/ZenoHR.Module.Leave/ZenoHR.Module.Leave.csproj src/ZenoHR.Module.Leave/
COPY src/ZenoHR.Module.Payroll/ZenoHR.Module.Payroll.csproj src/ZenoHR.Module.Payroll/
COPY src/ZenoHR.Module.Compliance/ZenoHR.Module.Compliance.csproj src/ZenoHR.Module.Compliance/
COPY src/ZenoHR.Module.Audit/ZenoHR.Module.Audit.csproj src/ZenoHR.Module.Audit/
COPY src/ZenoHR.Module.Risk/ZenoHR.Module.Risk.csproj src/ZenoHR.Module.Risk/

# Copy packages.lock.json files for locked-mode restore (REQ-SEC-010)
COPY src/ZenoHR.Api/packages.lock.json src/ZenoHR.Api/
COPY src/ZenoHR.Web/packages.lock.json src/ZenoHR.Web/
COPY src/ZenoHR.Domain/packages.lock.json src/ZenoHR.Domain/
COPY src/ZenoHR.Infrastructure/packages.lock.json src/ZenoHR.Infrastructure/
COPY src/ZenoHR.Module.Employee/packages.lock.json src/ZenoHR.Module.Employee/
COPY src/ZenoHR.Module.TimeAttendance/packages.lock.json src/ZenoHR.Module.TimeAttendance/
COPY src/ZenoHR.Module.Leave/packages.lock.json src/ZenoHR.Module.Leave/
COPY src/ZenoHR.Module.Payroll/packages.lock.json src/ZenoHR.Module.Payroll/
COPY src/ZenoHR.Module.Compliance/packages.lock.json src/ZenoHR.Module.Compliance/
COPY src/ZenoHR.Module.Audit/packages.lock.json src/ZenoHR.Module.Audit/
COPY src/ZenoHR.Module.Risk/packages.lock.json src/ZenoHR.Module.Risk/

RUN dotnet restore ZenoHR.sln --locked-mode

# Copy all source (after restore for better layer caching)
COPY src/ src/

# Publish ZenoHR.Api (includes ZenoHR.Web as a project reference)
RUN dotnet publish src/ZenoHR.Api/ZenoHR.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Security: run as non-root user (principle of least privilege — REQ-SEC-001)
# The aspnet base image includes the 'app' user (uid 1654). Use groupadd/useradd (coreutils).
RUN groupadd --system --gid 1001 zenohr && \
    useradd --system --uid 1001 --gid zenohr --no-create-home zenohr

# Copy published output from build stage
COPY --from=build /app/publish .

# Copy statutory seed data (read at startup by StatutoryRuleSetSeeder — REQ-OPS-009)
# These are static JSON files with PAYE/UIF/SDL/ETI/BCEA tables — no PII.
COPY --from=build /src/docs/seed-data ./docs/seed-data

# Health check — Azure Container Apps liveness probe (REQ-OPS-007)
# curl is available in the aspnet base image; probe checks the /health endpoint.
HEALTHCHECK --interval=30s --timeout=10s --start-period=60s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

USER zenohr
EXPOSE 8080

# Use HTTP inside the container; TLS terminated at Azure Container Apps ingress.
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "ZenoHR.Api.dll"]
