# Vector Icon Render Fix - 2026-05-09

## Sorun

Convert ve transfer listelerindeki işlem butonları `Segoe MDL2 Assets` fontundaki Unicode glifleriyle çiziliyordu. Bazı sistemlerde veya WPF render zincirinde font doğru uygulanmadığında bu glifler küçük kare/boş kutu olarak görünüyordu.

## Çözüm

Font glifine bağlı buton içerikleri kaldırıldı ve yerine WPF `Viewbox + Path` tabanlı vektörel ikonlar kullanıldı. Böylece ikonlar işletim sistemi fontuna bağlı kalmadan her makinede doğru render edilir.

## Güncellenen alanlar

- Dosya kaldırma butonları çöp kutusu vektör ikonu oldu.
- Convert çıktı satırındaki Yazdırma sayfasına gönder butonu ev vektör ikonu oldu.
- Convert çıktı satırındaki direkt yazdırma butonu yazıcı vektör ikonu oldu.
- Convert transfer listesi kaldırma butonu çöp kutusu vektör ikonu oldu.

## Değiştirilen dosya

- `src/MusicShell.Wpf/MainWindow.xaml`
