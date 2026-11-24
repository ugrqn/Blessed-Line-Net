# Doğrulama kaydı

## BlessedLine 0.4.0

Windows kaynak derlemesi 21 birim testiyle birlikte geçti. Windows çalıştırıcısında ana ve mini pencere, mini panelden 1.234,5 MB kotanın yazılması, tepsiden dönüş, Aero tercihi, 11 dilde açılış, iki süreçte canlı trafik atfı ve standart kullanıcıda bağlantı listesi kontrol edildi. [Windows doğrulaması](https://github.com/ugrqn/Blessed-Line-Net/actions/runs/45377940525)

Kota tetikleyici testleri mevcut kotanın aşılması, sınırı yeni geçme, sayaç duraklatma ve farklı bağdaştırıcıyı kapsamaktadır. GitHub Windows çalıştırıcısında fiziksel Wi-Fi radyosu yoktur. Radyo kapanması ve açılmasının donanımdaki sonucu [Windows kontrol listesinde](windows-kontrol.md) yer alır.

Debian 13 üzerinde Linux arşivinden başlatılan Tk arayüzü, yerel ağ bağdaştırıcısı, mini panel ve 11 dil kontrolüyle açıldı. NetworkManager kapatma ve açma komutlarının çağrılması testte taklit edildi. Aynı Linux ortamında Chromium ile indirme sayfasının 11 dilinde tarayıcı diline göre açılış, Arapça ve Urduca sağdan sola yön, mobil yerleşim, dil değiştirme ve üç bağlantının hedefi kontrol edildi.

Windows x64 ve x86 paketleri .NET çalışma zamanı dahil olarak üretildi ve 7-Zip bütünlük testinden geçti. SHA-256 listesi GitHub sürümünün Assets bölümündedir. [Sürüm](https://github.com/ugrqn/Blessed-Line-Net/releases/tag/v0.4.0-preview)

## BlessedLine 0.3.0

Windows Server 2025 x64 çalıştırıcısında 15 birim testi geçti. WPF uygulaması açıldı. 550 × 550 ana pencere 304 × 123 küçük çubuğa geçti, ana boyutuna döndü ve × ile tepsiye gizlendi. EXE ikinci defa çalıştırılınca aynı pencere açıldı. Ayarlardan Windows 7 Aero seçildi ve tercih diske yazıldı. [Windows test kaydı](https://github.com/ugrqn/Blessed-Line-Net/actions/runs/45296969597)

Standart kullanıcı belirteciyle çalışan pencerede, yönetici izni öncesinde etkin bir curl bağlantısı listelendi. Yönetici oturumunda eşzamanlı curl ve PowerShell indirmeleri ayrı satırlarda görüldü. Ayrı ölçüm yardımcısı kendi yerel kanalından bir PowerShell aktarımı için 60.225 alınan bayt bildirdi. Bu sayı testte örneklenen aktarım dilimine aittir. Aynı 15 birim testi Linux’ta da geçti. Fiziksel Windows 11 masaüstünde bildirimlerin görünmesi ve mağaza uygulamalarının kendilerine özgü simgeleri elle kontrol edilebilir. [Windows kontrol listesi](windows-kontrol.md)

Taşınabilir arşiv 49.803.566 bayt, açılmış dosyalar 169.634.617 bayttır. 7-Zip bütünlük denetimi geçti. SHA-256 değeri `7ede62873178a5a914171dd2004be757b7d28aae61344f1d99aa05239f75bcdc`.

Arayüz Microsoft’un WPF Luna ve Aero tema kaynaklarını kullanır. Sistem simgeleri kurulu Windows sürümünün Shell kitaplığından gelir. Etkin TCP bağlantıları süreç kimlikleriyle eşleştirilir. Dosya simgesi Windows Shell’den, paketlenmiş uygulamaların simgeleri Windows’un AppsFolder kaydından alınır. [Microsoft TCP tablosu](https://learn.microsoft.com/en-us/windows/win32/api/iphlpapi/nf-iphlpapi-getextendedtcptable) · [Sistem simgeleri](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shgetstockiconinfo) · [Uygulama simgesi](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellitemimagefactory-getimage)

## BlessedLine 0.2.0

Debian x64 ve .NET SDK 10.0.404 ile Release çözüm derlemesi 0 uyarı ve 0 hatayla tamamlandı. 15 xUnit testi geçti. Testler kota eşiğini, tek limit bildirimini, kota uzatmayı, duraklatmayı, Türkçe veri birimlerini, monotonik ölçüm aralıklarını, bağdaştırıcı değişiminde referans yenilemeyi ve duvara yaklaşan kullanım göstergesinin sınırlarını kapsar.

`dotnet publish` ile çalışma zamanı içeren `win-x64` yayın üretildi. `BlessedLine.exe`, x64 Windows GUI yürütülebilir dosyası olarak doğrulandı. Uygulama ve filtreleme uygulama süreciyle sınırlıdır. Kurucu kaynağı güncellendi, kurucu derlenmedi.

[Windows çalıştırıcısında](https://github.com/ugrqn/Blessed-Line-Net/actions/runs/45290725637) aynı kaynaklar derlendi ve 15 test geçti. WPF uygulaması açıldı. Ana panel 568 × 568, mini panel 365 × 234 ölçüldü ve ana panele dönüş kontrol edildi. Yönetici oturumundaki gerçek TCP trafiği `curl` ve PowerShell için ayrı satırlara yazıldı. Başka DPI değerleri, UDP/IPv6, ses çıkışı ve WFP kota engeli ile kurtarma [Windows kontrol listesindeki](windows-kontrol.md) sonraki deneylerdir.

7z arşivi bütünlük testinden geçti. İndirilen arşiv 44.709.485 bayt, açılmış dosyalar 153.502.667 bayttır. Arşivin SHA-256 değeri `4b9a6d6089f0b6ffff0b5435d79c2c5ca6d1d4e6e6b681536a0f142f29f135b3` olarak ölçüldü.

## RetroNet Control 0.1.0

2026-11-23, Debian 13 x64. .NET SDK 10.0.404 yerel `/tmp/retronet-dotnet` konumuna kuruldu. Başlangıçta sistem `dotnet` komutu yoktu.

| İşlem | Sonuç |
|---|---|
| Core Release derleme | Başarılı, 0 uyarı ve 0 hata |
| WPF Release çapraz derleme | Başarılı, 0 uyarı ve 0 hata |
| xUnit kota testleri | 5 geçti, 0 başarısız |
| win-x64 self-contained publish | Başarılı, `artifacts/win-x64/Desktop` |
| Taşınabilir ZIP | 402 dosya, yayın 146.452.424 bayt, ZIP 62.963.094 bayt |
| Windows 11 üzerinde başlatma, ağ ölçümü, DPI ve kurulum | Beklemede, Windows ortamı yok |
| ETW uygulama atfı, servis, IPC, SQLite, WFP engelleme ve kurtarma | Henüz uygulanmadı |

Bu paket, faz 1'in masaüstü önizleme dilimidir. Kotalar yalnızca bildirim üretir. Bağdaştırıcı seçimi tek adaptörle sınırlıdır. Oturum toplamı uygulama açıldığında sıfırdan başlar. Sayaçlar kaydedilir ancak uygulama kapalıyken tüketim toplanmaz. Inno Setup kaynağı hazırdır, kurucu derlenmemiştir. İmza yoktur.

ZIP SHA-256: `f9acd44b7dbfdab376bed324a85c7de7de75c3b3e5de4c42e76c15605a5bcbd6`.

GitHub’a aktarım için hazırlanan çözüm Debian üzerinde Release modunda yeniden derlendi. Derleme 0 uyarı ve 0 hatayla tamamlandı. Beş kota testi yeniden geçti. Sürüm varlığı olarak kullanılan taşınabilir ZIP’in bütünlüğü ve SHA-256 değeri tekrar doğrulandı.
