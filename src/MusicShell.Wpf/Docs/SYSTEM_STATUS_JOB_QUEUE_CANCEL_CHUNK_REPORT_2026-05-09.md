# ToolBridge - Sistem Durumu, Job Queue, İptal ve Parçalı Transfer Onarım Raporu

Tarih: 09.05.2026

## Uygulanan Geliştirmeler

### 1. Convert motorları için Sistem Durumu ekranı
Ayarlar sağ paneline **Sistem Durumu** bölümü eklendi. Bu bölüm aşağıdaki motorları kontrol eder:

- Dahili görsel dönüştürücü
- PDFium / Docnet
- SumatraPDF
- LibreOffice
- ImageMagick
- Microsoft Office COM otomasyonu

Her motor için **Hazır / Eksik** durumu, kullanım amacı ve dosya yolu / eksik bilgi gösterilir. `Yenile` butonu ile durum yeniden taranabilir.

### 2. Merkezi İş Kuyruğu eklendi
Yeni model:

- `Models/OperationJobItem.cs`

Ayarlar sağ paneline **İş Kuyruğu** alanı eklendi. Yazdırma, Convert ve Transfer işlemleri bu kuyruğa iş olarak eklenir. Aynı anda uzun işlem çakışmasını azaltmak için işlemler `SemaphoreSlim` ile sıraya alınır.

### 3. Her uzun işlem için iptal butonu
İş Kuyruğu satırlarına **İptal** butonu eklendi. Aşağıdaki işlemler cancellation token ile kontrol edilir:

- Convert işlemleri
- Yazdırma işlemleri
- Convert çıktısını yazdırma
- Transfer gönderimi
- Gelen transfer kabul/indirme
- Transfer geçmişinden doğrudan yazdırma

Not: Harici motorlar veya Windows yazdırma kuyruğu çalışmaya başladıktan sonra bazı işlemler Windows tarafında tamamlanabilir; uygulama iptal isteğini sonraki güvenli durakta işler.

### 4. 50 MB üstü transfer desteği
Transfer dosyalarında 50 MB üst limit kaldırıldı. 50 MB üzerindeki dosyalar artık:

- Parçalı aktarım modunda işaretlenir.
- 4 MB bloklarla kopyalanır.
- SHA256 checksum hazırlanır.
- Alıcı kabul ettiğinde hedef dosya checksum ile doğrulanır.

### 5. Checksum doğrulama bilgisi
Transfer dosya modeline aşağıdaki alanlar eklendi:

- `SourceChecksum`
- `VerifiedChecksum`
- `ChecksumText`
- `TransferModeText`
- `IsLargeTransfer`

Gelen transfer popup dosya listesinde artık aktarım modu ve checksum durumu da gösterilir.

### 6. Log ve hata güvenliği
Kuyruk içindeki hatalar ilgili iş satırına yazılır ve uygulama log sistemine gönderilir.

## Değiştirilen / Eklenen Dosyalar

- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`
- `src/MusicShell.Wpf/Models/OperationJobItem.cs`
- `src/MusicShell.Wpf/Models/SystemStatusItem.cs`
- `src/MusicShell.Wpf/Models/TransferFileItem.cs`
- `src/MusicShell.Wpf/MainWindow.xaml`
- `src/MusicShell.Wpf/Docs/SYSTEM_STATUS_JOB_QUEUE_CANCEL_CHUNK_REPORT_2026-05-09.md`

## Kontrol Sonucu

- `MainWindow.xaml` XML parse: başarılı
- `App.xaml` XML parse: başarılı
- `ToolBridge.UI.xaml` XML parse: başarılı
- XAML event handler kontrolü: eksik yok
- Command binding kontrolü: kritik eksik yok
- StaticResource kontrolü: eksik yok
- C# kaba parantez kontrolü: başarılı

## Publish

```powershell
cd C:\ToolBridge
.\publish.ps1
```

Script engellenirse:

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\publish.ps1
```

Manuel publish:

```powershell
dotnet publish .\src\MusicShell.Wpf\MusicShell.Wpf.csproj -c Release -r win-x64 --self-contained true -o .\publish
```
