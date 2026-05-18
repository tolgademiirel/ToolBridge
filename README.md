# ToolBridge WPF

ToolBridge, yazdırma, dosya transferi, convert ve ayar yönetimi için hazırlanmış modern .NET/WPF arayüz prototipidir.

## İçerik

- Kaynak kodlu WPF uygulama projesi
- MVVM benzeri yapı
- Harici NuGet bağımlılığı yok
- Örnek veri ile çalışan arayüz
- Sol menü, ana işlem alanı, işlem kuyruğu ve online kullanıcı bölümü

## Gereksinimler

- Windows 10/11
- Visual Studio 2022
- .NET 8 SDK

## Çalıştırma

```powershell
cd ToolBridge_WPF_Source
dotnet restore .\MusicShell.sln
dotnet build .\MusicShell.sln
cd .\src\MusicShell.Wpf
dotnet run
```

Alternatif olarak `MusicShell.sln` dosyasını Visual Studio ile açıp `F5` tuşuna basabilirsiniz.

## Publish

Önerilen publish yöntemi kaynak kökteki script üzerinden çalıştırmaktır:

```powershell
.\publish.ps1 -Runtime win-x64
```

DOCX/XLSX/PPTX dönüşümlerinde LibreOffice Portable da publish içine girsin istenirse:

```powershell
.\publish.ps1 -Runtime win-x64 -PrepareLibreOffice
```

7-Zip/NanaZip yoksa ve PortableApps kurucusunu sessiz fallback ile denemek gerekirse:

```powershell
.\publish.ps1 -Runtime win-x64 -PrepareLibreOffice -UseLibreOfficeInstallerFallback
```

7-Zip destekli hazırlık akışı:

```powershell
.\setup_7zip_build_tool.ps1
```

`7z*-x64.exe` dosyası `src\MusicShell.Wpf\Tools\Packages`, proje kökü, `Packages` veya `Downloads` altında bulunursa sadece publish hazırlığı için `build_tools\7zip` altına kurulur. Bu klasör publish zip'ine eklenmez; son kullanıcıdan 7-Zip kurulumu istenmez.


Çalıştırılacak dosya:

```text
.\publish\ToolBridge.exe
```

Script işlem sonunda ayrıca şu paketi üretir:

