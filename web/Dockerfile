# Multi-stage build for the AERai web app. Used by docker-compose (--profile app); Azure App
# Service deploys the published output directly, so this image is for local/container hosting.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, using only project files, so the restore layer is cached across code changes.
COPY global.json Directory.Build.props .editorconfig AERai.Web.slnx ./
COPY src/AERai.Web.Domain/AERai.Web.Domain.csproj src/AERai.Web.Domain/
COPY src/AERai.Web.Application/AERai.Web.Application.csproj src/AERai.Web.Application/
COPY src/AERai.Web.Infrastructure/AERai.Web.Infrastructure.csproj src/AERai.Web.Infrastructure/
COPY src/AERai.Web.UI/AERai.Web.UI.csproj src/AERai.Web.UI/
RUN dotnet restore src/AERai.Web.UI/AERai.Web.UI.csproj

COPY src/ src/
RUN dotnet publish src/AERai.Web.UI/AERai.Web.UI.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# The official image ships a non-root "app" user; never run the web process as root.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "AERai.Web.UI.dll"]
