# Transfer, Tooltip ve Çift Tıklama Onarım Raporu - 2026-05-09

## 1. Buton üzerine gelince okunmayan açıklama metinleri

Uygulama genelindeki `ToolTip` görünümü tema paletine bağlandı. Açıklama kutuları artık açık/koyu tema fark etmeksizin `PanelBrush`, `PrimaryTextBrush` ve `LineBrush` kaynaklarını kullanır.

Etkilenen dosya:

- `App.xaml`

## 2. Convert çıktısına çift tıklayınca aynı dosyanın listeye tekrar eklenmesi

Convert çıktı satırında çift tıklama ile açma ve sürükleme işlemleri ayrıştırıldı.

Yapılanlar:

- Çıktı sürükleme işlemi için minimum hareket eşiği eklendi.
- Convert çıktısı sürüklenirken özel `ToolBridge.ConvertOutputDrag` veri işareti eklendi.
- Convert dosya bırakma alanı bu özel işareti görürse dosyayı tekrar dönüştürme listesine eklemez.
- Çift tıklama sadece kaynak/çıktı dosyasını açar.

Etkilenen dosya:

- `MainWindow.xaml.cs`

## 3. Online kullanıcı transfer bildirimi

Online kullanıcıların yanında fosforlu turuncu bildirim noktası eklendi. Bir kullanıcıya transfer yapıldığında bu gösterge aktif olur ve tooltip üzerinde kaç dosya geldiği ile indirme klasörü görünür.

Etkilenen dosyalar:

- `MainWindow.xaml`
- `Models/OnlineUserItem.cs`
- `ViewModels/MainViewModel.cs`

## 4. Transfer edilen dosyaların seçilen klasöre indirilmesi

Ayarlar sayfasına `Transfer indirme klasörü` seçimi eklendi. Gönderilen dosyalar artık LocalAppData içindeki kuyruk klasörüne değil, kullanıcının seçtiği transfer indirme klasörüne kopyalanır.

Yapılanlar:

- `TransferDownloadFolder` ayarı eklendi.
- Ayar kalıcı olarak `print-settings.json` içinde saklanır.
- Transfer klasörü seçilmemişse gönderim engellenir ve kullanıcıya uyarı verilir.
- Dosya adları çakışırsa otomatik benzersiz dosya adı üretilir.

## Not

Bu paket gerçek ağ transfer servisi içermeyen mevcut kaynak üzerinde çalışır. Bu nedenle gönderim, seçili yerel transfer indirme klasörüne kopyalama ve online kullanıcı bildirim simülasyonu şeklinde uygulanmıştır. Gerçek ağ servisi bağlandığında aynı ayar hedef kullanıcının kendi indirme klasörü olarak kullanılabilir.
