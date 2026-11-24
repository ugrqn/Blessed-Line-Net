# BlessedLine Linux önizlemesi / Linux preview

Debian veya Ubuntu masaüstünde Python 3 ve Tk gerekir. Arşivi ayıkladıktan sonra `./blessedline.py` ile çalıştırın. Wi-Fi kotası için NetworkManager (`nmcli`) ve sistemin Wi-Fi değiştirme izni gerekir. Bu seçenek tüm NetworkManager Wi-Fi radyolarını etkiler.

On a Debian or Ubuntu desktop, install Python 3 with Tk support, extract the archive and run `./blessedline.py`. The optional Wi-Fi cutoff requires NetworkManager (`nmcli`) and permission to control Wi-Fi. This switch affects all Wi-Fi radios managed by NetworkManager.

```sh
sudo apt install python3-tk
./blessedline.py
```

Linux önizlemesi ağ bağdaştırıcısının toplamını, küçük pencereyi, kotayı ve dil seçimini içerir. Uygulama başına ölçüm ve Windows bildirim alanı entegrasyonu Windows sürümündedir.

The Linux preview includes network-interface totals, the mini window, quota editing and language selection. Per-application measurements and Windows tray integration are available in the Windows build.
