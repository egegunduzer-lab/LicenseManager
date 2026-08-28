# LicenseManager

LicenseManager; birden fazla şirket ve yazılım projesi için lisans oluşturma, imzalama, yenileme, engelleme ve çevrimdışı doğrulama işlemlerini yöneten örnek bir lisans altyapısıdır.

Sistem şu sorulara cevap verir:

- Hangi şirket hangi projeyi kullanabilir?
- Lisans hangi modülleri kapsar?
- Müşterinin gerçek kullanım hakkı ne zaman biter?
- İstemcideki lisans dosyası değiştirilmiş mi?
- Lisans hangi anahtar sürümüyle imzalanmış?
- Lisans yenilenebilir, engellenebilir veya yeniden açılabilir mi?

Güncel teknoloji yığını:

| Katman | Teknoloji |
|---|---|
| API | .NET 9, ASP.NET Core Web API |
| Veri erişimi | Entity Framework Core 9, Npgsql |
| Veritabanı | PostgreSQL |
| Kriptografi | SHA-256, Ed25519, AES-256-GCM, NSec.Cryptography |
| İstemci | .NET 9 `LicenseManager.Client` |
| Ortak katman | .NET 9 `LicenseManager.Core` |
| Yönetim paneli | Angular 22.1.3, standalone components, strict TypeScript |
| Test | xUnit, EF Core InMemory, 81 otomatik test |

> Güncel doğrulama durumu: **81/81 .NET testi başarılıdır.** Angular production build’i de tamamlanmaktadır; `licenses.component.css` için derlemeyi durdurmayan bir CSS budget uyarısı bulunmaktadır.

## Amaç ve örnek kullanım

Bir ERP ürününün farklı şirketlere farklı haklarla satıldığını düşünün:

```text
ABC Holding
└── Giltas ERP 1.0
    ├── Reports
    ├── Inventory
    └── ValidUntil: 31.12.2027

XYZ Ltd.
└── Giltas ERP 1.0
    ├── Reports
    └── ValidUntil: 30.06.2027
```

İki şirket aynı uygulamayı kullanır; ancak şirket kimliği, modüller ve süre bilgisi imzalı lisans tarafından ayrı ayrı sınırlandırılır. İstemci lisans dosyasındaki modülleri veya bitiş tarihini değiştirse bile Ed25519 imza doğrulaması başarısız olur.

## Mimari

```text
Angular Admin Panel
        │  X-Admin-Api-Key
        ▼
ASP.NET Core API ───────────────► PostgreSQL
        │                         Companies
        │                         Projects
        │                         ProjectKeys
        │                         Licenses
        │
        │  Canonicalize → SHA-256 → Ed25519 Sign
        ▼
Signed License { body, signature }
        │
        ▼
LicenseManager.Client
        ├── Build-time embedded public keys
        ├── Ed25519 Verify
        ├── Local cache
        └── Background renewal
```

Lisans oluşturma ve yenileme çevrimiçi yapılır. Daha önce alınmış geçerli bir lisansın doğrulanması ise istemcideki gömülü public key ve yerel cache sayesinde çevrimdışı çalışabilir.

## Klasör yapısı

```text
LicenseManager/
├── LicenseManager.slnx
├── LicenseManager.Api/
│   ├── Controllers/          # REST endpointleri
│   ├── Data/                 # ApplicationDbContext
│   ├── Migrations/           # EF Core migrations
│   └── Services/             # Lisans, imza, key ve protector servisleri
├── LicenseManager.Client/
│   ├── Configuration/        # Client ayarları ve embedded public keys
│   └── Services/             # Cache, doğrulama, API ve renewal servisleri
├── LicenseManager.Core/
│   ├── DTOs/                 # API/client ortak sözleşmeleri
│   ├── Models/               # Veri modeli
│   └── Services/             # Ortak JSON canonicalization
├── LicenseManager.Tests/     # xUnit testleri
├── LicenseManager.Admin/     # Angular 22.1.3 yönetim paneli
└── LicenseKeys.local.props   # Client build'ine gömülecek public keys
```

`LicenseManager.slnx` dört .NET projesini içerir. Angular uygulaması kendi `package.json` ve build akışıyla ayrıca yönetilir.

## Sınıflar ve Sorumlulukları

Bu bölüm yalnızca repository'de gerçekten bulunan yapıları açıklar. Kritik servislerde güvenlik akışı ayrıntılandırılmış; yalnızca veri taşıyan model ve DTO'larda ise aynı bilgiler kompakt tablolarla verilmiştir.

### `LicenseManager.Api`

#### Controllers

##### `CompaniesController`

- **Konumu:** `LicenseManager.Api/Controllers/CompaniesController.cs`
- **Görevi:** Şirketleri listeleyen, şirket detayını getiren ve yeni şirket oluşturan admin endpointlerini sunar.
- **Kim tarafından kullanılıyor:** Angular `CompaniesApiService`, Swagger veya aynı admin API sözleşmesini kullanan yönetim araçları tarafından çağrılır.
- **Hangi verilerle çalışıyor:** `ApplicationDbContext` üzerinden `Company` ve ilişkili `License` kayıtlarını okur; `CreateCompanyRequest` alır.
- **Güvenlik açısından önemli noktası:** Her action `X-Admin-Api-Key` değerini `AdminApiKeyValidator` ile doğrular. Boş isimleri reddeder, aynı isimli şirket için conflict döndürür ve yetkisiz istekte veritabanını değiştirmez.

##### `ProjectsController`

- **Konumu:** `LicenseManager.Api/Controllers/ProjectsController.cs`
- **Görevi:** Proje listeleme/detay, public key geçmişi, proje oluşturma ve iki aşamalı key rotation endpointlerini yönetir.
- **Kim tarafından kullanılıyor:** Angular `ProjectsApiService` ve admin araçları tarafından kullanılır; iş kurallarını `ProjectService` üzerinden çalıştırır.
- **Hangi verilerle çalışıyor:** `Project`, `ProjectKey`, `CreateProjectRequest`, `PreparedProjectKeyResponse` ve key version değerleriyle çalışır.
- **Güvenlik açısından önemli noktası:** Tüm endpointler admin key gerektirir. Response'larda public key gösterilebilir ancak protected private key hiçbir zaman dışarı verilmez; hazırlanan anahtar admin ayrıca aktive edene kadar pasif kalır.

##### `LicensesController`

- **Konumu:** `LicenseManager.Api/Controllers/LicensesController.cs`
- **Görevi:** Lisans listeleme/detay, oluşturma, signed license üretme, renewal ve block/unblock endpointlerini sunar.
- **Kim tarafından kullanılıyor:** Admin işlemlerinde Angular `LicensesApiService`; public `generate` ve `renew` işlemlerinde `LicenseManager.Client/LicenseApiService` tarafından çağrılır.
- **Hangi verilerle çalışıyor:** `License`, `CreateLicenseRequest`, `LicenseGenerateRequest`, `CreatedLicenseResponse` ve `SignedLicenseResponse` verilerini işler.
- **Güvenlik açısından önemli noktası:** Liste, detay, create ve block/unblock işlemleri admin key ister; yalnızca client'ın `generate` ve `renew` endpointleri public'tir. Public akışta missing, blocked ve süresi bitmiş lisanslar `Lisans doğrulanamadı.` mesajıyla maskelenir; `GET /api/licenses/{licenseKey}` minimum veri döndürür.

##### `DashboardController`

- **Konumu:** `LicenseManager.Api/Controllers/DashboardController.cs`
- **Görevi:** Şirket, proje ve lisans sayaçlarıyla son lisanslar ve yaklaşan bitişleri tek bir admin response'unda toplar.
- **Kim tarafından kullanılıyor:** Angular `DashboardApiService`, `DashboardComponent` ve login sırasında admin key'i sınayan `LoginComponent` tarafından kullanılır.
- **Hangi verilerle çalışıyor:** `Companies`, `Projects` ve `Licenses` tablolarından salt-okunur özet sorguları üretir.
- **Güvenlik açısından önemli noktası:** Endpoint admin key olmadan veri döndürmez. Lisans anahtarları API response'unda yer alsa da arayüz bunları listede maskeli gösterir; bu maskeleme sunucu yetkilendirmesinin yerine geçmez.

Controller dosyalarında bulunan basit request sınıfları:

| Sınıf | Konumu | Görevi ve kullanım | Veri / güvenlik notu |
|---|---|---|---|
| `CreateCompanyRequest` | `Controllers/CompaniesController.cs` | `CompaniesController.CreateCompany` için şirket adını taşır. | Yalnızca `Name` içerir; boş ve mükerrer değer kontrolü controller ve veritabanı kurallarıyla yapılır. |
| `CreateProjectRequest` | `Controllers/ProjectsController.cs` | `ProjectsController.CreateProject` için proje adı ve sürümünü taşır. | `Name` ve `Version` içerir; aynı kombinasyon servis ve benzersiz indeks tarafından engellenir. |

#### Data ve composition root

##### `ApplicationDbContext`

- **Konumu:** `LicenseManager.Api/Data/ApplicationDbContext.cs`
- **Görevi:** EF Core üzerinden PostgreSQL'deki `Projects`, `Companies`, `Licenses` ve `ProjectKeys` tablolarını yönetir.
- **Kim tarafından kullanılıyor:** Controller'lar, `ProjectService`, `LicenseService`, migration servisi ve testlerdeki InMemory context oluşturucuları tarafından kullanılır.
- **Hangi verilerle çalışıyor:** Dört domain modeli, ilişkiler, JSONB `Modules` alanı ve benzersiz indekslerle çalışır.
- **Güvenlik açısından önemli noktası:** License key'in, `(Project.Name, Project.Version)` ve `(ProjectId, KeyVersion)` çiftlerinin benzersizliğini DB seviyesinde uygular. Filtreli unique index ile proje başına yalnızca bir aktif `ProjectKey` bulunmasını zorunlu tutar.

