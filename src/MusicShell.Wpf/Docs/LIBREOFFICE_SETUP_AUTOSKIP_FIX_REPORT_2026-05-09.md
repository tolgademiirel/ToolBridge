# LibreOffice Portable Auto-Setup Fix - 2026-05-09

## Sorun
`validate.ps1` ve `publish.ps1`, LibreOffice Portable paketini her çalışmada otomatik hazırlamaya çalışıyordu. PortableApps PAF kurucusu bazı makinelerde sessiz kurulum sırasında bekleyebildiği için publish/validate süreci takılı gibi görünüyordu.

## Düzeltme
- `setup_external_tools.ps1` artık varsayılan olarak yalnızca ImageMagick Portable hazırlığını yapar.
- LibreOffice Portable otomatik hazırlanmaz; isteğe bağlıdır.
- `validate.ps1` ve `publish.ps1` içine `-PrepareLibreOffice` ve `-SkipExternalTools` anahtarları eklendi.
- `setup_libreoffice_portable.ps1`, 7-Zip/NanaZip yoksa PAF kurucusunu varsayılan olarak çalıştırmaz.
- Kurucuyu denemek için ayrıca `-UseInstallerFallback` kullanılabilir.

## Önerilen kullanım
```powershell
cd C:\ToolBridge
.\validate.ps1
.\publish.ps1
```

LibreOffice hazırlamak için:
```powershell
.\setup_libreoffice_portable.ps1
```

LibreOffice kurucusunu zorlamak için:
```powershell
.\setup_libreoffice_portable.ps1 -UseInstallerFallback
```
