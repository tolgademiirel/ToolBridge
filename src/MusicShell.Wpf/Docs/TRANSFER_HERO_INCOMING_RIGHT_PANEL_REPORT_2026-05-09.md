# Transfer Hero ve Gelen Transfer Sağ Panel Düzenlemesi - 2026-05-09

## Kapsam

Kullanıcı talebi doğrultusunda Transfer sayfasının yerleşimi yeniden düzenlendi.

## Yapılan Değişiklikler

- Transfer ekranındaki hero alanı iki kolonlu yapıya çevrildi.
  - Sol kolon: transfer edilecek dosya/doküman listesi.
  - Sağ kolon: online personel listesi.
- Online personel listesi sağ dış panelden kaldırıldı.
- Transfer menüsünün sağ dış paneli artık sadece Gelen Transferler alanı olarak çalışır.
- Gelen transfer kartları sağ panelde listelenir.
- Gelen transfer kartına çift tıklanınca mevcut transfer kabul/reddet pop-up'ı açılır.
- Sol navigasyon altındaki Gelen Transferler listesi kaldırıldı; gelen transferlerin tek konumu sağ panel oldu.
- Transfer sayfasındaki butonlar küçültülerek uygulama genelindeki buton oranlarına yaklaştırıldı.
- Online personel kartlarında sadece kullanıcı adı gösterilir; transfer durumu açıklaması kart içinde gösterilmez.
- Bekleyen transfer yoksa sağ panelde boş durum mesajı gösterilir.

## Kontrol

- `MainWindow.xaml` XML parse kontrolünden geçti.
- `App.xaml` XML parse kontrolünden geçti.
- Gerçek `.NET publish` bu ortamda SDK olmadığı için çalıştırılamadı.
