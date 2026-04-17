# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first to maximize restore layer cache reuse
COPY FileSanitizerService.Api/FileSanitizerService.Api.csproj FileSanitizerService.Api/
COPY FileSanitizerService.Core/FileSanitizerService.Core.csproj FileSanitizerService.Core/
COPY FileSanitizerService.Infrastructure/FileSanitizerService.Infrastructure.csproj FileSanitizerService.Infrastructure/

RUN dotnet restore FileSanitizerService.Api/FileSanitizerService.Api.csproj

# Copy the remaining source after restore
COPY FileSanitizerService.Api/ FileSanitizerService.Api/
COPY FileSanitizerService.Core/ FileSanitizerService.Core/
COPY FileSanitizerService.Infrastructure/ FileSanitizerService.Infrastructure/

RUN dotnet publish FileSanitizerService.Api/FileSanitizerService.Api.csproj \
    -c Release \
    --no-restore \
    -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Run as non-root user
RUN adduser --disabled-password --gecos "" appuser \
    && chown -R appuser:appuser /app

COPY --from=build --chown=appuser:appuser /app/publish/ .

USER appuser

ENTRYPOINT ["dotnet", "FileSanitizerService.Api.dll"]
