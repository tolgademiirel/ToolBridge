# Final Full Publish Hazırlık Raporu - 10.05.2026

## Amaç

Son kullanıcıya sunulacak pakette temel motorların eksik kalmasını önlemek.

## Yapılanlar

- `publish.ps1` full modu varsayılan hale getirildi.
- Full modda LibreOffice zorunlu doğrulamaya alındı.
- `-Lite` parametresi eklendi; küçük paket için LibreOffice opsiyonel bırakıldı.
- `-DownloadLibreOfficeIfMissing` parametresi eklendi.
- Publish sonrası `Tools\LibreOfficePortable\...\soffice.exe` kontrolü eklendi.
- Publish sonrası `Tools\ImageMagick\magick.exe` kontrolü eklendi.
- ZIP oluşturma 7-Zip varsa 7-Zip ile yapılacak şekilde korunmuştur.
- `build_full_publish.bat` ve `build_lite_publish.bat` eklendi.

## Beklenen full çıktı

`ToolBridge_Full_publish_win-x64.zip`

## Beklenen lite çıktı

`ToolBridge_Lite_publish_win-x64.zip`
