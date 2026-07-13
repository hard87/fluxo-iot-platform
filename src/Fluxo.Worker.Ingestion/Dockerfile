FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY . .
RUN dotnet restore src/Fluxo.Worker.Ingestion/Fluxo.Worker.Ingestion.csproj
RUN dotnet publish src/Fluxo.Worker.Ingestion/Fluxo.Worker.Ingestion.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Fluxo.Worker.Ingestion.dll"]
