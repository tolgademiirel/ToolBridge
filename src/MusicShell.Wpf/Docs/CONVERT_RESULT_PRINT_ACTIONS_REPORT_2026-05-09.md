# Convert Çıktı Aksiyonları Güncelleme Raporu - 2026-05-09

## Talep
Convert listesinde görünen kaldırma ikonunun çöp kutusu olarak düzeltilmesi, dönüştürülen çıktı için aynı aksiyon alanına doğrudan yazdırma ve yazdırma sayfasına aktarma butonlarının eklenmesi istendi.

## Yapılanlar
- Uygulama genelindeki yanlış/boş görünen kaldırma ikonu `Segoe MDL2 Assets` çöp kutusu ikonuyla değiştirildi.
- Convert işlem listesindeki her satıra üç aksiyon eklendi:
  - Ev ikonu: Çıktıyı yazdırma sayfasına aktarır ve yazdırma havuzuna ekler.
  - Yazıcı ikonu: Çıktıyı varsayılan yazıcıya doğrudan gönderir.
  - Çöp kutusu ikonu: Dosyayı convert listesinden kaldırır.
- Convert çıktıları için toplu işlem seçimi eklendi.
  - Satırdaki seçim kutusu işaretlenirse ev/yazıcı aksiyonu seçili tüm tamamlanmış çıktılara uygulanır.
  - Herhangi bir seçim yoksa aksiyon yalnızca tıklanan satırdaki çıktı için çalışır.
- Yazdırma havuzuna aktarılan dönüştürülmüş dosyalar, Yazdırma sayfasındaki mevcut dosya listesine düşer.
- Ev ikonuna basıldığında uygulama otomatik olarak Yazdırma sayfasına geçer.
- Doğrudan yazdırmada öncelik sırası:
  1. Uygulamada varsayılan işaretlenmiş yazıcı,
  2. Seçili yazıcı,
  3. Kayıtlı ilk yazıcı,
  4. Windows varsayılan yazıcısı.

## Değiştirilen dosyalar
- `src/MusicShell.Wpf/MainWindow.xaml`
- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`
- `src/MusicShell.Wpf/Models/ConvertFileItem.cs`
