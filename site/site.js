"use strict";

const languages = {
  tr: ["Türkçe", "Dil", "İnternet kullanımını takip et.", "Hızları, toplam veriyi ve kalan kotayı tek pencerede gör. İstersen kota dolunca Wi‑Fi otomatik kapansın.", "İşletim sistemini seç", "Windows 11 ve 64 bit Windows 10 için taşınabilir paket.", "32 bit Windows 10 için taşınabilir paket.", "Önizleme", "Debian ve Ubuntu için ağ sayacı. Python 3 ve Tk gerekir.", "İndir", "İndirmek için depoya erişimi olan GitHub hesabınla giriş yap.", "Sürüm notları", "GitHub projesi"],
  en: ["English", "Language", "Track your internet usage.", "See speeds, total usage and remaining quota in one window. Optionally turn off Wi‑Fi when you reach the quota.", "Choose your operating system", "Portable package for Windows 11 and 64-bit Windows 10.", "Portable package for 32-bit Windows 10.", "Preview", "Network monitor for Debian and Ubuntu. Requires Python 3 and Tk.", "Download", "Sign in to GitHub with an account that can access the repository to download.", "Release notes", "GitHub project"],
  zh: ["简体中文", "语言", "追踪网络用量。", "在同一窗口查看速度、总用量和剩余限额。达到限额时可自动关闭 Wi‑Fi。", "选择操作系统", "适用于 Windows 11 和 64 位 Windows 10 的便携包。", "适用于 32 位 Windows 10 的便携包。", "预览版", "适用于 Debian 和 Ubuntu 的网络监控工具。需要 Python 3 和 Tk。", "下载", "请使用有仓库访问权限的 GitHub 帐号登录后下载。", "版本说明", "GitHub 项目"],
  hi: ["हिन्दी", "भाषा", "इंटरनेट उपयोग पर नज़र रखें।", "एक विंडो में गति, कुल उपयोग और बचा कोटा देखें। चाहें तो कोटा पूरा होने पर Wi‑Fi अपने आप बंद करें।", "अपना ऑपरेटिंग सिस्टम चुनें", "Windows 11 और 64-बिट Windows 10 का पोर्टेबल पैकेज।", "32-बिट Windows 10 का पोर्टेबल पैकेज।", "पूर्वावलोकन", "Debian और Ubuntu के लिए नेटवर्क मॉनिटर। Python 3 और Tk ज़रूरी हैं।", "डाउनलोड करें", "डाउनलोड करने के लिए रिपॉज़िटरी की पहुँच वाले GitHub खाते में लॉग इन करें।", "रिलीज़ नोट्स", "GitHub प्रोजेक्ट"],
  es: ["Español", "Idioma", "Controla tu uso de internet.", "Consulta la velocidad, el uso total y la cuota restante en una ventana. Puedes apagar Wi‑Fi al alcanzar la cuota.", "Elige tu sistema operativo", "Paquete portátil para Windows 11 y Windows 10 de 64 bits.", "Paquete portátil para Windows 10 de 32 bits.", "Vista previa", "Monitor de red para Debian y Ubuntu. Requiere Python 3 y Tk.", "Descargar", "Inicia sesión en GitHub con una cuenta que tenga acceso al repositorio.", "Notas de la versión", "Proyecto en GitHub"],
  ar: ["العربية", "اللغة", "راقب استخدام الإنترنت.", "اطّلع على السرعة والاستخدام الإجمالي والحصة المتبقية في نافذة واحدة. يمكنك إيقاف Wi‑Fi عند بلوغ الحصة.", "اختر نظام التشغيل", "حزمة محمولة لنظام Windows 11 وWindows 10 ‏64 بت.", "حزمة محمولة لنظام Windows 10 ‏32 بت.", "معاينة", "مراقب شبكة لنظامي Debian وUbuntu. يتطلب Python 3 وTk.", "تنزيل", "سجّل الدخول إلى GitHub بحساب لديه صلاحية الوصول إلى المستودع للتنزيل.", "ملاحظات الإصدار", "مشروع GitHub"],
  fr: ["Français", "Langue", "Suivez votre consommation Internet.", "Consultez les débits, la consommation totale et le quota restant dans une fenêtre. Vous pouvez couper le Wi‑Fi au quota.", "Choisissez votre système", "Version portable pour Windows 11 et Windows 10 64 bits.", "Version portable pour Windows 10 32 bits.", "Aperçu", "Moniteur réseau pour Debian et Ubuntu. Python 3 et Tk sont requis.", "Télécharger", "Connectez-vous à GitHub avec un compte ayant accès au dépôt pour télécharger.", "Notes de version", "Projet GitHub"],
  bn: ["বাংলা", "ভাষা", "ইন্টারনেট ব্যবহার দেখুন।", "এক উইন্ডোয় গতি, মোট ব্যবহার ও বাকি কোটা দেখুন। চাইলে কোটা পূর্ণ হলে Wi‑Fi বন্ধ হবে।", "অপারেটিং সিস্টেম বেছে নিন", "Windows 11 ও 64-বিট Windows 10-এর পোর্টেবল প্যাকেজ।", "32-বিট Windows 10-এর পোর্টেবল প্যাকেজ।", "প্রিভিউ", "Debian ও Ubuntu-র নেটওয়ার্ক মনিটর। Python 3 ও Tk প্রয়োজন।", "ডাউনলোড", "ডাউনলোড করতে রিপোজিটরিতে অ্যাক্সেস আছে এমন GitHub অ্যাকাউন্টে প্রবেশ করুন।", "রিলিজ নোট", "GitHub প্রকল্প"],
  pt: ["Português", "Idioma", "Acompanhe seu uso da internet.", "Veja as velocidades, o uso total e a cota restante em uma janela. Você pode desligar o Wi‑Fi ao atingir a cota.", "Escolha seu sistema operacional", "Pacote portátil para Windows 11 e Windows 10 de 64 bits.", "Pacote portátil para Windows 10 de 32 bits.", "Prévia", "Monitor de rede para Debian e Ubuntu. Requer Python 3 e Tk.", "Baixar", "Entre no GitHub com uma conta que tenha acesso ao repositório para fazer o download.", "Notas da versão", "Projeto no GitHub"],
  ru: ["Русский", "Язык", "Следите за расходом интернета.", "Скорость, общий расход и остаток лимита в одном окне. При достижении лимита можно выключить Wi‑Fi.", "Выберите операционную систему", "Переносимый пакет для Windows 11 и 64-разрядной Windows 10.", "Переносимый пакет для 32-разрядной Windows 10.", "Предварительная версия", "Монитор сети для Debian и Ubuntu. Нужны Python 3 и Tk.", "Скачать", "Для скачивания войдите в GitHub с доступом к репозиторию.", "Примечания к выпуску", "Проект на GitHub"],
  ur: ["اردو", "زبان", "انٹرنیٹ کے استعمال پر نظر رکھیں۔", "رفتار، کل استعمال اور باقی کوٹا ایک ونڈو میں دیکھیں۔ چاہیں تو کوٹا پورا ہونے پر Wi‑Fi بند ہو جائے گا۔", "اپنا آپریٹنگ سسٹم منتخب کریں", "Windows 11 اور 64 بٹ Windows 10 کے لیے پورٹیبل پیکیج۔", "32 بٹ Windows 10 کے لیے پورٹیبل پیکیج۔", "پیش منظر", "Debian اور Ubuntu کے لیے نیٹ ورک مانیٹر۔ Python 3 اور Tk درکار ہیں۔", "ڈاؤن لوڈ", "ڈاؤن لوڈ کے لیے ایسے GitHub اکاؤنٹ میں داخل ہوں جسے ریپوزٹری تک رسائی ہو۔", "ریلیز کی تفصیل", "GitHub منصوبہ"],
};

