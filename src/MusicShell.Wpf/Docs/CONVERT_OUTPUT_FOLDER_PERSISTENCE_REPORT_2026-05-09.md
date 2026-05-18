# Convert Kayıt Klasörü Kalıcılığı Raporu - 2026-05-09

## Talep
Convert bölümünde dönüştürülen dosyaların kayıt klasörünü personel kendisi seçsin. Uygulama kapanıp açılsa bile seçilen klasör, personel yeniden değiştirene kadar sabit kalsın.

## Yapılan Düzeltme
- Convert ekranındaki otomatik `Belgeler\ToolBridge\Converted` varsayılanı kaldırıldı.
- İlk kullanımda kayıt klasörü boş gelir; personel `Varsayılan Klasör Seç` butonuyla kendi klasörünü belirler.
- Seçilen klasör uygulama yeniden açılsa da korunur.
- Personel aynı ekrandan farklı bir klasör seçene kadar dönüştürülen dosyalar aynı klasöre kaydedilir.
- Convert işlemi, kayıt klasörü seçilmeden başlatılamaz ve kullanıcıya uyarı verir.

## Kullanıcı Deneyimi
1. Personel Convert bölümüne girer.
2. `Varsayılan Klasör Seç` butonuna basar.
3. Kayıt klasörünü seçer.
4. Dönüştürülen dosyalar bu klasöre kaydedilir.
5. Uygulama kapatılıp açılsa bile aynı klasör seçili kalır.

## Teknik Not
Uygulama sadece seçilen klasör yolunu hatırlar; dönüştürülen dosyalar uygulama ayar klasörüne kaydedilmez.
