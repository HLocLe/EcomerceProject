## Cursor Cloud specific instructions

### Project overview

HappyBox is an ASP.NET Core 8 Web API (Clean Architecture) for a gift box e-commerce platform. See `README.md` for full details on architecture, endpoints, and configuration.

### Required services

| Service | Default port | Purpose |
|---------|-------------|---------|
| PostgreSQL | 5432 | Primary database (`HappyBoxDb`) |
| Redis | 6379 | Distributed cache for refresh tokens |
| HappyBox API | 5142 | The application itself |

### Starting services

```bash
sudo service postgresql start
sudo service redis-server start
dotnet run --project PRN2322 --urls "http://0.0.0.0:5142"
```

The app auto-runs pending EF Core migrations on startup — no manual `dotnet ef database update` needed.

### Key gotchas

- **Database**: Despite the README and `docker-compose.yml` mentioning SQL Server, the actual code uses **PostgreSQL** via Npgsql. The connection string in `appsettings.json` is already configured for PostgreSQL (`Host=localhost;Port=5432`).
- **Redis required**: The app will fail to start if Redis is not running — it's used for refresh token storage via `StackExchangeRedis`.
- **No test projects**: `dotnet test` finds nothing to run. There are no unit/integration test projects in the solution.
- **Swagger always on**: Swagger UI is enabled in all environments (not just Development), accessible at `/swagger`.
- **CORS origins**: Configured for `localhost:5173`, `localhost:3000`, and `tet-den-roi.vercel.app`.
- **appsettings.json**: Contains placeholder API keys for external services (OpenRouter, GoogleAI, MoMo). The app runs fine without valid keys — those features just return errors gracefully.
- **Authorize attributes**: Many write endpoints have `[Authorize]` commented out in the codebase. The API is largely open for development/testing purposes.

### Common commands

| Task | Command |
|------|---------|
| Restore packages | `dotnet restore` |
| Build | `dotnet build` |
| Run (dev) | `dotnet run --project PRN2322` |
| Add migration | `dotnet ef migrations add <Name> -p Infrastructure -s PRN2322` |
| Apply migrations | `dotnet ef database update -p Infrastructure -s PRN2322` |
