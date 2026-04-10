FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER app
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
# Copia todo desde la raíz (incluyendo la carpeta src/)
COPY . .

# Entramos a la ruta donde está el proyecto API
# La ruta es /src (workdir) + /src (carpeta del repo) + /GestorOT.Api
WORKDIR "/src/src/GestorOT.Api"
RUN dotnet restore "GestorOT.Api.csproj"
RUN dotnet build "GestorOT.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GestorOT.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --chown=app --from=publish /app/publish .
ENTRYPOINT ["dotnet", "GestorOT.Api.dll"]