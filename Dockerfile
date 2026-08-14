# ============================================================================
# 1. AŞAMA — Derleme
# .NET SDK imajı derleyici, NuGet ve tüm araçları içerir; ~800 MB'dır.
# Bu ağırlığın son imaja taşınmaması için derleme ayrı bir aşamada yapılır.
# ============================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce SADECE proje dosyası kopyalanır ve bağımlılıklar indirilir.
# Sebebi Docker'ın katman önbelleği: kaynak kod değiştiğinde bu katman
# değişmediği için NuGet paketleri yeniden indirilmez, derleme hızlanır.
# Tüm kod baştan kopyalansaydı, tek satırlık bir değişiklik bile her seferinde
# paketlerin yeniden indirilmesine yol açardı.
COPY FileTrackingAndProcessingServices.csproj ./
RUN dotnet restore FileTrackingAndProcessingServices.csproj

# Şimdi geri kalan kaynak kod kopyalanır ve yayına hazır çıktı üretilir.
COPY . ./
RUN dotnet publish FileTrackingAndProcessingServices.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ============================================================================
# 2. AŞAMA — Çalıştırma
# ASP.NET runtime imajı derleyici içermez, yalnızca uygulamayı çalıştıracak
# kadarını taşır. Son imaj bu aşamadan oluşur.
# ============================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Derleme aşamasından yalnızca yayın çıktısı alınır; kaynak kod, NuGet önbelleği
# ve SDK son imaja hiç girmez.
COPY --from=build /app/publish ./

# İki ayrı klasör:
#   /app/data   → SQLite veritabanı burada durur
#   /data/watch → izlenen klasör; host'tan buraya bağlama (volume) yapılır
# APP_UID, .NET imajlarının tanımladığı root olmayan kullanıcının kimliği.
# Klasörlerin sahipliği ona verilmezse uygulama yazma izni bulamaz.
RUN mkdir -p /app/data /data/watch \
    && chown -R $APP_UID:$APP_UID /app/data /data/watch

# --- Yapılandırma ---
# appsettings.json'daki değerler container'a uymuyor; ortam değişkenleriyle
# eziliyor. ASP.NET Core'da iç içe ayarlar çift alt çizgi ile yazılır:
# "WatchSettings:FolderPath" -> WatchSettings__FolderPath

# İzlenen klasör. appsettings.json'daki değer bir Windows yolu
# (C:\Users\...) ve Linux container içinde hiçbir anlam ifade etmez.
ENV WatchSettings__FolderPath=/data/watch

# Veritabanının yeri. Varsayılan "Data Source=dosyatakip.db" çalışma dizinine
# göreli olduğu için container silindiğinde veri de giderdi. /app/data'ya
# alınıp oraya volume bağlanabilir hale getiriliyor.
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/dosyatakip.db"

# Swagger arayüzü Program.cs'te yalnızca Development ortamında açılıyor.
# Container varsayılan olarak Production'da başlar ve Swagger görünmezdi.
# Bu bir ödev/demo tercihi: gerçek bir dağıtımda Production'da kalınır ve
# Swagger ya kapatılır ya da kimlik doğrulamasının arkasına alınır.
ENV ASPNETCORE_ENVIRONMENT=Development

# .NET 8'den beri container imajları 8080 portunu dinler (eskiden 80'di).
EXPOSE 8080

# Uygulama root olarak çalışmaz: container'da bir açık bulunsa bile saldırgan
# doğrudan root yetkisi elde edemez.
USER $APP_UID

ENTRYPOINT ["dotnet", "FileTrackingAndProcessingServices.dll"]
