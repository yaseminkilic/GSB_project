# Kimlik doğrulama ve yetkilendirme

API, güvenilen bir OIDC kimlik sağlayıcısının ürettiği JWT erişim token'larını doğrular. Kullanıcı girişi, parola, MFA ve token üretimi kimlik sağlayıcısında yapılır. Uygulamada herkese açık kayıt veya rol atama ucu yoktur.

## Erişim kuralları

| İşlem | Reader | Manager | Admin | Anonim |
| --- | --- | --- | --- | --- |
| GET /api/events/{id} | Evet | Evet | Evet | 401 |
| POST /api/events | 403 | Evet | Evet | 401 |
| GET/POST /api/participants | 403 | Evet | Evet | 401 |
| GET/POST /api/registrations | 403 | Evet | Evet | 401 |

GET katılımcı ve kayıt rotaları `{id}` ile kullanılır. Bilinmeyen veya eksik rol, etkinlik okuması dahil 403 alır. Token yoksa veya geçersizse 401 döner. Varsayılan fallback politikası yeni eklenen uçlarda da kimlik doğrulamayı zorunlu tutar. Mevcut API, yönetim paneli servisi olarak ele alınmıştır: Manager/Admin tüm katılımcı ve kayıtları yönetir. Katılımcının kendi kaydını yönetmesi için ayrıca hesap-katılımcı eşleştirmesi ve sahiplik politikası geliştirilmelidir.

## Yerel Development ortamı

Kimlik sağlayıcısı henüz belirlenmediğinde Development ortamında .NET SDK'nın `dotnet user-jwts` aracı kullanılabilir. İlk kurulumda depo kökünden:

```powershell
$project = './GSB_project/src/GSB_project.WebApi/GSB_project.WebApi.csproj'
$env:API_TOKEN = dotnet user-jwts create --project $project --audience gsb-api --role Manager --valid-for 8h --output token
dotnet run --project $project
```

Bu çalışma alanında ilk kurulum yapıldı; doğrudan `dotnet run --project .\GSB_project\src\GSB_project.WebApi\GSB_project.WebApi.csproj` komutu kullanılabilir. `launchSettings.json` Development ortamını seçer. SDK, imza anahtarını kaynak kodunun dışındaki user-secrets alanına yazar; appsettings.Development.json yalnız issuer/audience bilgisi içerir. Token veya anahtarı depoya eklemeyin.

Token süresi dolunca `dotnet user-jwts create` komutunu yeniden çalıştırın. Token üretimi API sunucusunun başlaması için her seferinde gerekmez; imza anahtarının user-secrets alanında bulunması yeterlidir. Başka bir geliştirici kendi bilgisayarında ilk kurulum komutunu çalıştırmalıdır.

Development ortamında Scalar ve OpenAPI anonim görüntülenebilir; API uçlarında token ve roller zorunludur. Yerel doğrulama yalnız `dotnet-user-jwts` issuer'ına, yapılandırılmış API audience değerine ve en az 256 bit imza anahtarına izin verir. Authority verilirse OIDC doğrulaması önceliklidir. Yerel anahtarlar Production/Staging ortamlarında kullanılmaz.

Doğrulama: 70 otomatik test geçti. Gerçek API sürecinde başlangıç ve OpenAPI 200, anonim API isteği 401, imzalı Manager token'ıyla geçersiz etkinlik isteği 400 olarak kontrol edildi; bu kontrol veritabanına veri yazmadı.

