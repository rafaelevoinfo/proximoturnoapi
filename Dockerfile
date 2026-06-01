# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

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
FROM mcr.microsoft.com/dotnet/aspnet:10.0

WORKDIR /app

# Copy published files from build stage
COPY --from=build /app/publish .

# Set environment variables
ENV ASPNETCORE_HTTP_PORTS=80
ENV TZ=America/Sao_Paulo

RUN apt-get update && apt-get install -y tzdata curl && \
    ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone && \
    rm -rf /var/lib/apt/lists/*

# Run the application
ENTRYPOINT ["dotnet", "ProximoTurnoApi.dll"]
