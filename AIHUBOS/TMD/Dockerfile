# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /src

# Copy csproj
COPY aihubos_system/AIHUBOS/TMD/AIHUBOS.csproj ./TMD/
RUN dotnet restore "./TMD/AIHUBOS.csproj"

# Copy full source
COPY aihubos_system/AIHUBOS/TMD/. ./TMD/
WORKDIR /src/TMD

# Publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AIHUBOS.dll"]
