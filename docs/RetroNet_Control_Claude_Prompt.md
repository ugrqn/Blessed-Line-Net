# RetroNet Control — Claude geliştirme promptu

## 1. Rolün ve teslim edeceğin ürün

Kıdemli bir Windows masaüstü ve ağ yazılımı geliştiricisi olarak **RetroNet Control** adlı, Türkçe arayüzlü, yerel çalışan bir Windows uygulaması geliştir. Kullanıcı masaüstünde ne kadar veri harcadığını, hangi uygulamanın ne hızda indirme ve yükleme yaptığını görebilsin. Tek uygulama, seçili uygulama grubu veya bilgisayar geneli için bağımsız sayaç başlatabilsin. Örneğin **1 GB limit, 100 MB kala uyar, limitte Steam'i engelle** ayarını seçebilsin. Varsayılan kota eylemi **Yalnızca uyar** olsun.

İlk hedef Windows 11 x64. Windows XP ve Windows 7, özgün görsel temalara ilham verir. Windows 10 desteği ancak hedef sürüm, runtime ve gerçek Windows testleri doğrulandıktan sonra duyurulabilir. Kullanıcının cihazında çalışma sırasında ücretli servis, abonelik, Node.js, WebView2, özel kernel sürücüsü veya paket yakalama sürücüsü gerektirme. Uygulama çevrimdışı açılabilsin. Kurulumdan sonra olağan çalışma için ek indirme gerektirmeyen yayın seçeneklerini karşılaştır ve gerçek paket boyutunu raporla.

## 2. İlk yanıt, plan onayı ve çalışma biçimi

İlk yanıtın **yalnızca Faz 0 planı** olsun. Dosya oluşturma veya kod yazma. Kısa bir ürün özeti ve ardından şunları ver:

1. Ortam matrisi. Windows, Linux/cloud ve sadece sohbet ortamında neleri derleyebildiğini veya koşabildiğini açıkça belirt.
2. Kapsam matrisi. Global/uygulama ölçümü ve global/uygulama engellemesi için veri kaynağı, yetki, LAN/VPN etkisi, mevcut bağlantı davranışı ve doğrulama yöntemi.
3. Mimari akış, başlangıç klasör ağacı, gerekli NuGet paketleri ve her paketin gerekçesi.
4. İlk Windows fizibilite deneyleri. Test trafiği, karşılaştırılacak sayaçlar, beklenen davranış ve deneyin başarısız sayılacağı koşullar.
5. Faz başına somut teslimat ve kabul ölçütü. Özellikle uygulama bazında doğru atıf, global kapsam ve güvenli geri alma risklerini öne al.

Planın sonunda **“BAŞLA”** onayımı bekle. Bu tek onaydan sonra uygulanabilir fazları sırayla ilerlet. Rutin kararlar için tekrar onay isteme. Canlı makinenin erişimini kesebilecek doğrulama deneylerini yalnızca izinli ve izole Windows ortamında çalıştır. Bir özellik o ortamda doğrulanamıyorsa kod ve platformdan bağımsız testlerle ilerle, Windows doğrulamasını açıkça beklemede işaretle. Sadece sohbet erişimin varsa her fazda tam dosyaları göreli yollarıyla ver. Çalışma alanın varsa dosyaları doğrudan yaz, güncelle ve çalıştırabildiğin komutları çalıştır. Gerçek sonuçları raporla.

## 3. Teknoloji ve mimari

