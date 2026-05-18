# LibreOffice Portable Setup Fix Report — 2026-05-09

## Sorun
`setup_libreoffice_portable.ps1` PortableApps PAF kurulumunda hedef olarak doğrudan `Tools\LibreOfficePortable` klasörünü veriyordu. Bazı sistemlerde PAF installer bu klasörün altında tekrar `LibreOfficePortable` klasörü oluşturduğu için beklenen `App\libreoffice\program\soffice.exe` yolu bulunamıyordu.

Ek olarak `validate.ps1` ve `publish.ps1`, opsiyonel LibreOffice hazırlama adımı başarısız olduğunda tüm işlemi durdurabiliyordu.

## Düzeltme
- PAF installer hedefi `Tools` köküne alındı.
- İç içe `LibreOfficePortable\LibreOfficePortable` oluşursa otomatik normalize edilir.
- Kurulum sonrası `soffice.exe` için bekleme ve doğrulama eklendi.
- 7-Zip/NanaZip/7zz/7za varsa alternatif çıkartma denenir.
- Otomatik hazırlama başarısız olursa script manuel yönerge verir ve validate/publish akışını durdurmaz.
- `setup_external_tools.ps1` opsiyonel motor hatalarını warning olarak ele alacak şekilde güncellendi.

## Beklenen Yol
`src\MusicShell.Wpf\Tools\LibreOfficePortable\App\libreoffice\program\soffice.exe`

## Publish Sonrası Beklenen Yol
`publish\Tools\LibreOfficePortable\App\libreoffice\program\soffice.exe`
