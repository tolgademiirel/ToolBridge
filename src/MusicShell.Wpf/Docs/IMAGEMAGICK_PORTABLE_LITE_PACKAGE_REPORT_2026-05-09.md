# ImageMagick Portable Lite Paket Raporu - 09.05.2026

Önceki tam entegre ZIP dosyası büyük olduğu için karşıya yükleme/dosya hazırlama hatası verebiliyordu.
Bu paket hafifletildi.

## Yapılan değişiklik

- 230 MB açılmış ImageMagick klasörü kaynak ZIP içine doğrudan konmadı.
- Kullanıcının verdiği portable `.7z` paketi `src/MusicShell.Wpf/Tools/Packages/` altına eklendi.
- `setup_imagemagick_portable.ps1` eklendi.
- `publish.ps1`, publish öncesinde bu setup scriptini çağıracak şekilde güncellendi.

## Çalışma mantığı

1. `setup_imagemagick_portable.ps1`, portable 7z paketini `src/MusicShell.Wpf/Tools/ImageMagick` içine çıkarır.
2. `magick.exe` oluşursa publish çıktısına dahil edilir.
3. Sistem Durumu ekranında ImageMagick hazır görünür.

## Manuel çözüm

Otomatik çıkarma olmazsa 7-Zip veya NanaZip ile şu dosyayı:

`src/MusicShell.Wpf/Tools/Packages/ImageMagick-7.1.2-21-portable-Q16-x64.7z`

şu klasöre çıkarın:

`src/MusicShell.Wpf/Tools/ImageMagick`

Beklenen dosya:

`src/MusicShell.Wpf/Tools/ImageMagick/magick.exe`
