# Universal Convert Bridge Report - 2026-05-10

## Amaç
Kullanıcının sadece PDF değil, desteklenen belge/görsel formatları arasında daha esnek dönüşüm yapabilmesi için dönüşüm zinciri güçlendirildi.

## Yapılanlar
- DOCX/XLSX/PPTX kaynakları için dahili Open XML içerik çıkarma hattı genişletildi.
- Eski Office/OpenDocument dosyaları için LibreOffice üzerinden ara Open XML formatına çevirme ve ardından hedef formata yazma eklendi.
- Çapraz aile dönüşümleri için akıllı içerik köprüsü eklendi:
  - Word/Writer ailesi ↔ Excel/Calc ailesi
  - Word/Writer ailesi ↔ PowerPoint/Impress ailesi
  - Excel/Calc ailesi ↔ PowerPoint/Impress ailesi
  - PDF/TXT/CSV/HTML kaynaklarından Office hedeflerine içerik aktarımı
- PPTX ve ODP üretimi için basit sunum yazıcıları eklendi.
- PPT hedefi için PPTX ara dosyası + LibreOffice dönüştürme köprüsü eklendi.
- Görsel hedef listesine WEBP, SVG, AVIF, HEIC ve TGA eklendi.

## Not
Çapraz aile dönüşümlerinde hedef dosya, kaynak içeriği güvenli şekilde aktarır; Excel formülleri, makrolar, gelişmiş sayfa düzeni ve karmaşık görsel yerleşimler birebir korunmayabilir. Birebir görsel sadakat gereken senaryolarda PDF hedefi hâlâ en güvenli formattır.
