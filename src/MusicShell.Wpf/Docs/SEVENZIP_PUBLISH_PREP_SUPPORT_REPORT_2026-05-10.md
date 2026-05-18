# 7-Zip Publish Hazırlık Desteği - 2026-05-10

## Amaç

7-Zip yalnızca geliştirici/publish hazırlığı aşamasında desteklendi. Son kullanıcı publish paketinde 7-Zip bağımlılığı oluşturulmadı.

## Yapılan Değişiklikler

- `setup_7zip_build_tool.ps1` eklendi.
- `setup_external_tools.ps1`, ImageMagick ve LibreOffice hazırlığından önce 7-Zip hazırlık kontrolünü çalıştıracak şekilde güncellendi.
- `setup_imagemagick_portable.ps1`, yerel `build_tools\7zip\7z.exe`, sistem 7-Zip ve NanaZip yollarını doğru sırayla arayacak şekilde güçlendirildi.
- `setup_libreoffice_portable.ps1`, LibreOffice PAF zip paketlerini mümkünse 7-Zip/NanaZip ile açacak şekilde güncellendi.
- `ProgramFiles(x86)` gibi PowerShell'de özel karakter içeren environment değişkenleri güvenli yöntemle okunacak hale getirildi.

## Davranış

- `7z*-x64.exe` installer dosyası `src\MusicShell.Wpf\Tools\Packages`, proje kökü, `Packages` veya kullanıcının `Downloads` klasöründe aranır.
- Installer bulunursa sadece publish hazırlığı için `build_tools\7zip` altına yerel kurulum denenir.
- `build_tools` klasörü `.csproj` publish içeriğine dahil değildir.
- Son kullanıcı `ToolBridge_publish_win-x64.zip` paketini açıp `ToolBridge.exe` çalıştırabilir; 7-Zip kurması gerekmez.
