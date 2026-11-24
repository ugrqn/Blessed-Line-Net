import re
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from threading import Thread
from pathlib import Path

from playwright.sync_api import sync_playwright

class QuietHandler(SimpleHTTPRequestHandler):
    def log_message(self, *args):
        pass


site = Path(__file__).resolve().parents[2] / "site"
server = ThreadingHTTPServer(("127.0.0.1", 0), partial(QuietHandler, directory=site))
Thread(target=server.serve_forever, daemon=True).start()
root = f"http://127.0.0.1:{server.server_port}/"
languages = ("tr", "en", "zh", "hi", "es", "ar", "fr", "bn", "pt", "ru", "ur")

with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path="/usr/bin/chromium", args=["--no-sandbox"])
    for locale in languages:
        page = browser.new_page(locale=locale, viewport={"width": 390, "height": 850})
        page.goto(root)
        assert page.locator("html").get_attribute("lang") == locale
        assert page.locator("html").get_attribute("dir") == ("rtl" if locale in ("ar", "ur") else "ltr")
        downloads = page.locator(".download")
        assert downloads.count() == 3
        assert all(re.search(r"/releases/download/v0\.4\.(1-preview/BlessedLine-0\.4\.1-win-x(64|86)\.7z|0-preview/BlessedLine-0\.4\.0-linux\.tar\.gz)$",
                             downloads.nth(index).get_attribute("href"))
                   for index in range(3))
        assert page.evaluate("document.documentElement.scrollWidth === innerWidth")
        page.close()
    page = browser.new_page(locale="en-US", viewport={"width": 1280, "height": 850})
    page.goto(root)
    assert page.locator("h1").text_content() == "Track your internet usage."
    page.select_option("#language", "tr")
    assert page.locator("h1").text_content() == "İnternet kullanımını takip et."
    assert page.url.endswith("?lang=tr")
    browser.close()

server.shutdown()
server.server_close()
print("11 language layouts, mobile bounds, language switching and download targets passed.")
