import importlib.util
import sys
import tempfile
import tkinter as tk
from pathlib import Path
from unittest.mock import patch

path = Path(sys.argv[1]).resolve()
spec = importlib.util.spec_from_file_location("blessedline", path)
app = importlib.util.module_from_spec(spec)
spec.loader.exec_module(app)

assert app.parse_limit("1,5", "tr") == 1_500_000
assert app.parse_limit("١٫٥", "ar") == 1_500_000
assert app.usage_delta((100, 40), (125, 45)) == (25, 5)
assert app.usage_delta((100, 40), (1, 2)) == (0, 0)
assert len(app.LANGUAGES) == 11
assert all(set(app.LANGUAGES).issubset(row) for row in app.STRINGS.values())

with tempfile.TemporaryDirectory() as temporary:
    app.STATE_DIR = Path(temporary)
    root = tk.Tk()
    monitor = app.Monitor(root)
    root.update()
    assert monitor.state["interface"] in app.interfaces()
    monitor.state["limit"] = 500_000_000
    monitor.state["used"] = 350_000_000
    monitor.toggle()
    root.update()
    assert "500,00 MB" in monitor.hint.get() and "150,00 MB" in monitor.hint.get()
    assert root.winfo_width() < 400
    for language in app.LANGUAGES:
        monitor.locale = language
        monitor.layout()
        root.update()
        assert monitor.label("QuotaLeftOf", monitor.units(500_000_000), monitor.units(150_000_000)) == monitor.hint.get()
    monitor.state["used"] = monitor.state["limit"]
    monitor.armed = True
    with patch.object(app, "wireless", return_value=True), patch.object(app, "change_wifi") as radio:
        monitor.tick()
        assert monitor.state["wifi_off"]
        radio.assert_called_once_with(False)
        assert monitor.restore_wifi()
        assert radio.call_args.args == (True,)
    monitor.close()
    assert app.read_state()["limit"] == 500_000_000

print("Linux UI, 11 languages, quota threshold and Wi-Fi command path passed.")
