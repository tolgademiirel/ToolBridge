# ImageMagick Portable Entegrasyon Raporu — 09.05.2026

## Amaç
Kullanıcının sağladığı `ImageMagick-7.1.2-21-portable-Q16-x64.7z` paketi ToolBridge içine portable motor olarak entegre edildi. Böylece kullanıcı bilgisayarına ayrıca ImageMagick kurulumu yapılmadan `magick.exe` uygulama klasöründen çalışabilir.

## Yapılanlar
- Portable paket `src/MusicShell.Wpf/Tools/ImageMagick/` klasörüne çıkarıldı.
- Eski kurulum dosyası ve `install_imagemagick.ps1` kaldırıldı.
- `MusicShell.Wpf.csproj` içine `Tools\ImageMagick\**\*` publish kuralı eklendi.
- Convert motor araması `Tools\ImageMagick\magick.exe` yolunu birinci öncelik olarak kullanacak şekilde güncellendi.
- Sistem Durumu ekranında ImageMagick artık `Portable ImageMagick hazır` bilgisiyle görünür.
- `RunHiddenProcess` çağrılarında çalışma dizini ilgili EXE klasörüne ayarlandı. Bu, portable ImageMagick XML/delegate dosyalarını daha güvenli bulmasını sağlar.
- `validate.ps1` içine `Tools\ImageMagick\magick.exe` kontrolü eklendi.

## Publish sonrası beklenen yol
```text
publish\Tools\ImageMagick\magick.exe
```

## Kullanıcı aksiyonu
Ek kurulum gerekmez. Publish alınca ImageMagick portable motor uygulamayla birlikte gelir.
