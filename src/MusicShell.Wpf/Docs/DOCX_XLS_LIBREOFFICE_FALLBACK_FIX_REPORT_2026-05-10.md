# DOCX / XLSX Dahili Dönüştürme ve LibreOffice Bağımlılığı Azaltma Raporu

Tarih: 10.05.2026

## Sorun

Convert ekranında LibreOffice Portable hazır değilken özellikle `DOCX -> XLS/XLSX/PDF/TXT/HTML/CSV` gibi dönüşümler hata veriyordu:

```text
Bu dönüşüm tamamlanmadı. LibreOffice: LibreOffice bulunamadı.
```

Bu durum Portable LibreOffice kurulumunun hazırlanamaması veya sistemde LibreOffice/Microsoft Office bulunmaması halinde kullanıcı akışını kesiyordu.

## Yapılan Onarım

`MainViewModel.cs` içine dahili Office Open XML dönüştürme motoru eklendi.

Desteklenen kaynaklar:

- DOCX
- XLSX
- PPTX

Desteklenen hedefler:

- TXT
- CSV
- HTML
- RTF
- DOC
- DOCX
- XLS
- XLSX
- ODT
- ODS
- PDF

## Çalışma Mantığı

- DOCX içeriği `word/document.xml` üzerinden okunur.
- XLSX içeriği `xl/sharedStrings.xml` ve `xl/worksheets/sheet*.xml` üzerinden okunur.
- PPTX içeriği `ppt/slides/slide*.xml` üzerinden okunur.
- XLS hedefi için Excel'in açabildiği HTML tablo çıktısı üretilir.
- XLSX hedefi için minimal geçerli Office Open XML çalışma kitabı üretilir.
- PDF hedefi için basit metin tabanlı PDF çıktısı üretilir.

## Notlar

Bu dahili motor, LibreOffice/Microsoft Office yerine temel içerik çıkarma ve dönüştürme sağlar. Karmaşık sayfa düzeni, görsel, tablo stilleri ve makrolar korunmaz. Profesyonel düzen koruma gerekiyorsa LibreOffice Portable veya Microsoft Office motoru yine daha iyi sonuç verir.

## Etki

LibreOffice bulunmadığında DOCX/XLSX/PPTX kaynaklı temel dönüşümler artık doğrudan hata vermeden tamamlanabilir.
