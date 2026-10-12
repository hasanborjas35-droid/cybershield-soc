FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

COPY backend/CyberShield.Api/CyberShield.Api.csproj ./backend/CyberShield.Api/
RUN dotnet restore ./backend/CyberShield.Api/CyberShield.Api.csproj

COPY backend/CyberShield.Api/ ./backend/CyberShield.Api/
WORKDIR /src/backend/CyberShield.Api
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine
WORKDIR /app
COPY --from=build /app ./
COPY public/ ./wwwroot/

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_GCConserveMemory=9
ENV DOTNET_GCHeapHardLimit=314572800
ENV Database__AutoMigrate=true
ENV Database__Seed=true

EXPOSE 8080
ENTRYPOINT ["dotnet", "CyberShield.Api.dll"]
