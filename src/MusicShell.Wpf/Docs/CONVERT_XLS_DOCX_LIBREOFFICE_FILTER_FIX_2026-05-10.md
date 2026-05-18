# Convert XLS/DOCX LibreOffice Filter Fix - 2026-05-10

## Sorun
Convert ekranında hedef format DOCX seçiliyken XLS dosyaları LibreOffice'e doğrudan `XLS -> DOCX` olarak gönderiliyordu. LibreOffice Calc kaynaklı dosyaları doğrudan Writer DOCX çıktısına export edemediği için işlem şu hata ile bitiyordu:

`Error: no export filter ... .docx found, aborting.`

## Yapılan Düzeltme
- LibreOffice PDF çıktıları kaynak aileye göre filtrelendi:
  - Writer belgeleri: `pdf:writer_pdf_Export`
  - Calc tabloları: `pdf:calc_pdf_Export`
  - Impress sunumları: `pdf:impress_pdf_Export`
- LibreOffice'in doğrudan desteklemediği çapraz aile dönüşümleri için erken kontrol eklendi.
- Legacy XLS/PPT dosyalarında gerekli olduğunda Open XML ara dönüşüm köprüsü eklendi:
  - `XLS -> XLSX -> DOCX/DOC/ODT/RTF/TXT/HTML/CSV/ODS`
  - `PPT/PPS -> PPTX -> DOCX/DOC/ODT/RTF/TXT/HTML`
- Kullanıcıya ham LibreOffice `no export filter` mesajı yerine daha anlaşılır hata dönmesi sağlandı.

## Not
DOCX/XLSX/PPTX gibi farklı belge aileleri arası dönüşümlerde içerik temel tablo/metin olarak aktarılır. Sayfa düzeni, makro, karmaşık stil ve formül davranışı korunmayabilir. En doğru kurumsal akış için hedef format PDF seçilmelidir.
