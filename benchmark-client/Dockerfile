FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["benchmark-client/benchmark-client.csproj", "benchmark-client/"]
RUN dotnet restore "benchmark-client/benchmark-client.csproj"
COPY . .
WORKDIR "/src/benchmark-client"
RUN dotnet build "benchmark-client.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "benchmark-client.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "benchmark-client.dll"]