```text
ToolBridge_publish_win-x64.zip
```

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
```

## Son Güncelleme

- Üst mini oynatıcı/boş kontrol alanı kaldırıldı.
- Uygulama açılışta maksimize olacak şekilde ayarlandı.
- Sol menüdeki `Ara` kutusu ve `Arama` bölümü kaldırıldı.
- Menü sırası `Yazdırma`, `Transfer`, `Convert`, `Ayarlar` olarak güncellendi.
- Sol menüye `Online Kullanıcılar` bölümü eklendi.
- Sol menüye glow-menu mantığı uyarlandı.
- Menü öğeleri için aktif seçim, hover glow, item bazlı radial gradient ve ikon rengi eklendi.
- Yazdırma, Transfer, Convert ve Ayarlar için bağımsız vurgu/aktif durum davranışı hazırlandı.
- Ana içerik ve sağ panel ToolBridge kullanım senaryosuna göre güncellendi.


## v3 Güncellemesi

Bu sürümde sol menü, React/Tailwind tabanlı glow-menu davranışı WPF/XAML karşılığıyla yeniden düzenlendi.

- `activeItem` mantığı WPF tarafında `NavItem.IsActive` ile çalışır.
- Her menü öğesinde ayrı ikon rengi ve radial glow efekti bulunur.
- Hover sırasında seçili kart hissi, glow yayılımı ve cam/pill vurgusu oluşur.
- Seçim değişimi `SelectNavCommand` üzerinden yapılır.
- Harici NuGet paketi kullanılmadı; çözüm saf WPF kaynak koduyla çalışır.

## v4 Güncellemesi

Bu sürümde hover/aktif vurgu rengi menü öğesine özel hale getirildi.

- `Yazdırma`: turuncu fosforlu vurgu
- `Transfer`: mavi fosforlu vurgu
- `Convert`: kırmızı fosforlu vurgu
- `Ayarlar`: yeşil fosforlu vurgu
- Sabit turuncu border davranışı kaldırıldı; border rengi artık ilgili menü öğesinin `IconBrush` değerinden gelir.
- Radial glow opaklığı artırılarak hover sırasında renk daha net görünür hale getirildi.

## Button Tasarım Standardı

Uygulamadaki yeni buton gerektiren alanlarda `App.xaml` içindeki shadcn/21st.dev mantığına uyarlanmış WPF buton stilleri kullanılmalıdır:

- `ShadcnButtonStyle` - birincil aksiyon
- `ShadcnSecondaryButtonStyle` - ikincil aksiyon
- `ShadcnOutlineButtonStyle` - çizgili/nötr aksiyon
- `ShadcnGhostButtonStyle` - arka plansız hafif aksiyon
- `ShadcnDestructiveButtonStyle` - silme/tehlikeli aksiyon
- `ShadcnIconButtonStyle` - sadece ikon kullanılan butonlar

Bu stiller rounded-lg, hafif shadow, hover, pressed, disabled ve keyboard focus ring davranışlarını merkezi olarak yönetir.

## v10 Güncellemesi

Bu sürümde Yazdırma ekranına 21st.dev upload/download bileşeninin çalışma mantığı WPF/XAML tarafına uyarlanarak dosya yükleme alanı eklendi.

- Dosya yükleme kartı Yazdırma başlığının altına konumlandırıldı.
- Sürükle-bırak, Windows dosya seçici ve Ctrl+V ile panodan dosya ekleme desteği eklendi.
- Maksimum dosya boyutu 50 MB olarak ayarlandı.
- 50 MB üzerindeki dosyalar listeye alınmaz ve kullanıcıya uyarı gösterilir.
- Yüklenen dosya adı, dosya boyutu ve uzantısı listelenir.
- Tekil dosya kaldırma ve tüm listeyi temizleme aksiyonları eklendi.
- Yeni butonlarda merkezi `ShadcnButtonStyle` / `ShadcnGhostButtonStyle` standartları kullanıldı.


## v11 Güncellemesi

Bu sürümde Yazdırma ekranındaki yüklenen dosyalar listesi sabit yüksekliğe alındı.

- Dosya listesi `MaxHeight=280` olacak şekilde sınırlandı.
- Çok sayıda dosya eklendiğinde sayfa aşağıya doğru uzamaz.
- Liste kendi içinde sağdaki dikey kaydırma çubuğu ile gezilebilir.
- Yatay kaydırma kapalıdır; uzun dosya adları mevcut kart genişliği içinde kırpılır.

## v12 Güncellemesi

Bu sürümde Yazdırma ekranı başlık alanı sadeleştirildi.

- Yazdırma başlığı altındaki açıklama metni PDF/Word/Excel/PowerPoint/görsel dosya yükleme mantığını açıklayacak şekilde güncellendi.
- Sağ üstteki `Yeni İşlem` ve `Raporlar` butonları kaldırıldı.

## v14 - Cihazlar Bölümü

- Yazdırma ekranındaki **Hızlı İşlemler** başlığı **Cihazlar** olarak değiştirildi.
- Kartlar artık Windows sistemine ekli yazıcı adlarından otomatik beslenir.
- Yazıcı bulunamazsa arayüzün boş kalmaması için örnek olarak `RENKLİ` ve `SİYAH BEYAZ` cihazları gösterilir.

## v15 - Ayarlar / Manuel Yazıcı Yönetimi

Bu sürümde sol menüdeki **Ayarlar** sekmesi ayrı bir yazıcı yönetim ekranına bağlandı.

- **Ayarlar** seçildiğinde ana panelde `Kayıtlı Yazıcılar` sayfası açılır.
- Sağ panelde `Manuel Yazıcı Ekle` formu bulunur.
- Yazıcı adı, IP numarası ve opsiyonel Windows yazıcı kuyruğu bilgisi girilebilir.
- IP adresi doğrulaması yapılır; hatalı IP kayıt edilmez.
- Aynı yazıcı adı veya aynı IP ikinci kez eklenemez.
- Seçilen yazıcı `Varsayılan Yap` butonuyla varsayılan yazıcı olarak işaretlenir.
- Kayıtlar kullanıcı profilinde `%APPDATA%\ToolBridge\printers.json` dosyasında saklanır.
- Yazdırma ekranındaki **Cihazlar** kartları artık manuel kayıtlı yazıcılardan beslenir.

## v16 - Ayarlar Ekranı ve Input Düzeni

Bu sürümde Ayarlar ekranı sadeleştirildi ve manuel yazıcı formu 21st.dev/shadcn input-wrapper mantığının WPF karşılığıyla yeniden düzenlendi.

- Ana ayarlar kartı başlığı `Ayarlar` olarak değiştirildi.
- Seçili yazıcı aksiyonlarına `Cihazı Kaldır` butonu eklendi.
- Yazıcı silme işlemi `%APPDATA%\ToolBridge\printers.json` kaydını da günceller.
- Seçili yazıcı vurgu rengi sadeleştirildi; iç arka plan beyaz kaldı, dış turuncu border vurgusu korundu.
- Manuel yazıcı ekleme formunda merkezi input stilleri kullanıldı:
  - `InputWrapperBorderStyle`
  - `ShadcnTextBoxInputStyle`
  - `InputIconStyle`

## v17 - Aktif Menü Rengine Bağlı Sayfa Aksanı

Bu sürümde sayfa içindeki seçim, form ve buton vurguları aktif menü rengine bağlandı.

- Aktif menü rengi `MainViewModel.ActiveAccentBrush`, `ActiveAccentSoftBrush`, `ActiveAccentBorderBrush` ve `ActiveAccentRingBrush` ile merkezi hale getirildi.
- **Ayarlar** sekmesi seçildiğinde sayfa aksanı yeşil tona geçer.
- Manuel yazıcı ekleme formundaki `Yazıcı adı`, `IP numarası` ve `Windows yazıcı kuyruğu` alanlarının çerçeveleri aktif menü rengine göre görünür.
- Kayıtlı yazıcı seçimi yeşil dış vurgu ile gösterilir; iç arka plan sade beyaz kalır.
- `Varsayılan Yap` butonu ve ayarlar panelindeki ilgili aksiyonlarda focus/pressed durumunda aktif menünün fosforlu ring rengi kullanılır.
- İleride eklenecek kategori sayfalarında aynı aktif renk sistemi kullanılabilir.


## v18 Notları

- Ayarlar ekranında yeşil aksan yalnızca menü seçimi ve seçili yazıcı vurgusunda bırakıldı.
- Sağ ayarlar panelindeki Manuel Yazıcı Ekle kartı, shadcn Card / Input / Button düzenine benzer sade WPF kart yapısıyla yeniden düzenlendi.
- Transfer Alımı ve Görünüm kartları nötr beyaz/gri stile döndürüldü.


## v19 Notu
- Ayarlar sekmesinde sağ panel kartları, manuel yazıcı ekleme input çerçeveleri ve ilgili buton focus/outline vurguları aktif menü rengine bağlandı. Ayarlar ekranında bu aksan rengi yeşildir.

## v22 - Ayarlar sayfası görsel düzeltmeleri

- Varsayılan Yap butonunun normal durumda görünmemesine neden olan WPF template opacity sorunu giderildi.
- Ayarlar sayfasındaki tablo ve sağ paneldeki fosforlu/aktif çerçeve vurguları nötr stile çekildi.
- Manuel Yazıcı Ekle bölümündeki Yazıcıyı Kaydet butonunun form altında her durumda görünür olması sağlandı.
- Form inputlarının focus/hover çerçeveleri sade gri tonlara alındı.


## v23 Güncellemesi
- Varsayılan Yap butonu, Yazıcıyı Kaydet butonuyla aynı yeşil primary stile geçirildi.
- Transfer Alımı ve Gece Modu kontrolleri, Ark UI checkbox referansına uygun WPF checkbox stiline çevrildi.
- Ayarlar sayfasındaki ilgili XAML dosyaları kontrol edildi; React/TypeScript kodları projeye dahil edilmedi.

## v28 - Yazdırma butonu ve ayar paneli sadeleştirme

- Yazdırma ayarlarından `Parça sınırı` alanı kaldırıldı.
- Yazdırma ayarları kartının altındaki açıklama metni kaldırıldı.
- Yüklenen dosyalar başlığındaki `Listeyi Temizle` butonunun yanına `Yazdır` butonu eklendi.
- `Yazdır` butonu seçili yazıcı kuyruğuna yüklenen dosyaları göndermek için Windows `printto`/`print` shell komutlarını kullanır.
- Dosyanın yazdırılabilmesi için Windows üzerinde ilgili dosya türüne atanmış varsayılan uygulamanın yazdırma desteği olmalıdır.

## v31 - Doğrudan Yazdırma ve Kalıcı Ayarlar

- Yazdır butonu artık `UseShellExecute/printto` ile belgeyi görünür şekilde açmaya çalışmaz.
- PDF için önce SumatraPDF varsa sessiz yazdırma denenir; yoksa yazıcının IP adresine RAW 9100/PDF Direct Print gönderimi denenir.
- Word, Excel ve PowerPoint dosyalarında Microsoft Office kuruluysa uygulamalar görünmez modda kullanılarak yazdırma gönderilir.
- Görsel ve metin dosyaları WPF üzerinden seçili Windows yazıcı kuyruğuna doğrudan gönderilir.
- Yazdırma bittiğinde kullanıcıya sonuç bildirimi gösterilir.
- Renk, taraf, kopya, kağıt ebatı, kenar boşluğu, ölçeklendirme ve sayfa aralığı ayarları `%APPDATA%\ToolBridge\print-settings.json` dosyasına kaydedilir. Uygulama kapatılıp açıldığında son ayarlar korunur.

Not: PDF Direct Print/RAW 9100 yazdırma, yazıcının PDF veya ilgili dosya formatını doğrudan işleyebilmesine bağlıdır. Office belgeleri için Microsoft Office kurulumu gerekir.

## v32 - PDF Yazdırma ve Donma Sorunu Düzeltmesi

- Yazdırma işlemi UI thread üzerinden çıkarıldı; dosyalar arka planda STA thread ile sırayla yazdırılır.
- Çoklu doküman yazdırırken ekranın donmasına neden olan senkron yazdırma akışı düzeltildi.
- Yazdırma sırasında buton geçici olarak pasif olur ve metin `Yazdırılıyor` durumuna geçer.
- PDF yazdırma akışı genişletildi:
  - Önce uygulama klasörü / Tools klasörü / sistem kurulumu içinde SumatraPDF aranır.
  - SumatraPDF yoksa Adobe Reader/Acrobat komut satırı yazdırması denenir.
  - Ardından Windows varsayılan PDF uygulamasının `PrintTo` desteği denenir.
  - Son çare olarak PDF Direct destekli yazıcılar için RAW 9100 gönderimi denenir.
- Hata mesajları artık hangi PDF yazdırma yönteminin başarısız olduğunu detaylı gösterir.

PDF için en stabil sessiz yazdırma deneyimi istenirse `SumatraPDF.exe`, publish klasörüne doğrudan veya `Tools\SumatraPDF.exe` yoluna konulabilir.

## v33 - CloudConvert Benzeri Convert Motoru

- Convert hedef format listesi genişletildi: Archive, Audio, CAD, Document, Ebook, Font, Image, Presentation, Spreadsheet ve Video formatları eklendi.
- Kaynak format dosya uzantısından otomatik algılanır; kullanıcı yalnızca hedef formatı seçer.
- Dönüştürme işlemi UI thread dışında çalışır; çoklu dosyada ekran kilitlenmez.
- Uygulama yerel dönüştürme motorlarını sırayla dener: 7-Zip, LibreOffice, ImageMagick, FFmpeg, Calibre, Inkscape, FontForge ve Microsoft Office.
- Uygun motor yüklü değilse işlem başarısız olur ve hata detayında hangi motorun eksik olduğu gösterilir.
- Motorlar publish klasöründeki `Tools` dizininden, standart kurulum yollarından veya `PATH` değişkeninden aranır.
- Detaylı motor listesi için `src/MusicShell.Wpf/Docs/CONVERT_ENGINES.md` dosyasına bakılabilir.

## 2026-05-09 - Convert Çıktı Transferi
- Convert sağ paneline dönüştürülen çıktıların bırakılabileceği transfer kutusu eklendi.
- Çıktılar, kutuya sürüklendikten sonra online kullanıcıya tek tıkla gönderim kuyruğuna alınabilir.


## 2026-05-09 - Convert Transfer Sağ Panel Revizyonu
- Convert sağ panelindeki tekrar eden İşlem Takibi kartı kaldırıldı.
- Sağ panel yalnızca Çıktı Transferi ve Online Kullanıcılar akışına indirildi.
- Online kullanıcı kartına tıklayarak gönderim kuyruğuna alma davranışı korundu.
- Hedef kullanıcının transfer alımı kapalıysa gönderim komutu çalışmayacak şekilde kontrol eklendi.

## LAN Online Kullanıcılar

Demo online kullanıcılar kaldırılmıştır. ToolBridge çalışan bilgisayarlar aynı yerel ağda olduklarında UDP broadcast ile birbirini görür.

- UDP port: `47892`
- Uygulama açılınca kullanıcı online görünür.
- Uygulama kapanınca offline paketi gönderilir.
- Offline paketi alınamazsa kullanıcı yaklaşık 12 saniye içinde listeden düşer.
- Windows Firewall UDP broadcast trafiğini engellerse ToolBridge veya UDP `47892` için izin verilmelidir.

### Firewall izin komutu

Kurumsal ağda online kullanıcılar görünmüyorsa PowerShell'i yönetici olarak açıp şu script çalıştırılabilir:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\setup_firewall_toolbridge_presence.ps1
```
