# Transfer Menü Yerleşimi ve İş Akışı Güncellemesi - 2026-05-09

## Yapılan düzenlemeler

1. Transfer menüsüne Dosya Gönder alanı eklendi.
   - Online personel seçimi sonrası tek veya çoklu dosya gönderilebilir.
   - Dosya seçme, liste temizleme ve seçili personele gönderme butonları eklendi.
   - Ctrl+V ve sürükle-bırak desteği Transfer menüsüne de bağlandı.

2. Transfer menüsüne Transfer Geçmişi alanı eklendi.
   - Gönderilen dosyalar tarih, yön, dosya adı, personel ve durum bilgisiyle listelenir.
   - Geçmişteki dosyalar açılabilir, Yazdırma havuzuna alınabilir veya doğrudan yazdırılabilir.

3. Transfer menüsü sağ paneline Ağdaki Personeller alanı eklendi.
   - Sağ panel diğer sekmelerle aynı yapıda çalışır.
   - Gece modu renk kaynaklarıyla uyumludur.
   - Sadece online kullanıcılar listelenir ve arama alanı kullanılabilir.

4. Transfer bildirimi görseli sadeleştirildi.
   - Online kullanıcı yanındaki turuncu bildirim noktası korunmuştur.
   - Fosforlu/parlayan efekt kaldırılmıştır.

5. Transfer izin kontrolü korundu.
   - Transfer alımı kapalı olan personele gönderim yapılamaz.
   - Gönderilen dosyalar hedef kullanıcının seçili transfer indirme klasörüne kopyalanır.

## Değiştirilen dosyalar

- `src/MusicShell.Wpf/MainWindow.xaml`
- `src/MusicShell.Wpf/MainWindow.xaml.cs`
- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`
- `src/MusicShell.Wpf/Models/OnlineUserItem.cs`
- `src/MusicShell.Wpf/Models/TransferHistoryItem.cs`

## Not

Bu ortamda .NET SDK bulunmadığı için gerçek build çalıştırılamadı. XAML XML parse kontrolü yapılmıştır.