##### `Program.cs`

- **Konumu:** `LicenseManager.Api/Program.cs`
- **Görevi:** API'nin composition root'udur; DbContext, controller, Swagger, lisans servisleri, private-key protection ve development CORS kayıtlarını oluşturur.
- **Kim tarafından kullanılıyor:** ASP.NET Core host başlangıcında çalışır ve bütün API bağımlılık grafiğini kurar.
- **Hangi verilerle çalışıyor:** Configuration, connection string, environment adı ve dependency-injection kayıtlarıyla çalışır.
- **Güvenlik açısından önemli noktası:** Private-key provider seçimini environment'a göre yapar ve CORS'u yalnızca Development ortamında `http://localhost:4200` için açar. Production'da gerçek `IPrivateKeyProtector` kaydı olmadan başlatma hatası oluşur.

#### Servisler

##### `ProjectService`

- **Konumu:** `LicenseManager.Api/Services/ProjectService.cs`
- **Görevi:** Proje oluşturur, ilk Ed25519 keypair'i üretir, yeni pasif rotation key'i hazırlar ve seçilen key version'ı aktif eder.
- **Kim tarafından kullanılıyor:** `ProjectsController` tarafından; alt tarafta `Ed25519KeyService`, `IPrivateKeyProtector` ve `ApplicationDbContext` kullanılarak çağrılır.
- **Hangi verilerle çalışıyor:** `Project`, `ProjectKey`, `GeneratedKeyPair` ve `PreparedProjectKeyResponse` nesneleriyle çalışır.
- **Güvenlik açısından önemli noktası:** Raw private key'i DB'ye yazmadan önce `IPrivateKeyProtector.Protect` ile korur ve plaintext byte dizisini `CryptographicOperations.ZeroMemory` ile temizler. Rotation prepare aşamasında yeni key'i pasif oluşturur; activate aşamasında eski key'leri pasifleştirip yalnızca seçilen key'i aktif bırakır.

```text
ProjectService
→ Ed25519KeyService ile keypair oluşturur
→ private key'i IPrivateKeyProtector ile korur
→ plaintext private key belleğini temizler
→ Project ve ProjectKey olarak kaydeder
```

##### `LicenseService`

- **Konumu:** `LicenseManager.Api/Services/LicenseService.cs`
- **Görevi:** Lisans kaydı oluşturur, 30 günlük signed-license süresini hesaplar, generate/renew akışını yürütür ve block durumunu değiştirir.
- **Kim tarafından kullanılıyor:** `LicensesController` tarafından kullanılır; imzalama için `LicenseSigningService`, veri için `ApplicationDbContext` çağrılır.
- **Hangi verilerle çalışıyor:** `License`, `Project`, aktif `ProjectKey`, lisans DTO'ları, module listesi, `ValidUntil` ve `ExpiryDate` değerleriyle çalışır.
- **Güvenlik açısından önemli noktası:** `ExpiryDate = min(now + 30 gün, ValidUntil)` kuralını uygular; missing, blocked veya gerçek süresi bitmiş lisans için signed payload üretmez. Tam olarak bir aktif signing key bulunmasını zorunlu tutar ve benzersiz license key'i kriptografik rastgele byte'lardan üretir.

##### `LicenseSigningService`

- **Konumu:** `LicenseManager.Api/Services/LicenseSigningService.cs`
- **Görevi:** `LicensePayload` nesnesini canonicalize eder, SHA-256 hash'ini çıkarır ve Ed25519 ile imzalar.
- **Kim tarafından kullanılıyor:** `LicenseService` generate ve renew işlemlerinde çağırır; private key erişimini `IPrivateKeyProtector` üzerinden yapar.
- **Hangi verilerle çalışıyor:** Canonical UTF-8 payload byte'ları, SHA-256 hash, protected private key ve Base64 signature ile çalışır.
- **Güvenlik açısından önemli noktası:** Protected private key'i yalnızca imza sırasında açar; import edilen raw private key byte'larını `finally` bloğunda bellekten temizler. Private key veya hash hiçbir API response'una eklenmez.

```text
LicenseSigningService
→ protected private key'i IPrivateKeyProtector ile açar
→ payload'ı JsonCanonicalizer ile canonicalize eder
→ SHA-256 hash üretir
→ hash'i Ed25519 ile imzalar
→ raw private key belleğini temizler
```

##### `Ed25519KeyService`

- **Konumu:** `LicenseManager.Api/Services/Ed25519KeyService.cs`
- **Görevi:** NSec kullanarak Ed25519 public/private keypair üretir ve raw byte dizileri halinde `GeneratedKeyPair` döndürür.
- **Kim tarafından kullanılıyor:** Yalnızca `ProjectService` tarafından proje oluşturma ve rotation prepare akışlarında çağrılır.
- **Hangi verilerle çalışıyor:** Raw Ed25519 public ve private key byte dizileriyle çalışır.
- **Güvenlik açısından önemli noktası:** Private key export edildiği için kısa süre plaintext olarak bellekte bulunur; `ProjectService` bunu hemen korumak ve temizlemekle sorumludur. Bu servis tek başına anahtar saklamaz.

##### `AdminApiKeyValidator`

- **Konumu:** `LicenseManager.Api/Services/AdminApiKeyValidator.cs`
- **Görevi:** Request header'daki admin key ile `AdminSecurity:ApiKey` configuration değerini karşılaştırır.
- **Kim tarafından kullanılıyor:** Dört API controller'ındaki admin endpointleri tarafından kullanılır.
- **Hangi verilerle çalışıyor:** Configuration'daki secret ve `X-Admin-Api-Key` header değerleriyle çalışır.
- **Güvenlik açısından önemli noktası:** İki değeri SHA-256 ile özetleyip `CryptographicOperations.FixedTimeEquals` ile karşılaştırarak doğrudan erken çıkışlı string karşılaştırmasını önler. Configured veya supplied key boşsa güvenli biçimde `false` döner.

#### Private key protection yapıları

##### `IPrivateKeyProtector`

- **Konumu:** `LicenseManager.Api/Services/IPrivateKeyProtector.cs`
- **Görevi:** Saklama ortamına özel private-key korumasını `Protect` ve `Unprotect` sözleşmeleriyle soyutlar.
- **Kim tarafından kullanılıyor:** `ProjectService` koruma, `LicenseSigningService` açma işlemi için kullanır; Development/Demo'da `AesGcmPrivateKeyProtector` uygular.
- **Hangi verilerle çalışıyor:** Raw ve protected private-key byte dizileriyle çalışır.
- **Güvenlik açısından önemli noktası:** Uygulama servislerini AES, KMS veya Vault detayından ayırır. Production sağlayıcısı eklenirken aynı güvenlik sözleşmesi korunabilir.

##### `AesGcmPrivateKeyProtector`

- **Konumu:** `LicenseManager.Api/Services/AesGcmPrivateKeyProtector.cs`
- **Görevi:** Ed25519 private key'leri 32-byte master key ile AES-256-GCM kullanarak korur ve açar.
- **Kim tarafından kullanılıyor:** DI üzerinden `IPrivateKeyProtector` olarak `ProjectService` ve `LicenseSigningService`; doğrudan legacy migration servisi tarafından kullanılır.
- **Hangi verilerle çalışıyor:** `LMK1` header, 12-byte nonce, 16-byte authentication tag, ciphertext ve Base64 configuration master key ile çalışır.
- **Güvenlik açısından önemli noktası:** Her korumada yeni kriptografik nonce üretir, `LMK1` header'ı associated data olarak doğrular ve bozulmuş/wrong-key ciphertext'i reddeder. Master key'in tam 32 byte olmasını başlangıçta doğrular ve dispose sırasında kendi key kopyasını temizler.

##### `PrivateKeyProtectionOptions`

- **Konumu:** `LicenseManager.Api/Services/PrivateKeyProtectionOptions.cs`
- **Görevi:** `PrivateKeyProtection` configuration bölümündeki provider, master key ve legacy migration flag'ini strongly typed olarak taşır.
- **Kim tarafından kullanılıyor:** `PrivateKeyProtectionServiceCollectionExtensions` ve `AesGcmPrivateKeyProtector` tarafından options binding üzerinden okunur.
- **Hangi verilerle çalışıyor:** `Provider`, Base64 `MasterKey` ve `MigrateLegacyDpapi` değerleriyle çalışır.
- **Güvenlik açısından önemli noktası:** Master key'in kaynak kod veya `appsettings.json` yerine User Secrets/environment üzerinden verilmesi beklenir. Options doğrulaması geçersiz anahtarda uygulamayı başlangıçta durdurur.

##### `PrivateKeyProtectionServiceCollectionExtensions`

