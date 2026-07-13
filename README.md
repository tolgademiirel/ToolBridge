# ToolBridge

ToolBridge, Windows üzerinde yazdırma, dosya transferi, belge dönüştürme ve yazıcı ayarları için geliştirilmiş modern .NET/WPF kurumsal araç uygulamasıdır.

Bu repo tek aktif uygulama çatısı altında tutulur. README içinde uzayan ayrı sürüm başlıkları kullanılmaz; güncel durum ve tüm ana yetenekler aşağıdaki bölümlerde birlikte anlatılır.

## Güncel Uygulama Özeti

- Yazdırma, Transfer, Convert ve Ayarlar modülleri tek WPF uygulamasında birleşir.
- Arayüz modern kurumsal tasarım sistemine sahiptir: sade navigasyon, açık/koyu tema, merkezi buton/input stilleri ve modül bazlı aksan renkleri.
- Yazdırma akışı arka planda çalışır; çoklu dosya yazdırırken arayüz kilitlenmez.
- PDF, Office belgeleri, görsel/metin dosyaları ve desteklenen dönüşüm çıktıları yazdırma havuzuna alınabilir.
- LAN üzerindeki ToolBridge kullanıcıları UDP broadcast ile birbirini görebilir ve dosya transferi yapabilir.
- Convert modülü yerel motorları kullanarak belge, görsel, arşiv, e-kitap, sunum, tablo, ses/video ve font dönüşümlerini dener.
- Yazıcı kayıtları, varsayılan yazıcı, yazdırma ayarları ve kullanıcı tercihleri kullanıcı profilinde saklanır.
- Business Central / BC fatura hot folder eklentisi uygulamadan kaldırılmıştır.

## Ana Modüller

### Yazdırma

- Dosya seçme, sürükle-bırak ve liste temizleme desteği vardır.
- Seçili yazıcıya PDF, Word, Excel, PowerPoint, görsel ve metin dosyaları gönderilebilir.
- PDF için önce SumatraPDF denenir; uygun değilse Adobe/Acrobat, Windows PrintTo veya PDF Direct Print akışlarına düşülür.
- Office belgelerinde Microsoft Office kuruluysa görünmez modda yazdırma denenir.
- Yazdırma işlemleri kuyruklanır, durum bilgisi ve geçmiş kaydı üretilir.

### Transfer

- Aynı yerel ağdaki ToolBridge kullanıcıları online listesinde görünür.
- Dosyalar hedef kullanıcıya gönderilebilir, gelen transferler kabul veya reddedilebilir.
- Transfer alımı kullanıcı tarafından kapatılabilir.
- Transfer geçmişindeki dosyalar açılabilir, yazdırma havuzuna alınabilir veya doğrudan yazdırılabilir.

### Convert

- Kaynak format dosya uzantısından algılanır; kullanıcı hedef formatı seçer.
- 7-Zip, LibreOffice, ImageMagick, FFmpeg, Calibre, Inkscape, FontForge ve Microsoft Office gibi yerel motorlar sırayla denenir.
- Eksik motorlar Sistem Durumu ekranında görülür ve hata mesajlarında hangi motorun eksik olduğu belirtilir.
- Dönüştürülen çıktılar transfer kutusuna alınabilir veya yazdırma akışına gönderilebilir.

### Ayarlar

- Manuel yazıcı ekleme, kaldırma ve varsayılan yazıcı seçme desteklenir.
- Yazıcı kayıtları `%APPDATA%\ToolBridge\printers.json` dosyasında saklanır.
- Yazdırma ayarları `%APPDATA%\ToolBridge\print-settings.json` dosyasında korunur.
- Transfer alımı ve koyu tema gibi kullanıcı tercihleri uygulama içinden yönetilir.

## Gereksinimler

- Windows 10/11
- .NET 8 SDK
- Visual Studio 2022 veya `dotnet` CLI

Opsiyonel araçlar:

- SumatraPDF: stabil sessiz PDF yazdırma
- LibreOffice Portable: DOCX/XLSX/PPTX ve belge dönüşümleri
- ImageMagick: gelişmiş görsel dönüşümleri
- 7-Zip: arşiv işlemleri ve publish hazırlığı

## Çalıştırma

```powershell
dotnet restore .\MusicShell.sln
dotnet build .\MusicShell.sln
dotnet run --project .\src\MusicShell.Wpf\MusicShell.Wpf.csproj
```

Alternatif olarak `MusicShell.sln` dosyasını Visual Studio ile açıp `F5` ile çalıştırabilirsiniz.

## Doğrulama

Kaynak kodun derlenebilir olduğunu ve temel dosya yapısının hazır olduğunu kontrol etmek için:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\validate.ps1 -SkipExternalTools
```

Harici araçları da hazırlamak isterseniz:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\setup_external_tools.ps1
```

LibreOffice Portable dahil hazırlık:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\setup_external_tools.ps1 -IncludeLibreOffice
```

## Publish

Önerilen publish yöntemi:

```powershell
.\publish.ps1 -Runtime win-x64
```

LibreOffice Portable da publish paketine eklensin istenirse:

```powershell
.\publish.ps1 -Runtime win-x64 -PrepareLibreOffice
```

7-Zip/NanaZip yoksa ve PortableApps kurucusu sessiz fallback ile denenmek istenirse:

```powershell
.\publish.ps1 -Runtime win-x64 -PrepareLibreOffice -UseLibreOfficeInstallerFallback
```

Publish sonrası çalıştırılacak dosya:

```text
.\publish\ToolBridge.exe
```

Script işlem sonunda ayrıca şu paketi üretir:

```text
ToolBridge_publish_win-x64.zip
```

## Docker API

WPF uygulaması Windows hedeflidir ve Linux container içinde çalıştırılmaz. Linux/Docker hedefi için ayrıca hafif `ToolBridge.Api` servisi bulunur.

```bash
docker compose up --build -d
curl http://SUNUCU_IP:8080/api/health
curl http://SUNUCU_IP:8080/api/status
curl http://SUNUCU_IP:8080/api/network
```

Port değiştirmek için:

```bash
TOOLBRIDGE_API_PORT=8090 docker compose up --build -d
```

Varsayılan compose ayarı düşük kaynak profiline göre hazırlanmıştır:

- `mem_limit: 512m`
- `cpus: 1.0`
- `DOTNET_GCServer=0`
- `DOTNET_GCHeapHardLimitPercent=35`

## Proje Yapısı

```text
MusicShell.sln
src/
  MusicShell.Wpf/
    App.xaml
    MainWindow.xaml
    Models/
    ViewModels/
    Infrastructure/
    Services/
    Styles/
    Tools/
  ToolBridge.Api/
    Program.cs
```

## LAN Online Kullanıcılar

ToolBridge çalışan bilgisayarlar aynı yerel ağda olduklarında UDP broadcast ile birbirini görür.

- UDP port: `47892`
- Uygulama açılınca kullanıcı online görünür.
- Uygulama kapanınca offline paketi gönderilir.
- Offline paketi alınamazsa kullanıcı yaklaşık 12 saniye içinde listeden düşer.
- Windows Firewall UDP broadcast trafiğini engellerse ToolBridge veya UDP `47892` için izin verilmelidir.

Firewall izin komutu:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\setup_firewall_toolbridge_presence.ps1
```

## Dokümantasyon Politikası

README, uygulamanın tek güncel özetidir. Geçmişe ait detaylı teknik raporlar `src\MusicShell.Wpf\Docs` altında referans olarak tutulabilir; ancak yeni özellikler README'de yeni sürüm başlıkları açılarak değil, mevcut modül başlıkları güncellenerek anlatılmalıdır.
