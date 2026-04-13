FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
RUN apt-get update && apt-get install -y python3 --no-install-recommends && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY . .

RUN dotnet workload install wasm-tools

WORKDIR "/src/src/GestorOT.Api"
RUN dotnet restore "GestorOT.Api.csproj"
RUN dotnet build "GestorOT.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GestorOT.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --chown=app --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GestorOT.Api.dll"]