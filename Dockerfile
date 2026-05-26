# LocalGo API — SIT / container deploy (Render, etc.)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LocalGo.sln ./
COPY src/LocalGo.Api/LocalGo.Api.csproj src/LocalGo.Api/
COPY src/LocalGo.Application/LocalGo.Application.csproj src/LocalGo.Application/
COPY src/LocalGo.Domain/LocalGo.Domain.csproj src/LocalGo.Domain/
COPY src/LocalGo.Infrastructure/LocalGo.Infrastructure.csproj src/LocalGo.Infrastructure/

RUN dotnet restore src/LocalGo.Api/LocalGo.Api.csproj

COPY src/ src/
RUN dotnet publish src/LocalGo.Api/LocalGo.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Staging
ENV ASPNETCORE_URLS=http://+:8080

EXPOSE 8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "LocalGo.Api.dll"]
