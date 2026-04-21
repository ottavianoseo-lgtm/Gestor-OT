FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Instalar herramientas de WASM antes de copiar el código para aprovechar el cache
RUN dotnet workload install wasm-tools

COPY ["src/GestorOT.Api/GestorOT.Api.csproj", "src/GestorOT.Api/"]
COPY ["src/GestorOT.Client/GestorOT.Client.csproj", "src/GestorOT.Client/"]
COPY ["src/GestorOT.Shared/GestorOT.Shared.csproj", "src/GestorOT.Shared/"]
COPY ["src/GestorOT.Infrastructure/GestorOT.Infrastructure.csproj", "src/GestorOT.Infrastructure/"]
COPY ["src/GestorOT.Application/GestorOT.Application.csproj", "src/GestorOT.Application/"]
COPY ["src/GestorOT.Domain/GestorOT.Domain.csproj", "src/GestorOT.Domain/"]

RUN dotnet restore "src/GestorOT.Api/GestorOT.Api.csproj"
COPY . .

WORKDIR "/src/src/GestorOT.Api"
RUN dotnet build "GestorOT.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GestorOT.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --chown=app --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GestorOT.Api.dll"]