- **Konumu:** `LicenseManager.Api/Services/PrivateKeyProtectionServiceCollectionExtensions.cs`
- **Görevi:** Private-key protection options ve implementasyonlarını dependency-injection container'a kaydeder.
- **Kim tarafından kullanılıyor:** `Program.cs` içindeki `AddPrivateKeyProtection` çağrısı tarafından kullanılır.
- **Hangi verilerle çalışıyor:** Configuration bölümü, host environment ve mevcut DI registrations ile çalışır.
- **Güvenlik açısından önemli noktası:** Development/Demo için yalnızca `AesGcm` provider'ını kabul eder ve master key'i `ValidateOnStart` ile doğrular. Production'da daha önceden KMS/Vault tabanlı `IPrivateKeyProtector` kaydı yoksa fail-closed davranıp uygulamayı başlatmaz.

##### `LegacyDpapiPrivateKeyMigrationService`

- **Konumu:** `LicenseManager.Api/Services/LegacyDpapiPrivateKeyMigrationService.cs`
- **Görevi:** Eski DPAPI ile korunmuş `Project.PrivateKey` ve `ProjectKey.PrivateKey` değerlerini tek seferlik AES-GCM `LMK1` formatına taşır.
- **Kim tarafından kullanılıyor:** `MigrateLegacyDpapi=true` olduğunda hosted service olarak uygulama başlangıcında DI tarafından çalıştırılır.
- **Hangi verilerle çalışıyor:** PostgreSQL'deki legacy protected byte dizileri, Windows DPAPI, `AesGcmPrivateKeyProtector` ve EF transaction ile çalışır.
- **Güvenlik açısından önemli noktası:** Yalnızca kaydı oluşturan Windows kullanıcısı altında çalışabilir; AES'e geçirdikten sonra round-trip doğrulaması yapar ve tüm DB güncellemesini transaction içinde uygular. Plaintext ve doğrulama buffer'larını işlem sonunda temizler.

### `LicenseManager.Core`

#### Domain modelleri

| Sınıf | Konumu | Görevi / kullananlar | Veriler ve güvenlik notu |
|---|---|---|---|
| `Company` | `LicenseManager.Core/Models/Company.cs` | Lisans sahibi şirket aggregate'ini temsil eder; DbContext, controller'lar ve lisans servisi kullanır. | `Id`, `Name`, `CreatedAt` ve `Licenses` navigation'ını taşır. Kendi içinde yetkilendirme yapmaz; erişim controller ve DB katmanında korunur. |
| `Project` | `LicenseManager.Core/Models/Project.cs` | Benzersiz proje adı/sürümünü ve anahtar durumunun geriye dönük uyumlu aynasını temsil eder. | `PublicKey`, protected `PrivateKey`, `KeyVersion`, lisanslar ve key geçmişiyle çalışır. `PrivateKey` alanındaki değer raw değil protector çıktısı olmalıdır. |
| `ProjectKey` | `LicenseManager.Core/Models/ProjectKey.cs` | Projenin sürümlendirilmiş signing key kaydını temsil eder; rotation ve imzalama akışlarında kullanılır. | Public key, protected private key, `KeyVersion` ve `IsActive` taşır. API yalnızca public tarafını döndürür; tek aktif key kuralını DbContext uygular. |
| `License` | `LicenseManager.Core/Models/License.cs` | Şirket ile projeyi lisans hakları üzerinden bağlayan kalıcı domain modelidir. | License key, module listesi, `IssuedAt`, `ExpiryDate`, `ValidUntil` ve `IsBlocked` taşır. Karar alanları signed payload'a kontrollü biçimde aktarılır; `ValidUntil` sunucu sınırıdır. |

#### DTO'lar

| DTO | Konumu | Görevi / kullananlar | Veriler ve güvenlik notu |
|---|---|---|---|
| `CreateLicenseRequest` | `LicenseManager.Core/DTOs/CreateLicenseRequest.cs` | Admin create endpointinden `LicenseService`e lisans talebi taşır. | Company/project ID, modules ve `valid_until` içerir; servis varlık, modül ve gelecek tarih doğrulaması yapar. |
| `CreatedLicenseResponse` | `LicenseManager.Core/DTOs/CreatedLicenseResponse.cs` | Başarılı admin lisans oluşturma sonucunu döndürür; Angular `CreatedLicense` ile eşleşir. | License key ve süreleri içerir ancak signature veya private key içermez. |
| `GeneratedKeyPair` | `LicenseManager.Core/DTOs/GeneratedKeyPair.cs` | `Ed25519KeyService`ten `ProjectService`e yeni keypair taşır. | Raw private key içerdiği için yalnızca kısa ömürlü iç servis verisidir; loglanmamalı ve korumadan sonra temizlenmelidir. |
| `LicenseGenerateRequest` | `LicenseManager.Core/DTOs/LicenseGenerateRequest.cs` | Client'ın `generate` ve `renew` çağrılarındaki company ID + license key request'idir. | Public endpoint girişidir; sunucu sonuç farklarını maskeleyerek anahtar enumeration bilgisini azaltır. |
| `LicensePayload` | `LicenseManager.Core/DTOs/LicensePayload.cs` | İmzalanan ve client tarafından doğrulanan lisans gövdesidir. | Company, project, `key_version`, modules ve signed expiry içerir; alan değişikliği hash ve imzayı bozar. |
| `PreparedProjectKeyResponse` | `LicenseManager.Core/DTOs/PreparedProjectKeyResponse.cs` | Rotation prepare sonucunda admin'e project ID, key version ve Base64 public key verir. | Private key içermez; public key client build'ine eklenmeden activation yapılmamalıdır. |
| `SignedLicenseResponse` | `LicenseManager.Core/DTOs/SignedLicenseResponse.cs` | `LicensePayload` ile Base64 Ed25519 signature'ı birlikte taşır; API, cache ve client arasında ortak sözleşmedir. | Gizli değildir; güvenliği dosyanın saklanmasına değil signature doğrulamasına dayanır. |

#### `JsonCanonicalizer`

- **Konumu:** `LicenseManager.Core/Services/JsonCanonicalizer.cs`
- **Görevi:** Nesneyi JSON'a çevirir, object property'lerini ordinal sıraya dizer ve whitespace içermeyen deterministik UTF-8 byte dizisi üretir.
- **Kim tarafından kullanılıyor:** Server'da `LicenseSigningService`, client'ta `LicenseVerificationService` ve canonicalization/signature testleri tarafından kullanılır.
- **Hangi verilerle çalışıyor:** Object, array, string, number, boolean ve null JSON değerleriyle recursive çalışır; array sırasını korur.
- **Güvenlik açısından önemli noktası:** Server ve client'ın aynı payload için aynı hash'i üretmesini sağlar. Property sırası veya whitespace farkını etkisizleştirirken gerçek veri değişikliklerinin hash'i değiştirmesini korur.

### `LicenseManager.Client`

#### Başlangıç ve configuration

##### `Program.cs`

- **Konumu:** `LicenseManager.Client/Program.cs`
- **Görevi:** JSON, User Secrets ve environment ayarlarını birleştirir; client servislerini kurar, ilk lisans kontrolünü yapar ve background renewal döngüsünü başlatır.
- **Kim tarafından kullanılıyor:** Console client process'i başladığında entry point olarak çalışır.
- **Hangi verilerle çalışıyor:** `LicenseClientSettings`, company ID, license key, required module ve API base URL ile çalışır.
- **Güvenlik açısından önemli noktası:** Lisans doğrulanamazsa uygulamanın korunan kısmını başlatmaz. Secret değerler environment/User Secrets ile `appsettings.json` üzerine yazılabilir.

##### `LicenseClientSettings`

- **Konumu:** `LicenseManager.Client/Configuration/LicenseClientSettings.cs`
- **Görevi:** `LicenseClient` configuration bölümünü strongly typed olarak temsil eder.
- **Kim tarafından kullanılıyor:** `Program.cs` tarafından bind edilir ve API, manager ve renewal servisleri oluşturulurken kullanılır.
- **Hangi verilerle çalışıyor:** API URL, company ID, license key, required module ve renewal threshold gün sayısını taşır.
- **Güvenlik açısından önemli noktası:** License key hassas kabul edilerek User Secrets/environment ile verilmelidir; bu sınıf kendi başına secret saklama veya şifreleme yapmaz.

##### `EmbeddedLicenseKey`

- **Konumu:** `LicenseManager.Client/Configuration/EmbeddedLicenceKey.cs`
- **Görevi:** Build'e gömülü proje adı/sürümü ile `LicensePublicKey_N` assembly metadata değerlerini runtime dictionary'ye yükler.
- **Kim tarafından kullanılıyor:** `EmbeddedLicensePublicKeyProvider` ve missing-license masking akışındaki `LicenseValidatorService` tarafından kullanılır.
- **Hangi verilerle çalışıyor:** Project identity ve `key_version → Base64 public key` eşlemesiyle çalışır.
- **Güvenlik açısından önemli noktası:** Public key'i lisans dosyasından veya ağdan kabul etmez; güven kökü client binary'sine build sırasında gömülür. Geçersiz veya eksik metadata doğrulamayı başarısız yapar.

##### `EmbeddedLicensePublicKeyProvider`

- **Konumu:** `LicenseManager.Client/Configuration/EmbeddedLicensePublicKeyProvider.cs`
- **Görevi:** `ILicensePublicKeyProvider` sözleşmesi üzerinden proje kimliğini ve istenen key version'ın raw public key'ini sağlar.
- **Kim tarafından kullanılıyor:** Varsayılan olarak `LicenseVerificationService` tarafından kullanılır.
- **Hangi verilerle çalışıyor:** `EmbeddedLicenseKey.PublicKeys`, project name/version ve Base64 Ed25519 public key değerleriyle çalışır.
- **Güvenlik açısından önemli noktası:** Bilinmeyen key version veya geçersiz Base64 için `false` döner; başka anahtara fallback yapmaz. Böylece payload'daki `key_version` manipülasyonu yanlış key ile sessizce doğrulanmaz.

