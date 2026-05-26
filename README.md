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
cp src/LocalGo.Api/appsettings.Development.json.example src/LocalGo.Api/appsettings.Development.json
dotnet run --project src/LocalGo.Api
```

- Swagger: http://localhost:5080/swagger
- Health: http://localhost:5080/api/health

Requires Docker infra from repo root: `../scripts/dev-up.sh`

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