- **C# / .NET 10 LTS, yerel WPF + XAML ve MVVM.** Tema için WPF ResourceDictionary ve özgün bileşenler kullan. Paketleri sabitle ve derleme talimatlarını doğrula. CommunityToolkit.Mvvm, Microsoft.Data.Sqlite, ETW için Microsoft.Diagnostics.Tracing.TraceEvent ve test için xUnit seçimlerini Faz 0'da doğrula.
- Standart kullanıcıyla çalışan **WPF arayüzü**, ağ izleme ve kota işlemlerini yürüten **Windows Service**, platformdan bağımsız **Core** ve bağımsız **Recovery** aracı. Başlangıç ağacı: `src/RetroNet.Desktop`, `src/RetroNet.Service`, `src/RetroNet.Core`, `src/RetroNet.Recovery`, `tests/`, `installer/`, `docs/`. Windows entegrasyonunu serviste amaçlarına göre klasörle. Gerçek bağımlılık sınırı gerektirdiğinde ayrı proje aç.
- Akış: **Windows sayaçları ve ETW → toplulaştırma → kota motoru → kalıcı durum ve ağ politikası → IPC → WPF**. Servis, sayaçlar ve kurallar için tek yazıcı ve durum otoritesi olsun. Tepsiye gizlemek veya arayüzden çıkmak servisi ve kotaları durdurmasın. Menüde bunu açıkça belirt. Kullanıcının kota işleyişini duraklatabileceği ayrı bir kontrol sun.
- IPC yerel Named Pipes üzerinden çalışsın. Yalnızca yetkili yerel kullanıcıların bağlanabildiği ACL kur ve işletim sisteminden elde edilen kimlikle her komutu yetkilendir. Kullanıcının gönderdiği SID veya ham WFP/komut satırı talimatlarına güvenme. İstek kimliği, protokol sürümü, sınırlı mesaj boyutu, zaman aşımı ve iptal desteği ekle. UI yeniden bağlandığında güncel tam durum alabilsin. Global sayaç ve global engellemenin makine kapsamını görünür kıl ve kimlerin değiştirebileceğini açıkça tanımla.

## 4. Gerçek ağ ölçümü

**Bilgisayar geneli:** IP Helper arayüz sayaçlarını (örneğin `GetIfEntry2`) değerlendir. Yön başına 64 bit bayt farkını, monotonik saatle ölçülen gerçek süreye böl. Arayüz değişiminde, sayaç gerilediğinde ve uyku dönüşünde yeni referans al. Wi‑Fi, Ethernet ve USB paylaşımını doğrula. Birden fazla adaptör seçilebilsin. VPN tüneli ve fiziksel taşıyıcı trafik için aynı baytları iki kez ekleme riskini test et. Özellikle adaptör sayaçları LAN trafiğini de kapsıyorsa arayüzde **“Seçili bağdaştırıcı trafiği”** yaz ve kota formunda kapsamı göster. Güvenilir internet/LAN ayrımı doğrulanırsa ayrı filtre veya ölçüm kapsamı olarak sun. ISS faturasına birebir eşdeğer kullanım iddia etme.

**Uygulama bazında:** ETW TCP/UDP gönderme ve alma olaylarında kullanılan bayt alanını, olay sağlayıcısını ve süreç kimliğini kontrollü transferle doğrula. IPv4, IPv6 ve UDP/QUIC kapsamasını ayrı test et. ETW olay başlığındaki PID'yi körü körüne kullanma. Çalışan süreç örneğini PID + başlangıç zamanı veya doğrulanmış eşdeğeriyle tanı. Geçmiş, kota ve kural için doğrulanmış yürütülebilir yol veya desteklenen Windows uygulama kimliği kullan. Aynı kimliğe ait birden çok süreç tek satırda toplansın. Yardımcı EXE'ler isim benzerliğiyle ana uygulamaya eklenmesin. Kullanıcı açıkça grup seçebilsin. Kapanan süreçlerin birikimi korunsun. Erişilemeyen veya güvenilir biçimde atfedilemeyen trafiği **“Atfedilemeyen”** olarak işaretle. Loopback'i varsayılan kapsamdan çıkar.

