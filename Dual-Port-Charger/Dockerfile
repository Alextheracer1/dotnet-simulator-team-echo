# ==========================================
# Build & Publish Stage (.NET 10 SDK)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy NuGet configuration
COPY nuget.config ./

# Copy csproj files for optimal Docker layer caching
COPY Dual-Port-Charger/Dual-Port-Charger.csproj Dual-Port-Charger/
COPY TeamShared/TeamShared.csproj TeamShared/
COPY TeamConsoleShared/TeamConsoleShared.csproj TeamConsoleShared/
COPY TeamRabbitMQShared/TeamRabbitMQShared.csproj TeamRabbitMQShared/

# Restore dependencies
RUN dotnet restore Dual-Port-Charger/Dual-Port-Charger.csproj

# Copy source code for the referenced projects
COPY Dual-Port-Charger/ Dual-Port-Charger/
COPY TeamShared/ TeamShared/
COPY TeamConsoleShared/ TeamConsoleShared/
COPY TeamRabbitMQShared/ TeamRabbitMQShared/

# Build and publish the application in Release mode
WORKDIR /src/Dual-Port-Charger
RUN dotnet publish Dual-Port-Charger.csproj -c Release -o /app/publish --no-restore

# ==========================================
# Runtime Stage (.NET 10 Runtime)
# ==========================================
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app

# Ensure directory for simulation logs exists
RUN mkdir -p /app/logs

# Copy published application from build stage
COPY --from=build /app/publish .

# Standard environment variables
ENV SIMULATION_MASTER_HOST=host.docker.internal \
    SIMULATION_MASTER_PORT=1510 \
    RABBITMQ_HOST=rabbitmq \
    RABBITMQ_PORT=5672 \
    RABBITMQ_USER=guest \
    RABBITMQ_PASS=guest

# Expose Modbus TCP Server Port (LB02 standard for Dual-Port Charger)
EXPOSE 1502

# Start simulator
ENTRYPOINT ["dotnet", "Dual-Port-Charger.dll"]
