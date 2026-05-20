# ToolBridge Architecture Notes

Bu dokuman, ToolBridge kod tabaninin daha kolay bakilabilir hale gelmesi icin hedef mimariyi ozetler.

## Mevcut durum

Uygulama tek WPF projesi icinde calisir. UI tarafi `MainWindow.xaml`, uygulama davranisinin buyuk bolumu ise `MainViewModel` uzerinden yonetilir.

Ana kabiliyetler:

- Yazdirma islemleri
- Dosya donusturme
- PDF birlestirme
- LAN kullanici kesfi
- LAN dosya transferi
- Yazici kayit ve varsayilan yazici yonetimi
- Is kuyrugu ve islem durum takibi
- Kullanici ayarlari ve gecmis kayitlari

## Hedef ayrim

`MainViewModel` zamanla sadelestirilmelidir. Asagidaki servis ve koordinatörlere ayrim onerilir:

| Alan | Onerilen bilesen |
|---|---|
| Yazdirma | `PrintService` |
| Donusturme | `ConversionService` |
| PDF birlestirme | `PdfMergeService` |
| LAN presence | `LanPresenceService` |
| LAN dosya transfer koordinasyonu | `TransferCoordinator` |
| Transfer staging temizligi | `TransferStagingCleanupService` |
| Ayar kayit ve yukleme | `SettingsStore` |
| Is kuyrugu | `JobQueueService` |
| Harici arac kesfi | `ExternalToolLocator` |

## Oncelikli refactor plani

1. Dosya sistemi ve staging temizligi ViewModel disina tasinmali.
2. PDF birlestirme algoritmasi `PdfMergeService` altinda izole edilmeli.
3. Yazdirma islemleri ve yazici cozumleme `PrintService` icine alinmali.
4. Donusturme motoru secimi `ConversionService` ve `ExternalToolLocator` ile ayrilmali.
5. ViewModel sadece binding state, komut tetikleme ve UI mesajlarini yonetmeli.

## PDF birlestirme notu

Mevcut PDF birlestirme akisi, PDF sayfalarini render ederek yeni PDF icine gorsel olarak yazar. Bu yontem pratik bir fallback saglar ancak metin secilebilirligi, linkler, bookmark yapisi ve vektorel kalite korunmayabilir.

Hedef davranis:

1. Once kayipsiz PDF sayfa kopyalama denenmeli.
2. Kayipsiz yontem basarisiz olursa mevcut render tabanli yontem fallback olarak kullanilmali.
3. UI tarafinda kalite veya metadata kaybi ihtimali acikca belirtilmeli.

## Transfer staging notu

Gelen transferler once uygulama veri klasorundeki `ToolBridge/incoming-staging` altinda bekletilir. Kullanici kabul ettiginde hedef klasore tasinir; reddedildiginde veya eski kaldiginda staging icerigi temizlenmelidir.

Bu amacla `TransferStagingCleanupService` eklenmistir. Uygulama acilisinda 24 saatten eski staging klasorleri temizlenir.