Global kota seçili global ölçüm kaynağından, uygulama kotası uygulama atfından beslensin. Global ve uygulama baytlarını toplayarak yeni bir toplam üretme. Ölçüm katmanları, VPN, arabellek kaybı ve veri boşlukları nedeniyle oluşan farkları raporla. Toplayıcı durduğunda ekranda **“Ölçüm kesildi”** veya **“Son ölçüm”** göster ve son ölçüm zamanını belirt. Veri yokken sıfır tüketim varsayma. Bayat, doğrulanmamış veya yeniden oynatılmış veriden yeni otomatik engel üretme.

UI'ya yaklaşık saniyede bir toplu anlık görüntü gönder. Kota motoru UI hızına bağlı çalışmasın. Yük altında uyarı ve engelleme gecikmesini ölçerek toplama aralığını seç. Sınırlı kuyruk, kayıp olay sinyali, iptal edilebilir işçiler ve dönen loglar kullan. ETW oturumunu kendi benzersiz kimliğiyle yönet, başka bir yazılımın oturumuna dokunma. Performans hedefi olarak olağan yükte toplam CPU için %2 civarı, UI için 120 MB altı başlangıç al. UI ve servisi ayrı ölç, donanım ve trafik koşullarını kaydet.

## 5. Sayaç, bildirim ve veri kuralları

- **Yeni Sayaç** formunda ad, kapsam, hedef, trafik yönü (indirme, yükleme veya ikisi), kota (isteğe bağlı), erken uyarı ve limit eylemi bulunsun. Sayaç bağımsız olarak başlatılabilsin, duraklatılabilsin, sürdürülebilsin, düzenlenebilsin, sıfırlanabilsin ve silinebilsin. Duraklatma genel izlemeyi durdurmasın. Sayım durumu ile engel durumu ayrı gösterilsin. Varsayılan olarak duraklatma mevcut engeli kaldırmasın, kaldırmak için açık kontrol sun.
- İç hesaplarda tam sayı bayt kullan. **1 MB = 1.000.000 bayt, 1 GB = 1.000.000.000 bayt**. Örneğin 1 GB kota ve 100 MB kala uyarıda eşik **900 MB**. İkili birimler sunuluyorsa MiB ve GiB biçiminde etiketle. Türkçe ondalık virgülü işle. Kota, uyarı miktarı ve yüzdeyi formda doğrula.
- Sabit miktar kala veya doluluk yüzdesiyle ön uyarı ver. Sayaç döngüsü, eşik ve limit sürümü bazında bildirim tekrarını önle. Yüksek hızda bir ölçümde iki eşik geçilirse tek bir anlaşılır **“Limit doldu”** bildirimi üret ve ön uyarının ayrı gösterilemediğini kaydet. **Engellemeden önce müdahale süresi istiyorum** senaryosu için isteğe bağlı geri sayım sun. Kullanıcı örneğin **15 saniye sonra engelle**, **+100 MB ekle** veya **Bu döngüde engelleme** seçebilsin. Geri sayımda trafiğin sürebileceğini göster. Bayt düzeyinde kesin kesme garantisi verme ve gerçek aşımı ölç.
- Limit eylemleri: **Yalnızca uyar**, **Onay iste**, **Seçili kapsamı engelle**, **Geri sayım sonunda engelle**. Kullanıcı otomatik engellemeyi açıkça seçsin. Ön uyarı ve limit bildirimlerinde, duruma göre **+100 MB**, **+500 MB**, **Şimdi engelle** veya **Engeli kaldır** gibi ilgili işlemleri göster. Kota uzatmak tüketimi silmesin. Uzatma sonrası limitin altında kalınırsa sadece o kotanın engelleme talebi kalksın ve yeni ön uyarı eşiği yeniden değerlendirilsin.
- Sayaç durumu, eşik teslimi ve ağ politikası sonucunu ayrı modelle. Aynı hedefi engelleyen iki kotadan biri kaldırıldığında diğerinin engeli sürsün. **“Tüm engelleri kaldır”** uygulanmış RetroNet engellerini kaldırır ve ilgili kota taleplerini mevcut döngü için askıya alır. Sonraki ölçüm engeli sessizce yeniden kurmasın. Kullanıcının yeni bir döngüde veya yeniden etkinleştirme sırasında ne olacağını arayüzde göster.
- SQLite'ta sayaçlar, tüketim özetleri, dönemler, bildirimler, politika niyet ve sonuçları ile ayarları sakla. Toplu yazma için yaklaşık **5 saniye** başlangıç hedefi kullan. Eşik, kota düzenleme ve politika geçişlerini gecikmeden işle. SQLite ve WFP işlemlerini tek atomik işlem gibi ele alma. Dayanıklı politika niyeti, idempotent uygulama ve başlangıçta gerçek durumla mutabakat kullan. Sürümlemeli şema, veri saklama sınırı ve bozuk/veri yazılamayan disk davranışı tanımla. Kalıcılık veya doğrulama başarısız olursa yeni otomatik engel oluşturma ve kullanıcıya durumunu bildir.
- İlk çalışan sürüm manuel ve tek seferlik sayaçları içersin. Günlük ve aylık kotaları aynı sürüm yol haritasında ekle. Günlük varsayılan 00.00 yerel saat. Aylık seçilen gün ilgili ayda yoksa ayın son günü. UTC zaman damgası, belirli saat dilimi ve kalıcı dönem kimliği kullan. Yaz saati, bilgisayar kapalıyken geçen dönem ve saat değişiminde aynı dönemin iki kez açılmasını test et.

