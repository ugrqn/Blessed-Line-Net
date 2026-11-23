# RetroNet Control | Sol için uygulama ve teslim planı

## Çalıştırma talimatı

Ekli RetroNet_Control_Claude_Prompt.md ürün şartnamesidir. Bu plan onun onay akışını günceller. Bu iki dosyayı birlikte kullan. Hemen depoyu incele, Faz 0–5'i sırayla uygula, komutları kendin çalıştır ve düzeltilebilir hataları gider. Faz sonunda kısa kayıt bırakıp sonraki faza geç. Nihai hedef Türkçe, çevrimdışı çalışan Windows 11 x64 uygulamasının kurulabilir paketidir. Her fazın yapabildiğin kısmını tamamla. Gerçek makinenin bağlantısını kesebilecek deneyi yalnız izinli, izole Windows ortamında çalıştır.

## Faz 0 • Ortam ve ölçüm deneyleri

Önce mevcut çözümü, depo talimatlarını, dotnet --info çıktısını, SDK listesini, işletim sistemi ve mimariyi kaydet. Var olan kullanıcı dosyalarını koru. .NET 10 SDK yama sürümünü global.json ile sabitle. Core ve mantık testleri net10.0, WPF net10.0-windows ve UseWPF=true olsun. Windows projelerinde Linux çapraz derlemesi için EnableWindowsTargeting kullan.

| Ortam | Çalıştırılabilecek iş | Kanıt |
|---|---|---|
| Linux ve .NET 10 SDK | Restore, Core testleri, Windows hedefleme paketleri erişilebilirse WPF derleme ve win-x64 publish | Çıkış kodları, test sayısı, dosya boyutları |
| Windows 11 x64 | WPF, ETW, IP Helper, servis, IPC, kurulum, tepsi ve DPI | Sürüm, yetki, adaptör ve gözlenen sonuç |
| İzole Windows VM ve ayrı trafik eşi | Bağlantı kesme, servis çökmesi, yeniden başlatma ve kurtarma | İki uçta zaman damgalı trafik, önce/sonra filtre envanteri |
| Yalnız sohbet | Dosya içerikleri ve komutlar | Çalıştırma sonucu bilinmiyor |

| İşlev | Kaynak, yetki ve kapsam | Geçiş ölçütü |
|---|---|---|
| Global ölçüm | Servisten IP Helper GetIfEntry2. Seçili adaptörlerde LAN dahil her iki yön. VPN ile taşıyıcısını birlikte seçince çift sayım riski | Tek adaptör, iki adaptör ve VPN ile uç değerleri ve Windows sayaç farklarını kaydet. Çakışan seçimde kullanıcıya kapsamı göster |
| Uygulama ölçümü | Servisin yetkili ETW oturumunda TCP/UDP ağ olayları. Adaptör ile süreç ölçümleri farklı katmanlarda | Veri alanı, sağlayıcı, olay sürümü, yön, PID kaynağı ve loopback ayrımı kanıtlanan olayları kabul et |
| Uygulama engeli | Yetkili serviste dinamik WFP oturumu, doğrulanmış uygulama kimliği, IPv4/IPv6 | Yeni ve açık TCP/UDP akışlarını ayrı gözle. İkinci uygulama çalışmalı, engel kaldırılınca hedef yeniden bağlanabilmeli |
| Global engel | Yönetici yetkisiyle seçili ve desteklenen arayüzlerde WFP. Loopback, DHCP, yerel erişim ve VPN yönlendirmesi kapsam kararına dahil | İstisnalar, mevcut bağlantılar ve geri alma doğrulanınca ilgili kapsam açılır |

Test eşi ayrı makinede bilinen uzunlukta yük üretip alsın. Örneğin iki farklı yoldan başlatılan istemciler biri 64 MB indirirken diğeri 16 MB yüklesin. Boş trafik tabanı al. Her yönü TCP/UDP ve IPv4/IPv6 ile tekrarla, ayrıca QUIC çalıştır. Protokol ek yükünü, yeniden iletimleri ve arka planı ayrı yorumla. Yanlış sürece atıf, yönün ters olması, olayı iki kez sayma veya kaybı gizleme ilgili deneyin başarısızlığıdır. Kesin bayt eşitliğini yalnız aynı katmanda ve kontrollü girdilerde bekle.

