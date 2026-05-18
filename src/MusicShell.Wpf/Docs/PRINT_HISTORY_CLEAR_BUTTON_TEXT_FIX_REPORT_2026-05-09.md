# Yazdırma Geçmişi - Temizleme Butonu Metin Düzeltme Raporu

Tarih: 2026-05-09

## Talep

Yazdırma Geçmişi bölümünün sağ üstündeki `Geçmişi Temizle` butonu ekranda `Geçmişi Temizl` şeklinde görünüyordu.

## Tespit

Kaynak metin doğruydu:

```text
Geçmişi Temizle
```

Sorun yazım hatasından değil, buton genişliğinin metin + iç boşluk için yetersiz kalmasından kaynaklanıyordu. Bu nedenle son harf görsel olarak kırpılıyordu.

## Uygulanan Düzeltme

Dosya:

```text
src/MusicShell.Wpf/MainWindow.xaml
```

Değişiklik:

- Sabit `Width="132"` kaldırıldı.
- Buton için `MinWidth="156"` verildi.
- İç boşluk `Padding="16,0"` olarak dengelendi.

## Sonuç

`Geçmişi Temizle` metni artık tam görünür. Farklı ölçeklendirme ve font render durumlarında metnin son harfinin kırpılması engellendi.
