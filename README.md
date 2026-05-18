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

---

## 🧪 Geliştirici Modunda Çalıştırma

```powershell
dotnet restore .\MusicShell.sln
dotnet build .\MusicShell.sln
dotnet run --project .\src\MusicShell.Wpf\MusicShell.Wpf.csproj
```

Alternatif olarak `MusicShell.sln` dosyasını Visual Studio 2022 ile açıp `F5` tuşuna basabilirsiniz.

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
├─ MusicShell.sln
├─ publish.ps1
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
- Uygulama açılınca kullanıcı online görünür.
- Uygulama kapanınca offline paketi gönderilir.
- Offline paketi alınamazsa kullanıcı kısa süre içinde listeden düşer.
- Windows Firewall UDP broadcast trafiğini engellerse ToolBridge veya UDP `47892` için izin verilmelidir.

Firewall izin scripti:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\scripts\setup_firewall_toolbridge_presence.ps1
```

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

## 🧭 Yol Haritası

- Daha gelişmiş hata raporlama ekranı
- Release notlarının otomatik üretilmesi
- Kurumsal dağıtım için MSI / installer paketi
- Merkezi ayar profili desteği
- Daha detaylı log görüntüleme arayüzü
- Mac/Linux desteği için alternatif UI araştırması

---

## 👤 Geliştirici

**Tolga Demirel**

- GitHub: [@tolgademiirel](https://github.com/tolgademiirel)
- E-posta: **tolgademiirel@gmail.com**

---

## 📄 Lisans

Bu proje kişisel ve kurumsal ihtiyaçlara göre geliştirilen özel bir masaüstü yardımcı uygulamasıdır. Lisans ve dağıtım koşulları proje sahibinin kararına bağlıdır.