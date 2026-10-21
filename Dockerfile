FROM node:24-bookworm-slim AS frontend
WORKDIR /client
COPY ClientApp/package.json ClientApp/package-lock.json ./
RUN npm ci --no-fund
COPY ClientApp/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY LMS.csproj ./
RUN dotnet restore LMS.csproj
COPY . ./
COPY --from=frontend /client/dist ./ClientApp/dist
RUN dotnet publish LMS.csproj -c Release -o /app/publish /p:UseAppHost=false /p:SkipFrontendBuild=true

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000
CMD ["sh", "-c", "dotnet LMS.dll --urls http://0.0.0.0:${PORT:-10000}"]
