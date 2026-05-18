# LibreOffice Portable Lite Entegrasyon Raporu - 09.05.2026

## Amaç
LibreOffice Portable motorunu kaynak ZIP'i aşırı büyütmeden ToolBridge'e entegre etmek.

## Uygulanan yöntem
- LibreOffice Portable açılmış hali kaynak ZIP içine konmadı.
- `setup_libreoffice_portable.ps1` eklendi.
- Script `LibreOfficePortable_26.2.1_MultilingualStandard.paf.zip` paketini proje kökü, `Tools\Packages` veya kullanıcının Downloads klasöründe arar.
- Paket bulunursa PAF içindeki kurulum dosyasını sessiz modda `src\MusicShell.Wpf\Tools\LibreOfficePortable` klasörüne kurar.
- Publish sırasında `Tools\LibreOfficePortable\**\*` çıktıya dahil edilir.

## Beklenen motor yolu
`src\MusicShell.Wpf\Tools\LibreOfficePortable\App\libreoffice\program\soffice.exe`

Publish sonrası:
`publish\Tools\LibreOfficePortable\App\libreoffice\program\soffice.exe`

## Kod tarafı
- `MainViewModel.FindLibreOfficeExecutable()` portable yolu birinci öncelik olarak arar.
- `SystemStatusService` portable LibreOffice'i Sistem Durumu ekranında gösterir.
- `validate.ps1` ve `publish.ps1` opsiyonel portable motorları otomatik kontrol eder.

## Not
Kaynak ZIP'in hafif kalması için 214 MB LibreOffice paketi gömülmedi. Paketin kullanıcı makinesinde proje köküne veya `Tools\Packages` klasörüne konması yeterlidir.
