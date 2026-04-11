# ── Build stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for optimal layer caching
COPY src/GWT.Domain/GWT.Domain.csproj              src/GWT.Domain/
COPY src/GWT.Application/GWT.Application.csproj    src/GWT.Application/
COPY src/GWT.Infrastructure/GWT.Infrastructure.csproj src/GWT.Infrastructure/
COPY src/GWT.Api/GWT.Api.csproj                    src/GWT.Api/

# Restore before copying all source (faster rebuilds on dep changes)
RUN dotnet restore src/GWT.Api/GWT.Api.csproj

# Copy remaining source
COPY src/ src/

# Publish release build
RUN dotnet publish src/GWT.Api/GWT.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Render injects $PORT at runtime; default to 4000 for local Docker use
EXPOSE 4000
ENV ASPNETCORE_URLS=http://+:4000

# Shell form so $PORT expands at container startup
CMD sh -c "ASPNETCORE_URLS=http://+:${PORT:-4000} dotnet GWT.Api.dll"
