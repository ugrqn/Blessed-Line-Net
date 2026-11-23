# Blessed-Line-Net

RetroNet Control, Windows 11 x64 için Türkçe ağ trafiği izleme uygulamasıdır. 0.1.0 önizlemesi, seçili bağdaştırıcının indirme ve yükleme hızını gösterir. Bağımsız manuel sayaçlar, kota uyarıları, duraklatma, sıfırlama, 100 MB kota uzatma ve Luna ile Aero temaları içerir.

## Windows’ta dene

[0.1.0 önizleme sürümündeki](https://github.com/ugrqn/Blessed-Line-Net/releases/tag/v0.1.0-preview) taşınabilir ZIP’i indir, **Tümünü ayıkla** seçeneğini kullan ve `RetroNet.Desktop.exe` dosyasını aç. Taşınabilir paket .NET çalışma zamanını içerir. Sayaçlar `%LOCALAPPDATA%\RetroNet\counters.json` dosyasına kaydedilir ve ölçüm uygulama açıkken sürer.

## Yerel geliştirme

Windows 11 x64 üzerinde depoyu klonla ve `RetroNet.slnx` dosyasının bulunduğu klasörü aç. `global.json`, .NET SDK `10.0.404` veya aynı özellik bandında daha yeni bir yamayı seçer.

```powershell
git clone https://github.com/ugrqn/Blessed-Line-Net.git
cd Blessed-Line-Net
dotnet --version
dotnet build RetroNet.slnx -c Release
dotnet test RetroNet.slnx -c Release --no-build
dotnet run --project src/RetroNet.Desktop -c Release
```

Taşınabilir paket üretmek için depo kökünden şu komutu kullan.

```powershell
dotnet publish src/RetroNet.Desktop -c Release -r win-x64 --self-contained true -o artifacts/win-x64/Desktop
```

Kurucu kaynağı `installer/RetroNet.iss` dosyasında. Bu önizleme taşınabilir ZIP olarak dağıtılır.

## Proje durumu

- `src/RetroNet.Core` kota mantığını ve birim dönüşümlerini içerir.
- `src/RetroNet.Desktop` WPF arayüzünü ve bağdaştırıcı ölçümünü içerir.
- `tests/RetroNet.Core.Tests` kota davranışlarını doğrular.
- [Ürün şartnamesi](docs/RetroNet_Control_Claude_Prompt.md) hedef kapsamı, [Sol planı](RetroNet_SOL_PLANI.md) uygulama sırasını açıklar.
- [Doğrulama kaydı](docs/dogrulama.md) geçen testleri ve bekleyen Windows kontrollerini listeler. Faz 2, uygulama bazlı ölçüm ve Windows servisini ele alır.
