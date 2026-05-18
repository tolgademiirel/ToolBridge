# ToolBridge Onarım Raporu - 09.05.2026

## Tarama Sonucu

- `App.xaml` ve `MainWindow.xaml` XML bütünlüğü kontrol edildi; yapısal bozulma bulunmadı.
- XAML içindeki `StaticResource` / `DynamicResource` referansları kontrol edildi; eksik resource bulunmadı.
- XAML event handler kontrolleri yapıldı; eksik handler bulunmadı.
- ViewModel komut bağları kontrol edildi; eksik `ICommand` bağı bulunmadı.
- WPF derlemesi bu ortamda `dotnet` yüklü olmadığı için çalıştırılamadı. Kaynak kod statik olarak tarandı ve onarıldı.

## Yapılan Onarımlar

### 1. PDF > JPG/JPEG/PNG/TIFF çok sayfa dönüştürme

- Docnet tarafında sayfa sayısı her ortamda doğru okunamayabiliyordu; bu nedenle çok sayfalı PDF çıktısı tek sayfada kalabiliyordu.
- `pdfium.dll` üzerinden native `FPDF_GetPageCount` okuması eklendi.
- Docnet reflection tarafı yedek yöntem olarak korundu.
- Çok sayfalı PDF çıktıları şu formatta üretilir:
  - `dosya.jpg`
  - `dosya_sayfa_002.jpg`
  - `dosya_sayfa_003.jpg`

### 2. PDF görsel çıktılarında okunabilirlik

- PDFium bazı sistemlerde alfa kanalını `0` döndürebildiği için yazılar beyaz zemine karışıp silik/okunmaz hâle gelebiliyordu.
- Alfa kanalı güvenilir değilse renk kanallarını doğrudan kullanan güvenli piksel işleme eklendi.
- PDF render çözünürlüğü yaklaşık A4 300 DPI seviyesine çıkarıldı: `2480 x 3508`.

### 3. Convert çift tıklama davranışı

- Convert listesindeki satırın yalnızca çıktı alanına değil, satır geneline çift tıklama desteği eklendi.
- Çıktı varsa çıktı dosyası, çıktı yoksa kaynak dosya hızlıca açılır.

### 4. Gece modu / üst başlık çubuğu

- Windows native başlık çubuğunun gece modu ile uyumlu çalışması için DWM dark title bar desteği eklendi.
- Bu işlem özel başlık çubuğu oluşturmaz; mevcut Windows başlık çubuğunu korur.

### 5. Gece modunda açık kalan rozetler

- Yazdırma geçmişi renk rozetleri gece moduna göre koyu arka planla üretilecek şekilde güncellendi.
- Ayarlar > yazıcı listesi renk rozetleri gece modunda açık renkli kalmayacak şekilde düzeltildi.

## Değiştirilen Dosyalar

- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`
- `src/MusicShell.Wpf/MainWindow.xaml.cs`
- `src/MusicShell.Wpf/MainWindow.xaml`
- `src/MusicShell.Wpf/Models/PrinterDeviceItem.cs`
- `src/MusicShell.Wpf/Docs/REPAIR_REPORT_2026-05-09.md`
- `publish.ps1`

## Notlar

- Uygulama WPF / `net8.0-windows` olduğu için macOS üzerinde doğrudan çalışmaz. Mac üzerinde test için Parallels, VMware Fusion, UTM veya uzak Windows makinesi gerekir.
- Publish çıktısı Windows hedefli alınmalıdır.