## Faz 1 • İskelet, servis ve gerçek global ölçüm

- src/RetroNet.Core altında ölçüm örneği, kaynak sağlığı, uygulama kimliği, sayaç döngüsü, kota ve politika talebi modellerini oluştur. Bu projeyi Windows API'lerinden bağımsız tut.
- src/RetroNet.Service altında Collection, Quotas, Persistence, Policy ve Ipc klasörlerini aç. Microsoft.Extensions.Hosting.WindowsServices ile servis yaşam döngüsünü bağla. Servis tüm ölçüm, SQLite ve politika yazımlarının tek sahibi olsun.
- src/RetroNet.Desktop altında Views, ViewModels, Themes ve yerel Assets kullan. CommunityToolkit.Mvvm komutlar ve gözlemlenebilir durum için, Microsoft.Diagnostics.Tracing.TraceEvent serviste ETW için, Microsoft.Data.Sqlite kalıcılık için, xUnit ve test SDK'sı test keşfi için kullanılsın. Önce net10 uyumluluğunu restore/build ile doğrula, seçilen tam sürümleri merkezi paket dosyası ve kilit dosyalarına kaydet.
- src/RetroNet.Recovery bağımsız başlatılabilsin. tests altında Core ve Windows entegrasyon testlerini ayır. installer altında kurulum kaynakları, docs altında doğrulama kaydı tut.
- Arayüz kimliğine göre 64 bit giriş/çıkış ve monoton zaman sakla. İlk okuma, sayaç gerilemesi, adaptör değişimi ve uykudan dönüşte referansı yenile. Başarısız okumada son zamanı ve “Ölçüm kesildi” durumunu taşı. Genel kartta “Seçili bağdaştırıcı trafiği” yaz. Bir saniyelik UI güncellemelerini kota işçisinden ayır.
- İlk dikey dilim gerçek adaptörden servise ve oradan kompakt WPF ekranına kadar çalışsın. Luna ve Aero kaynak sözlüklerini çevrimdışı yükle. Grafikte son 60 saniye ve veri kesintileri görünsün. Sentetik trafik yalnız açıkça etiketli önizleme veya testte kullanılsın.

## Faz 2 • Süreç atfı ve IPC

ETW oturumu benzersiz ve servise ait olsun. Olayı zaman damgasıyla süreç ömrüne bağla. PID yeniden kullanıldığında eski kaydı yeni yürütülebilir dosyaya aktarma. Doğrulanmış yol veya desteklenen paket kimliğine göre topla. Hızla kapanan süreçlerin birikimini koru. Çözülemeyen kimlik “Atfedilemeyen” satırına gider. Kullanıcı seçimiyle oluşturulan grupta aynı uygulamayı yalnız bir kez say. Global kotayı adaptör farkından, uygulama kotasını doğrulanmış olaylardan besle. Sınırlı kuyruğun taşması ve ETW kaybı ölçüm sağlığını düşürsün. Bayat, kayıp veya yeniden oynatılmış örnek otomatik engel başlatamasın.

Named Pipe yalnız yerel bağlantıları kabul etsin. Kurulumda belirlenen operatörlere ACL tanımla. Servis istemci kimliğini Windows erişim belirtecinden alıp her komutu yetkilendirsin. Makine çapında veri görünürlüğü ve global politika değişikliği için ayrı hak tanımla. Standart kullanıcı arayüzü sürekli yükseltme istemesin. Sürümlü, uzunluk önekli mesaj, üst boyut sınırı, istek kimliği, zaman aşımı ve iptal kullan. Tekrarlanan kota uzatma komutunun tutarı iki kez artırmasını önle. Yeniden bağlanmada tam durum ve kaçırılan bildirimler gelsin. İki istemci, yetkisiz kullanıcı, kopuk bağlantı ve hızlı PID devriyle doğrula.

## Faz 3 • Kota, kalıcılık ve bildirimler

