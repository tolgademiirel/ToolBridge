# Publish EXE Name Fix Report - 2026-05-09

## Sorun

`MusicShell.Wpf.csproj` içinde AssemblyName `ToolBridge` olarak tanımlı olduğu için publish çıktısında oluşan EXE adı `ToolBridge.exe` olur.

Eski `publish.ps1` scripti yanlış şekilde `MusicShell.Wpf.exe` dosyasını kontrol ediyordu. Bu nedenle publish başarılı olsa bile script sonunda şu hatayı veriyordu:

```text
Publish failed. EXE was not created: publish\MusicShell.Wpf.exe
```

## Onarım

`publish.ps1` güncellendi.

Yeni script:

- Proje dosyasını otomatik bulur.
- `AssemblyName` değerini csproj içinden okur.
- Doğru EXE adını kontrol eder: `ToolBridge.exe`.
- Beklenen isim bulunamazsa publish klasöründeki herhangi bir EXE dosyasını güvenli şekilde yakalar.
- EXE hiç oluşmadıysa doğru hata verir.
- Konsol karakter bozulmasını önlemek için publish mesajlarında sade ASCII çıktı kullanır.

## Çalıştırılacak dosya

```text
C:\ToolBridge\publish\ToolBridge.exe
```
