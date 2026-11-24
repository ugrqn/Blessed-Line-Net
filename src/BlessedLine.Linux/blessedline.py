#!/usr/bin/env python3
"""BlessedLine's Linux network-interface monitor."""
import argparse
import csv
import fcntl
import json
import os
import shutil
import subprocess
import tempfile
import time
import tkinter as tk
from decimal import Decimal, InvalidOperation, ROUND_HALF_UP
from pathlib import Path
from tkinter import messagebox, ttk

LANGUAGES = {
    "tr": "Türkçe", "en": "English", "zh": "简体中文", "hi": "हिन्दी",
    "es": "Español", "ar": "العربية", "fr": "Français", "bn": "বাংলা",
    "pt": "Português", "ru": "Русский", "ur": "اردو",
}
CATALOG = Path(__file__).with_name("Languages.tsv")
if not CATALOG.exists():
    CATALOG = Path(__file__).resolve().parent.parent / "BlessedLine.Desktop" / "Languages.tsv"
with CATALOG.open(encoding="utf-8", newline="") as catalog_file:
    STRINGS = {row["key"]: row for row in csv.DictReader(catalog_file, delimiter="\t")}

STATE_DIR = Path(os.environ.get("XDG_CONFIG_HOME", Path.home() / ".config")) / "blessedline"
NETWORK_DIR = Path("/sys/class/net")
DECIMAL_COMMA = {"tr", "es", "fr", "pt", "ru"}


def read_state():
    state = {"used": 0, "limit": None, "interface": "", "locale": "tr", "theme": "xp", "wifi_off": False}
    try:
        with (STATE_DIR / "settings.json").open("rb") as file:
            contents = file.read(65537)
        saved = json.loads(contents) if len(contents) <= 65536 else None
        if isinstance(saved, dict):
            state.update({key: saved[key] for key in state if key in saved})
    except (OSError, ValueError):
        pass
    state["used"] = state["used"] if type(state["used"]) is int and state["used"] >= 0 else 0
    state["limit"] = state["limit"] if type(state["limit"]) is int and state["limit"] > 0 else None
    state["locale"] = state["locale"] if isinstance(state["locale"], str) and state["locale"] in LANGUAGES else "tr"
    return state


def interfaces():
    return sorted(path.name for path in NETWORK_DIR.iterdir() if path.name != "lo")


def wireless(interface):
    path = NETWORK_DIR / interface
    try:
        return (path / "wireless").exists() or "DEVTYPE=wlan" in (path / "uevent").read_text().splitlines()
    except OSError:
        return False


def sample(interface):
    path = NETWORK_DIR / interface / "statistics"
    return (int((path / "rx_bytes").read_text()), int((path / "tx_bytes").read_text()))


def change_wifi(enabled):
    manager = shutil.which("nmcli")
    if not manager:
        raise RuntimeError("nmcli")
    subprocess.run([manager, "--wait", "3", "radio", "wifi", "on" if enabled else "off"],
                   check=True, capture_output=True, text=True, timeout=5)


def parse_limit(text, locale):
    text = text.strip()
    if not text:
        return None
    if locale in DECIMAL_COMMA:
        text = text.replace(",", ".")
    if locale == "ar":
        text = text.replace("٫", ".")
    if not all(char.isdecimal() or char == "." for char in text):
        raise ValueError(text)
    try:
        amount = Decimal(text)
        if amount <= 0 or amount > Decimal("9223372036854"):
            raise ValueError(text)
        result = int((amount * 1_000_000).quantize(Decimal(1), rounding=ROUND_HALF_UP))
    except InvalidOperation as error:
        raise ValueError(text) from error
    if result == 0:
        raise ValueError(text)
    return result


def usage_delta(previous, current):
    if previous is None or any(a > b for a, b in zip(previous, current)):
        return (0, 0)
    return tuple(b - a for a, b in zip(previous, current))


