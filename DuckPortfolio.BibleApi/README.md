# DuckPortfolio Bible API

Small ASP.NET Core Minimal API wrapper around the YouVersion Platform API.

## Local Run

Set the YouVersion app key before starting the API:

```powershell
$env:YouVersion__AppKey="YOUR_YOUVERSION_APP_KEY"
dotnet run --project DuckPortfolio.BibleApi
```

Open Swagger:

```text
http://localhost:5007/swagger
```

## Confirmed Test Values

BSB metadata:

```http
GET /api/bibles/3034
```

John 3:16:

```http
GET /api/passages?bibleId=3034&reference=JHN.3.16&format=Text&includeHeadings=false&includeNotes=false
```

John 3:

```http
GET /api/passages?bibleId=3034&reference=JHN.3&format=Text&includeHeadings=false&includeNotes=false
```

## Azure Deployment

Resource names:

```text
Resource group: RubberDuckWebApp_group
Container Apps environment: cae-portfolio
ACR: acrduckportfolio15334
Container app: duckportfolio-bible-api
Public URL: https://duckportfolio-bible-api.ashyrock-6e3ca991.eastus2.azurecontainerapps.io
```

Build and push the image:

```powershell
az acr build `
  --registry acrduckportfolio15334 `
  --resource-group RubberDuckWebApp_group `
  --image duckportfolio-bible-api:latest `
  --file DuckPortfolio.BibleApi/Dockerfile .
```

Update the running Container App after future builds:

```powershell
az containerapp update `
  --name duckportfolio-bible-api `
  --resource-group RubberDuckWebApp_group `
  --image acrduckportfolio15334.azurecr.io/duckportfolio-bible-api:latest
```

Set the YouVersion app key as an Azure Container Apps secret:

```powershell
az containerapp secret set `
  --name duckportfolio-bible-api `
  --resource-group RubberDuckWebApp_group `
  --secrets youversion-app-key=YOUR_YOUVERSION_APP_KEY
```

Expose the secret to the app as configuration:

```powershell
az containerapp update `
  --name duckportfolio-bible-api `
  --resource-group RubberDuckWebApp_group `
  --set-env-vars YouVersion__AppKey=secretref:youversion-app-key
```

Check logs:

```powershell
az containerapp logs show `
  --name duckportfolio-bible-api `
  --resource-group RubberDuckWebApp_group `
  --tail 40
```