#### Interface ve sonuç modelleri

| Yapı | Konumu | Görevi / kullananlar | Veri ve güvenlik notu |
|---|---|---|---|
| `ILicenseManagerService` | `Services/ILicenseManagerService.cs` | Company, license key ve required module için asenkron lisans kontrol sözleşmesidir; background servis kullanır. | Gerçek implementasyon `LicenseManagerService`tir; testlerde sahte implementasyonla renewal döngüsü izole edilir. |
| `ILicenseVerificationService` | `Services/ILicenseVerificationService.cs` | Payload + signature kriptografik doğrulama sözleşmesidir; `LicenseValidatorService` kullanır. | Implementasyon başarısız format ve kripto durumlarında exception sızdırmadan `false` döner. |
| `ILicensePublicKeyProvider` | `Services/ILicensePublicKeyProvider.cs` | Project identity ve key-version bazlı public key sağlayıcı sözleşmesidir. | Test key provider'larını mümkün kılar; production akışında embedded provider güven köküdür. |
| `LicenseFetchStatus` | `Models/LicenseFetchResult.cs` | API sonucunu `Success`, `Rejected` veya `Unavailable` olarak sınıflandırır. | Rejected ile network/server unavailable ayrımı offline grace kararında kullanılır; kullanıcıya hassas sunucu ayrıntısı verilmez. |
| `LicenseFetchResult` | `Models/LicenseFetchResult.cs` | API fetch durumunu ve varsa `SignedLicenseResponse`u taşır. | Yapısal olarak geçersiz response başarı kabul edilmez. |
| `LicenseValidationResult` | `Models/LicenseValidationResult.cs` | Son lisans kararını `IsValid` ve kullanıcı mesajıyla taşır. | Başarısızlıklar genel mesajla maskelenebilir; teknik doğrulama nedeni dışarı açılmaz. |

#### Doğrulama, cache ve renewal servisleri

##### `LicenseVerificationService`

- **Konumu:** `LicenseManager.Client/Services/LicenseVerificationService.cs`
- **Görevi:** `key_version` ile public key seçer, payload'ı canonicalize edip SHA-256 hash üretir ve Ed25519 signature'ı doğrular.
- **Kim tarafından kullanılıyor:** `LicenseValidatorService` tarafından kullanılır; public key'i `ILicensePublicKeyProvider`dan alır.
- **Hangi verilerle çalışıyor:** `LicensePayload`, Base64 signature, embedded project identity ve raw public key ile çalışır.
- **Güvenlik açısından önemli noktası:** Önce signature, sonra gömülü project name/version eşleşmesi kontrol edilir. Bilinmeyen key version, invalid Base64, yanlış key veya cryptographic exception güvenli biçimde `false` üretir.

```text
LicenseVerificationService
→ payload.key_version ile embedded public key'i seçer
→ JsonCanonicalizer + SHA-256 uygular
→ Ed25519 Verify çalıştırır
→ imza geçerliyse project name/version kontrol eder
```

##### `LicenseValidatorService`

- **Konumu:** `LicenseManager.Client/Services/LicenseValidatorService.cs`
- **Görevi:** Kriptografik doğrulamadan sonra company binding, signed expiry ve required module kontrollerini uygular.
- **Kim tarafından kullanılıyor:** `LicenseManagerService` hem cache hem online response doğrulamasında kullanır.
- **Hangi verilerle çalışıyor:** `SignedLicenseResponse`, required module ve beklenen company ID ile çalışır.
- **Güvenlik açısından önemli noktası:** Signature doğrulanmadan iş kuralı alanlarına güvenmez ve tüm başarısızlıkları `Lisans doğrulanamadı.` mesajına çevirir. Dosya yokken dummy signature doğrulaması çalıştıran masking metodu, belirgin davranış farkını azaltır.

##### `LicenseManagerService`

- **Konumu:** `LicenseManager.Client/Services/LicenseManagerService.cs`
- **Görevi:** Cache-first lisans yaşam döngüsünü orkestre eder; cache'i doğrular, renewal eşiğini hesaplar, gerekirse API'den generate/renew ister ve geçerli sonucu kaydeder.
- **Kim tarafından kullanılıyor:** Client `Program.cs` ve `BackgroundLicenseRenewalService` tarafından `ILicenseManagerService` olarak kullanılır.
- **Hangi verilerle çalışıyor:** Company ID, license key, required module, cached/online signed license ve `LicenseFetchStatus` ile çalışır.
- **Güvenlik açısından önemli noktası:** API'den gelen lisansı kaydetmeden önce yeniden doğrular. Renewal reddedilse veya ağ kapalı olsa bile mevcut cache geçerliyse yalnızca kendi `ExpiryDate` değerine kadar offline grace uygular; geçerli cache yoksa fail-closed davranır.

##### `LicenseFileService`

- **Konumu:** `LicenseManager.Client/Services/LicenseFileService.cs`
- **Görevi:** Signed license response'u `<LocalApplicationData>/LicenseManager/license.json` dosyasına yazar ve geri yükler.
- **Kim tarafından kullanılıyor:** `LicenseManagerService` tarafından cache okuma/yazma için; testlerde özel geçici dosya yolu ile kullanılır.
- **Hangi verilerle çalışıyor:** JSON biçimindeki `body + signature` ve yerel dosya yolu ile çalışır.
- **Güvenlik açısından önemli noktası:** Cache şifreli veya gizli değildir; güvenlik, `LicenseValidatorService`in signature ve binding kontrollerine dayanır. Bozuk JSON, eksik signature, erişim veya I/O hataları `null` dönerek geçersiz cache gibi ele alınır.

##### `LicenseApiService`

- **Konumu:** `LicenseManager.Client/Services/LicenseApiService.cs`
- **Görevi:** `generate` ve `renew` endpointlerine HTTP request gönderir ve response'u güvenli bir fetch sonucuna dönüştürür.
- **Kim tarafından kullanılıyor:** `LicenseManagerService` tarafından yeni/güncel lisans alma ve renewal için kullanılır.
- **Hangi verilerle çalışıyor:** `LicenseGenerateRequest`, HTTP status code ve `SignedLicenseResponse` ile çalışır.
- **Güvenlik açısından önemli noktası:** 4xx cevapları `Rejected`, 5xx/network/timeout veya bozuk JSON durumlarını `Unavailable` olarak sınıflandırır. Başarı response'unda gerekli ID, project identity, key version ve signature alanlarını yapısal olarak doğrular; kriptografik güveni ise validator'a bırakır.

##### `BackgroundLicenseRenewalService`

- **Konumu:** `LicenseManager.Client/Services/BackgroundLicenseRenewalService.cs`
- **Görevi:** Varsayılan 12 saatlik `PeriodicTimer` ile lisans kontrolünü tekrarlar; `RunOnceAsync` tek kontrol için de kullanılabilir.
- **Kim tarafından kullanılıyor:** Client `Program.cs`; testlerde `ILicenseManagerService` sahte implementasyonları tarafından kullanılır.
- **Hangi verilerle çalışıyor:** Company ID, license key, required module, interval, cancellation token ve isteğe bağlı log callback'i ile çalışır.
- **Güvenlik açısından önemli noktası:** Kararı kendisi üretmez, merkezi `ILicenseManagerService`e devreder. Exception ayrıntısını kullanıcı loguna sızdırmadan genel tamamlanamadı mesajı verir.

### `LicenseManager.Tests`

Her test sınıfı repository'de bulunan gerçek sınıf adıyla aşağıda listelenmiştir:

