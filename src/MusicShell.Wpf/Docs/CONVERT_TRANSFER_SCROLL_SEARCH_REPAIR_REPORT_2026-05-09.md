# ToolBridge - Convert Transfer Scroll/Search + Genel Tarama Raporu

Tarih: 2026-05-09

## İstenen Düzenlemeler

- Convert sağ panelindeki `Gönderilecek Çıktılar` başlığının yanındaki yazılı `Temizle` butonu vektörel süpürge ikonuna çevrildi.
- Convert sağ panelindeki gönderilecek çıktı listesi scroll kontrollü kaldı; uzun liste paneli aşağı uzatmadan içeride kaydırılabilir.
- Convert sağ panelindeki `Online Kullanıcılar` listesine dikey scroll eklendi.
- Sol frame altındaki `Online Kullanıcılar` listesine arama kutusu eklendi.
- Convert sağ panelindeki `Online Kullanıcılar` listesine arama kutusu eklendi.
- Online kullanıcı listeleri ortak filtreleme kullanır. Kullanıcı adı arandığında sol frame ve sağ panel aynı filtreyle daralır.
- Transfer > Dosya Gönder bölümündeki `Listeyi Temizle` butonu genişletildi; metnin kırpılması engellendi.
- Transfer indirme klasörü seçili değilse verilen uyarı metni Ayarlar yerine Transfer sağ panelini işaret edecek şekilde güncellendi.

## Statik Tarama Sonucu

Kontrol edilen alanlar:

- MainWindow.xaml XML/XAML parse kontrolü
- App.xaml XML/XAML parse kontrolü
- XAML event handler bağlantıları
- ICommand binding bağlantıları
- StaticResource/DynamicResource referansları
- Görsel asset referansları
- C# dosyalarında kaba parantez dengesi

Sonuç:

- Eksik event handler bulunmadı.
- Eksik command binding bulunmadı.
- Eksik resource referansı bulunmadı.
- Eksik asset referansı bulunmadı.
- XAML parse hatası bulunmadı.
- C# kaba parantez dengesizliği bulunmadı.

## Build Notu

Bu ortamda .NET SDK yüklü olmadığı için gerçek `dotnet publish` çalıştırılamadı. Kaynak statik olarak tarandı ve XAML/XML doğrulaması yapıldı.

## Tavsiyeler

- Gerçek ağ transferi için discovery + TCP dosya aktarımı servisleri ayrı sınıflara bölünmeli.
- Gelen transfer kabul/reddet akışı disk kopyalama öncesi checksum kontrolü yapmalı.
- Büyük dosyalarda ilerleme yüzdesi ve iptal butonu eklenmeli.
- Kullanıcı/cihaz keşfi için UDP broadcast yerine kurumsal ağlarda opsiyonel manuel IP/hostname listesi desteklenmeli.
- Dosya transfer geçmişi JSON ya da SQLite ile kalıcı tutulmalı.
- Ayarlar kalıcı yapılandırması tek bir SettingsService üzerinden yönetilmeli.
- Convert motorları için LibreOffice, ImageMagick ve PDFium durum kontrol ekranı eklenmeli.
- Loglama için günlük dosya tutulmalı ve hata ekranlarında kısa kullanıcı mesajı + teknik detay ayrımı yapılmalı.
- UI tarafında ortak buton, kart, input ve liste stilleri merkezi resource dosyalarına ayrılmalı.
