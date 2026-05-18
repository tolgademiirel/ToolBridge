# Transfer Sağ Panel / Arama / Bildirim / Scroll Düzeltme Raporu

Tarih: 2026-05-09

## Yapılan değişiklikler

### 1. Online kullanıcı aramaları ayrıldı

Önceden sol menü, Transfer ekranındaki online kullanıcı listesi ve Convert sağ panelindeki online kullanıcı listesi aynı `OnlineUserSearchText` ve aynı `FilteredOnlineUsers` koleksiyonunu kullanıyordu. Bu yüzden bir yerde yapılan arama diğer listeyi de filtreliyordu.

Yeni ayrı alanlar:

- `LeftOnlineUserSearchText`
- `TransferOnlineUserSearchText`
- `ConvertOnlineUserSearchText`

Yeni ayrı filtrelenmiş listeler:

- `LeftFilteredOnlineUsers`
- `TransferFilteredOnlineUsers`
- `ConvertFilteredOnlineUsers`

Böylece sol frame'de yapılan arama sağ/orta transfer listesini etkilemez. Transfer ekranındaki arama da sol frame'i etkilemez.

### 2. Gelen Transferler temizleme ikonu yenilendi

Sağ paneldeki bildirim temizleme butonundaki eski süpürge ikonu değiştirildi. Daha net görünen, fonta bağlı olmayan, tamamen vektörel yeni temizleme/süpürge ikonu eklendi.

### 3. Sağ panelde gelen transfer listesine sabit scroll alanı verildi

Gelen transfer bildirimi arttıkça sağ panelin aşağı doğru uzaması engellendi.

- Gelen transfer listesi sabit yükseklikte tutuldu.
- Liste içine dikey scroll eklendi.
- Transfer indirme klasörü alanı aşağıya itilmez.
- Mouse tekerleğiyle bildirimler içinde gezilebilir.

### 4. Bildirimler görüntülenince turuncu bildirim noktaları temizlenir

Kullanıcı Transfer sekmesine girdiğinde ve sağdaki Gelen Transferler paneli görünür olduğunda ilgili kullanıcıların turuncu bildirim noktaları temizlenir.

Ayrıca gelen transfer kartı popup ile açıldığında da ilgili kullanıcının bildirim işareti temizlenir.

Bu davranış tüm uygulamadaki aynı kullanıcı nesnelerine yansır:

- Sol frame online kullanıcı listesi
- Transfer ekranı online kullanıcı listesi
- Convert sağ panel online kullanıcı listesi

## Korunan fonksiyonlar

- Dosya seçme
- Sürükle-bırak ile dosya ekleme
- Online kullanıcı seçme
- Dosya gönderme
- Gelen transfer popup açma
- Kabul / reddet
- Bildirimleri temizleme
- Transfer indirme klasörü seçme
- Transfer geçmişi
- Yazdırma havuzuna alma / açma / yazdırma

## Statik kontrol sonucu

- `MainWindow.xaml` parse: başarılı
- `App.xaml` parse: başarılı
- `ToolBridge.UI.xaml` parse: başarılı
- Eksik XAML event handler: yok
- Eksik command binding: yok
- Eksik StaticResource: yok
- ViewModel binding kontrolleri: başarılı
- C# kaba parantez dengesi: başarılı

## Not

Bu ortamda Windows WPF build/publish çalıştırılamadığı için gerçek derleme testi yapılamadı. Kaynak kod, XAML ve bağlantı kontrolleri statik olarak doğrulandı.
