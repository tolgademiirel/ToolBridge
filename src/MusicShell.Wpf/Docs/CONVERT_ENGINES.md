# ToolBridge Convert Motorları

Convert sayfasındaki hedef format seçici sadeleştirilmiştir. Kullanıcı hedef format olarak yalnızca doküman ve görsel formatlarını seçebilir.

## Hedef Format Grupları

- Document: PDF, DOC, DOCX, ODT, RTF, TXT, HTML, CSV, XLS, XLSX, ODS, PPT, PPTX, ODP
- Image: JPG, JPEG, PNG, BMP, GIF, TIF, TIFF, ICO

## Kullanılan Yerel Motorlar

Uygulama dönüşüm için uygun yerel motoru sırayla dener:

1. Yerleşik WPF görsel dönüştürücü: PNG, JPG/JPEG, BMP, GIF, TIFF gibi temel görsel dönüşümleri
2. Docnet.Core + pdfium: PDF dosyalarının tüm sayfalarını ayrı PNG/JPG çıktısı olarak aktarma
3. LibreOffice / soffice: doküman dönüşümleri ve dokümanları PDF ara formatına alma
4. ImageMagick / magick: gelişmiş görsel dönüşümleri ve bazı PDF/görsel işlemleri
5. Microsoft Office: Word, Excel ve PowerPoint tabanlı dosyaları desteklenen hedeflere aktarma
6. Metin tabanlı yerleşik dönüşüm: TXT, CSV, MD, RST, TEX gibi düz metin çıktıları

İlgili motor sistemde yüklü değilse veya belirli format çifti motor tarafından desteklenmiyorsa hata detayı Convert durum alanında gösterilir.
