FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY Viariato.sln ./
COPY src/Viariato.Api/Viariato.Api.csproj src/Viariato.Api/
COPY src/Viariato.Shared/Viariato.Shared.csproj src/Viariato.Shared/
COPY src/Viariato.Infrastructure/Viariato.Infrastructure.csproj src/Viariato.Infrastructure/
COPY src/Viariato.Modules.Users/Viariato.Modules.Users.csproj src/Viariato.Modules.Users/
COPY tests/Viariato.Modules.Users.Tests/Viariato.Modules.Users.Tests.csproj tests/Viariato.Modules.Users.Tests/
COPY tests/Viariato.Api.IntegrationTests/Viariato.Api.IntegrationTests.csproj tests/Viariato.Api.IntegrationTests/
RUN dotnet restore src/Viariato.Api/Viariato.Api.csproj

COPY src/ src/
RUN dotnet publish src/Viariato.Api/Viariato.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Viariato.Api.dll"]
