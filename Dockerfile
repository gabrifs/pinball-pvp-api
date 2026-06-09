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

# NOTE: this image does NOT run EF Core migrations on startup.
# Run migrations as a separate step in CI/CD before deploying:
#   dotnet ef database update --project PinballPVP.Api
# or use the equivalent EF bundle / migration script approach.
ENTRYPOINT ["dotnet", "PinballPVP.Api.dll"]
