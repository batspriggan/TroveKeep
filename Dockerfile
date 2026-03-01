# Stage 1: Build the Vue UI
FROM node:22-alpine AS ui-builder
WORKDIR /app/ui
COPY ui/package*.json ./
RUN npm ci
COPY ui/ ./
RUN npm run build

# Stage 2: Build the .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-builder
WORKDIR /app
COPY src/ ./src/
RUN dotnet publish src/TroveKeep.Api/TroveKeep.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-self-contained

# Stage 3: Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=api-builder /app/publish ./
COPY --from=ui-builder /app/ui/dist ./wwwroot

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TroveKeep.Api.dll"]