| Test sınıfı / dosyası | Neyi doğrular | Güvenlik veya davranış odağı |
|---|---|---|
| `AdminLicenseSecurityTests` | Admin key yok, yanlış veya doğru olduğunda lisans create sonucunu doğrular. | Yetkisiz isteğin kayıt oluşturmadığını ve doğru key'in gerekli olduğunu kanıtlar. |
| `AesGcmPrivateKeyProtectorTests` | AES round-trip, benzersiz nonce, tamper, yanlış master key ve 256-bit key zorunluluğunu test eder. | Private key envelope gizlilik ve bütünlük kurallarını korur. |
| `LicenseRevocationPolicyTests` (`BackgroundLicenseRenewalServiceTests.cs`) | Renewal reddedildiğinde geçerli cache'in çalışmaya devam ettiğini, süresi bitmiş cache'in ise geçersiz olduğunu test eder. | Offline grace ile revocation sınırının bilinçli davranış olduğunu doğrular. |
| `CompaniesControllerTests` | Şirket listeleme, detay, create, duplicate/boş isim ve admin yetkisini test eder. | Yetkisiz create'in DB'yi değiştirmemesini kontrol eder. |
| `JsonCanonicalizerTests` | Snake-case sözleşme, property sırası, whitespace, nested sıralama ve veri değişikliği hash davranışını test eder. | Server/client deterministik imza girdisini güvence altına alır. |
| `KeyRotationTests` | V1/V2 doğrulama, eski key'in geçerliliği, bilinmeyen/yanlış/tampered version ve `ValidUntil` korunmasını test eder. | Rotation'ın eski lisansları kırmamasını ve key confusion oluşmamasını doğrular. |
| `LicenseApiServiceTests` | Başarılı response, bozuk JSON, eksik signature, 404/500/network ve renew route davranışını test eder. | Rejected ile unavailable ayrımını ve yapısal response kontrolünü güvenceye alır. |
| `LicenseBlockUnblockTests` | Block/unblock'ın generate akışına etkisini ve eksik/yanlış admin key davranışını test eder. | Yetkisiz block denemesinin lisans durumunu değiştirmemesini doğrular. |
| `LicenseCompanyBindingTests` | Aynı ve farklı company ID sonuçlarını ve genel hata mesajını test eder. | Geçerli signed lisansın başka şirket adına kullanılamamasını sağlar. |
| `LicenseControllerMaskingTests` | Missing/blocked generate-renew sonuçlarının aynı görünmesini ve license detail response'unun minimum veri taşımasını test eder. | Enumeration ve gereksiz Company/Project ID sızıntısını azaltan sözleşmeyi korur. |
| `LicenseCreationEndToEndTests` | Oluşturulan lisansın server'da generate edilip client tarafından doğrulanmasını uçtan uca test eder. | API, signing, ortak DTO ve client verify zincirinin birlikte çalıştığını doğrular. |
| `LicenseCreationValidationTests` | Missing company/project, boş modül, trim/deduplication ve aynı company-project için birden çok lisans davranışını test eder. | Geçersiz input'un kalıcı lisans üretmemesini kontrol eder. |
| `LicenseFileServiceTests` | Eksik ve bozuk cache dosyalarının `null` dönmesini test eder. | Manipüle/okunamaz cache'in geçerli kabul edilmemesini sağlar. |
| `LicenseIntegrationTests` | Server signature'ın client'ta doğrulanmasını; body veya signature değişikliğinin reddedilmesini test eder. | Katmanlar arası kriptografik uyumu doğrular. |
| `LicenseProjectIdentityTests` | Eşleşen proje kimliğini ve farklı ad/sürüm durumlarını test eder. | Başka ürün veya sürüme ait geçerli imzanın yanlış client'ta kullanılmasını engeller. |
| `LicenseSignatureTests` | Orijinal lisans, module/expiry/version/signature değişikliği ve yanlış public key senaryolarını test eder. | İmzalı karar alanlarının tamper-evident olduğunu kanıtlar. |
| `LicenseValidatorTests` | Geçerli lisans, invalid signature, expiry, missing module, company mismatch ve genel hata maskelemesini test eder. | İş kuralı başarısızlıklarının ayrıntı sızdırmamasını doğrular. |
| `LicenseValidityPeriodTests` | 30 günlük signed lifetime, `ValidUntil` üst sınırı ve geçmiş tarih reddini test eder. | Renewal'ın gerçek lisans hakkını aşmamasını güvenceye alır. |
| `LicenseVerificationServiceTests` | Geçersiz Base64 signature'ın exception yerine `false` döndürmesini test eder. | Bozuk dış verinin client'ı düşürmeden reddedilmesini sağlar. |
| `TestPrivateKeyProtector` | Testler için rastgele 32-byte key ile `AesGcmPrivateKeyProtector` üretir. | Production secret kullanmadan gerçek protector davranışını testlere taşır; bir test sınıfı değil ortak test helper'ıdır. |

### `LicenseManager.Admin`

#### Angular componentleri

##### `App`

- **Konumu:** `LicenseManager.Admin/src/app/app.component.ts`
- **Görevi:** Angular uygulamasının kök componentidir ve route içeriği için ana host görevi görür.
- **Kim tarafından kullanılıyor:** `main.ts` tarafından `appConfig` ile bootstrap edilir.
- **Hangi verilerle çalışıyor:** Doğrudan domain verisi işlemez; router outlet üzerinden alt componentleri gösterir.
- **Güvenlik açısından önemli noktası:** Yetkilendirme kararı burada verilmez; route guard ve API interceptor katmanlarına bırakılır.

##### `AdminShellComponent`

- **Konumu:** `LicenseManager.Admin/src/app/layout/admin-shell/admin-shell.component.ts`
- **Görevi:** Admin sayfalarının ortak navigasyon ve layout kabuğudur; çıkış işlemini yönetir.
- **Kim tarafından kullanılıyor:** `app.routes.ts` içindeki korumalı parent route tarafından kullanılır.
- **Hangi verilerle çalışıyor:** `AdminSessionService` oturum sinyali ve Angular Router ile çalışır.
- **Güvenlik açısından önemli noktası:** Logout sırasında bellek içindeki API key'i temizleyip `/login`e döner. Sunucu yetkilendirmesini kendisi yapmaz.

##### `LoginComponent`

- **Konumu:** `LicenseManager.Admin/src/app/pages/login/login.component.ts`
- **Görevi:** Admin API key'i formdan alır ve dashboard isteğiyle gerçekten geçerli olup olmadığını sınar.
- **Kim tarafından kullanılıyor:** `/login` route'u tarafından gösterilir; `AdminSessionService`, `DashboardApiService` ve Router kullanır.
- **Hangi verilerle çalışıyor:** Form control'deki API key, unauthorized query reason ve dashboard response ile çalışır.
- **Güvenlik açısından önemli noktası:** Key'i URL, localStorage veya sessionStorage'a yazmaz. Deneme başarısızsa session'ı temizler; başarılıysa `/dashboard`a geçer.

##### `DashboardComponent`

- **Konumu:** `LicenseManager.Admin/src/app/pages/dashboard/dashboard.component.ts`
- **Görevi:** Dashboard özet kartlarını, son lisansları ve yaklaşan gerçek bitişleri gösterir.
- **Kim tarafından kullanılıyor:** `/dashboard` child route'u; veriyi `DashboardApiService`ten alır.
- **Hangi verilerle çalışıyor:** `DashboardResponse`, summary sayaçları, recent ve expiring license listeleriyle çalışır.
- **Güvenlik açısından önemli noktası:** Lisans anahtarlarını görünümde maskeler ve status'u mevcut tarihle hesaplar. Bu yalnızca sunum katmanıdır; gerçek erişim kontrolü API'dedir.

##### `CompaniesComponent`

- **Konumu:** `LicenseManager.Admin/src/app/pages/companies/companies.component.ts`
- **Görevi:** Şirketleri yükler, create formunu doğrular ve yeni şirket oluşturur.
- **Kim tarafından kullanılıyor:** `/companies` route'u; `CompaniesApiService` ile konuşur.
- **Hangi verilerle çalışıyor:** `CompanyListItem`, şirket adı form control'ü ve API hata/başarı mesajlarıyla çalışır.
- **Güvenlik açısından önemli noktası:** Client-side required kontrolü kullanıcı deneyimi sağlar fakat güvenlik sınırı değildir; duplicate ve authorization yeniden API'de doğrulanır.

##### `ProjectsComponent`

- **Konumu:** `LicenseManager.Admin/src/app/pages/projects/projects.component.ts`
- **Görevi:** Proje listeleme/create, seçili projenin key geçmişi, prepare ve activate rotation işlemlerini yönetir.
- **Kim tarafından kullanılıyor:** `/projects` route'u; bütün HTTP işlemlerini `ProjectsApiService`e devreder.
- **Hangi verilerle çalışıyor:** Project list/create modelleri, `ProjectKey`, prepared ve activated key response'larıyla çalışır.
- **Güvenlik açısından önemli noktası:** Arayüze yalnızca public key gelir; private key modeli veya görünümü yoktur. Activation öncesi kullanıcı onayı ister, ancak asıl tek-aktif-key kuralı API/DB'de uygulanır.

##### `LicensesComponent`

- **Konumu:** `LicenseManager.Admin/src/app/pages/licenses/licenses.component.ts`
- **Görevi:** Lisansları, şirketleri ve projeleri yükler; lisans create, client-side arama/durum filtresi, key kopyalama ve block/unblock işlemlerini yönetir.
- **Kim tarafından kullanılıyor:** `/licenses` route'u; `LicensesApiService`, `CompaniesApiService` ve `ProjectsApiService` kullanır.
- **Hangi verilerle çalışıyor:** `LicenseListItem`, create response, company/project listeleri, modules ve `valid_until` form alanlarıyla çalışır.
- **Güvenlik açısından önemli noktası:** Liste key'lerini maskeler, block/unblock öncesi onay ister ve geçmiş tarihi client'ta reddeder. Tüm kontroller API'de tekrar uygulanır; component sorting veya pagination sağlamaz.

#### Angular API ve session servisleri

| Servis | Konumu | Görevi / kullananlar | Veri ve güvenlik notu |
|---|---|---|---|
| `AdminSessionService` | `core/services/admin-session.service.ts` | API key'i Angular signal içinde tutar; login, shell, guard ve interceptor kullanır. | Key yalnızca sekme belleğindedir; sayfa yenileme veya logout ile kaybolur, Web Storage'a yazılmaz. |
| `DashboardApiService` | `core/services/dashboard-api.service.ts` | `GET /api/dashboard` çağrısını typed `DashboardResponse` olarak sunar; login ve dashboard kullanır. | Header'ı kendisi eklemez; interceptor merkezi olarak ekler. |
| `CompaniesApiService` | `core/services/companies-api.service.ts` | Şirket listeleme ve create HTTP çağrılarını kapsüller; Companies ve Licenses componentleri kullanır. | Typed modeller client hatasını azaltır ancak API authorization'ın yerini tutmaz. |
| `ProjectsApiService` | `core/services/projects-api.service.ts` | Proje liste/create, key history, prepare ve activate endpointlerini kapsüller. | Yalnız public key response tipleri kullanır; private key için client modeli bulunmaz. |
| `LicensesApiService` | `core/services/licenses-api.service.ts` | Lisans liste/create ve block/unblock çağrılarını sunar; `LicensesComponent` kullanır. | Admin-only endpointlere gider ve header interceptor tarafından eklenir; generate/renew client API'sini kullanmaz. |

