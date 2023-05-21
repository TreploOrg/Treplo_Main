FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Treplo.PlayersService/Treplo.PlayersService.csproj", "Treplo.PlayersService/"]
COPY ["Treplo.Common.OrleansGrpcConnector/Treplo.Common.OrleansGrpcConnector.csproj", "Treplo.Common.OrleansGrpcConnector/"]
COPY ["Treplo.Infrastructure/Treplo.Infrastructure.csproj", "Treplo.Infrastructure/"]
RUN dotnet restore "Treplo.PlayersService/Treplo.PlayersService.csproj"
COPY . .
WORKDIR "/src/Treplo.PlayersService"
RUN dotnet build "Treplo.PlayersService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Treplo.PlayersService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Treplo.PlayersService.dll"]
