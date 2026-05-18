# PDF Editable Convert Fix Report - 2026-05-09

## Amaç
Convert bölümünde PDF kaynak dosyaların XLSX ve diğer düzenlenebilir hedef formatlara dönüştürülürken yalnızca LibreOffice'e bağımlı kalması nedeniyle oluşan "LibreOffice bulunamadı" hatasını gidermek.

## Yapılan Değişiklikler

- PDF kaynak dosyalar için dahili metin çıkarma ve dışa aktarma motoru eklendi.
- PDFium `FPDFText_*` metin okuma API'leri kullanılarak seçilebilir PDF metni okunur.
- PDFium metin okuma başarısız olursa basit PDF stream metin çıkarma yedeği devreye girer.
- PDF -> XLSX dönüşümü için Open XML tabanlı minimal Excel dosyası uygulama içinde üretilir.
- PDF -> DOCX dönüşümü için Open XML tabanlı minimal Word dosyası uygulama içinde üretilir.
- PDF -> TXT, CSV, HTML, RTF, DOC, XLS, ODT ve ODS çıktıları için dahili üretim yolları eklendi.
- LibreOffice kurulu olmasa bile seçilebilir metin içeren PDF'ler düzenlenebilir formatlara dönüştürülebilir hale getirildi.

## Desteklenen Dahili PDF Çıkışları

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

## Teknik Not

Bu onarım, seçilebilir metin içeren PDF dosyalarını dönüştürür. Taranmış/görsel PDF dosyalarında OCR motoru bulunmadığı için Excel/Word gibi düzenlenebilir formatlara gerçek metin çıkarımı yapılamaz. Bu durumda JPG/PNG dönüşümü çalışmaya devam eder; düzenlenebilir çıktı için OCR veya harici motor gerekir.

## Değiştirilen Dosya

- `src/MusicShell.Wpf/ViewModels/MainViewModel.cs`