const fields = ["nativeName", "language", "title", "intro", "downloads", "x64", "x86",
  "linuxTitle", "linux", "download", "access", "release", "source"];
const selector = document.getElementById("language");
selector.replaceChildren(...Object.entries(languages).map(([code, entries]) => new Option(entries[0], code)));

function setLanguage(code) {
  const copy = Object.fromEntries(fields.map((key, i) => [key, languages[code][i]]));
  document.documentElement.lang = code;
  document.documentElement.dir = ["ar", "ur"].includes(code) ? "rtl" : "ltr";
  selector.value = code;
  for (const item of document.querySelectorAll("[data-copy]")) item.textContent = copy[item.dataset.copy];
  for (const card of document.querySelectorAll(".card")) {
    card.querySelector(".download").setAttribute("aria-label", `${copy.download} • ${card.querySelector(".platform").textContent} ${card.querySelector("h3").textContent}`);
  }
}

const url = new URL(window.location);
const code = [url.searchParams.get("lang"), ...navigator.languages, "en"]
  .map(value => value?.toLowerCase().split("-")[0])
  .find(value => Object.hasOwn(languages, value));
setLanguage(code);
selector.addEventListener("change", () => {
  url.searchParams.set("lang", selector.value);
  history.replaceState(null, "", url);
  setLanguage(selector.value);
});
