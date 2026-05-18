# Transfer History / Convert Drag / Scroll Fix Report - 2026-05-09

## Yapılan düzeltmeler

1. Transfer Geçmişi bölümündeki `Transfer Geçmişini Temizle` butonunun metin kırpma problemi düzeltildi.
   - Buton `MinWidth` ile genişletildi.
   - İçerik ortalama davranışı netleştirildi.

2. Convert > Seçilen Dosyalar listesinden, tamamlanmış convert çıktıları sağdaki `Gönderilecek Çıktılar` alanına sürüklenebilir hale getirildi.
   - Tek çıktı sürükleme korunmuştur.
   - Tiklenmiş bir çıktıdan sürükleme başlatılırsa, tikli ve tamamlanmış tüm çıktı dosyaları birlikte sürüklenir.
   - Çıktısı henüz oluşmamış veya dosyası diskte bulunmayan satırlar sürükleme paketine dahil edilmez.

3. Convert sağ panelindeki `Gönderilecek Çıktılar` listesi sabit yükseklikli scroll alanına alındı.
   - Çok sayıda çıktı eklendiğinde sağ panel aşağı doğru uzamaz.
   - Mouse tekerleği ile liste içinde gezilebilir.

4. Convert sağ panelindeki temizleme butonu daha anlaşılır vektörel süpürge ikonu ile yenilendi.
   - İkon font karakteri değildir.
   - Gece/gündüz tema renklerine DynamicResource ile uyumludur.

## Kontrol sonucu

- MainWindow.xaml XML parse kontrolü başarılı.
- App.xaml XML parse kontrolü başarılı.
- ToolBridge.UI.xaml XML parse kontrolü başarılı.
- ZIP bütünlüğü üretim sırasında korunmuştur.

## Publish

```powershell
cd C:\ToolBridge
.\publish.ps1
```

Manuel:

```powershell
dotnet publish .\src\MusicShell.Wpf\MusicShell.Wpf.csproj -c Release -r win-x64 --self-contained true -o .\publish
```