##### `adminAuthGuard`

- **Konumu:** `LicenseManager.Admin/src/app/core/guards/admin-auth.guard.ts`
- **Görevi:** Bellekte admin key yoksa korumalı admin route'larına navigasyonu `/login`e yönlendirir.
- **Kim tarafından kullanılıyor:** `app.routes.ts` içindeki `AdminShellComponent` parent route'unda `canActivate` olarak çalışır.
- **Hangi verilerle çalışıyor:** `AdminSessionService.isAuthenticated` signal'ı ve Router URL tree ile çalışır.
- **Güvenlik açısından önemli noktası:** Bu yalnızca client-side navigation guard'dır ve gerçek yetkilendirme değildir. Her API endpointi ayrıca server tarafında admin key doğrulaması yapar.

##### `adminApiKeyInterceptor`

- **Konumu:** `LicenseManager.Admin/src/app/core/interceptors/admin-api-key.interceptor.ts`
- **Görevi:** Configured API base URL altındaki `/api/` isteklerine bellek içindeki `X-Admin-Api-Key` header'ını ekler.
- **Kim tarafından kullanılıyor:** `app.config.ts` içinde Angular HTTP client'a global functional interceptor olarak kaydedilir.
- **Hangi verilerle çalışıyor:** HTTP request/response, environment API URL ve `AdminSessionService` içindeki key ile çalışır.
- **Güvenlik açısından önemli noktası:** Header'ı yalnız beklenen API origin/prefix isteklerine ekleyerek başka adreslere key gönderimini sınırlar. `401` durumunda session'ı temizler ve kullanıcıyı unauthorized nedeni ile login'e yönlendirir.

#### Angular model interface'leri

| Interface | Dosya | Görevi / kullananlar | Veri ve güvenlik notu |
|---|---|---|---|
| `CompanyListItem` | `core/models/company.model.ts` | Şirket listesi satırını Companies/Licenses ekranlarına taşır. | ID, ad, oluşturulma ve lisans sayısı içerir; güvenlik mantığı içermez. |
| `CreateCompanyRequest` | `core/models/company.model.ts` | Companies create request'ini typed yapar. | Yalnız `name` taşır; server doğrulaması zorunludur. |
| `CreatedCompany` | `core/models/company.model.ts` | Başarılı create response'unu temsil eder. | ID, ad ve tarih içerir. |
| `ProjectListItem` | `core/models/project.model.ts` | Proje listesi ve seçim alanlarında kullanılır. | Aktif key version ile lisans/key sayılarını taşır; private key içermez. |
| `CreateProjectRequest` | `core/models/project.model.ts` | Project create request'ini typed yapar. | Ad ve sürüm taşır; benzersizlik server/DB'de uygulanır. |
| `CreatedProject` | `core/models/project.model.ts` | Yeni proje response'unu temsil eder. | Public key ve key version içerebilir; private key içermez. |
| `ProjectKey` | `core/models/project.model.ts` | Key history listesindeki tek key version'ı temsil eder. | Public key ve aktiflik taşır; protected private key alanı yoktur. |
| `PreparedProjectKey` | `core/models/project.model.ts` | Prepare rotation response'unu temsil eder. | Yeni public key'i client build dağıtımı için sunar. |
| `ActivatedProjectKey` | `core/models/project.model.ts` | Activate sonucu project/key version ve başarı durumunu taşır. | Activation gerçekliği API tarafından belirlenir. |
| `LicenseListItem` | `core/models/license.model.ts` | Admin lisans tablosundaki tam satır modelidir. | Company/project, modules, süreler, block ve license key içerir; UI key'i maskeli gösterir. |
| `CreateLicenseRequest` | `core/models/license.model.ts` | Admin lisans create request'ini typed yapar. | Company/project ID, modules ve gerçek `valid_until` taşır; server yeniden doğrular. |
| `CreatedLicense` | `core/models/license.model.ts` | Oluşturulan lisansın API response'unu temsil eder. | Yeni license key'i içerdiği için arayüz yalnız kullanıcı işleminde gösterir/kopyalar. |
| `LicenseBlockStatus` | `core/models/license.model.ts` | Block/unblock sonucunu taşır. | Lisans ID ve yeni block durumunu içerir. |
| `DashboardResponse` | `core/models/dashboard.model.ts` | Dashboard response'unun üst sözleşmesidir. | Generation zamanı, summary ve iki listeyi birleştirir. |
| `DashboardSummary` | `core/models/dashboard.model.ts` | Dashboard sayaçlarını temsil eder. | Signed 7 gün ve gerçek 30 gün metriklerini ayrı tutar. |
| `RecentLicense` | `core/models/dashboard.model.ts` | Son lisans satırlarını temsil eder. | License key içerir; component görünümde maskeler. |
| `ExpiringLicense` | `core/models/dashboard.model.ts` | Yaklaşan gerçek lisans bitişlerini temsil eder. | `valid_until` ve kalan gün taşır; yalnız admin endpointinden gelir. |

#### Angular bootstrap ve route yapısı

##### `appConfig`

- **Konumu:** `LicenseManager.Admin/src/app/app.config.ts`
- **Görevi:** Router, browser global error listener ve interceptor'lı HttpClient provider'larını kaydeder.
- **Kim tarafından kullanılıyor:** `main.ts` Angular uygulamasını bootstrap ederken kullanır.
- **Hangi verilerle çalışıyor:** `routes` ve `adminApiKeyInterceptor` provider tanımlarıyla çalışır.
- **Güvenlik açısından önemli noktası:** Interceptor'ın bütün API servislerinde merkezi uygulanmasını sağlar; tek tek servislerin header unutması riskini azaltır.

##### `routes`

- **Konumu:** `LicenseManager.Admin/src/app/app.routes.ts`
- **Görevi:** `/login`, `/dashboard`, `/companies`, `/projects` ve `/licenses` route'larını; default ve wildcard yönlendirmelerini tanımlar.
- **Kim tarafından kullanılıyor:** `appConfig` içindeki `provideRouter` ve root `App` router outlet'i tarafından kullanılır.
- **Hangi verilerle çalışıyor:** Component referansları, sayfa title değerleri ve `adminAuthGuard` ile çalışır.
- **Güvenlik açısından önemli noktası:** Login dışındaki admin sayfalarını guarded `AdminShellComponent` altında toplar. Guard atlatılsa bile API'nin server-side key kontrolü yetkisiz veriyi engeller.

##### `main.ts`

- **Konumu:** `LicenseManager.Admin/src/main.ts`
- **Görevi:** Root `App` componentini `appConfig` ile bootstrap eden Angular entry point'idir.
- **Kim tarafından kullanılıyor:** Angular build/runtime tarafından uygulama başlangıcında çalıştırılır.
- **Hangi verilerle çalışıyor:** Component ve application configuration metadata'sıyla çalışır.
- **Güvenlik açısından önemli noktası:** Doğrudan secret veya domain verisi işlemez; güvenlik provider'larının kurulması `appConfig` üzerinden gerçekleşir.

## Veri modeli

Temel tablolar şunlardır:

| Tablo | Görev |
|---|---|
| `Companies` | Lisans sahibi şirketleri tutar. |
| `Projects` | Benzersiz `(Name, Version)` yazılım ürünlerini tutar. |
| `ProjectKeys` | Projeye ait sürümlendirilmiş Ed25519 anahtar geçmişini tutar. |
| `Licenses` | Şirket ile projeyi bağlar; modül, süre ve block durumunu taşır. |

İlişki modeli:

```text
Company 1 ── N License N ── 1 Project
                         └── N ProjectKey
```

- Bir şirket birden fazla projeye lisanslanabilir.
- Bir proje birden fazla şirkete lisanslanabilir.
- Projeler globaldir; doğrudan bir şirkete bağlı değildir.
- Aynı şirket ve proje için gerektiğinde birden fazla lisans oluşturulabilir.
- Her proje için aynı anda yalnızca bir aktif signing key bulunur.
- `(ProjectId, KeyVersion)` benzersizdir.

`License` kaydı temel olarak `CompanyId`, `ProjectId`, `LicenseKey`, `Modules`, `IssuedAt`, `ExpiryDate`, `ValidUntil`, `IsBlocked` ve `CreatedAt` alanlarını taşır. `Modules`, PostgreSQL tarafında JSON olarak saklanan dinamik bir string listesidir. `Reports` ve `Inventory` gibi değerler örnek lisans kapsamlarıdır; bu repository gerçek ERP modüllerini içermez.

## Lisans imzalama ve doğrulama

Her `(proje adı + sürüm)` kaydı kendi Ed25519 keypair'ine sahiptir. Private key şirkete değil projeye aittir. Şirket kimliği ise payload içinde imzalandığı için bir şirkete ait geçerli lisans başka şirket adına kullanılamaz.

İmzalama akışı:

```text
LicensePayload
    ↓
Ortak JsonCanonicalizer
    ↓
Deterministik UTF-8 JSON
    ↓
SHA-256
    ↓
Ed25519 Sign(active private key)
    ↓
{ body, signature }
```

İmzalı payload şu alanları içerir:

```text
license_id
company_id
project_id
project_name
project_version
key_version
modules
issued_at
expiry_date
```

