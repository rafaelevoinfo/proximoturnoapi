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

# Set environment variables
ENV ASPNETCORE_HTTP_PORTS=80
ENV TZ=America/Sao_Paulo
ENV PUPPETEER_EXECUTABLE_PATH=/usr/bin/google-chrome

RUN apt-get update && apt-get install -y --no-install-recommends \
    tzdata \
    curl \
    wget \
    gnupg \
    ca-certificates && \
    wget -q https://dl.google.com/linux/direct/google-chrome-stable_current_amd64.deb && \
    apt-get install -y ./google-chrome-stable_current_amd64.deb && \
    rm google-chrome-stable_current_amd64.deb && \
    ln -snf /usr/share/zoneinfo/$TZ /etc/localtime && echo $TZ > /etc/timezone && \
    rm -rf /var/lib/apt/lists/*

# Copy published files from build stage
COPY --from=build /app/publish .

# Run the application
ENTRYPOINT ["dotnet", "ProximoTurnoApi.dll"]
