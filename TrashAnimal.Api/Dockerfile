FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY TrashAnimal/TrashAnimal.csproj TrashAnimal/
COPY TrashAnimal.Api/TrashAnimal.Api.csproj TrashAnimal.Api/
RUN dotnet restore TrashAnimal.Api/TrashAnimal.Api.csproj
COPY TrashAnimal/ TrashAnimal/
COPY TrashAnimal.Api/ TrashAnimal.Api/
RUN dotnet publish TrashAnimal.Api/TrashAnimal.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "TrashAnimal.Api.dll"]
