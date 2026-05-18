# Klavye Kısayolu Metin Düzeltme Raporu - 2026-05-09

## İstek

Dosya ekleme alanındaki `Ctrl+V` ifadesi, toplu seçim beklentisiyle karışıklık oluşturuyordu. Toplu dosya seçimi için kullanıcı beklentisi `Ctrl+A` olduğu için arayüz metni revize edildi.

## Yapılan değişiklikler

- Yazdırma / dosya yükleme alanındaki yardımcı metin güncellendi.
- `Ctrl+V ile ekle` ifadesi kaldırıldı.
- Kullanıcıya doğru işlem akışı gösterildi: `Dosya Seç ekranında Ctrl+A ile toplu seçim yapabilirsiniz`.
- Convert dosya seçme alanında da aynı toplu seçim bilgisiyle tutarlı yönlendirme sağlandı.

## Not

`Ctrl+V`, Windows panosundan dosya yapıştırma için teknik olarak çalışmaya devam eder; ancak arayüzde toplu seçim beklentisini karıştırmaması için görünür metinden çıkarıldı. Toplu dosya seçimi Windows dosya seçim penceresinde `Ctrl+A` ile yapılır.

## Değiştirilen dosya

- `src/MusicShell.Wpf/MainWindow.xaml`
