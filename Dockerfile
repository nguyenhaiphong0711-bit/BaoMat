FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY LMS.csproj ./
RUN dotnet restore LMS.csproj

COPY . ./
RUN dotnet publish LMS.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 10000

CMD ["sh", "-c", "dotnet LMS.dll --urls http://0.0.0.0:${PORT:-10000}"]
