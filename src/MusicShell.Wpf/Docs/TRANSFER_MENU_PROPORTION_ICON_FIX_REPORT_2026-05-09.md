# Transfer Menü Oran / İkon Düzeltme Raporu - 2026-05-09

## Yapılan düzenlemeler

- Transfer sayfasındaki kart başlıkları ve buton yükseklikleri diğer sayfalarla daha uyumlu olacak şekilde yeniden oranlandı.
- Transfer menüsündeki `Dosya Seç` butonu aktif sayfa rengiyle çalışacak şekilde `ThemedPrimaryButtonStyle` ile bağlandı.
- Sağ panel genişliği artırıldı ve `Ağdaki Personeller` listesindeki personel kartları panele tam genişlikte oturacak şekilde düzenlendi.
- Online/personel durum noktaları daha mat renge çekildi; fosforlu görünüm kaldırıldı.
- Transfer geçmişindeki metin tabanlı işlem butonları vektörel ikonlara çevrildi:
  - `Aç` yerine göz ikonu
  - `Havuza Al` yerine ev ikonu
  - `Yazdır` yerine yazıcı ikonu
- İşlem butonlarına açıklayıcı tooltip eklendi.

## Değiştirilen dosya

- `MainWindow.xaml`

## Kontrol

- `MainWindow.xaml` XML parse kontrolünden geçti.
- `App.xaml` XML parse kontrolünden geçti.