## 6. Engelleme ve çevrimdışı kurtarma

- Tercih edilen ilk yaklaşım servisin sahip olduğu **dinamik Windows Filtering Platform oturumu** ve kendi provider/sublayer/filtre GUID'leri. Uygulama kimliği ile eşleştirme, IPv4/IPv6, yeni bağlantılar, mevcut TCP/UDP akışları ve VPN üzerinde doğrula. Alternatif olarak yalnız uygulama kapsamındaki Windows Firewall API yaklaşımını değerlendir. Aynı kural için aynı anda iki altyapıyı etkinleştirme. Sistemin güvenlik duvarı, başka kurallar ve ağ bağdaştırıcılarını değiştirme.
- Uygulama bazında hedef kimliğini doğrula ve etkilenmeyecek ikinci uygulamada eşzamanlı trafik testi yap. Global engelde hedef kapsamı, etkin arayüzleri ve yerel ağ davranışını önceden göster. Loopback, ağın temel çalışması, DHCP ve yerel erişim gereksinimlerini Faz 0'da değerlendir. Örneğin sadece özel IP bloklarına bakarak LAN istisnasını çözülmüş sayma. Bir engel türünün kapsaması ve geri alınması doğrulanana kadar otomatik çalışmasını kapalı tut ve sınırlamayı göster.
- Filtrelerin uygulanmasını ve gerektiğinde kaldırılmasını idempotent ve kayıtlı GUID'lerle yönet. Kısmi uygulamayı geri al veya açık hata durumu göster. UI'da **“Uygulanıyor”**, **“Filtre etkin”**, **“Kısmi/hata”** gibi sonuçları ayırt et. Filtre kaydının oluşmasıyla gerçek trafiğin durmasının ayrı doğrulamalar olduğunu kabul et.
- Engeller aktifken görünür bir kurtarma bandı ve tepsi menüsünde **“Tüm engelleri kaldır”** aksiyonu sun. Uygulama açılmasa bile bağımsız, yerel ve gerektiğinde UAC isteyen kurtarma aracı çalışsın. Dinamik nesneler sağlıklı oturuma aitken servis üzerinden güvenli kaldırmayı kullan. Servis yanıt vermiyorsa sahibini güvenli biçimde durduracak, yeniden başlatmada eski engeli hemen kurmayacak bir kurtarma akışı tasarla. Yetki, erişim, durdurma veya doğrulama hatasını açıkça göster. Üçüncü taraf kurallarını geniş isim eşleşmesiyle silme.
- Varsayılan hata davranışı bağlantıyı serbest bırakacak şekilde tasarlansın. UI çökse bile çalışan servis sayaçları sürdürür. Servis çökünce veya durunca kendi dinamik oturum nesnelerinin temizlenmesini Windows üzerinde doğrula. Sayaç verisini yeniden başlatmada geri getir. Uygulanmış engelleri varsayılan olarak sessizce yeniden kurma. Yeniden etkinleştirme ve dönem yenileme davranışını görünür kıl. Kaldırıcı önce servisi ve uygulamaya ait politikaları güvenle temizlesin.

