# Vitta.API (.NET 10). Build a partir da RAIZ do repositorio:
#   docker build -f src/Vitta.API/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /repo
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Vitta.Domain/Vitta.Domain.csproj src/Vitta.Domain/
COPY src/Vitta.Application/Vitta.Application.csproj src/Vitta.Application/
COPY src/Vitta.Persistence/Vitta.Persistence.csproj src/Vitta.Persistence/
COPY src/Vitta.Infrastructure/Vitta.Infrastructure.csproj src/Vitta.Infrastructure/
COPY src/Vitta.API/Vitta.API.csproj src/Vitta.API/
RUN dotnet restore src/Vitta.API/Vitta.API.csproj
COPY src/ src/
RUN dotnet publish src/Vitta.API/Vitta.API.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
# O driver do Postgres (Npgsql) carrega a lib de Kerberos ao conectar; sem ela, so polui o log.
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Vitta.API.dll"]
