# PDF/JPG Siyah Zemin Dönüştürme Onarım Raporu - 2026-05-09

## Sorun
PDF, DOC/DOCX veya benzeri belgeler JPG/JPEG/PNG formatına dönüştürüldüğünde bazı sayfalarda çıktı zemini siyah oluşuyor ve metinler okunamaz hale geliyordu.

## Teknik sebep
Docnet `GetImage()` çıktısı bazı PDFium sürümlerinde BGRA tamponundaki alfa/arka plan bilgisini güvenilir döndürmüyor. Beyaz sayfa zemini bazı durumlarda `B=0, G=0, R=0, A=0` olarak geldiği için JPEG encoder bunu siyah zemin olarak kaydediyordu.

## Yapılan onarım
- PDF görsel dönüştürmede öncelikli yol olarak native PDFium render eklendi.
- `FPDFBitmap_Create` ile bitmap oluşturuluyor.
- Sayfa render edilmeden önce bitmap `FPDFBitmap_FillRect(..., 0xFFFFFFFF)` ile beyaz zemine boyanıyor.
- PDF sayfası beyaz zemin üzerine `FPDF_RenderPageBitmap` ile işleniyor.
- BGRA buffer güvenli şekilde BGR24 `BitmapSource` haline getiriliyor.
- Çok sayfalı PDF çıktısı korunuyor: ilk sayfa ana dosya adına, diğerleri `_sayfa_002`, `_sayfa_003` formatında kaydediliyor.
- Sayfa oranı bozulmasın diye render ölçüsü PDF sayfa boyutuna göre hesaplanıyor ve maksimum boyut kontrollü uygulanıyor.
- Native PDFium başarısız olursa eski Docnet yolu yedek olarak devam ediyor; yedek yolda alfa 0 + siyah piksel problemi de beyaz zeminle korunacak şekilde iyileştirildi.

## Etkilenen akışlar
- PDF -> JPG/JPEG/PNG/BMP/GIF/TIFF
- DOC/DOCX/ODT/RTF/PPT/PPTX -> PDF ara çıktısı -> JPG/JPEG/PNG
- Çok sayfalı PDF görsel çıktıları

## Değiştirilen dosya
- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`

## Kontrol
- XAML parse gerektiren dosyalara dokunulmadı.
- C# parantez dengesi kontrol edildi.
- ZIP bütünlük testi yapılacaktır.
