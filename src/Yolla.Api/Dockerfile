# Yolla API görüntüsü.
#
# Depo kökünden derlenir:
#   docker build -f src/Yolla.Api/Dockerfile -t yolla-api .
#
# Çok aşamalı: derleme araçları son görüntüye girmez, boyut ve saldırı yüzeyi küçülür.

# --- Derleme ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyaları kopyalanıyor: kaynak kod değiştiğinde paket geri
# yükleme katmanı önbellekten geliyor ve derleme belirgin şekilde hızlanıyor.
COPY Yolla.slnx ./
COPY src/Yolla.Domain/Yolla.Domain.csproj              src/Yolla.Domain/
COPY src/Yolla.Application/Yolla.Application.csproj    src/Yolla.Application/
COPY src/Yolla.Infrastructure/Yolla.Infrastructure.csproj src/Yolla.Infrastructure/
COPY src/Yolla.Api/Yolla.Api.csproj                    src/Yolla.Api/
COPY src/Yolla.Harvester/Yolla.Harvester.csproj        src/Yolla.Harvester/

RUN dotnet restore src/Yolla.Api/Yolla.Api.csproj

COPY src/ src/

RUN dotnet publish src/Yolla.Api/Yolla.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# --- Çalıştırma ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Sağlık kontrolü için curl; aspnet görüntüsünde bulunmuyor
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Kök olmayan kullanıcı: bir açık oluşursa saldırganın yetkisi sınırlı kalır
RUN useradd --uid 5001 --create-home --shell /usr/sbin/nologin yolla
USER 5001

COPY --from=build --chown=5001:5001 /app/publish .

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=1

EXPOSE 8080

# Sağlık kontrolü: konteyner yöneticisi hazır olmayan örneğe trafik göndermesin.
# start-period uzun tutuldu: ilk açılışta veritabanı bağlantısı kuruluyor.
HEALTHCHECK --interval=30s --timeout=5s --start-period=45s --retries=3 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Yolla.Api.dll"]
