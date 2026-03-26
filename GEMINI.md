# GEMINI.md - Próximo Turno API

## Project Overview
**Próximo Turno API** is a backend system for managing board game rentals. It is built using **.NET 8.0** and follows a clean, layered architecture designed for maintainability and scalability.

### Core Technologies
- **Framework:** .NET 8.0 (ASP.NET Core Web API)
- **Database:** MySQL (via Pomelo.EntityFrameworkCore.MySql)
- **ORM:** Entity Framework Core
- **Authentication:** ASP.NET Core Identity with Bearer tokens
- **Validation:** Flunt (Notification Pattern)
- **Logging:** Serilog
- **Documentation:** Swagger/OpenAPI (Swashbuckle)
- **Environment Management:** DotNetEnv for `.env` support

## Architecture
The project is organized into several key layers within the `Src/` directory:

- **Application:**
    - `Controllers/`: API endpoints handling HTTP requests.
    - `UseCases/`: Core business logic. Each use case typically inherits from `UseCaseBasico` and uses the notification pattern for validation/error handling.
    - `DTOs/`: Data Transfer Objects for requests and responses.
- **Domain:** Contains domain-specific logic and entities.
- **Infrastructure:**
    - `Models/`: Database entities (EF Core).
    - `Repositories/`: Data access implementations (following the Repository pattern).
    - `DatabaseContext.cs`: Entity Framework database context.
- **Migrations:** Entity Framework Core database migration files.

## Building and Running

### Prerequisites
- .NET 8.0 SDK
- MySQL Server (or Docker)
- Docker Desktop (optional, for containerized execution)

### Key Commands

#### Local Development
- **Build:** `dotnet build`
- **Run:** `dotnet run --project Src/ProximoTurnoApi.csproj`
- **Restore Dependencies:** `dotnet restore`

#### Docker (Recommended)
- **Start API and MySQL:** `docker-compose up --build`
- **Stop Containers:** `docker-compose down`
- **Reset Database (Volumes):** `docker-compose down -v`

#### Database Migrations
- **Add Migration:** `dotnet ef migrations add <MigrationName> --project Src/ProximoTurnoApi.csproj`
- **Update Database:** `dotnet ef database update --project Src/ProximoTurnoApi.csproj`

### API Access
- **API Base URL:** `http://localhost:8080` (Docker) or `http://localhost:5000` (Local)
- **Swagger UI:** `/swagger/index.html`
- **Health Check:** `/health`

## Development Conventions

### Business Logic (Use Cases)
All business logic should be encapsulated in the `Application/UseCases` layer. Use cases should:
1. Inherit from `UseCaseBasico`.
2. Use **Flunt** for validation and error reporting.
3. Be registered as `Scoped` in `Program.cs`.

### Data Access
- Use the **Repository Pattern** for all database operations.
- Repositories should be defined in `Infrastructure/Repositories` and registered in `Program.cs`.

### Authentication
- The API uses **ASP.NET Core Identity API Endpoints** for user management.
- Authentication is handled via **Bearer tokens**.
- Protected endpoints should use the `[Authorize]` attribute.

### Configuration
- Sensitive configurations (connection strings, etc.) should be stored in a `.env` file (see `.env.example`).
- `DotNetEnv` loads these variables automatically in the `Development` environment.
