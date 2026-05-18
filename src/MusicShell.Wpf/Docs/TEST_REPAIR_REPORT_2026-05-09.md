# ToolBridge Test ve Onarım Raporu - 2026-05-09

## Test Kapsamı

- XAML parse kontrolü: App.xaml, MainWindow.xaml, Styles/ToolBridge.UI.xaml.
- Event handler bağlantıları: XAML içinde tanımlı event metotlarının MainWindow.xaml.cs içinde varlığı.
- Resource kontrolü: StaticResource ve DynamicResource referanslarının tanımlı kaynaklarla eşleşmesi.
- Command binding kontrolü: XAML komutlarının MainViewModel içindeki ICommand alanlarıyla eşleşmesi.
- Dosya bağımlılık kontrolü: SumatraPDF.exe, Docnet.Core.dll, pdfium.dll, ikon varlığı.
- Transfer, Convert, Yazdırma ve Ayarlar modüllerinde statik akış kontrolü.

## Yapılan Onarımlar

1. Uygulama geneli log sistemi eklendi.
   - Log klasörü: `%AppData%\ToolBridge\logs`
   - Beklenmeyen UI, arka plan görev ve uygulama hataları loglanır.

2. Transfer geçmişi kalıcı hâle getirildi.
   - Kayıt dosyası: `%AppData%\ToolBridge\transfer-history.json`
   - Uygulama kapanıp açılsa da transfer geçmişi korunur.

3. Transfer gönderme/kabul etme/yazdırma akışlarında kritik hatalar loglanır.

4. Transfer geçmişinden doğrudan yazdırma işleminde beklenmeyen hata yakalama eklendi.

5. `validate.ps1` doğrulama scripti eklendi.
   - Gerekli dosyaları kontrol eder.
   - .NET SDK varsa restore/build çalıştırır.

## Bilinen Sınırlar

- Bu ortam Linux tabanlı olduğu ve .NET SDK bulunmadığı için gerçek WPF runtime testi çalıştırılamadı.
- Gerçek yazıcı, Windows shell, Microsoft Office COM ve WPF UI etkileşimleri Windows makinede doğrulanmalıdır.
- Transfer akışı halen yerel/prototip simülasyon mantığıyla çalışır. Gerçek ağ aktarımı için ayrı TCP/UDP servis katmanı önerilir.

## Önerilen Windows Test Komutu

```powershell
cd C:\ToolBridge
.\validate.ps1
.\publish.ps1
```
