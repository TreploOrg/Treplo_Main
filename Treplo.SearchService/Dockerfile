FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Treplo.SearchService/Treplo.SearchService.csproj", "Treplo.SearchService/"]
COPY ["Treplo.Infrastructure/Treplo.Infrastructure.csproj", "Treplo.Infrastructure/"]
RUN dotnet restore "Treplo.SearchService/Treplo.SearchService.csproj"
COPY . .
WORKDIR "/src/Treplo.SearchService"
RUN dotnet build "Treplo.SearchService.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Treplo.SearchService.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Treplo.SearchService.dll"]