## 7. Kompakt arayüz ve iki tema

- **Kompakt** öncelikli tasarım. İlk açılışta aktif monitörün görev çubuğu hariç kullanılabilir alanının yaklaşık altıda birini hedefle. 1920×1080, %100 ölçekli örnekte **500×660 DIP** civarı. Önerilen alt sınır **380×420 DIP** ve dar ekranda öncelikli kontrolleri koruyan kaydırma. Kullanıcı pencereyi büyütebilsin. Yaklaşık **720 DIP** genişlikte iki sütunlu **Geniş** düzene geç. **Mini** alan yaklaşık **300×84 DIP** ve yalnızca hızlar ile seçili ana kota. Son konumu ve boyutu hatırla. Monitör ayrıldığında pencereyi görünür alana geri taşı. %100, %125 ve %150 DPI, klavye erişimi ve standart Windows pencere hareketlerini doğrula.
- Kompakt ana ekranda sırayla bağlantı ve ölçüm durumu, büyük **↓ İndirme / ↑ Yükleme** sayıları, 60 saniyelik küçük grafik, toplam kullanım, etkin kota ilerlemesi, kalan veri ve **Yeni Sayaç** düğmesi. Uygulamalar sayfasında ikon, ad, anlık iki yönlü hız, birikim, arama, sıralama ve ilgili kota/engel aksiyonları. Sayaçlar sayfasında tüm sayaçların durumu ve işlemleri. Genel/Ayrıntılı görünümde ölçüm kapsamı ve servis durumu kolayca bulunabilsin.
- **XP Luna esintisi:** Mavi gradyanlı başlık, krem içerik, kabartmalı düğmeler, parçalı kota çubuğu ve sade temalı bildirimler. **Windows 7 Aero esintisi:** Açık mavi-gümüş yüzey, cam hissi veren katmanlı başlık, parlak kenarlar ve yumuşak kota çubuğu. DWM bulanıklığının bulunmadığı sistemlerde WPF gradyanlarıyla görünümü koru. Özgün ikon ve kontroller kullan. Tema anında değişsin, seçim saklansın. URL üzerinden kaynak yükleme veya zorunlu tema indirme ekleme.
- Tepsi, her zaman üstte tutma, mini geçişi, görünür engel durumu ve bağlantı geri alma. Uyarı renklerine metin/ikon eşlik etsin. Ses isteğe bağlı. Uygulama içinde bildirim geçmişi olsun, sistem bildiriminin gösterilmesi Windows ayarlarına bağlı olabilir. UI yeniden bağlanınca kaçırdığı önemli olayları durum geçmişinden alabilsin.

## 8. Uygulama fazları, doğrulama ve yayın

