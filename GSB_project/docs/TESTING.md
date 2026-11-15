# Gereksinim doğrulama ve test süreci

İş ilanındaki test süreçleri, teknikleri ve otomasyon araçları maddesi için bu projede çalıştırılabilir örnekler bulunur.

## Gereksinim ve test eşleştirmesi

| Gereksinim | Teknik | Otomatik test |
| --- | --- | --- |
| Etkinlik adı zorunlu, en fazla 150 karakter | Eşdeğer bölümlendirme, sınır değer (150/151) | EventValidationTests |
| Kapasite pozitif olmalı | Sınır değer (-1/0/1) | EventValidationTests |
| Başlangıç tarihi doğrulama anından ileride olmalı | Zaman sınırı, regresyon | Date_is_compared_with_validation_time |
| Geçersiz istek kaydedilmemeli | Negatif test, MediatR doğrulama zinciri | Invalid_event…, Invalid_participant… |
| Katılımcı adı ve geçerli e-posta zorunlu | Eşdeğer bölümlendirme | Invalid_participant… |
| Aynı e-posta tekrar kaydedilmemeli | İş kuralı, durum geçişi | Duplicate_email_is_rejected |
| Kayıt için etkinlik ve katılımcı mevcut olmalı | Negatif referans kontrolü | Unknown_registration_references… |
| Aynı etkinlik-katılımcı kaydı tekrarlanmamalı | İş kuralı, regresyon | Duplicate_registration_is_rejected |
| Oluşturma 201 ve Location; sorgulama 200; eksik kaynak 404 | HTTP sözleşmesi | Complete_registration_flow…, Missing_resource… |
| Kayıt ilişkileri, UTC zamanı ve varsayılan iptal durumu saklanmalı | Entegrasyon, kalıcılık kontrolü | Complete_registration_flow… |

## Araçlar ve çalıştırma

.NET 10 SDK, xUnit, ASP.NET Core WebApplicationFactory/TestServer, EF Core SQLite ve Coverlet kullanılır. API testleri HTTP isteğinden controller, MediatR, validator ve ilişkisel veritabanına kadar çalışır. Her test kendi bellekte SQLite veritabanını kullanır; SQL Server kurulumu gerekmez.

Depo kökünden:

```powershell
dotnet test GSB_project.slnx --logger trx --collect:"XPlat Code Coverage" --results-directory TestResults
```

TRX sonucu ve `coverage.cobertura.xml` dosyası TestResults altında oluşur. `.github/workflows/tests.yml`, GitHub'a taşındığında push ve pull request üzerinde aynı kontrolü yapar ve sonuçları artifact olarak saklar. Yerel dizinde Git deposu bulunmadığından uzaktaki workflow çalışması doğrulanmadı.

Süreç: gereksinimi tanımla → olumlu/olumsuz/sınır senaryolarını seç → testi çalıştır → hatayı düzelt → regresyon testini koru. Kabul ölçütü tüm otomatik testlerin geçmesidir; kapsam raporu eksik senaryoları değerlendirmek içindir, tek başına kalite garantisi değildir.

## Kapsam sınırları ve açık noktalar

SQLite testleri SQL Server collation, uzunluk kısıtları, eşzamanlı kayıt yarışları, transaction ve üretim şema davranışını kanıtlamaz. SQL Server üzerinde ayrıca entegrasyon testi gerekir. Kapasite dolduğunda kayıt engelleme, geçmiş etkinliğe kayıt, iptal ve RegisteredCount güncelleme kuralları mevcut uygulamada tanımlı/uygulanmış değildir; kabul şartları netleştirilerek ayrı geliştirilmelidir. Güvenlik ve performans kontrolleri ile yük testi kapsamı [Web kalite rehberinde](WEB_QUALITY.md) açıklanır.

Testlerde açığa çıkan iki hata düzeltildi: Events/Registrations DbSet erişimindeki NotImplementedException ve gelecekteki tarih kontrolünün validator oluşturulma anını kullanması.

## Moq ile birim testleri

`CommandHandlerTests.cs`, `IApplicationDbContext` ve `DbSet<T>` bağımlılıklarını Moq ile taklit eder. Arrange–Act–Assert düzeninde alanların aktarımı, kaydetme sonrası dönen kimlik, `Times.Once` ile tek kaydetme, cancellation token aktarımı ve kaydetme hatasının iletilmesi doğrulanır. Bu testler veritabanı açmaz. Gerçek EF sorguları ve HTTP davranışı mevcut SQLite API entegrasyon testleriyle doğrulanır.

Yalnızca Moq testlerini çalıştırmak için:

```powershell
dotnet test GSB_project.slnx --filter FullyQualifiedName~CommandHandlerTests
```


