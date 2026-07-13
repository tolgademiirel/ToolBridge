# ToolBridge — Başka Bilgisayara Kurulum Kılavuzu

## Özet: Kurulum sihirbazı YOK, kopyala-çalıştır

ToolBridge **self-contained** derlenir — yani kendi .NET 8 çalışma zamanını (runtime) içinde taşır.
Hedef bilgisayarda **.NET kurmanıza, setup sihirbazı çalıştırmanıza gerek yoktur.**

### Adımlar

1. `ToolBridge_Full_publish_win-x64.zip` dosyasını hedef bilgisayara kopyalayın.
2. Zip'i bir klasöre çıkarın (örn. `C:\ToolBridge`).
   > Önemli: **Tüm klasörü** çıkarın. `ToolBridge.exe`'yi tek başına kopyalamayın —
   > yanındaki DLL'ler ve `Tools\` klasörü olmadan çalışmaz.
3. `ToolBridge.exe`'ye çift tıklayın.
4. (İsteğe bağlı) Sağ tık → "Kısayol oluştur" ile masaüstüne kısayol ekleyin.

Klasörü olduğu gibi taşıyabilir, USB ile kopyalayabilir veya ağ paylaşımına koyabilirsiniz;
yol bağımlılığı yoktur (taşınabilir/portable çalışır).

## "Sadece exe'yi kopyalasam olmaz mı?" → Hayır

`ToolBridge.exe` yalnızca ~0,2 MB'lık bir başlatıcıdır. Tek başına:
- Yanındaki .NET DLL'leri olmadan **hiç açılmaz**,
- `Tools\` klasörü olmadan **açılır ama dönüştürme ve sessiz PDF yazdırma çalışmaz**.

Bu yüzden her zaman **klasörün tamamını** dağıtın.

## Neden bu kadar büyük?

Boyutun ~%89'u, kurulum gerektirmeden çalışsın diye pakete gömülen dönüştürme motorlarıdır:

| Motor | Ne için |
|---|---|
| LibreOffice Portable | Word/Excel/PowerPoint → PDF dönüşümü ve Office dosyası yazdırma |
| ImageMagick | Görsel format dönüşümleri |
| SumatraPDF | Sessiz (arka planda) PDF yazdırma |
| 7-Zip + pdfium | Arşiv dönüşümü ve dahili PDF işleme |

Bu araçlar hedef bilgisayarda **kurulu olmadığı** için pakete dahil edilir; kurumsal
dağıtımda "eksik tool" sorununu tamamen ortadan kaldırır.

## Ağ / Firewall (LAN transferi ve online kullanıcılar için)

Aynı yerel ağdaki ToolBridge kullanıcılarının birbirini görmesi için:
- UDP **47892** (kullanıcı keşfi / presence)
- TCP **47893** (dosya transferi)

Windows Firewall bunları engellerse, hedef bilgisayarda yönetici PowerShell'de:

```powershell
powershell.exe -ExecutionPolicy Bypass -File .\setup_firewall_toolbridge_presence.ps1
```

> Not: Bu betik zip içinde değildir; gerekiyorsa proje kökünden hedefe ayrıca kopyalayın.
> LAN transferi kurulum için zorunlu değildir — yalnızca çok-kullanıcılı senaryoda gerekir.

## Sistem gereksinimleri

- Windows 10 / 11 (64-bit)
- Ek yazılım gerekmez (.NET dahildir)
- Yazdırma için: hedef ağdaki yazıcılar uygulama içinde **Ayarlar → Manuel Yazıcı Ekle** ile tanıtılır.