İstemci aynı canonicalization ve SHA-256 akışını uygular, `key_version` ile doğru gömülü public key'i seçer ve Ed25519 imzasını doğrular. İmza geçerliyse ayrıca proje adı/sürümü, şirket kimliği, `ExpiryDate` ve gerekli modül kontrol edilir.

Public key lisans response'undan alınmaz. Böylece saldırganın kendi keypair'iyle sahte lisans üretip kendi public key'ini lisansla birlikte göndermesi engellenir.

## Lisans süre modeli

Sistemde iki ayrı bitiş tarihi vardır:

| Alan | Anlamı |
|---|---|
| `ValidUntil` | Müşterinin gerçek veya sözleşmesel lisans hakkının sonu. Sunucu ve veritabanı tarafında tutulur. |
| `ExpiryDate` | İstemciye verilen mevcut imzalı lisans dosyasının sonu. İmzalı payload içindedir. |

Signed license ömrü en fazla 30 gündür:

```text
ExpiryDate = min(IssuedAt + 30 gün, ValidUntil)
```

Bu nedenle renewal veya key rotation müşterinin gerçek lisans hakkını uzatamaz. Eski veriler için eklenen migration, güvenli varsayılan olarak `ValidUntil = ExpiryDate` kullanır.

### Renewal ve çevrimdışı kullanım

İstemcinin varsayılan renewal eşiği 7 gündür:

```text
ExpiryDate'e 7 günden fazla var
└── Geçerli cache kullanılmaya devam eder.

ExpiryDate'e 7 gün veya daha az var
└── POST /api/licenses/renew denenir.
```

Sunucuya ulaşılamazsa ve cache hâlâ geçerliyse istemci `ExpiryDate` anına kadar çevrimdışı çalışmaya devam eder. Geçerli cache yoksa veya cache süresi bittiyse lisans doğrulaması başarısız olur.

Varsayılan cache konumu:

```text
<LocalApplicationData>/LicenseManager/license.json
```

Windows'ta bu konum genellikle `%LOCALAPPDATA%\LicenseManager\license.json` olur. Background kontrolü varsayılan olarak 12 saatte bir çalışır.

### Block / unblock ve hata maskeleme

Admin lisansı block veya unblock yapabilir. Çevrimdışı client sunucudaki değişikliği anında göremeyeceği için block işlemi bir sonraki `generate` veya `renew` isteğinde etkili olur; daha önce alınmış imzalı cache kendi `ExpiryDate` değerine kadar çalışabilir.

Public `generate` ve `renew` akışlarında bulunamayan, engellenmiş veya gerçek süresi bitmiş lisanslar aynı genel sonuçla maskelenir:

```text
Lisans doğrulanamadı.
```

İstemci de imza, şirket, proje, süre, modül, bozuk dosya ve reddedilen istek gibi başarısızlıkları teknik ayrıntı sızdırmadan genel lisans hatasına dönüştürür.

## Private key güvenliği

### AES-256-GCM ve `IPrivateKeyProtector`

Yeni Ed25519 private key'ler veritabanına düz metin yazılmaz. Development/Demo ortamında `IPrivateKeyProtector` arkasındaki `AesGcmPrivateKeyProtector` kullanılır:

```text
Raw Ed25519 Private Key
        ↓
AES-256-GCM
        ↓
LMK1 header + 12-byte nonce + 16-byte tag + ciphertext
        ↓
PostgreSQL
```

- Her `Protect()` çağrısında kriptografik rastgele nonce üretilir.
- GCM authentication tag, ciphertext manipülasyonunu tespit eder.
- Master key Base64 formatında tam 32 byte olmalıdır.
- Master key kaynak kodda veya veritabanında tutulmamalıdır.
- Kullanılan key buffer'ları uygun noktalarda bellekten temizlenir.

### Legacy DPAPI → AES migration

Eski DPAPI kayıtlarını korumak için tek seferlik migration servisi bulunur. `PrivateKeyProtection:MigrateLegacyDpapi=true` olduğunda uygulama başlangıcında LMK1 formatında olmayan `Projects.PrivateKey` ve `ProjectKeys.PrivateKey` kayıtları aranır.

Migration:

1. Eski değeri DPAPI ile açar.
2. AES-256-GCM ile yeniden korur.
3. Yeni değeri tekrar açarak doğrular.
4. Tüm değişiklikleri transaction içinde kaydeder.

DPAPI yalnızca Windows'ta ve kaydı oluşturan aynı Windows kullanıcısı altında açılabildiği için bu geçiş Linux deployment'tan önce o kullanıcıyla yapılmalıdır. Önce veritabanı yedeği alınmalı; geçiş doğrulandıktan sonra `MigrateLegacyDpapi` kapatılmalıdır. Yeni anahtarlar hiçbir zaman DPAPI ile oluşturulmaz.

### Production KMS/Vault hazırlığı

Mimari `IPrivateKeyProtector` ile soyutlanmıştır. Development/Demo dışındaki ortamlarda önceden KMS/Vault tabanlı bir implementasyon kaydedilmemişse API bilinçli olarak başlatılmaz. Repository şu anda gerçek bir bulut KMS/Vault adapter'ı içermez; yalnızca bu geçişe hazır arayüz ve production guard içerir.

## Kurulum

### Gereksinimler

- .NET 9 SDK veya daha yeni uyumlu SDK
- PostgreSQL 17 veya uyumlu bir PostgreSQL sürümü
- İsteğe bağlı Docker Desktop
- Angular 22 ile uyumlu Node.js ve npm
- EF Core CLI (`dotnet-ef`)

Repository kökünde:

```powershell
dotnet restore .\LicenseManager.slnx
```

EF Core aracı kurulu değilse:

```powershell
dotnet tool install --global dotnet-ef --version 9.*
```

### PostgreSQL'i Docker ile başlatma

Aşağıdaki örnek development container'ı oluşturur:

```powershell
docker run --name postgres17 `
  -e POSTGRES_USER=postgres `
  -e POSTGRES_PASSWORD=CHANGE_ME `
  -e POSTGRES_DB=licenceDB `
  -p 5432:5432 `
  -d postgres:17
```

Daha sonraki kullanımlarda:

```powershell
docker start postgres17
```

Connection string'i gerçek parola ile User Secrets'a yazın:

```powershell
dotnet user-secrets set `
  "ConnectionStrings:DefaultConnection" `
  "Host=localhost;Port=5432;Database=licenceDB;Username=postgres;Password=CHANGE_ME" `
  --project .\LicenseManager.Api
```

### API User Secrets

Admin API key'i belirleyin:

```powershell
dotnet user-secrets set `
  "AdminSecurity:ApiKey" `
  "DEVELOPMENT-ADMIN-KEY" `
  --project .\LicenseManager.Api
```

32 byte AES master key üretip kaydedin:

```powershell
$masterKey = [Convert]::ToBase64String(
  [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32)
)

dotnet user-secrets set `
  "PrivateKeyProtection:MasterKey" `
  $masterKey `
  --project .\LicenseManager.Api
```

Development yapılandırmasında provider `AesGcm`, legacy migration ise `true` olarak tanımlıdır. Mevcut DPAPI verisi yoksa migration herhangi bir değişiklik yapmadan çıkar. API, geçerli 32 byte master key olmadan başlamaz.

Deployment ortamında eşdeğer environment variable adları:

```text
AdminSecurity__ApiKey
PrivateKeyProtection__MasterKey
ConnectionStrings__DefaultConnection
PrivateKeyProtection__Provider
PrivateKeyProtection__MigrateLegacyDpapi
```

### Migration ve API'yi çalıştırma

```powershell
dotnet ef database update `
  --project .\LicenseManager.Api\LicenseManager.Api.csproj `
  --startup-project .\LicenseManager.Api\LicenseManager.Api.csproj

dotnet run `
  --project .\LicenseManager.Api `
  --launch-profile https
```

Development adresleri:

```text
HTTPS API: https://localhost:7141
HTTP API:  http://localhost:5096
Swagger:   https://localhost:7141/swagger
```

Development CORS policy yalnızca Angular development origin'i olan `http://localhost:4200` için etkinleştirilmiştir. Production CORS politikası ayrıca ve izinli origin listesiyle tanımlanmalıdır.

## Angular Admin paneli

Kurulum ve çalıştırma:

```powershell
Set-Location .\LicenseManager.Admin
npm ci
npm start
```

Panel varsayılan olarak `http://localhost:4200` adresinde, API ise `https://localhost:7141` adresinde çalışır.

Route'lar:

```text
/login
/dashboard
/companies
/projects
/licenses
```

Mevcut ekranlar:

- **Login:** Admin API key'i alır. Key yalnızca sekme belleğinde tutulur; `localStorage` veya `sessionStorage` kullanılmaz.
- **Dashboard:** Şirket, proje, toplam/aktif/bloklu/süresi bitmiş lisans sayılarını; 7 günlük signed expiry ve 30 günlük gerçek expiry özetlerini; son lisansları ve yaklaşan bitişleri gösterir.
- **Companies:** Şirketleri listeler, yeni şirket oluşturur; boş ve mükerrer isim hatalarını gösterir.
- **Projects:** Projeleri oluşturur ve listeler; key geçmişini gösterir; pasif key hazırlama ve aktive etme işlemlerini yönetir.
- **Licenses:** Lisansları oluşturur ve listeler; şirket/proje/license key araması, active/blocked/expired filtresi, key maskeleme/kopyalama ve block/unblock işlemlerini sunar.

