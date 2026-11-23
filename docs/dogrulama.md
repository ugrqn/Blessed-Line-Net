# Doğrulama kaydı

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
