# Transfer Geçmişi Temizleme Butonu Düzeltmesi - 2026-05-09

## Amaç
Transfer Geçmişi kartındaki `Transfer Geçmişini Temizle` butonunun dar alanda yarım görünmesi ve diğer sayfalardaki temizleme aksiyonlarıyla görsel olarak uyumsuz olması düzeltildi.

## Yapılan Değişiklikler
- Metin tabanlı uzun kırmızı buton kaldırıldı.
- Diğer temizleme alanlarıyla uyumlu, fonta bağlı olmayan vektörel süpürge ikonu eklendi.
- Buton `TbIconOnlyButtonStyle` merkezi stiline bağlandı.
- `ToolTip` korunarak aksiyon açıklaması eklendi: `Transfer geçmişini temizle`.
- Koyu/açık temada ikon rengi `MutedTextBrush` üzerinden dinamik çalışacak şekilde düzenlendi.

## Etkilenen Dosya
- `src/MusicShell.Wpf/MainWindow.xaml`

## Kontrol
- XAML parse kontrolü başarılı.
- İlgili command binding: `ClearTransferHistoryCommand` korunmuştur.