`adminApiKeyInterceptor` API isteklerine otomatik olarak `X-Admin-Api-Key` header'ını ekler. API `401` döndürürse bellek içindeki oturum temizlenir ve kullanıcı `/login` sayfasına yönlendirilir. Sayfa yenileme de bellek içi key'i temizlediği için yeniden giriş gerekir.

Şu özellikler mevcut sürümde tamamlanmış değildir ve bu README onları varmış gibi kabul etmez:

- tablo sıralama,
- pagination ve server-side pagination,
- gelişmiş tarih/modül filtreleri,
- ayrı Company/Project detay route'ları,
- production seviyesi kullanıcı/rol tabanlı authentication.

Angular production build kontrolü:

```powershell
npm run build
```

## Client yapılandırması

`LicenseManager.Client/appsettings.json` örneği:

```json
{
  "LicenseClient": {
    "ApiBaseUrl": "https://localhost:7141",
    "CompanyId": "COMPANY-GUID",
    "LicenseKey": "LM-...",
    "RequiredModule": "Reports",
    "RenewalThresholdDays": 7
  }
}
```

Gerçek license key'i kaynak dosyada tutmak yerine Client User Secrets veya environment variable ile override etmek önerilir:

```powershell
dotnet user-secrets set `
  "LicenseClient:LicenseKey" `
  "LM-..." `
  --project .\LicenseManager.Client
```

İlgili environment variable biçimi `LicenseClient__LicenseKey` şeklindedir. Aynı yöntem diğer `LicenseClient` alanlarında da kullanılabilir.

### Public key'leri build sırasında gömme

Önce projenin public key geçmişini admin endpointinden alın:

```text
GET /api/projects/{projectId}/keys
X-Admin-Api-Key: ...
```

Sonra repository kökündeki, git tarafından ignore edilen `LicenseKeys.local.props` dosyasını hazırlayın:

```xml
<Project>
  <PropertyGroup>
    <LicensePublicKey1>BASE64_PUBLIC_KEY_V1</LicensePublicKey1>
    <LicensePublicKey2>BASE64_PUBLIC_KEY_V2</LicensePublicKey2>
  </PropertyGroup>
</Project>
```

`LicenseManager.Client.csproj` bu değerleri assembly metadata olarak sırasıyla `LicensePublicKey_1` ve `LicensePublicKey_2` adlarıyla gömer. `LicensePublicKey1` yoksa build bilinçli olarak hata verir. V2 mevcut değilse V2 satırı çıkarılabilir.

Ayrıca `Configuration/EmbeddedLicenceKey.cs` içindeki `ProjectName` ve `ProjectVersion` sabitlerini dağıtılan ürünle eşleştirin. Client, başarılı imzadan sonra bu kimliği de doğrular.

Yeni key version'lar eklenecekse ilgili `LicensePublicKeyN` property ve `LicensePublicKey_N` assembly metadata satırı `.csproj` dosyasına eklenmelidir. Runtime loader sayısal suffix'e sahip birden fazla key'i destekler.

Client'ı derleyip çalıştırın:

```powershell
dotnet build .\LicenseManager.Client\LicenseManager.Client.csproj
dotnet run --project .\LicenseManager.Client
```

## API endpointleri

Admin endpointlerinde aşağıdaki header zorunludur:

```http
X-Admin-Api-Key: <configured-admin-key>
```

| Metot | Endpoint | Yetki | Açıklama |
|---|---|---|---|
| `GET` | `/api/dashboard` | Admin | Dashboard özetini, son lisansları ve yaklaşan bitişleri getirir. |
| `GET` | `/api/companies` | Admin | Şirketleri listeler. |
| `GET` | `/api/companies/{id}` | Admin | Şirket detayını ve lisans özetini getirir. |
| `POST` | `/api/companies` | Admin | Yeni şirket oluşturur. |
| `GET` | `/api/projects` | Admin | Projeleri listeler. |
| `GET` | `/api/projects/{id}` | Admin | Proje özetini ve aktif public key bilgisini getirir. |
| `GET` | `/api/projects/{projectId}/keys` | Admin | Public key geçmişini listeler; private key dönmez. |
| `POST` | `/api/projects` | Admin | Proje ve ilk aktif Ed25519 keypair'i oluşturur. |
| `POST` | `/api/projects/{projectId}/keys/prepare` | Admin | Yeni pasif key version hazırlar. |
| `POST` | `/api/projects/{projectId}/keys/{keyVersion}/activate` | Admin | Hazırlanmış key'i aktif eder. |
| `GET` | `/api/licenses` | Admin | Lisansları listeler. |
| `GET` | `/api/licenses/{licenseKey}` | Admin | GUID yerine **license key** ile minimal lisans detayı getirir. |
| `POST` | `/api/licenses/create` | Admin | Company, project, modules ve `valid_until` ile lisans kaydı oluşturur. |
| `POST` | `/api/licenses/{licenseId}/block` | Admin | Lisansı engeller. |
| `POST` | `/api/licenses/{licenseId}/unblock` | Admin | Lisans engelini kaldırır. |
| `POST` | `/api/licenses/generate` | Client/Public | Company ID ve license key ile güncel signed license üretir. |
| `POST` | `/api/licenses/renew` | Client/Public | Geçerli lisansın signed-license penceresini yeniler. |

Özellikle `GET /api/licenses/{licenseKey}` route'u ID değil license key kullanır. Response minimum veri yaklaşımıyla tüm `Company` nesnesini ve `Project.Id` alanını dönmez; proje adı, sürümü ve aktif key version gibi gereken detaylarla sınırlıdır.

`generate` ve `renew` request gövdesi:

```json
{
  "company_id": "COMPANY-GUID",
  "license_key": "LM-..."
}
```

Lisans oluşturma request gövdesi:

```json
{
  "company_id": "COMPANY-GUID",
  "project_id": "PROJECT-GUID",
  "modules": ["Reports", "Inventory"],
  "valid_until": "2027-12-31T23:59:59Z"
}
```

## Key rotation akışı

Rotation otomatik veya 7 günlük periyodik bir işlem değildir; admin tarafından gerektiğinde iki aşamada yönetilir.

1. **Prepare** çağrısı yeni Ed25519 keypair ve sonraki `key_version` kaydını `IsActive=false` olarak oluşturur.
2. Yeni public key client build yapılandırmasına eklenir.
3. Yeni client sürümü dağıtılır; böylece hem eski hem yeni public key istemcide bulunur.
4. **Activate** çağrısı eski key'i pasif, yeni key'i aktif yapar.
5. Bundan sonraki `generate` ve `renew` işlemleri yeni private key ile imzalanır.
6. Eski signed lisanslar, taşıdıkları `key_version` sayesinde eski public key ile `ExpiryDate` değerine kadar doğrulanır.

```text
Key v1 ACTIVE
    │
    ├── Prepare v2 → v2 INACTIVE
    │
    ├── PublicKey v2 client build'ine eklenir
    │
    └── Activate v2
          ├── v1 INACTIVE (eski doğrulamalar için public key korunur)
          └── v2 ACTIVE   (yeni imzalar)
```

Eski public key, onunla imzalanmış tüm lisansların kullanım penceresi bittikten sonra ve dağıtılmış istemciler dikkate alınarak kaldırılmalıdır.

## Testler

Tüm .NET testlerini çalıştırmak için:

```powershell
dotnet test .\LicenseManager.slnx
```

Yalnızca test projesini çalıştırmak için:

```powershell
dotnet test .\LicenseManager.Tests\LicenseManager.Tests.csproj
```

Test kapsamı arasında şunlar bulunur:

- canonicalization ve Ed25519 sign/verify,
- payload, tarih ve modül manipülasyonu,
- yanlış key ve bilinmeyen `key_version`,
- proje kimliği ve company binding,
- lisans oluşturma, generate ve renewal,
- 30 günlük lifetime ile `ValidUntil` sınırı,
- offline cache ve renewal grace,
- block/unblock ve hata maskeleme,
- key rotation ve tek aktif key kuralı,
- admin API key güvenliği,
- AES-256-GCM round-trip, nonce ve tamper kontrolleri,
- API response sözleşmeleri.

Son doğrulanan sonuç:

```text
Başarısız: 0
Başarılı:  81
Atlanan:   0
Toplam:    81
```

## Kısa demo akışı

1. PostgreSQL, API ve Angular panelini başlatın.
2. `/login` ekranında development admin API key ile giriş yapın.
3. **Companies** ekranından bir şirket oluşturun.
4. **Projects** ekranından bir proje oluşturun; ilk Ed25519 key v1 otomatik oluşur.
5. Public key v1'i `LicenseKeys.local.props` içine ekleyip Client'ı build edin.
6. **Licenses** ekranından şirket, proje, modüller ve `ValidUntil` seçerek lisans oluşturun.
7. Oluşan license key ve company ID'yi Client ayarlarına verip Client'ı çalıştırın.
8. `license.json` içindeki `expiry_date` veya `modules` alanını değiştirerek imza kontrolünün lisansı reddettiğini gösterin.
9. Project key v2'yi hazırlayın, public key v2'yi Client'a gömüp yeni build'i dağıtın ve ardından v2'yi aktive edin.
10. Renewal sonrası `key_version=2` kullanıldığını; eski v1 lisansın ise v1 public key ile doğrulanmaya devam ettiğini gösterin.
11. Lisansı block yapın ve sonraki online generate/renew isteğinin genel hata ile reddedildiğini gösterin.
