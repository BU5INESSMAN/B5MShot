FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src
COPY B5MShot.Server.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app
RUN addgroup -g 10001 -S b5mshot && adduser -u 10001 -S b5mshot -G b5mshot \
    && mkdir -p /data/screenshots \
    && chown -R b5mshot:b5mshot /data /app
USER b5mshot
COPY --from=build --chown=b5mshot:b5mshot /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "B5MShot.Server.dll"]
