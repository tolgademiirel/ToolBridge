# Convert Transfer Sağ Panel ve Alım İzni Düzeltmesi - 2026-05-09

## Yapılanlar

- Convert sağ panelindeki tekrar eden `İşlem Takibi` kartı kaldırıldı.
- Hedef format ve personel varsayılan kayıt klasörü kontrolleri ana Convert ekranında bırakıldı; sağ panel yalnızca çıktı transferi için sadeleştirildi.
- Sağ paneldeki online kullanıcı listesi tıklanabilir gönderim listesi olarak düzenlendi.
- Online kullanıcılar artık düz metin yerine `OnlineUserItem` modeliyle tutuluyor.
- Her kullanıcı için `IsOnline`, `IsTransferReceiveEnabled` ve `CanReceiveTransfers` alanları eklendi.
- Transfer gönderim komutu, hedef kullanıcının transfer alımı kapalıysa çalışmayacak şekilde güncellendi.
- Komut tarafında ek güvenlik doğrulaması eklendi: hedef offline ise veya transfer alımına izin vermiyorsa gönderim iptal edilir.

## Değiştirilen dosyalar

- `src/MusicShell.Wpf/MainWindow.xaml`
- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`

## Eklenen dosya

- `src/MusicShell.Wpf/Models/OnlineUserItem.cs`

## Not

Mevcut kaynakta gerçek ağ keşif/transfer servisi bulunmadığı için gönderim mevcut mimarideki `TransferOutbox` kuyruğuna alınır. Gerçek peer discovery servisi eklendiğinde, karşı kullanıcının `IsTransferReceiveEnabled` durumu bu modele bağlanarak aynı UI ve komut mantığıyla doğrudan engelleme yapılabilir.
