FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore
COPY backend/CyberShield.Api/CyberShield.Api.csproj ./backend/CyberShield.Api/
RUN dotnet restore ./backend/CyberShield.Api/CyberShield.Api.csproj

# Build
COPY backend/CyberShield.Api/ ./backend/CyberShield.Api/
WORKDIR /src/backend/CyberShield.Api
RUN dotnet publish -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app ./
COPY public/ ./wwwroot/
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DOTNET_GCConserveMemory=9
ENV DOTNET_GCHeapHardLimit=402653184
ENV COMPlus_GCConserveMemory=9
ENV Database__Seed=false
ENV Database__AutoMigrate=true
EXPOSE 8080
ENTRYPOINT ["dotnet", "CyberShield.Api.dll"]
