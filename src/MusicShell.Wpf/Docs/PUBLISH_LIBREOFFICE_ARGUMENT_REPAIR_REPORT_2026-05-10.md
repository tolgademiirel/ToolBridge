# Publish / LibreOffice Argüman Onarım Raporu - 2026-05-10

## Yapılan Kontrol

Kaynak ZIP içindeki publish, validate ve portable araç hazırlama scriptleri tarandı.

## Bulgu

`setup_libreoffice_portable.ps1` içinde `-UseInstallerFallback` desteği vardı ancak bu argüman `validate.ps1`, `publish.ps1` ve `setup_external_tools.ps1` üzerinden geçirilemiyordu. Ayrıca LibreOffice paketi yalnızca `.paf.zip` adıyla aranıyordu; doğrudan `.paf.exe` paket veya yanlışlıkla `.zip` uzantısı verilmiş PortableApps kurucusu senaryosu yeterince dayanıklı değildi.

## Düzeltme

- `publish.ps1` içine `-UseLibreOfficeInstallerFallback` eklendi.
- `validate.ps1` içine `-UseLibreOfficeInstallerFallback` eklendi.
- `setup_external_tools.ps1` içine `-UseLibreOfficeInstallerFallback` eklendi ve `setup_libreoffice_portable.ps1 -UseInstallerFallback` argümanına bağlandı.
- `setup_libreoffice_portable.ps1`, hem `.paf.zip` hem `.paf.exe` paketleri bulacak şekilde güncellendi.
- MZ başlıklı PortableApps kurucu dosyası `.zip` uzantısıyla gelirse geçici `.paf.exe` kopyası üzerinden işlenecek hale getirildi.

## Önerilen Komutlar

```powershell
.\validate.ps1 -PrepareLibreOffice -UseLibreOfficeInstallerFallback
.\publish.ps1 -PrepareLibreOffice -UseLibreOfficeInstallerFallback
```

7-Zip veya NanaZip kuruluysa installer fallback gerekmeyebilir:

```powershell
.\publish.ps1 -PrepareLibreOffice
```
