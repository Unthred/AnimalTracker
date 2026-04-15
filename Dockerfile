FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ./src/AnimalTracker/ ./src/AnimalTracker/
WORKDIR /src/src/AnimalTracker
RUN dotnet restore
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

# App files
COPY --from=build /app/publish/ ./

# Persistent data dirs (mount these as volumes)
RUN mkdir -p /app/Data /app/App_Data/photos /app/App_Data/backgrounds /app/App_Data/keys

ENTRYPOINT ["dotnet", "AnimalTracker.dll"]

