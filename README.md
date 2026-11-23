# BlessedLine

BlessedLine, Windows 11 x64 için Türkçe ağ kullanımı ve kota takip uygulamasıdır. Kare ana panel, sağ üste yerleşen mini panel, XP Luna esintili görünüm ve projeye ait ikonlar içerir.

## Windows’ta çalıştır

[0.2.0 önizlemesindeki taşınabilir paketi](https://github.com/ugrqn/Blessed-Line-Net/releases/tag/v0.2.0-preview) indir. Windows 11’de arşivi ayıkla ve çıkan `BlessedLine` klasöründeki `BlessedLine.exe` dosyasını aç. Paket .NET çalışma zamanını içerir.

- **Bağlantı** alanında Wi-Fi, kablolu ağ ve varsa VPN gibi anlaşılır seçeneklerden birini seç.
- **Toplam harcanan** seçili bağlantıyı esas alır. Bir MB limiti girip **Uygula** deyince Wi-Fi göstergesi kullanımla birlikte kırmızı duvara yaklaşır. %90’da ve limitte sesli uyarı gelir.
- **Uygulamalar → Ölçümü aç** yönetici erişimiyle Windows trafik ölçümünü başlatır. Satırlarda kısa ad, ikon, anlık indirme ve yükleme hızları, oturum toplamı bulunur. Adın üstünde ayrı indirme ve yükleme toplamlarını görebilirsin. Bu tablo tüm bağlantılardaki atfedilebilir TCP/UDP trafiğini gösterir.
- Başlıktaki **Mini panel** düğmesi küçük görünümü sağ üste taşır. **Ayarlar** üzerinden detayların ana ve mini görünümde açık kalmasını ayrı seçebilir, üç sesi dinleyebilirsin.
- **Limitte bağlantıyı kes** varsayılan olarak kapalıdır. Yönetici erişimiyle açıkça etkinleştirildiğinde Windows’un geçici filtresi seçili bağlantının internet ve yerel ağ trafiğini keser. **Bağlantıyı geri aç** filtreyi kaldırır. Ayar uygulama yeniden açılınca tekrar onay ister.

Sayaçlar `%LOCALAPPDATA%\BlessedLine\state.json` dosyasında saklanır. İlk açılışta varsa RetroNet önizlemesinden kalan sayaçlar içe alınır. Bu sürümde ölçüm masaüstü uygulaması açıkken çalışır.

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
