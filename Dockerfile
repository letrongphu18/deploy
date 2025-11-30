# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /src

# Copy csproj từ đúng vị trí
COPY deploy/TMD/TMD/AIHUBOS.csproj ./TMD/
RUN dotnet restore "./TMD/AIHUBOS.csproj"

# Copy toàn bộ source code
COPY deploy/TMD/TMD/. ./TMD/
WORKDIR /src/TMD

# Build & Publish
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "AIHUBOS.dll"]