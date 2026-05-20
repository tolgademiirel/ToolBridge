# ToolBridge

**ToolBridge**, kurumsal Windows ortamları için geliştirilen modern bir masaüstü yardımcı uygulamasıdır.

Uygulama; yazdırma, dosya dönüştürme, PDF birleştirme ve aynı yerel ağdaki kullanıcılar arasında dosya transferi gibi günlük operasyonel işleri tek arayüzde toplamayı hedefler.

> Hedef: Kurumsal IT süreçlerinde sık tekrarlanan doküman ve dosya işlemlerini sade, hızlı ve yönetilebilir hâle getirmek.

---

## 🚀 Öne Çıkan Özellikler

- PDF, Word, Excel, PowerPoint, görsel ve metin dosyaları için yazdırma akışları
- SumatraPDF destekli sessiz PDF yazdırma
- Microsoft Office / LibreOffice destekli Office belge işleme
- PDF, DOCX, XLSX, PPTX ve görsel formatlar arasında dönüştürme
- PDF birleştirme
- LAN üzerinden online kullanıcı keşfi
- Aynı ağdaki kullanıcılara dosya transferi
- Yazıcı kayıt, seçim ve varsayılan yazıcı yönetimi
- İş kuyruğu ve işlem durumu takibi
- Gece modu desteği
- Scrollbar görselleri gizlenmiş sade arayüz
- Full publish paketinde harici araç desteği

---

## 🧩 Kullanım Senaryoları

ToolBridge özellikle aşağıdaki iş akışları için tasarlanmıştır:

- Kurumsal yazdırma işlemlerini merkezi arayüzden yönetmek
- PDF ve Office dokümanlarını hızlıca dönüştürmek
- Birden fazla PDF dosyasını tek dosyada birleştirmek
- Aynı yerel ağdaki kullanıcılara dosya göndermek
- Sık kullanılan yazıcıları manuel kaydedip kolay seçim yapmak
- IT destek süreçlerinde küçük ama etkili otomasyonlar sağlamak

---

## 🛠️ Teknolojiler

- **.NET 8**
- **C#**
- **WPF**
- **XAML**
- **PowerShell**
- **Windows Printing APIs**
- **LAN UDP/TCP dosya transfer akışı**
- **Portable external tools**

---

## ⚙️ Harici Araç Desteği

Full publish paketinde aşağıdaki motorlar kullanılabilir:

| Araç | Kullanım Alanı |
|---|---|
| LibreOffice Portable | Office belge dönüştürme ve alternatif yazdırma akışı |
| SumatraPDF | Sessiz PDF yazdırma |
| ImageMagick | Görsel dönüştürme |
| Docnet.Core / pdfium | PDF işleme desteği |
| 7-Zip | Paket açma ve build hazırlık işlemleri |
| FFmpeg | Video / medya dönüştürme senaryoları |
| Calibre | E-kitap dönüştürme senaryoları |
| Inkscape | SVG / vektörel dönüşümler |
| FontForge | Font dönüşüm senaryoları |

> Harici araçlar repository içine normal commit edilmez. Full publish paketinde veya release asset olarak yönetilir.

Harici araçların beklenen yolları ve manuel doğrulama bilgileri için:

```text
tools-manifest.json
```

---

## 📦 Full Publish

Full Windows x64 paketi almak için proje kökünde PowerShell ile çalıştırın:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\publish.ps1 -Runtime win-x64
```

Başarılı işlem sonunda çalıştırılacak dosya:

```text
.\publish\ToolBridge.exe
```

Oluşan ZIP paketi:

```text
ToolBridge_Full_publish_win-x64.zip
```

Full publish paketinde `publish\Tools` klasörü de oluşur ve gerekli harici motorlar buradan çalışır.

Lite paket veya CI smoke test için:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\publish.ps1 -Runtime win-x64 -Lite -SkipExternalTools -NoStrictToolCheck
```

---

## 🧪 Geliştirici Modunda Çalıştırma

```powershell
dotnet restore .\ToolBridge.sln
dotnet build .\ToolBridge.sln
dotnet run --project .\src\MusicShell.Wpf\MusicShell.Wpf.csproj
```

Alternatif olarak `ToolBridge.sln` dosyasını Visual Studio 2022 ile açıp `F5` tuşuna basabilirsiniz.

> Not: Geriye uyumluluk için `MusicShell.sln` dosyası korunur. Yeni kullanımda önerilen çözüm dosyası `ToolBridge.sln` dosyasıdır.

---

## ✅ Gereksinimler

Geliştirme ortamı için:

