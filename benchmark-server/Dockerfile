FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["benchmark-server/benchmark-server.csproj", "benchmark-server/"]
RUN dotnet restore "benchmark-server/benchmark-server.csproj"
COPY . .
WORKDIR "/src/benchmark-server"
RUN dotnet build "benchmark-server.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "benchmark-server.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "benchmark-server.dll"]
