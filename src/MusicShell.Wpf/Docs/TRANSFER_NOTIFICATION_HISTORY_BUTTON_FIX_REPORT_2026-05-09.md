# Transfer Notification and History Button Fix Report - 2026-05-09

## Kapsam

Bu paket, Transfer sayfasından dosya gönderimi sonrasında online kullanıcı bildirim göstergesinin görünmemesi ve Transfer Geçmişi başlığındaki temizleme ikonunun istenen tipografik yapıya uymaması sorunlarını düzeltir.

## Düzeltilenler

1. Transfer sayfasından bir personele dosya gönderildiğinde, ilgili personelin yanında turuncu transfer bildirim noktası görünür.
2. Gönderim işlemi Transfer sekmesi açıkken yapıldığında bildirim artık aynı anda temizlenmez.
3. Gelen transfer bildirimi açıldığında, kabul edildiğinde veya reddedildiğinde ilgili gönderici/alıcı bildirimi temizlenir.
4. Transfer sekmesine geçişte yalnızca gerçekten mevcut kullanıcıya gelen transferlerin bildirimi görüldü kabul edilir. Kullanıcının başka personele gönderdiği transfer bildirimi yanlışlıkla temizlenmez.
5. Transfer Geçmişi başlığındaki vektörel temizlik ikonu kaldırıldı.
6. İkon yerine uygulamanın yazı stiline uygun `Geçmişi Temizle` butonu eklendi.
7. Buton genişliği metni kırpmayacak şekilde ayarlandı.

## Değiştirilen Dosyalar

- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`
- `src/MusicShell.Wpf/MainWindow.xaml`

## Statik Kontrol

- `MainWindow.xaml` XML parse kontrolü başarılı.
- `App.xaml` XML parse kontrolü başarılı.
- `ToolBridge.UI.xaml` XML parse kontrolü başarılı.
- `MainViewModel.cs` kaba parantez dengesi başarılı.

## Not

Bu ortamda Windows WPF runtime ve .NET SDK ile gerçek uygulama başlatma testi yapılamadı. Windows tarafında son kontrol için `validate.ps1` ve ardından `publish.ps1` çalıştırılmalıdır.
