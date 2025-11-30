FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /src

# Copy csproj
COPY TMD/TMD/AIHUBOS.csproj ./TMD/
RUN dotnet restore ./TMD/AIHUBOS.csproj

# Copy full source
COPY TMD/TMD/. ./TMD/
WORKDIR /src/TMD
RUN dotnet publish -c Release -o /app/published

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview
WORKDIR /app
COPY --from=build /app/published .
ENTRYPOINT ["dotnet", "AIHUBOS.dll"]