class Monitor:
    def __init__(self, root):
        self.root = root
        self.state = read_state()
        self.locale = self.state["locale"]
        self.previous = None
        self.previous_at = time.monotonic()
        self.rate = (0, 0)
        self.mini = False
        self.armed = False
        self.last_save = 0
        self.status = tk.StringVar()
        self.choice = tk.StringVar(value=self.state["interface"])
        self.hint = tk.StringVar()
        self.speed = tk.StringVar()
        self.last_warning = False
        self.root.title("BlessedLine")
        self.root.protocol("WM_DELETE_WINDOW", self.close)
        self.choose(self.choice.get())
        self.layout()
        self.tick()

    def label(self, key, *args):
        return STRINGS[key][self.locale].format(*args)

    def units(self, size):
        value = next(((size / scale, suffix) for scale, suffix in
                      [(10**9, "GB"), (10**6, "MB"), (10**3, "KB")] if size >= scale), None)
        if value is None:
            return f"{size:d} B"
        amount, suffix = value
        number = f"{amount:.2f}"
        if self.locale in DECIMAL_COMMA:
            number = number.replace(".", ",")
        if self.locale == "ar":
            number = number.replace(".", "٫")
        return f"{number} {suffix}"

    def choose(self, saved=None):
        options = interfaces()
        previous_interface = self.state["interface"]
        if saved is None:
            saved = self.choice.get()
        if saved in options:
            selected = saved
        else:
            active = []
            for name in options:
                try:
                    if (NETWORK_DIR / name / "operstate").read_text().strip() == "up":
                        active.append(name)
                except OSError:
                    continue
            selected = next(iter(active or options), "")
        self.choice.set(selected)
        self.state["interface"] = selected
        if selected != previous_interface:
            self.armed = False
            self.previous = None

    def layout(self):
        for child in self.root.winfo_children():
            child.destroy()
        base = "#ece9d8" if self.state["theme"] == "xp" else "#e8f1f8"
        self.root.configure(bg=base)
        style = ttk.Style(self.root)
        style.theme_use("clam")
        style.configure("Blessed.TFrame", background=base)
        style.configure("Blessed.TLabel", background=base)
        style.configure("Quota.Horizontal.TProgressbar", background="#408c4f")
        width, height = (325, 134) if self.mini else (510, 362)
        self.root.minsize(width, height)
        self.root.attributes("-topmost", self.mini)
        position = f"+{max(0, self.root.winfo_screenwidth() - width - 24)}+32" if self.mini else ""
        self.root.geometry(f"{width}x{height}{position}")
        panel = ttk.Frame(self.root, padding=12, style="Blessed.TFrame")
        panel.pack(fill="both", expand=True)
        top = ttk.Frame(panel, style="Blessed.TFrame")
        top.pack(fill="x")
        ttk.Button(top, text="□" if self.mini else "–", width=3, command=self.toggle).pack(side="right")
        ttk.Button(top, text="⚙ " + self.label("EditQuota"),
                   command=self.quota_dialog).pack(side="right", padx=4)
        if not self.mini:
            ttk.Button(top, text=self.label("Settings"), command=self.settings_dialog).pack(side="right")
            available = interfaces()
            adapter = ttk.Combobox(panel, textvariable=self.choice, values=available, state="readonly")
            adapter.bind("<<ComboboxSelected>>", lambda event: self.choose())
            adapter.pack(fill="x", pady=(12, 6))
            ttk.Label(panel, text=self.label("TotalUsage"), style="Blessed.TLabel").pack(anchor="w")
        self.progress = ttk.Progressbar(panel, maximum=100, style="Quota.Horizontal.TProgressbar")
        self.progress.pack(fill="x", pady=(10, 5))
        ttk.Label(panel, textvariable=self.hint, wraplength=width - 30,
                  style="Blessed.TLabel").pack(anchor="w")
        ttk.Label(panel, textvariable=self.speed, style="Blessed.TLabel").pack(anchor="w")
        if not self.mini:
            ttk.Label(panel, textvariable=self.status, wraplength=width - 30,
                      style="Blessed.TLabel").pack(anchor="w", pady=8)
        self.render()

    def toggle(self):
        self.mini = not self.mini
        self.layout()

    def settings_dialog(self):
        dialog = tk.Toplevel(self.root)
        dialog.title(self.label("Settings"))
        dialog.transient(self.root)
        panel = ttk.Frame(dialog, padding=14)
        panel.pack(fill="both", expand=True)
        ttk.Label(panel, text=self.label("Language")).pack(anchor="w")
        languages = ttk.Combobox(panel, values=list(LANGUAGES.values()), state="readonly", width=27)
        languages.current(list(LANGUAGES).index(self.locale))
        languages.pack(fill="x", pady=(4, 8))
        selected = tk.StringVar(value=self.state["theme"])
        for name, theme in ((self.label("ClassicTheme"), "xp"), ("Windows 7 Aero", "win7")):
            ttk.Radiobutton(panel, text=name, value=theme, variable=selected).pack(anchor="w")

        def apply():
            self.locale = list(LANGUAGES)[languages.current()]
            self.state["locale"] = self.locale
            self.state["theme"] = selected.get()
            self.status.set("")
            self.save()
            dialog.destroy()
            self.layout()

        ttk.Button(panel, text=self.label("Apply"), command=apply).pack(anchor="e", pady=(8, 0))

    def quota_dialog(self):
        dialog = tk.Toplevel(self.root)
        dialog.title(self.label("CounterAndQuota"))
        dialog.transient(self.root)
        dialog.attributes("-topmost", self.mini)
        panel = ttk.Frame(dialog, padding=14)
        panel.pack(fill="both", expand=True)
        ttk.Label(panel, text=self.label("LimitMb")).pack(anchor="w")
        entry = ttk.Entry(panel, width=24)
        entry.pack(fill="x", pady=(4, 8))
        if self.state["limit"]:
            text = f"{self.state['limit'] / 1_000_000:.6f}".rstrip("0").rstrip(".")
            if self.locale in DECIMAL_COMMA:
                text = text.replace(".", ",")
            entry.insert(0, text)
        armed = tk.BooleanVar(value=self.armed)
        ttk.Checkbutton(panel, variable=armed, text=self.label("WifiAtQuota")).pack(anchor="w")
        error = tk.StringVar()
        ttk.Label(panel, textvariable=error, wraplength=285).pack(anchor="w", pady=4)
        if self.state["wifi_off"]:
            ttk.Button(panel, text=self.label("RestoreWifi"), command=self.restore_wifi).pack(anchor="w")

        def apply():
            try:
                limit = parse_limit(entry.get(), self.locale)
            except ValueError:
                error.set(self.label("InvalidLimit"))
                return
            activate = armed.get()
            if activate and not (limit and wireless(self.choice.get()) and shutil.which("nmcli")):
                error.set(self.label("NeedWifiQuota") + " (NetworkManager)")
                return
            if activate and not self.armed and not messagebox.askyesno(
                    "BlessedLine · Wi-Fi", self.label("ConfirmWifiOff"), parent=dialog):
                return
            if self.state["wifi_off"] and (limit is None or self.state["used"] < limit):
                if not self.restore_wifi():
                    return
                activate = False
            self.armed = activate
            self.state["limit"] = limit
            self.last_warning = False
            self.save()
            dialog.destroy()
            self.render()

        actions = ttk.Frame(panel)
        actions.pack(fill="x")
        ttk.Button(actions, text=self.label("Apply"), command=apply).pack(side="right")
        ttk.Button(actions, text=self.label("Reset"), command=self.reset).pack(side="left")
        entry.focus_set()

    def reset(self):
        if messagebox.askyesno("BlessedLine", self.label("ResetCounter", self.label("TotalCounter"))):
            if not self.restore_wifi():
                return
            self.state["used"] = 0
            self.armed = self.last_warning = False
            self.save()
            self.render()

    def restore_wifi(self):
        if not self.state["wifi_off"]:
            return True
        try:
            change_wifi(True)
        except (OSError, RuntimeError, subprocess.SubprocessError) as error:
            self.status.set(self.label("WifiOnError", str(error)))
            return False
        self.state["wifi_off"] = False
        self.armed = False
        self.previous = None
        self.status.set(self.label("WifiRestored"))
        self.save()
        return True

    def tick(self):
        now = time.monotonic()
        interval = max(0.1, now - self.previous_at)
        self.previous_at = now
        interface = self.state["interface"]
        if interface and not self.state["wifi_off"]:
            try:
                current = sample(interface)
                received, sent = usage_delta(self.previous, current)
                self.previous = current
                self.rate = (int(received / interval), int(sent / interval))
                self.state["used"] += received + sent
            except (OSError, ValueError):
                self.previous = None
                self.rate = (0, 0)
        limit = self.state["limit"]
        if limit and self.state["used"] >= limit:
            if not self.last_warning:
                self.root.bell()
                self.last_warning = True
            if self.armed and wireless(interface) and not self.state["wifi_off"]:
                self.armed = False
                self.state["wifi_off"] = True
                if self.save():
                    try:
                        change_wifi(False)
                        self.rate = (0, 0)
                        self.status.set(self.label("WifiOff"))
                    except (OSError, RuntimeError, subprocess.SubprocessError) as error:
                        self.state["wifi_off"] = False
                        self.save()
                        self.status.set(self.label("WifiOffError", str(error)))
                else:
                    self.state["wifi_off"] = False
        if now - self.last_save >= 5:
            self.save()
        self.render()
        self.root.after(1000, self.tick)

    def render(self):
        limit = self.state["limit"]
        used = self.state["used"]
        percent = min(100, used / limit * 100) if limit else 0
        ttk.Style(self.root).configure("Quota.Horizontal.TProgressbar",
                                       background="#bb362c" if percent == 100 else "#408c4f")
        self.progress["value"] = percent
        if limit:
            self.hint.set(self.label("QuotaLeftOf", self.units(limit), self.units(max(0, limit - used))))
        else:
            self.hint.set(f"{self.units(used)} · {self.label('NoLimit')}")
        per_second = self.label("PerSecond")
        self.speed.set(f"↓ {self.units(self.rate[0])}{per_second}     ↑ {self.units(self.rate[1])}{per_second}")

    def save(self):
        temporary = None
        try:
            with tempfile.NamedTemporaryFile(mode="w", dir=STATE_DIR, encoding="utf-8", delete=False) as file:
                temporary = Path(file.name)
                json.dump(self.state, file, ensure_ascii=False)
            temporary.replace(STATE_DIR / "settings.json")
            self.last_save = time.monotonic()
            return True
        except OSError:
            self.status.set(self.label("SaveError"))
            return False
        finally:
            if temporary is not None:
                temporary.unlink(missing_ok=True)

    def close(self):
        self.restore_wifi()
        self.save()
        self.root.destroy()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--version", action="version", version="BlessedLine 0.4.0 (Linux preview)")
    parser.parse_args()
    STATE_DIR.mkdir(mode=0o700, parents=True, exist_ok=True)
    if STATE_DIR.is_symlink() or STATE_DIR.stat().st_uid != os.getuid():
        parser.exit(1, "BlessedLine requires a settings directory owned by the current user.\n")
    STATE_DIR.chmod(0o700)
    lock_path = STATE_DIR / "instance.lock"
    with os.fdopen(os.open(lock_path, os.O_WRONLY | os.O_CREAT | os.O_NOFOLLOW, 0o600), "w") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError:
            parser.exit(1, "BlessedLine is already open.\n")
        root = tk.Tk()
        Monitor(root)
        root.mainloop()


if __name__ == "__main__":
    main()
