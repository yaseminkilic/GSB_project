# Web performansı, güvenlik ve testler

Bu geliştirme, iş ilanındaki “Web teknolojileri performans, güvenlik ve testleri konusunda tecrübe sahibi olmak” maddesini uygulamalı olarak gösterir.

## Yapılan geliştirmeler ve kanıtları

| Alan | Geliştirme | Doğrulama |
| --- | --- | --- |
| Trafik kontrolü | IP başına 60 saniyede 100 istek; kuyruk yok; aşımda 429, Retry-After | İki isteklik test kotasının üçüncü isteği reddetmesi; sahte X-Forwarded-For ile atlatılamaması |
| Hata güvenliği | Beklenmeyen hatalarda genel başlık, 500 ProblemDetails ve traceId; exception metni gönderilmez | Moq ile gizli bilgi içeren hata oluşturulur, yanıtta bulunmadığı doğrulanır |
| Tarayıcı güvenliği | nosniff, DENY, no-referrer, API için CSP | Gerçek HTTP yanıt başlıkları test edilir |
| Kişisel veri | API yanıtlarında Cache-Control: no-store | Hata ve oran sınırı yanıtlarında da kontrol edilir |
| HTTPS | Development dışında HSTS; Kestrel Server başlığı kapalı | HSTS localhost dışındaki HTTPS test adresinde kontrol edilir; Kestrel ayarı TestServer tarafından test edilmez |
| Ortam ayrımı | OpenAPI ve Scalar yalnız Development ortamında | Diğer ortamda 404 doğrulanır |
| Girdi kontrolü | E-posta 150, telefon 15 karakter; bozuk JSON için 400 | Uzunluk sınırları ve veri eklenmediği test edilir |
| Veritabanı maliyeti | Hatalı e-postada benzersizlik sorgusuna geçmeden doğrulama durur | Strict Moq, veritabanına hiç erişilmediğini kanıtlar |
| Okuma maliyeti | Var olan AsNoTracking davranışı korunur | Sorgu sonrası ChangeTracker boş kalır |
| Bağımlılık güvenliği | Test SQLitePCLRaw bundle sürümü 2.1.14'e yükseltildi | Release testleri ve transitif NuGet açık denetimi |

Varsayılan istek sınırı appsettings.json içindeki `RateLimiting:PermitLimit` ve `RateLimiting:WindowSeconds` ile ayarlanır. Üretimde iş yüküne göre boyutlandırılmalıdır. Limiter süreç belleğindedir; çok örnekli dağıtımda ortak kota sağlamaz. Ters vekil arkasında istemci IP'si için yalnızca güvenilen vekiller tanımlanmalıdır; rastgele Forwarded/X-Forwarded-For başlıklarını güvenilir kabul etmiyoruz. NAT arkasındaki istemciler aynı kotayı paylaşır. Bu kontrol tek başına DDoS koruması değildir.

## Çalıştırılan kontroller

```powershell
dotnet test GSB_project.slnx --configuration Release --logger trx --collect:"XPlat Code Coverage" --results-directory TestResults
dotnet list GSB_project.slnx package --include-transitive --vulnerable --format json --no-restore
```

15 Kasım 2026 doğrulaması: **70 test geçti, 0 başarısız**. Transitif NuGet denetiminde bilinen açık bildirilmedi. Bu sonuç yeni açıkların bulunamayacağı anlamına gelmez; CI aynı kontrolü tekrarlar.

`WebQualityTests` içindeki performans smoke testi ısınma isteğinden sonra 50 ardışık GET çalıştırır. Yerel Release ölçümü: p50 **0,77 ms**, p95 **2,10 ms**, en yüksek **4,12 ms**. Sonuçlar TRX StdOut alanındadır. Ölçüm süreç içi TestServer + bellekte SQLite içindir; ağ, TLS, SQL Server, üretim verisi ve eşzamanlı yük maliyetini içermez. Önce/sonra karşılaştırması yapılmadı. Makine hızına bağlı bir test hatası üretmemek için bu smoke testte gecikme eşiği yoktur.

## JWT ve çalıştırılan k6 yük testi

JWT doğrulaması ve Reader/Manager/Admin rol politikaları uygulamaya eklendi. Katılımcı ve kayıt uçları artık anonim erişime kapalıdır. Üretimde eksik kimlik sağlayıcısı yapılandırmasında başlangıç engellenir. Development ortamı imzalı `dotnet user-jwts` token ve user-secrets anahtarlarını destekler.

k6 2.4.0 proje içine kuruldu ve yerel HTTP üzerinde gerçek JWT doğrulamasıyla 5 kullanıcılı, 30 saniyelik test çalıştırıldı: **1.435 istek, %0 hata, p95 3,47 ms**. Eşiklerin tamamı geçti. Son ölçüm ve kurulum/çalıştırma adımları [Kimlik doğrulama ve yük testi rehberindedir](AUTHENTICATION.md).

Yerel yük testi SQLite kullanır; SQL Server, TLS ve üretim kapasitesi hakkında sonuç çıkarılmaz. Gerçek kimlik sağlayıcısı adresi henüz yapılandırılmadı. Katılımcı hesabına bağlı sahiplik kuralları ve SQL Server eşzamanlılık kontrolleri ayrıca geliştirilmelidir. Bu çalışma bir sızma testi değildir.

## Teknik kaynaklar

- [Microsoft: ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0)
- [Microsoft: hata yönetimi](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling?view=aspnetcore-10.0)
- [SQLite bağımlılığının eski sürümündeki açık](https://github.com/advisories/GHSA-2m69-gcr7-jv3q)
- [Kullanılan SQLitePCLRaw bundle 2.1.14](https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/2.1.14)



