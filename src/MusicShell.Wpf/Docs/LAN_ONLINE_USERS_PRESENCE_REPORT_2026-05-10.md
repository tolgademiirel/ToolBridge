# LAN Online Kullanıcılar Presence Düzeltmesi - 2026-05-10

## Amaç

Demo/hardcoded online kullanıcılar kaldırıldı. Uygulamayı çalıştıran gerçek kullanıcıların aynı yerel ağ üzerinde birbirlerini online listesinde görmesi sağlandı.

## Yapılan değişiklikler

- `MainViewModel.cs` içindeki demo kullanıcı listesi kaldırıldı.
- `Services/LanPresenceService.cs` eklendi.
- Uygulama açıldığında UDP broadcast ile kendini ağda yayınlar.
- Aynı ağdaki diğer ToolBridge istemcileri bu yayını dinleyerek online kullanıcı listesine eklenir.
- Uygulama kapanırken `offline` paketi gönderilir.
- Kapanış paketi alınamazsa kullanıcı yaklaşık 12 saniye içinde listeden otomatik düşer.
- Online kullanıcılar kullanıcı adı, bilgisayar adı ve IP bilgisiyle takip edilir.
- Arama alanları artık kullanıcı adı yanında bilgisayar adı ve IP adresiyle de filtreleme yapabilir.

## Teknik detay

- UDP port: `47892`
- Yayın aralığı: yaklaşık 3 saniye
- Offline zaman aşımı: yaklaşık 12 saniye
- Harici sunucu, veritabanı veya internet bağlantısı gerekmez.

## Not

Windows Defender Firewall veya kurumsal güvenlik duvarı UDP broadcast trafiğini engellerse kullanıcılar birbirini göremeyebilir. Böyle bir durumda ToolBridge uygulaması veya UDP `47892` portu için aynı ağ/profil üzerinde izin verilmelidir.

## Yayın paketi desteği

- `setup_firewall_toolbridge_presence.ps1` eklendi.
- `publish.ps1`, bu firewall scriptini publish klasörüne de kopyalar.
