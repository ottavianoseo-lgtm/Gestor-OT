FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/GestorOT.Api/GestorOT.Api.csproj", "src/GestorOT.Api/"]
COPY ["src/GestorOT.Client/GestorOT.Client.csproj", "src/GestorOT.Client/"]
COPY ["src/GestorOT.Shared/GestorOT.Shared.csproj", "src/GestorOT.Shared/"]
COPY ["src/GestorOT.Application/GestorOT.Application.csproj", "src/GestorOT.Application/"]
COPY ["src/GestorOT.Infrastructure/GestorOT.Infrastructure.csproj", "src/GestorOT.Infrastructure/"]
COPY ["src/GestorOT.Domain/GestorOT.Domain.csproj", "src/GestorOT.Domain/"]
RUN dotnet restore "src/GestorOT.Api/GestorOT.Api.csproj"
COPY . .
WORKDIR "/src/src/GestorOT.Api"
RUN dotnet build "GestorOT.Api.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "GestorOT.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "GestorOT.Api.dll"]