1. Yeni Sayaç formunda kapsam, hedef, yön, isteğe bağlı kota, ön uyarı ve eylem seçilsin. Varsayılan “Yalnızca uyar”. Türkçe ondalık virgülüyle girilen pozitif değeri taşma denetimiyle tam sayı bayta çevir. Başlat, duraklat, sürdür, düzenle, sıfırla ve sil komutlarının her biri tek işlem olarak işlensin. Duraklatınca tüketim dursun, mevcut engel ayrıca yönetilsin.
2. SQLite şemasında sayaç, grup üyeliği, dönem, tüketim, eşik teslimi, politika niyeti/sonucu ve ayarlar bulunsun. Dönem ve eşik sürümü için benzersiz anahtar kullan. Normal birikimi yaklaşık beş saniyede bir, kritik geçişi anında transaction ile yaz. Son yazımdan sonraki belirsiz örnekleri işaretle. Şema geçişi, saklama süresi, dönerek temizlenen loglar ve disk doluluğu test edilsin. Kalıcılık hatasında otomatik engel kapansın.
3. 1 GB kotada 899.999.999 → 900.000.000 bayt bir ön uyarı, 1.000.000.000 baytta tek limit olayı üretmeli. Tek örnekte iki eşik aşılırsa yalnız limit bildirimi gösterilip atlanan ön uyarı kaydedilsin. +100 MB tüketimi koruyarak limiti 1.100.000.000 bayta çıkarsın. +500 MB, onay iste, şimdi engelle, bu döngüde engelleme, engeli kaldır ve isteğe bağlı 15 saniye geri sayımı ayrı test et. Eylem anında güncel dönem, sağlık ve talebi tekrar denetle. Süre ve bayt aşımını kaydet.
4. Manuel sayaçtan sonra günlük ve aylık dönemleri ekle. UTC zaman, kota saat dilimi ve kalıcı dönem kimliği kullan. Ayın 31'i olmayan aylar, yaz saati, geri alınan saat ve kapalı geçen dönemlerde aynı dönem ikinci kez açılmamalı. UI kapanınca servis sürsün. Bildirim geçmişi ve tepsi aksiyonları aynı komutları kullansın.

## Faz 4 • Politika ve bağlantı kurtarma

Uygulama filtresinden başla. Sonra arayüz kapsamlı global filtreyi ekle. WFP dinamik oturumunda yalnız RetroNet provider/sublayer ve filtre kimliklerini yönet. Aynı hedefi isteyen kotaları talep kümesi olarak sakla. Önce niyeti SQLite'a yaz, WFP işleminde ilişkili filtreleri uygula, gerçek sonucu sorgula ve kaydet. Yarım işlem, servis çökmesi ve tekrar denemede mükerrer filtre üretme. Filtre kurulumu, gerçek akış kesilmesi ve kullanıcıya gösterilen sonuç ayrı kaydedilsin. Başlangıçta eski engeller varsayılan olarak askıya alınsın. Alternatif uygulama kapsamlı Firewall API ancak ayrı doğrulanırsa seçilebilsin.

“Tüm engelleri kaldır” önce mevcut döngülerin taleplerini askıya alsın, sonra sahip olunan filtreleri kaldırıp doğrulasın. Bir kotanın normal kaldırılması diğer kotanın talebini korusun. Servis yanıtsızsa Recovery gerektiğinde UAC ile erişimi kısıtlı kalıcı kurtarma işareti yazsın, SCM ile yalnız RetroNet servisini durdursun, dinamik nesnelerin kalktığını sorgulasın. Servis başlangıçta bu işareti politika döngüsünden önce okuyup otomatik kurulumu durdursun. Erişim veya doğrulama başarısız olursa sonucu açıkça göster. UI, tepsi ve bağımsız Recovery çevrimdışı kullanılabilsin.

İzole VM'de açık TCP aktarımı, yeni TCP, UDP ve VPN ile her yönü dene. Steam engellenirken Chrome veri taşımalı. Steam tekrar açılınca etkin kural aynı doğrulanmış kimliği kapsamalı. UI çökmesi, servis çökmesi, disk hatası, iki çakışan kota, acil kaldırma ve sonraki örneği sınayarak sonucu kaydet. Sınanamayan kapsam için otomatik engel denetimi servis tarafında kapalı kalsın. Ölçüm ve uyarı kullanılabilsin.

