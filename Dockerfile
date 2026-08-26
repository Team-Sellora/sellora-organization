FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["global.json", "./"]
COPY ["Sellora.CoreService.sln", "./"]
COPY ["src", "src"]
COPY ["tests", "tests"]

RUN dotnet restore Sellora.CoreService.sln

FROM build AS publish
WORKDIR /src
RUN dotnet publish src/Sellora.CoreService.Api/Sellora.CoreService.Api.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

EXPOSE 8080
EXPOSE 8443

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=publish /app/publish .

ENTRYPOINT ["dotnet", "Sellora.CoreService.Api.dll"]
