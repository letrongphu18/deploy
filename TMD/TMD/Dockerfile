# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0-preview AS build
WORKDIR /src

# Copy csproj
COPY AIHUBOS.csproj ./
RUN dotnet restore

# Copy full source
COPY . .
RUN dotnet publish -c Release -o /app/published

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0-preview
WORKDIR /app
COPY --from=build /app/published .
EXPOSE 8080
ENTRYPOINT ["dotnet", "AIHUBOS.dll"]
