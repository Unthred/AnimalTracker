FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

RUN apt-get update \
    && apt-get install -y --no-install-recommends nodejs npm \
    && rm -rf /var/lib/apt/lists/*

COPY package*.json ./
COPY tailwind.config.js ./
COPY postcss.config.js ./
RUN npm ci --prefix /src

COPY ./src/AnimalTracker/AnimalTracker.csproj ./src/AnimalTracker/
RUN dotnet restore ./src/AnimalTracker/AnimalTracker.csproj

COPY ./src/AnimalTracker/ ./src/AnimalTracker/
RUN npm run build:css --prefix /src
RUN dotnet publish ./src/AnimalTracker/AnimalTracker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

# App files
COPY --from=build /app/publish/ ./

# Persistent data dirs (mount these as volumes)
RUN mkdir -p /app/Data /app/App_Data/photos /app/App_Data/backgrounds /app/App_Data/keys

ENTRYPOINT ["dotnet", "AnimalTracker.dll"]