| Faz | Teslimat ve kabul ölçütü |
|---|---|
| 0. Fizibilite | İlk yanıtta plan. Onaydan sonra küçük Windows deneyleriyle IP Helper sayacı, ETW atfı ve geri alınabilir uygulama/global filtre davranışını kontrol et. Sonuçları platform bazında kaydet. |
| 1. İskelet ve gerçek global ölçüm | Çözüm, WPF, iki özgün tema, kompakt düzen ve gerçek adaptör ölçümü. Test verisi yalnızca ayrı ve açıkça etiketli önizleme kipinde bulunabilir. |
| 2. Uygulama ölçümü ve servis | ETW, PID yaşam döngüsü, uygulama gruplama, yerel IPC ve ölçüm kaybı göstergesi. Tarayıcı ve ayrı test uygulamasında eşzamanlı trafiği doğrula. |
| 3. Sayaç ve kalıcılık | Gerçek örneklerle sayaç, erken uyarı, limit, uzatma, dönem, SQLite ve tepsi. Yeniden başlatmada veri ve eşik davranışını test et. |
| 4. Ağ politikası ve kurtarma | Önce tek uygulama, ardından global engelleme. İzole Windows ortamında uygulanmış kural, gerçek trafik ve çevrimdışı geri alma kanıtlarını topla. Ulaşılamayan Windows doğrulamasını beklemede göster. |
| 5. Ürün ve dağıtım | Geniş/Mini düzenleri ve son erişilebilirlik kontrolleri. Temiz Windows'ta servis, UI, kurtarma aracı ve kaldırıcı içeren win-x64 paketini test et. Paket boyutu, bağımlılıklar ve imza durumunu raporla. |

Kabul senaryoları:

1. **Steam 1 GB / 100 MB kala.** 900 MB eşiği ve 1.000 MB limitini deterministik testte doğrula. Yüksek hızda iki eşik aynı toplu örnekte geçilirse tek limit bildirimi çıkmalı. Gerçek Steam testinde Chrome çalışmaya devam etmeli ve Steam yeniden başlatıldığında doğrulanmış hedef kimliği uygulanmalı.
2. **Global kota ve çakışan kotalar.** Global eylem doğrulanmış ağ kapsamını etkilemeli. Alttaki uygulama engeli, başka bir kotanın kaldırılmasından etkilenmemeli. Acil geri alma sonrası bir sonraki örnek eski engeli yeniden kurmamalı.
3. **Ağ değişimi ve süreç ömrü.** Wi‑Fi → Ethernet, VPN açık/kapalı, IPv4/IPv6, TCP/QUIC, uyku dönüşü, sayaç sıfırlanması, PID yeniden kullanımı, hızlı süreç kapanışı ve uzun çalışma. Yerel ağ ve internet kapsamlarını ayrı gözle.
4. **Dayanıklılık ve yetki.** UI çökmesi, servis çökmesi, yeniden başlatma, servis/pipe yetki hatası, disk yazma hatası, yarım politika işlemi ve kaldırma. Yetkisiz yerel istemci ağ politikası oluşturamamalı. Kurtarma çevrimdışı çalışmalı.
5. **Gerçek paket.** Temiz Windows 11 x64 üzerinde kur, standart kullanıcı olarak aç, iki temayı değiştir, gerçek ölçümü ve kota akışını kullan, kaldır ve uygulamaya ait engel kalmadığını doğrula.

Faz sonunda değişen dosyalar, gerçekten çalıştırılan komutlar, sonuçlar, doğrulanmış davranış, bekleyen Windows testleri ve bir sonraki fazı kısa raporla. Paket geri yükleme, derleme, unit test ve gerçek Windows çalışma doğrulaması farklı çıktılardır. Uçtan uca doğrulama için Windows testini ayrıca listele. Sonraki sürüm işleri arasında gelişmiş zamanlama, uzun dönem raporları, CSV/JSON, tahmini kalan süre ve ek uygulama profilleri olsun. Bunlar temel ölçüm, kota ve kurtarma akışının önüne geçmesin.

## 9. Başlangıç teknik referansları

- [IP Helper — GetIfEntry2](https://learn.microsoft.com/en-us/windows/win32/api/netioapi/nf-netioapi-getifentry2)
- [WFP — Nesne yönetimi ve dinamik oturumlar](https://learn.microsoft.com/en-us/windows/win32/fwp/object-management)
- [WFP — ALE akışlarının yeniden yetkilendirilmesi](https://learn.microsoft.com/en-us/windows/win32/fwp/ale-re-authorization)

**Şimdi yalnızca Faz 0 planını ver ve “BAŞLA” mesajımı bekle.**