## Faz 5 • Masaüstü ve dağıtım

Kompakt görünüm 1920×1080 ve %100 ölçekte yaklaşık 500×660 DIP, alt sınır 380×420 DIP. Yaklaşık 720 DIP genişlikte iki sütun, Mini görünümde yaklaşık 300×84 DIP kullan. Monitör ayrılınca pencereyi kullanılabilir alana taşı. Uygulamalar listesinde arama, iki yönlü hız, birikim, sıralama ve kota işlemleri olsun. Temayı anında değiştirme, klavye odağı, %100/125/150 DPI, her zaman üstte, tepsi ve görünür kurtarma bandını dene. Olağan yükte UI ve servis CPU/belleğini ayrı kaydet. Yaklaşık %2 toplam CPU ve 120 MB altı UI hedeflerini ölçülen donanımla raporla.

Temiz kurulum için ilk tercih üç yürütülebilir dosyanın self-contained win-x64 klasör yayınlarını içeren tek çevrimdışı kurucudur. Framework-dependent yayın ile gerekli Desktop Runtime'ı çevrimdışı birlikte paketleme seçeneğini toplam kurucu boyutu üzerinden karşılaştır. WPF veya ETW uyumluluğu doğrulanmadan trimming açma. Kurucu servis ve ACL'leri kursun, standart kullanıcı UI'si ve Recovery kısayolu oluştursun. Kaldırıcı, dosyaları silmeden önce servisi ve sahip olunan filtreleri temizleyip doğrulasın.

## Komut sırası ve teslim

Çözümü RetroNet.slnx, mantık test projesini tests/RetroNet.Core.Tests olarak oluşturduysan depo kökünde aşağıdaki sırayı kullan. Mevcut depoda gerçek adlara uyarla. İlk restore kilitleri oluşturduktan sonra tekrar üretilebilir derlemede locked mode kullan.

~~~sh
dotnet --info
dotnet restore RetroNet.slnx --use-lock-file -p:EnableWindowsTargeting=true
dotnet restore RetroNet.slnx --locked-mode -p:EnableWindowsTargeting=true
dotnet build RetroNet.slnx -c Release --no-restore -p:EnableWindowsTargeting=true
dotnet test tests/RetroNet.Core.Tests -c Release --no-build --logger trx
dotnet publish src/RetroNet.Desktop -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -o artifacts/win-x64/Desktop
dotnet publish src/RetroNet.Service -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -o artifacts/win-x64/Service
dotnet publish src/RetroNet.Recovery -c Release -r win-x64 --self-contained true -p:EnableWindowsTargeting=true -o artifacts/win-x64/Recovery
~~~

Windows'ta entegrasyon testlerini ayrıca çalıştır. Kurucuyu örneğin sürümü sabitlenmiş Inno Setup ile installer/RetroNet.iss üzerinden ISCC çağırarak üret. Temiz Windows 11 x64 VM'de ağı kapatıp kur, standart kullanıcı olarak başlat, iki temayı değiştir, ölçüm için test ağına bağlan, kota ve geri alma senaryosunu çalıştır, kaldır. Diskteki gerçek paketin boyutunu, SHA-256 değerini ve Authenticode durumunu raporla. Windows erişimi yoksa platformdan bağımsız testleri ve mümkünse çapraz yayını çalıştır, VM senaryolarını yeniden çalıştırılabilir betikler halinde bırak. Başarıyı yalnız gerçekten geçen komut veya gözlem için yaz.

Her fazda dosya, komut, çıkış kodu, ölçülen sonuç ve eksik Windows kanıtını docs/dogrulama.md içinde güncelle. Son yanıtta mevcut kurucu veya yayın klasörüne bağlantı ver. Paketin kurulum, çalışma, kaldırma ve imza durumunu ayrı belirt. Gelişmiş zamanlama, dışa aktarım, uzun dönem raporları ve tahmini kalan süreyi temel ürün tesliminden sonraya bırak.
