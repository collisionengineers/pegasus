FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore ./CollisionBrain.slnx --locked-mode
RUN dotnet publish ./src/CollisionBrain/CollisionBrain.csproj --configuration Release --no-restore --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ENV NODE_ENV=production
WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /app/.data/objects && chown -R $APP_UID:$APP_UID /app
USER $APP_UID
EXPOSE 3000
ENTRYPOINT ["dotnet", "CollisionBrain.dll"]
