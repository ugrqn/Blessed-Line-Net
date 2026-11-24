# Doğrulama kaydı

## BlessedLine 0.6.0

Windows Server 2025 üzerinde 23 çekirdek testi ve 32 uygulama kontrolü çalıştı. Gerçek indirmede veri kotasının seçili EXE’nin bağlantısını kestiği, başka bir işlemin internet erişiminin sürdüğü ve satırdaki kilitle bağlantının geri açıldığı doğrulandı. Sıfırlama, BlessedLine’ın güvenlik duvarı ve QoS kurallarını temizledi. İlgisiz test kuralı korundu. [Windows kaynak testi](https://github.com/ugrqn/Blessed-Line-Net/actions/runs/45408926770)

Çift tıklamayla açılan canlı grafik, favori kaydı ve sıralaması, trafik renklerinin kapatılması, ping alanı ve Wi-Fi bilgi ekranı denetlendi. QoS yükleme kuralının Windows’a doğru EXE yolu ve 1.500 kbit/sn değeriyle yazıldığı görüldü. QoS doğrulaması kural kaydını kapsadı. Wi-Fi doğrulaması çalıştırıcıda ekran ve tarama denetimleriyle sınırlıydı. Fiziksel radyo, yakın ağ sinyali ve yeniden bağlanma için fiziksel bir Wi-Fi bağdaştırıcısı gerekir.

İndirilen x64 ve x86 arşivlerine aynı 32 kontrol uygulandı. Ayrıca Windows başlangıcı, tepsi ve küçük panel, Aero başlık köşesi, kota uyarıları, iki gerçek sürece veri atfı, standart kullanıcı yardımcısı ve 11 dilde açılış geçti. x86 paketi Windows’un 32 bit uyumluluk katmanında çalıştırıldı. [Windows paket testi](https://github.com/ugrqn/Blessed-Line-Net/actions/runs/45409549648)

| Çalışma kümesi | Windows x64 | Windows x86 |
|---|---:|---:|
| Ana pencere ve ayrı yetki yardımcısı, örneklenen en yüksek toplam | 97,97 MiB | 97,99 MiB |
| Tepsi, otomatik uygulama ölçümü açık | 71,8 MiB | 66,8 MiB |
| Tepsi, uygulama ölçümü kapalı | 65,0 MiB | 56,2 MiB |

Ana pencere ve yardımcı için 25 örnek 100 ms arayla alındı. Tepsi değerleri üç ölçümün ortancasıdır. Windows kural yazımında kısa süreli ayrı bir PowerShell işlemi çalışır.

İki Windows arşivi 7-Zip bütünlük testini geçti. GitHub Releases’ten indirilen arşiv ve indirme kartlarının SHA-256 değerleri yerel dosyalarla eşleşti. EXE Authenticode sonucu `NotSigned`. [Sürüm ve SHA-256 listesi](https://github.com/ugrqn/Blessed-Line-Net/releases/tag/v0.6.0-preview)

## BlessedLine 0.5.0

Windows paket testleri Win7 başlığının sol üst pikselini, otomatik ölçümle canlı süreç trafiğini, Windows başlangıç kaydını ve standart kullanıcı yardımcı kanalını kontrol etti. Tepsi modunda erken uyarı ve kota doldu pencerelerinin üstte olduğu doğrulandı. +200 MB ile seçili kota arttı, devam et seçimi pencereyi kapattı ve 150 MB uyarı payı formdan kaydedildi. 23 çekirdek testi geçti. [Windows paket testi](https://github.com/ugrqn/Blessed-Line-Net/actions/runs/45393548644)

### Tepsi başlangıcında RAM

Windows Server 2025 yönetici oturumunda üç örneğin ortancası ölçüldü. Otomatik ölçüm açıkken bu oturum tek süreçte ETW kullanır. Windows standart kullanıcı için ayrı yardımcı süreç açarsa toplam bellek kullanımı farklı olabilir.

| Paket | 0.4.1 ölçüm kapalı | 0.5.0 ölçüm kapalı | 0.5.0 otomatik ölçüm |
|---|---:|---:|---:|
| x64 çalışma kümesi | 63,8 MiB | 63,5 MiB | 71,3 MiB |
| x86 çalışma kümesi | 54,9 MiB | 55,1 MiB | 66,3 MiB |
| x64 özel bayt | 15,2 MiB | 15,3 MiB | 19,8 MiB |
| x86 özel bayt | 13,9 MiB | 13,7 MiB | 22,4 MiB |

Wi-Fi geri dönüşü, radyoyu açtıktan sonra yakalanabilen önceki Windows ağına bağlanmayı dener. Windows test çalıştırıcılarında fiziksel Wi-Fi bağdaştırıcısı ve kayıtlı ağ olmadığı için gerçek ağdan ayrılma ve yeniden bağlanma adımı donanım üzerinde doğrulanamadı. [Donanım kontrolü](windows-kontrol.md) · [Windows WLAN bağlantı API’si](https://learn.microsoft.com/windows/win32/api/wlanapi/ns-wlanapi-wlan_connection_parameters)

## BlessedLine 0.4.1

Yüklenen ICO’nun orijinal 256 piksel karesi x64 ve x86 EXE kaynaklarında bayt düzeyinde doğrulandı. Simge dosyası ayrıca küçük görev çubuğu ve bildirim alanı boyutları için 16–64 piksel kareler içerir. Windows testi mini penceredeki simgeyi, başlangıç kayıt defteri değerini, açılışı kapatıp tekrar açmayı, penceresiz ağ ölçümünü ve ikinci EXE çalıştırmasının mevcut pencereyi açmasını doğruladı. [Windows paket testi](https://github.com/ugrqn/Blessed-Line-Net/actions/runs/45386216755)

Windows Server 2025 üzerinde 0.4.0 uygulamasında ana pencere açıldı ve tepsiye gizlendi. 0.4.1 doğrudan tepside başlatıldı. Her süreç için üç ölçümün ortanca değeri kullanıldı. Çalışma kümesi o anda RAM’de bulunan sayfaları, özel baytlar ise sürecin kendi ayırdığı belleği gösterir.

| Paket | 0.4.0 çalışma kümesi | 0.4.1 çalışma kümesi | 0.4.0 özel bayt | 0.4.1 özel bayt |
|---|---:|---:|---:|---:|
| x64 | 146,8 MiB | 64,1 MiB | 55,7 MiB | 15,4 MiB |
| x86 | 129,7 MiB | 55,2 MiB | 54,4 MiB | 13,6 MiB |

İki Windows paketi de 21 çekirdek testini, çekirdek ağ ölçümü ve arayüz testlerini geçti. Oturum açılışında kullanılan kayıt doğrulandı ve kayıtlı komut test ortamında çalıştırıldı.

7-Zip her iki Windows arşivinin bütünlüğünü doğruladı. GitHub’dan indirilen paketlerin SHA-256 değerleri yerel paketlerle eşleşti. [Arşivler ve SHA-256 listesi](https://github.com/ugrqn/Blessed-Line-Net/releases/tag/v0.4.1-preview)

## BlessedLine 0.4.0

Windows kaynak derlemesi 21 birim testiyle birlikte geçti. Windows çalıştırıcısında ana ve mini pencere, mini panelden 1.234,5 MB kotanın yazılması, tepsiden dönüş, Aero tercihi, 11 dilde açılış, iki süreçte canlı trafik atfı ve standart kullanıcıda bağlantı listesi kontrol edildi. [Windows doğrulaması](https://github.com/ugrqn/Blessed-Line-Net/actions/runs/45377940525)

Kota tetikleyici testleri mevcut kotanın aşılması, sınırı yeni geçme, sayaç duraklatma ve farklı bağdaştırıcıyı kapsamaktadır. GitHub Windows çalıştırıcısında fiziksel Wi-Fi radyosu yoktur. Radyo kapanması ve açılmasının donanımdaki sonucu [Windows kontrol listesinde](windows-kontrol.md) yer alır.

Debian 13 üzerinde Linux arşivinden başlatılan Tk arayüzü, yerel ağ bağdaştırıcısı, mini panel ve 11 dil kontrolüyle açıldı. NetworkManager kapatma ve açma komutlarının çağrılması testte taklit edildi. Aynı Linux ortamında Chromium ile indirme sayfasının 11 dilinde tarayıcı diline göre açılış, Arapça ve Urduca sağdan sola yön, mobil yerleşim, dil değiştirme ve üç bağlantının hedefi kontrol edildi.

Her iki Windows paketi de Windows Server 2025 x64 çalıştırıcısında arşivden ayıklandı ve EXE üzerinden aynı arayüz, 11 dil ve trafik testlerini geçti. x86 programı Windows’un 32 bit uyumluluk katmanında çalıştırıldı. Her iki arşiv 7-Zip bütünlük testini geçti. GitHub Releases’ten indirilen Windows, Linux ve indirme sayfası paketlerinin SHA-256 değerleri yerel paketlerle eşleşti. [Windows paket testi](https://github.com/ugrqn/Blessed-Line-Net/actions/runs/45378388914) [Sürüm](https://github.com/ugrqn/Blessed-Line-Net/releases/tag/v0.4.0-preview)

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
