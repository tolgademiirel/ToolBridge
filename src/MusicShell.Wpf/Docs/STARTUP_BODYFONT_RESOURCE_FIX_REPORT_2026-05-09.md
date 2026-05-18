# Startup BodyFont Resource Fix Report - 2026-05-09

## Sorun

Uygulama açılırken global hata penceresi gösteriyordu. Statik kaynak taramasında `MainWindow.xaml` içinde `BodyFont` adlı `StaticResource` kullanıldığı, ancak uygulama kaynaklarında `BodyFont` tanımının bulunmadığı tespit edildi.

WPF, `StaticResource` çözümlenemediğinde pencere yüklenirken `XamlParseException` üretir. Bu nedenle uygulama başlangıcında beklenmeyen hata penceresi görülebiliyordu.

## Onarım

`App.xaml` içine merkezi font ailesi olarak `BodyFont` eklendi.

```xml
<FontFamily x:Key="BodyFont">Inter, Segoe UI Variable Text, Segoe UI</FontFamily>
```

## Ek Düzeltme

`publish.ps1` ve `validate.ps1` scriptleri güçlendirildi. Artık `dotnet restore`, `dotnet build` veya `dotnet publish` hata kodu dönerse script sahte başarı mesajı yazmaz; işlemi durdurup hatayı gösterir.

## Kontrol

- Eksik `StaticResource` kontrolü: temiz
- XML/XAML parse kontrolü: başarılı
- ZIP bütünlük testi: başarılı
