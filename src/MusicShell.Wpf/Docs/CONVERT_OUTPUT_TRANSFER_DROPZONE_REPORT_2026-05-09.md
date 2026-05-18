# Convert Çıktı Transferi Güncelleme Raporu - 2026-05-09

## İstek
Convert sayfasının sağ paneline, dönüştürülen çıktılar için sürükle-bırak kutusu eklenmesi; kutuya bırakılan çıktıların online kullanıcıya tek tıkla gönderilebilir olması.

## Yapılanlar
- Convert sağ paneline `Çıktı Transferi` bölümü eklendi.
- Bölüme, görsel referanstaki stile uygun kesik çizgili sürükle-bırak kutusu eklendi.
- Convert işlem listesindeki çıktı alanı artık çıktı dosyası oluştuysa sürüklenebilir.
- Çıktı dosyası kutuya bırakıldığında `Gönderilecek Çıktılar` listesine eklenir.
- Aynı çıktı ikinci kez eklendiğinde tekrar eklenmez.
- 50 MB üst limit kontrolü gönderim kutusu için de uygulanır.
- Gönderim kutusundaki dosyalar tek tek kaldırılabilir veya liste tamamen temizlenebilir.
- Sağ panelde online kullanıcı listesi eklendi.
- Online kullanıcıya tek tıklanınca dosyalar gönderim kuyruğuna alınır.
- Başarılı gönderim sonrası işlem, son işlemler ve kuyruk alanına yansıtılır.

## Teknik Not
Mevcut kaynakta gerçek zamanlı ağ transfer servisi veya kullanıcı-IP eşleştirme altyapısı bulunmadığı için tek tık işlemi güvenli bir `TransferOutbox` kuyruğuna bağlandı. Ağ transfer altyapısı eklendiğinde aynı komut noktası gerçek gönderim servisine yönlendirilebilir.

## Değiştirilen / Eklenen Dosyalar
- `src/MusicShell.Wpf/MainWindow.xaml`
- `src/MusicShell.Wpf/MainWindow.xaml.cs`
- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`
- `src/MusicShell.Wpf/Models/TransferFileItem.cs`