- Windows 10 / Windows 11
- .NET 8 SDK
- Visual Studio 2022 veya uyumlu IDE
- PowerShell

Full publish hazırlığı için:

- LibreOffice Portable paketi
- ImageMagick portable paketi
- SumatraPDF.exe
- Docnet.Core.dll
- pdfium.dll
- 7-Zip / NanaZip

---

## 📁 Proje Yapısı

```text
ToolBridge/
├─ README.md
├─ ToolBridge.sln
├─ MusicShell.sln
├─ publish.ps1
├─ tools-manifest.json
├─ docs/
│  └─ ARCHITECTURE.md
├─ .github/
│  └─ workflows/
│     └─ dotnet-build.yml
├─ scripts/
│  ├─ setup_external_tools.ps1
│  ├─ setup_7zip_build_tool.ps1
│  ├─ setup_imagemagick_portable.ps1
│  ├─ setup_libreoffice_portable.ps1
│  └─ setup_firewall_toolbridge_presence.ps1
└─ src/
   └─ MusicShell.Wpf/
      ├─ Assets/
      ├─ Docs/
      ├─ Infrastructure/
      ├─ Models/
      ├─ Services/
      ├─ ViewModels/
      ├─ Tools/
      ├─ App.xaml
      └─ MainWindow.xaml
```

Kök dizinde yalnızca proje giriş dosyaları tutulur. Yardımcı PowerShell scriptleri `scripts/` klasörü altında toplanmıştır.

---

## 🌐 LAN Online Kullanıcılar

ToolBridge çalışan bilgisayarlar aynı yerel ağda olduklarında UDP broadcast ile birbirini görür.

- UDP port: `47892`
- TCP transfer portu: `47893`
- Uygulama açılınca kullanıcı online görünür.
- Uygulama kapanınca offline paketi gönderilir.
- Offline paketi alınamazsa kullanıcı kısa süre içinde listeden düşer.
- Windows Firewall UDP broadcast veya TCP transfer trafiğini engellerse ToolBridge için izin verilmelidir.

Firewall izin scripti:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\scripts\setup_firewall_toolbridge_presence.ps1
```

---

## 🧹 Transfer Staging Temizliği

Gelen transferler önce yerel uygulama veri klasöründeki staging alanına alınır. Uygulama açılışında 24 saatten eski staging klasörleri otomatik temizlenir.

İlgili servis:

```text
src/MusicShell.Wpf/Services/TransferStagingCleanupService.cs
```

---

## 🧱 Mimari Notlar

Bakım ve refactor planı için:

```text
docs/ARCHITECTURE.md
```

Bu dokümanda `MainViewModel` içindeki sorumlulukların servis bazlı ayrılması, PDF birleştirme stratejisi ve staging temizliği notları bulunur.

---

## 🔐 Repository Notları

Aşağıdaki içerikler repository içine commit edilmemelidir:

```text
publish/
ToolBridge_Full_publish_win-x64.zip
src/MusicShell.Wpf/Tools/LibreOfficePortable/
src/MusicShell.Wpf/Tools/ImageMagick/
src/MusicShell.Wpf/Tools/SumatraPDF.exe
src/MusicShell.Wpf/Tools/*.dll
build_tools/
```

Büyük uygulama paketleri için önerilen yöntem:

```text
GitHub Releases > Release asset olarak ZIP yükleme
```

---

## ✅ CI / Build Kontrolü

GitHub Actions workflow dosyası eklenmiştir:

```text
.github/workflows/dotnet-build.yml
```

Workflow; `ToolBridge.sln` üzerinden restore/build yapar ve lite publish smoke test çalıştırır.

---

## 🧭 Yol Haritası

- Daha gelişmiş hata raporlama ekranı
- Release notlarının otomatik üretilmesi
- Kurumsal dağıtım için MSI / installer paketi
- Merkezi ayar profili desteği
- Daha detaylı log görüntüleme arayüzü
- Mac/Linux desteği için alternatif UI araştırması
- `MainViewModel` sorumluluklarının servis bazlı ayrılması
- PDF birleştirme için kayıpsız merge yönteminin ana akışa alınması

---

## 👤 Geliştirici

**Tolga Demirel**

- GitHub: [@tolgademiirel](https://github.com/tolgademiirel)
- E-posta: **tolgademiirel@gmail.com**

---

## 📄 Lisans

Bu proje kişisel ve kurumsal ihtiyaçlara göre geliştirilen özel bir masaüstü yardımcı uygulamasıdır. Lisans ve dağıtım koşulları proje sahibinin kararına bağlıdır.
