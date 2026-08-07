# ----------------------------------------------------------------------------
# Collision Renderer — cloud API image.
# The runtime stage uses the official Playwright .NET image, which ships the
# matching Chromium build + all native dependencies. We add Liberation fonts so
# the documents' Arial body copy renders with identical metrics on Linux.
# ----------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
WORKDIR /src
COPY workspaces/report-renderer/ workspaces/report-renderer/
COPY design/ design/
WORKDIR /src/workspaces/report-renderer
RUN dotnet restore src/CollisionRenderer.Api/CollisionRenderer.Api.csproj \
 && dotnet publish src/CollisionRenderer.Api/CollisionRenderer.Api.csproj -c Release -o /app /p:UseAppHost=false

FROM mcr.microsoft.com/playwright/dotnet:v1.61.0-noble AS final
WORKDIR /app

# Arial-metric (Liberation/Arimo) + DejaVu fallback for the document body face.
RUN apt-get update \
 && apt-get install -y --no-install-recommends fonts-liberation fonts-dejavu-core \
 && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .

# The Playwright image pre-installs browsers here.
ENV PLAYWRIGHT_BROWSERS_PATH=/ms-playwright
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
EXPOSE 8080

ENTRYPOINT ["dotnet", "CollisionRenderer.Api.dll"]
