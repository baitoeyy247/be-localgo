# be-localgo

ASP.NET Core Web API for LocalGo.

## Structure

```text
src/
  LocalGo.Api/           HTTP, Swagger, middleware
  LocalGo.Application/   use cases (upcoming)
  LocalGo.Domain/        entities and enums
  LocalGo.Infrastructure/ EF Core, LINE, Redis (upcoming)
tests/
  LocalGo.Tests/
```

## Run

```bash
docker compose up -d db redis
cp src/LocalGo.Api/appsettings.Development.json.example src/LocalGo.Api/appsettings.Development.json
dotnet run --project src/LocalGo.Api
```

- Swagger: http://localhost:5080/swagger
- Health: http://localhost:5080/api/health

The compose stack starts PostgreSQL with PostGIS enabled and Redis locally. The default development connection string uses:

```text
Host=localhost;Port=5432;Database=localgo;Username=admin;Password=P@ssw0rd
```

If you use your own PostgreSQL instead of Docker Compose, connect with a privileged PostgreSQL user and enable PostGIS in the `localgo` database before the first run:

```sql
CREATE EXTENSION IF NOT EXISTS postgis;
```

For Neon or another managed PostgreSQL provider, run `scripts/sit-neon-init.sql` in the provider SQL editor before starting the API.

If you previously created the local Docker database before this compose file existed, recreate the volume so the init script runs:

```bash
docker compose down -v
docker compose up -d db redis
```

To run the API in Docker too:

```bash
docker compose --profile api up --build
```

### Migrations

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
dotnet ef database update --project src/LocalGo.Infrastructure --startup-project src/LocalGo.Api
```

Development startup runs migrations and seeds categories automatically.

### Dev login (Development only)

```bash
curl -X POST http://localhost:5080/api/auth/dev/login \
  -H "Content-Type: application/json" \
  -d '{"displayName":"Dev User","activeRole":"Requester"}'
```
