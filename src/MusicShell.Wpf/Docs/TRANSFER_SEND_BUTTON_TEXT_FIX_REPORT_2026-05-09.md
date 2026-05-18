# Transfer Gönder Butonu Yazı Kırpılması Düzeltmesi - 2026-05-09

## Sorun

Transfer ekranındaki gönder butonu dar kaldığı için `Personele Gönder` metni `personele G` gibi kırpılmış görünüyordu.

## Çözüm

- `TransferSendButtonText` sadeleştirildi.
- Alıcı seçili olsun veya olmasın buton metni `Gönder` olarak ayarlandı.
- Buton genişliği sabit dar `Width=104` değerinden çıkarıldı.
- `MinWidth=112`, daha dengeli padding ve açıklayıcı tooltip eklendi.
- İşlem sırasında buton metni `Aktarılıyor` olarak kalmaya devam eder.

## Etkilenen dosyalar

- `src/MusicShell.Wpf/MainWindow.xaml`
- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`
