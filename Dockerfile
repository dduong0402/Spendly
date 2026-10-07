# syntax=docker/dockerfile:1

# ---- Build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Chép file project trước để tận dụng cache của bước restore.
COPY Spendly.sln ./
COPY src/Spendly.Web/Spendly.Web.csproj src/Spendly.Web/
COPY tests/Spendly.Tests/Spendly.Tests.csproj tests/Spendly.Tests/
RUN dotnet restore Spendly.sln

COPY src/ src/
RUN dotnet publish src/Spendly.Web/Spendly.Web.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---- Runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Image aspnet:8.0 chạy bằng user không phải root (app) và lắng nghe cổng 8080.
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DataProtection__KeysPath=/app/keys

COPY --from=build /app/publish .

# Thư mục lưu khóa DataProtection; hãy mount volume vào đây để khóa không mất khi container tạo lại.
RUN mkdir -p /app/keys && chown -R app:app /app/keys
VOLUME ["/app/keys"]

USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "Spendly.Web.dll"]
