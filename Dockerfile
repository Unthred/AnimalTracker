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
RUN dotnet publish ./src/AnimalTracker/AnimalTracker.csproj -c Release -o /app/publish

# Fail fast if publish output is missing Blazor runtime assets.
RUN test -f /app/publish/wwwroot/_framework/blazor.server.js
RUN test -f /app/publish/wwwroot/_framework/blazor.web.js
RUN grep -q "blazor.server.js" /app/publish/AnimalTracker.staticwebassets.endpoints.json
RUN grep -q "blazor.web.js" /app/publish/AnimalTracker.staticwebassets.endpoints.json

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

# App files
COPY --from=build /app/publish/ ./
# Belt-and-braces: explicitly copy runtime framework scripts too.
COPY --from=build /app/publish/wwwroot/_framework/ /app/wwwroot/_framework/

# Fail if final image is missing framework runtime scripts.
RUN test -f /app/wwwroot/_framework/blazor.server.js
RUN test -f /app/wwwroot/_framework/blazor.web.js

# Persistent data dirs (mount these as volumes)
RUN mkdir -p /app/Data /app/App_Data/photos /app/App_Data/backgrounds /app/App_Data/keys

ENTRYPOINT ["dotnet", "AnimalTracker.dll"]

