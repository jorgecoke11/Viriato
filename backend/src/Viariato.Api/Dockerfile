FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Baked into the published assembly's AssemblyInformationalVersion (as "{Version}+{SourceRevisionId}")
# by the SDK's own versioning targets — no extra package needed. Defaults keep local `docker build`
# without --build-arg working, just with an obviously-a-dev-build version.
ARG APP_VERSION=0.0.0-dev
ARG GIT_SHA=unknown

COPY Viariato.sln ./
COPY src/Viariato.Api/Viariato.Api.csproj src/Viariato.Api/
COPY src/Viariato.Shared/Viariato.Shared.csproj src/Viariato.Shared/
COPY src/Viariato.Infrastructure/Viariato.Infrastructure.csproj src/Viariato.Infrastructure/
COPY src/Viariato.Modules.Users/Viariato.Modules.Users.csproj src/Viariato.Modules.Users/
COPY src/Viariato.Modules.Markets/Viariato.Modules.Markets.csproj src/Viariato.Modules.Markets/
COPY src/Viariato.Modules.Ops/Viariato.Modules.Ops.csproj src/Viariato.Modules.Ops/
COPY src/Viariato.Modules.Flujos/Viariato.Modules.Flujos.csproj src/Viariato.Modules.Flujos/
COPY src/Viariato.Modules.Casos/Viariato.Modules.Casos.csproj src/Viariato.Modules.Casos/
COPY tests/Viariato.Modules.Users.Tests/Viariato.Modules.Users.Tests.csproj tests/Viariato.Modules.Users.Tests/
COPY tests/Viariato.Modules.Markets.Tests/Viariato.Modules.Markets.Tests.csproj tests/Viariato.Modules.Markets.Tests/
COPY tests/Viariato.Modules.Flujos.Tests/Viariato.Modules.Flujos.Tests.csproj tests/Viariato.Modules.Flujos.Tests/
COPY tests/Viariato.Modules.Casos.Tests/Viariato.Modules.Casos.Tests.csproj tests/Viariato.Modules.Casos.Tests/
COPY tests/Viariato.Api.IntegrationTests/Viariato.Api.IntegrationTests.csproj tests/Viariato.Api.IntegrationTests/
RUN dotnet restore src/Viariato.Api/Viariato.Api.csproj

COPY src/ src/
RUN dotnet publish src/Viariato.Api/Viariato.Api.csproj -c Release -o /app --no-restore \
    -p:Version=$APP_VERSION -p:SourceRevisionId=$GIT_SHA

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Viariato.Api.dll"]
