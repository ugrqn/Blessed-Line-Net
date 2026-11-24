# BlessedLine

BlessedLine, Windows ve Linux için internet kullanımı ve kota sayacıdır. Ana pencere seçili bağlantının hızını ve kotasını, altındaki tablo ise ağ kullanan uygulamaları gösterir.

[English](docs/README.en.md) · [Sürüm notları](https://github.com/ugrqn/Blessed-Line-Net/releases/tag/v0.4.0-preview)

## İndir

| Windows 64 bit | Windows 32 bit | Linux |
| :--- | :--- | :--- |
| **[İndir · x64 .7z](https://github.com/ugrqn/Blessed-Line-Net/releases/download/v0.4.0-preview/BlessedLine-0.4.0-win-x64.7z)** | **[İndir · x86 .7z](https://github.com/ugrqn/Blessed-Line-Net/releases/download/v0.4.0-preview/BlessedLine-0.4.0-win-x86.7z)** | **[İndir · .tar.gz](https://github.com/ugrqn/Blessed-Line-Net/releases/download/v0.4.0-preview/BlessedLine-0.4.0-linux.tar.gz)** |
| Windows 11 / 64 bit Windows 10 | 32 bit Windows 10 | Debian ve Ubuntu önizlemesi |

Windows’ta eski sürüm açıksa bildirim alanındaki simgeden **Çıkış**’ı seç. Yeni arşivi ayıkla ve `BlessedLine.exe` dosyasını aç. Linux paketinde Python 3 ve Tk gerekir. [Linux kullanım bilgileri](src/BlessedLine.Linux/README.md)

## Uygulamalar ve sayaçlar

- **Uygulamalar** açıkken etkin TCP bağlantısı olan programlar otomatik listelenir. Bir programı görmek için o programda bir sayfa aç veya indirme başlat. Windows’un verdiği dosya ya da uygulama paketi simgesi, kısa adın yanında görünür. Simge okunamıyorsa Windows’un genel uygulama simgesi kullanılır.
- **Hızları ölç…** düğmesine basıp Windows’un yönetici iznini onayla. Ana pencere açık kalır. Tabloda her uygulamanın indirme ve yükleme hızını ve ölçüm başladığından beri iki yöndeki toplamını görebilirsin. Çizgi (—) henüz ölçüm alınmadığını gösterir. Yönetici olarak başlatılan oturumlarda ölçüm kendiliğinden açılır.
- Üstteki kota **seçili bağlantıya**, uygulama tablosu Windows’un süreçlere bağlayabildiği **tüm bağlantılardaki TCP ve UDP trafiğine** dayanır. Kaynağı belirlenemeyen trafik “Atfedilemeyen” adıyla gösterilir.
- **Kota ekle…** veya **Kotayı düzenle…** içinden sayacı seç, limiti MB olarak gir ve **Uygula**’ya bas. Çubuk kullanımla dolar ve kota dolunca kırmızı olur. %90’da ve limitte bildirim verilir. **Kota dolunca Wi-Fi’yi kapat** seçeneğini aç, Windows yönetici iznini ve uygulamadaki onayı ver. Seçili Wi-Fi radyosu kota dolunca kapanır. Kota zaten aşılmışsa kapatma hemen çalışır. **Wi-Fi’yi geri aç** ile radyoyu yeniden açabilirsin. Otomatik kapatmayı her yeni oturumda yeniden seç.
- **Ayarlar…** içinde **XP Klasik** veya **Windows 7 Aero** görünümünü seçebilirsin. Pencere çerçevesi, yazı tipi ve denetimler temayla birlikte değişir. Ana pencere ve küçük çubuk için uygulama listesini ayrı ayrı açabilirsin. Dili Türkçe ve on ek dil arasından seçebilir, üç uyarı sesini dinleyebilirsin.

## Küçük çubuk ve bildirim alanı

**–** pencereyi ekranın sağ üstünde küçük çubuğa dönüştürür. Toplam kotayı, kalan miktarı ve iki yöndeki hızı burada görebilirsin. Dişli simgesinden kotayı küçük pencereyi kapatmadan düzenleyebilirsin. **□** ana pencereye döner. **×** pencereyi bildirim alanına gizler ve sayaç çalışmaya devam eder. Saatin yanındaki **∧** altında BlessedLine simgesine çift tıklayarak geri açabilirsin. Tamamen kapatmak için simgeye sağ tıklayıp **Çıkış**’ı seç.

Sayaç ve ayarların konumu `%LOCALAPPDATA%\BlessedLine\state.json` dosyasıdır. Uygulama satırlarındaki toplamlar o ölçüm oturumuna aittir.

## Yerel geliştirme

Windows 11 x64 üzerinde `BlessedLine.slnx` dosyasının bulunduğu klasörü aç. `global.json` .NET SDK 10.0.404 veya aynı özellik bandındaki yeni yamaları seçer.

```powershell
git clone https://github.com/ugrqn/Blessed-Line-Net.git
cd Blessed-Line-Net
dotnet build BlessedLine.slnx -c Release
dotnet test BlessedLine.slnx -c Release --no-build
dotnet run --project src/BlessedLine.Desktop -c Release
```

Taşınabilir yayın üretmek için depo kökünde çalıştır.

```powershell
dotnet publish src/BlessedLine.Desktop -c Release -r win-x64 --self-contained true -o artifacts/win-x64/Desktop
```

Kurucu kaynağı `installer/BlessedLine.iss` dosyasındadır. [Windows kontrol listesi](docs/windows-kontrol.md) önizlemenin yerel ölçüm ve geri alma senaryolarını içerir. [Doğrulama kaydı](docs/dogrulama.md) çalıştırılan testleri, [ilk ürün planı](RetroNet_SOL_PLANI.md) yol haritasını gösterir.

[Uygulama bazlı ağ kuralları araştırması](docs/uygulama-kurallari.md) · [GitHub indirme sayfası kaynakları](site/)
