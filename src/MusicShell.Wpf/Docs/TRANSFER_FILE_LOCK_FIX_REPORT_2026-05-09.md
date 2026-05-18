# Transfer Dosya Kilidi ve Checksum Onarım Raporu

## Sorun
Gelen transfer kabul edilirken bazı dosyalarda şu hata oluşuyordu:

`The process cannot access the file ... because it is being used by another process.`

## Kök Neden
Aktarım tamamlandıktan sonra checksum doğrulaması yapılırken hedef dosya akışı hâlâ `FileShare.None` ile açık kalabiliyordu. Bu nedenle uygulama kendi oluşturduğu dosyayı tekrar okumaya çalıştığında Windows dosya kilidi hatası üretiyordu.

## Onarım
- Dosya artık önce `.toolbridge-*.tmp` geçici dosyasına parça parça yazılır.
- Yazma akışları tamamen kapandıktan sonra checksum doğrulaması yapılır.
- Doğrulama başarılıysa geçici dosya final dosya adına taşınır.
- Hata veya iptal durumunda geçici dosya temizlenir.
- Kaynak dosya okuma işlemi `FileShare.ReadWrite | FileShare.Delete` ile daha toleranslı hâle getirildi.
- Dosya geçici olarak başka işlem tarafından tutuluyorsa 8 denemelik kısa retry mekanizması eklendi.

## Sonuç
- Checksum aşamasında uygulamanın kendi dosyasını kilitleme problemi giderildi.
- Büyük dosya transferlerinde yarım/bozuk çıktı kalma riski azaltıldı.
- Dosya başka uygulamada açıksa kullanıcıya daha anlaşılır hata mesajı verilir.
