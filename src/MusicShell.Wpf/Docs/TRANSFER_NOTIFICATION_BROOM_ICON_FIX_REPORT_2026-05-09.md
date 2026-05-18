# Transfer Notification / Dropzone UI Fix Report - 2026-05-09

## Kapsam
Bu paket, Transfer sekmesinde bildirilen üç arayüz ve durum yönetimi problemini düzeltir.

## Yapılan Düzeltmeler

### 1. Dosya Gönder / Listeyi Temizle
- `Listeyi Temizle` buton genişliği artırıldı.
- Padding azaltıldı.
- Metnin karanlık ve aydınlık modda kırpılmadan okunması hedeflendi.

### 2. Dosya Gönder Sürükle-Bırak İkonu
- Yukarı ok görünümü kaldırıldı.
- Transfer sekmesindeki çift yönlü transfer simgesine uygun vektörel ikon eklendi.
- İkon kutusu daha küçük ve dengeli hale getirildi.

### 3. Gelen Transferler / Bildirimleri Temizle
- Yazılı `Bildirimleri Temizle` yerine vektörel süpürge ikonlu buton eklendi.
- Buton tooltip ile desteklendi.
- Bekleyen bildirim yoksa komut pasif kalır.
- Bildirimler temizlendiğinde:
  - Gelen transfer listesi temizlenir.
  - Açık transfer popup varsa kapatılır.
  - Sol menü ve online personel kartlarındaki turuncu bildirim noktaları temizlenir.

## Değiştirilen Dosyalar
- `src/MusicShell.Wpf/MainWindow.xaml`
- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`

## Kontrol
- `MainWindow.xaml` XML parse kontrolünden geçti.
- `App.xaml` XML parse kontrolünden geçti.
- `.NET SDK` bu ortamda bulunmadığı için gerçek WPF build/publish çalıştırılamadı.
