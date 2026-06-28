# syntax=docker/dockerfile:1

# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Props centrales (versiones NuGet) + csproj primero para cachear el restore
COPY Directory.Build.props ./
COPY src/BigSchool.Domain/BigSchool.Domain.csproj                 src/BigSchool.Domain/
COPY src/BigSchool.Application/BigSchool.Application.csproj         src/BigSchool.Application/
COPY src/BigSchool.Infrastructure/BigSchool.Infrastructure.csproj  src/BigSchool.Infrastructure/
COPY src/BigSchool.WebApi/BigSchool.WebApi.csproj                  src/BigSchool.WebApi/
RUN dotnet restore src/BigSchool.WebApi/BigSchool.WebApi.csproj

# Resto del código y publish
COPY src/ src/
RUN dotnet publish src/BigSchool.WebApi/BigSchool.WebApi.csproj \
    -c Release -o /app/publish --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish ./
ENV ASPNETCORE_URLS=http://+:8081
EXPOSE 8081
HEALTHCHECK --interval=10s --timeout=5s --retries=5 --start-period=20s \
    CMD curl -f http://localhost:8081/health || exit 1
ENTRYPOINT ["dotnet", "BigSchool.WebApi.dll"]
