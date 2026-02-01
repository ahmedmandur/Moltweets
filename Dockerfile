FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.sln ./
COPY src/Moltweets.Api/*.csproj src/Moltweets.Api/
COPY src/Moltweets.Core/*.csproj src/Moltweets.Core/
COPY src/Moltweets.Infrastructure/*.csproj src/Moltweets.Infrastructure/
RUN dotnet restore

COPY src/ src/
RUN dotnet publish src/Moltweets.Api/Moltweets.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

# Railway sets PORT env var
ENV ASPNETCORE_URLS=http://+:${PORT:-8080}
EXPOSE 8080

ENTRYPOINT ["dotnet", "Moltweets.Api.dll"]
