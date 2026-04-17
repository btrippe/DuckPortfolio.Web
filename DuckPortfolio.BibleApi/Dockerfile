# build
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY DuckPortfolio.BibleApi/DuckPortfolio.BibleApi.csproj DuckPortfolio.BibleApi/
RUN dotnet restore DuckPortfolio.BibleApi/DuckPortfolio.BibleApi.csproj
COPY DuckPortfolio.BibleApi/ DuckPortfolio.BibleApi/
RUN dotnet publish DuckPortfolio.BibleApi/DuckPortfolio.BibleApi.csproj -c Release -o /app/publish

# run
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet","DuckPortfolio.BibleApi.dll"]
