# Uygulama başına bağlantı kesme ve hız kotası

BlessedLine 0.4’te Windows trafik ölçümü işlemleri çalıştırılabilir dosyanın tam yoluyla toplar. Masaüstünde görünen kısa ad kullanıcıya yöneliktir. Bir kural kaydı gerçek yürütülebilir dosya kimliğini, paketli uygulamalarda Windows paket kimliğini, yönü, eşik değerini, seçili ağı ve etkinlik durumunu taşımalıdır.

## Seçili uygulamayı engelleme

Windows Filtering Platform’un ALE katmanları IPv4 ve IPv6 bağlantılarına uygulama, kullanıcı ve ağ koşulu ekleyebiliyor. Masaüstü programlarında Windows’un yürütülebilir dosya yolundan türettiği uygulama kimliği kullanılabilir. Paketli uygulamalar için paket SID’si ele alınmalı. Aynı uygulamanın birden fazla süreci varsa kurala hangi yürütülebilir dosyaların dahil olduğu arayüzde açıkça gösterilmeli. Windows’un politika değişikliğinde mevcut akışları yeniden yetkilendirme davranışı aktif TCP ve UDP akışlarında doğrulanmalı. [WFP uygulama kimliği](https://learn.microsoft.com/en-us/windows-hardware/drivers/network/filtering-condition-identifiers) · [ALE yeniden yetkilendirme](https://learn.microsoft.com/en-us/windows/win32/fwp/ale-re-authorization)

Kuralları yönetici izniyle çalışan bir Windows bileşeni uygular. Kural ekleme ve kaldırma işlemleri kendi alt katmanında, işlem grupları halinde yürütülür. Dinamik WFP oturumunda süreç çıkarsa Windows kuralları kaldırır. Sağ tık menüsünde tek uygulamayı veya BlessedLine’ın tüm kurallarını çözme komutu bulunabilir. İnternet engelinin yerel ağdan ayrılması için yerel aralıklar ve her bağdaştırıcının gerçek yönlendirmesi test edilmeli. [Windows Filtering Platform](https://learn.microsoft.com/en-us/windows/win32/fwp/about-windows-filtering-platform)

## Veri kotası ve hız sınırı

Veri kotası, ETW’den sayılan baytlar eşiği geçince seçili uygulamanın engel kuralını devreye alır. Hız sınırlama ise anlık akışı düzenler. Windows QoS ilkeleri belirli bir uygulamanın veya dosya yolunun giden trafiğini yavaşlatabilir. Gelen hız için ayrı bir veri yolu yaklaşımı gerekir. Sürücü gerektiren yaklaşım ele alınırsa imzalama, güvenli kaldırma ve performans ölçümü dağıtım kararının parçası olur. [Microsoft QoS politika yönetimi](https://learn.microsoft.com/en-us/windows-server/networking/technologies/qos/qos-policy-manage)

İlk doğrulama senaryosunda iki uygulama eşzamanlı veri aktarır. Birine uygulanan kuralın diğerine, DNS çözümlemesine ve yerel dosya paylaşımına etkisi ölçülür. IPv4 ve IPv6, TCP ve QUIC, yönetici izninin iptali, kuralı kaldırma, uygulama çökmesi ve yeniden başlatma ayrı ayrı denenir. 0.4 arayüzünde devreye alınan kontrol seçili Wi-Fi radyosunun kota ile kapanmasıdır.