Microsoft referansı: [dotnet user-jwts ile yerel token üretimi](https://learn.microsoft.com/aspnet/core/security/authentication/jwt-authn).
## Üretim yapılandırması

Gerçek sağlayıcınızın issuer/authority adresi ve API audience değerini ortam değişkenleri veya dağıtım yapılandırmasıyla girin:

```powershell
$env:Authentication__Authority = 'https://YOUR-IDENTITY-PROVIDER/YOUR-ISSUER'
$env:Authentication__Audience = 'gsb-api'
$env:Authentication__RoleClaimType = 'role'
dotnet run --project GSB_project/src/GSB_project.WebApi
```

Authority, sağlayıcının HTTPS OIDC discovery adresinin tabanıdır; imza anahtarları sağlayıcının metadata/JWKS verisinden alınır. Audience, alınan erişim token'ının `aud` alanıyla aynı olmalıdır. `role` claim'i Reader, Manager veya Admin değerlerinden birini taşımalıdır. Sağlayıcınız `roles` kullanıyorsa RoleClaimType değerini `roles` yapın. İç içe rol nesneleri için sağlayıcı tarafında düz claim eşlemesi gerekir. Rol atamalarını sadece güvenilir yöneticiler yapmalıdır.

JWT imzası, issuer, audience, son kullanım zamanı ve imzalı token şartı denetlenir; saat toleransı 30 saniyedir. Ayrıntılı doğrulama hataları istemciye açıklanmaz. Authority yapılandırılmadan üretim uygulaması başlangıçta hata verir; anonim erişime dönüşmez. Depoda gerçek sağlayıcı adresi, parola veya üretim imza anahtarı tutulmaz.

İstek örneği (token'ı sağlayıcınızdan alın):

```powershell
$headers = @{ Authorization = "Bearer $env:API_TOKEN" }
Invoke-RestMethod -Uri 'https://localhost:7043/api/events/1' -Headers $headers
```

Kimlik sağlayıcısı henüz belirlenmediğinden üretim sağlayıcısıyla uçtan uca giriş akışı doğrulanmadı. Yerel token üreticisi yalnız test projelerine derlenir; üretim WebApi projesine dahil değildir.

## Testler

`AuthenticationTests.cs` anonim erişim, yanlış imza/issuer/audience, süresi dolmuş ve imzasız token, eksik/bilinmeyen rol, Reader kısıtları ve yönetim rolleriyle oluşturmayı doğrular. Testler sahte bir authentication handler ile erişimi atlamaz; gerçek imzalı JWT'ler JwtBearer tarafından doğrulanır. Eksik güvenilen issuer yapılandırmasının başlangıcı engellediği de test edilir.

```powershell
dotnet test GSB_project.slnx --configuration Release --filter FullyQualifiedName~AuthenticationTests
```

## Yerel k6 kurulumu ve çalıştırma

Depo kökünden Windows PowerShell ile:

```powershell
./tools/Install-K6.ps1
./tools/Run-LoadTest.ps1
```

Kurulum k6 2.4.0 resmi Windows ZIP paketini indirir, resmi SHA256 dosyasıyla doğrular ve `.tools/k6` altına açar. Sistem PATH veya yönetici kurulumu gerekmez.

Çalıştırıcı, ayrı PerformanceHost projesini derler ve yalnız IPv4 loopback üzerinde rastgele bir portta başlatır. Aynı üretim controller, middleware ve JWT doğrulama hattı kullanılır. SQL Server yerine ayrı, geçici SQLite dosyası ve tek etkinlik kaydı bulunur. Her süreç için rastgele test imza anahtarı üretilir; Reader token'ı komut satırına yazılmadan k6 sürecine ortam değişkeniyle aktarılır. Kota yalnız test sunucusunda yükseltilir. Test bitince sunucu kapatılır; token oturum dosyası ve veritabanı dosyaları temizlenir, önceki ortam değişkenleri geri yüklenir. HTTP yalnız loopback test ortamında kullanılır; bu ölçüm TLS performansını içermez.

Bu çalıştırıcı Windows içindir. Farklı işletim sistemlerinde k6 kurup aynı `events.k6.js` dosyası BASE_URL, EVENT_ID ve API_TOKEN ortam değişkenleriyle kullanılabilir. Üretim bearer token'larını komut satırı argümanı olarak vermeyin.

Son doğrulamada toplam **70 otomatik test geçti**; doğrudan ve dolaylı NuGet bağımlılıklarında bilinen açık bildirilmedi.

## Ölçüm sonucu — 15 Kasım 2026

5 eşzamanlı kullanıcı, 30 saniye, her döngüde 100 ms bekleme:

| Ölçüm | Sonuç | Eşik |
| --- | --- | --- |
| HTTP istek sayısı | 1.435 | — |
| İstek hızı | 47,69/sn | — |
| HTTP hata oranı | %0 | <%1 |
| Yanıt süresi p95 | 3,47 ms | <500 ms |
| Ortalama yanıt süresi | 4,13 ms | — |
| En yüksek yanıt süresi | 670,07 ms | — |
| Kontrol başarısı | %100 (4.305/4.305) | >%99 |

Bütün k6 eşikleri geçti. Ham sonuçlar `TestResults/load-c725c8ae1c094e4d9d8aeaea307c1398/k6-summary.json` altında bulunur. Yerel HTTP + JWT + SQLite okuma testi, üretim SQL Server verisi, yazma yoğunluğu, TLS, dağıtık kota veya kapasite sınırı ölçümü değildir. İstek hızına 100 ms kullanıcı beklemesi dahildir; maksimum kapasite sonucu olarak kullanılmamalıdır.

Kaynaklar: [Microsoft JWT Bearer yapılandırması](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0), [Grafana k6 kurulumu](https://grafana.com/docs/k6/latest/set-up/install-k6/).


