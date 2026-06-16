# syntax=docker/dockerfile:1

# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy the project file first so NuGet restore is cached as a separate layer.
# The restore layer is only invalidated when PinballPVP.Api.csproj changes.
COPY PinballPVP.Api/PinballPVP.Api.csproj PinballPVP.Api/
RUN dotnet restore PinballPVP.Api/PinballPVP.Api.csproj

COPY PinballPVP.Api/ PinballPVP.Api/
RUN dotnet publish PinballPVP.Api/PinballPVP.Api.csproj \
        -c Release \
        --no-restore \
        -o /app/publish

# Build the EF Core migration bundle.
# Self-contained so the bundle runs in the aspnet runtime image without the SDK.
# PinballPVPContextFactory (Data/) lets the tool create the DbContext without
# requiring the full app startup (JWT keys, email config, etc.).
RUN dotnet tool install --global dotnet-ef
ENV PATH="/root/.dotnet/tools:${PATH}"
RUN dotnet ef migrations bundle \
        --project PinballPVP.Api/PinballPVP.Api.csproj \
        --self-contained \
        --runtime linux-x64 \
        --output /app/publish/efbundle

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

RUN adduser --disabled-password --gecos "" appuser

COPY --from=build --chown=appuser:appuser /app/publish .

USER appuser

# Listen on HTTP only — TLS termination is handled by the reverse proxy/load balancer.
# Override ASPNETCORE_ENVIRONMENT at runtime (e.g. -e ASPNETCORE_ENVIRONMENT=Production).
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080

# efbundle (alongside this binary) is used by the docker-compose migrate service to
# apply pending migrations before the API rolls out — it is NOT run on startup here.
ENTRYPOINT ["dotnet", "PinballPVP.Api.dll"]
