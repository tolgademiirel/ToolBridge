# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0-jammy AS build
WORKDIR /src

COPY src/ToolBridge.Api/ToolBridge.Api.csproj src/ToolBridge.Api/
RUN dotnet restore src/ToolBridge.Api/ToolBridge.Api.csproj

COPY src/ToolBridge.Api/ src/ToolBridge.Api/
RUN dotnet publish src/ToolBridge.Api/ToolBridge.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    DOTNET_GCServer=0 \
    DOTNET_GCHeapHardLimitPercent=35 \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080
COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=3s --start-period=10s --retries=3 \
    CMD ["bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /api/health HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && grep -q '200 OK' <&3"]

ENTRYPOINT ["dotnet", "ToolBridge.Api.dll"]
