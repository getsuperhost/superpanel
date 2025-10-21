# Dockerfile for SuperPanel Web API
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/WebAPI/SuperPanel.WebAPI.csproj", "src/WebAPI/"]
RUN dotnet restore "src/WebAPI/SuperPanel.WebAPI.csproj"
COPY . .
WORKDIR "/src/src/WebAPI"
RUN dotnet build "SuperPanel.WebAPI.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "SuperPanel.WebAPI.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install required system packages for C++ interop
RUN apt-get update && apt-get install -y \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

ENTRYPOINT ["dotnet", "SuperPanel.WebAPI.dll"]