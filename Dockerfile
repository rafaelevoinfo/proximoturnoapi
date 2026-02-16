# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

WORKDIR /build

# Copy project files
COPY ["Src/ProximoTurnoApi.csproj", "Src/"]

# Restore dependencies
RUN dotnet restore "Src/ProximoTurnoApi.csproj"

# Copy source code
COPY . .

# Build and publish the application
RUN dotnet publish "Src/ProximoTurnoApi.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0

WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_HTTP_PORTS=80

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD dotnet --version || exit 1

# Run the application
ENTRYPOINT ["dotnet", "ProximoTurnoApi.dll"]
