# syntax=docker/dockerfile:1
# One image for the whole app: the API serves the React build from wwwroot (same origin; see WebHosting.cs).
# Targets:
#   final    (default) the app, on port 8080, as a non-root user
#   migrator the EF Core migrations bundle: applies the migrations and exits (run before deploying the app)

# --- Frontend --------------------------------------------------------------------------------------------------------
FROM node:25-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# --- Backend ---------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Project files first: the restore layer is reused until a dependency changes.
COPY global.json Directory.Build.props dotnet-tools.json ./
COPY src/Farmacia.Domain/Farmacia.Domain.csproj src/Farmacia.Domain/
COPY src/Farmacia.Application/Farmacia.Application.csproj src/Farmacia.Application/
COPY src/Farmacia.Infrastructure/Farmacia.Infrastructure.csproj src/Farmacia.Infrastructure/
COPY src/Farmacia.Api/Farmacia.Api.csproj src/Farmacia.Api/
RUN dotnet restore src/Farmacia.Api/Farmacia.Api.csproj
COPY src/ src/
RUN dotnet publish src/Farmacia.Api/Farmacia.Api.csproj -c Release -o /app --no-restore /p:UseAppHost=false

# --- Migrations bundle -----------------------------------------------------------------------------------------------
FROM build AS migrations-build
# EF builds the app's host to find the DbContext; it stops before the app runs, but the services it registers need a
# connection string to exist (any value: the bundle receives the real one with --connection).
ENV ConnectionStrings__Farmacia="Server=build-time-placeholder"
RUN dotnet tool restore \
 && dotnet ef migrations bundle --project src/Farmacia.Infrastructure --startup-project src/Farmacia.Api \
      --configuration Release --self-contained --target-runtime linux-x64 --output /bundle/efbundle --verbose

# "extra": with ICU (SqlClient needs globalization) and time zones, still chiseled (no shell, no package manager).
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled-extra AS migrator
COPY --from=migrations-build /bundle/efbundle /efbundle
# Same reason as at build time: the bundle builds the app's host to create the DbContext. The real connection string
# arrives with --connection and is the one used.
ENV ConnectionStrings__Farmacia="Server=runtime-placeholder"
ENTRYPOINT ["/efbundle"]

# --- App -------------------------------------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra AS final
WORKDIR /app
COPY --from=build /app ./
COPY --from=web /web/dist ./wwwroot
# TLS ends at the platform's ingress (Azure Container Apps): trust its X-Forwarded-Proto/For, so the app knows the
# request was HTTPS (Secure cookie, HSTS) and logs the real client IP.
ENV ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    ASPNETCORE_ENVIRONMENT=Production
# The chiseled image runs as a non-root user ("app") and listens on 8080 by default.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Farmacia.Api.dll"]
