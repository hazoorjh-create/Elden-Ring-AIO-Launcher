import os
import webbrowser
import sys
import shutil
import glob
import json
import subprocess
import traceback
import customtkinter as ctk
from tkinter import filedialog, messagebox

try:
    from PIL import Image
    HAS_PIL = True
    import io
    import base64
except ImportError:
    HAS_PIL = False

# ── Elden Ring Gold / Dark Theme ──────────────────────────────────────
GOLD         = "#C8A23C"
GOLD_HOVER   = "#DABB52"
GOLD_DIM     = "#6E5A1E"
NEXUS_URL    = "https://www.nexusmods.com/eldenring/mods/10293"
DARK_BG      = "#0A0A0A"
PANEL_BG     = "#0F0F0FE8"  # will be parsed below
PANEL_SOLID  = "#0F0F0F"
CARD_BG      = "#181818"
CARD_HOVER   = "#222222"
CARD_BORDER  = "#2A2218"
TEXT_MAIN    = "#E8DFC8"
TEXT_SUB     = "#8A7F6A"
RED_BTN      = "#4A1515"
RED_HOVER    = "#6B2222"
RED_TEXT     = "#C09090"

WINDOW_W = 900
WINDOW_H = 500
PANEL_W  = 320

FONT_FAMILY = "Segoe UI"

ctk.set_appearance_mode("Dark")
ctk.set_default_color_theme("blue")


def _resolve_exe_path():
    """Find the real path of this executable, works for Nuitka, PyInstaller, and raw Python."""
    # Nuitka compiled binary
    if '__compiled__' in dir() or '__nuitka_binary_dir' in dir():
        return os.path.abspath(sys.argv[0])
    # PyInstaller
    if getattr(sys, 'frozen', False):
        return os.path.abspath(sys.executable)
    # Raw Python script
    return os.path.abspath(sys.argv[0])


class EldenRingLauncher(ctk.CTk):
    def __init__(self):
        super().__init__()

        self.title("Elden Ring Launcher")
        self.geometry(f"{WINDOW_W}x{WINDOW_H}")
        self.resizable(False, False)
        self.configure(fg_color=DARK_BG)

        # ── Paths ─────────────────────────────────────────────────────
        self.current_file = _resolve_exe_path()
        self.base_dir = os.path.dirname(self.current_file)
        
        # Write a debug log so we can trace path issues
        try:
            with open(os.path.join(self.base_dir, "launcher_debug.log"), "w") as f:
                f.write(f"current_file: {self.current_file}\n")
                f.write(f"base_dir: {self.base_dir}\n")
                f.write(f"sys.executable: {sys.executable}\n")
                f.write(f"sys.argv: {sys.argv}\n")
                f.write(f"cwd: {os.getcwd()}\n")
                f.write(f"__compiled__: {'__compiled__' in dir()}\n")
                f.write(f"frozen: {getattr(sys, 'frozen', False)}\n")
                f.write(f"files in base_dir: {os.listdir(self.base_dir)}\n")
        except Exception:
            pass

        self.real_exe_path = os.path.join(
            self.base_dir, "real_start_protected_game.exe")
        self.config_path = os.path.join(
            self.base_dir, "launcher_config.json")

        try:
            # On Windows, setting the icon to the .exe extracts the embedded icon!
            if self.current_file.endswith(".exe"):
                self.iconbitmap(self.current_file)
            else:
                self.iconbitmap("icon.ico")
        except Exception:
            pass

        # ── Background ────────────────────────────────────────────────
        self._set_background()

        # ── Route ─────────────────────────────────────────────────────
        if os.path.exists(self.real_exe_path):
            self._build_launcher_ui()
        else:
            self._build_installer_ui()

    # ══════════════════════════════════════════════════════════════════
    #  Background
    # ══════════════════════════════════════════════════════════════════
    def _set_background(self):
        if not HAS_PIL:
            return
        try:
            import assets
            raw = base64.b64decode(assets.BG_BASE64)
            bg = Image.open(io.BytesIO(raw)).convert("RGBA")

            # Cover-fit
            ir = bg.width / bg.height
            wr = WINDOW_W / WINDOW_H
            if ir > wr:
                h = WINDOW_H; w = int(h * ir)
            else:
                w = WINDOW_W; h = int(w / ir)
            bg = bg.resize((w, h), Image.LANCZOS)
            l = (w - WINDOW_W) // 2
            t = (h - WINDOW_H) // 2
            bg = bg.crop((l, t, l + WINDOW_W, t + WINDOW_H))

            # Darken right edge so panel text is readable
            overlay = Image.new("RGBA", (WINDOW_W, WINDOW_H), (0, 0, 0, 0))
            from PIL import ImageDraw
            draw = ImageDraw.Draw(overlay)
            # Gradient from transparent to dark on the right side
            gradient_start = WINDOW_W - PANEL_W - 60
            for x in range(gradient_start, WINDOW_W):
                alpha = int(200 * (x - gradient_start) / (WINDOW_W - gradient_start))
                draw.line([(x, 0), (x, WINDOW_H)], fill=(10, 10, 10, alpha))
            bg = Image.alpha_composite(bg, overlay)

            self._bg_ctk = ctk.CTkImage(
                light_image=bg, dark_image=bg,
                size=(WINDOW_W, WINDOW_H))
            lbl = ctk.CTkLabel(self, text="", image=self._bg_ctk)
            lbl.place(x=0, y=0, relwidth=1, relheight=1)
        except Exception as e:
            print("BG load failed:", e)

    # ══════════════════════════════════════════════════════════════════
    #  Installer UI
    # ══════════════════════════════════════════════════════════════════
    def _build_installer_ui(self):
        self.title("Elden Ring Launcher  -  Setup")

        # Centred panel
        panel = ctk.CTkFrame(self, width=420, height=290,
                             fg_color=PANEL_SOLID, corner_radius=14,
                             border_width=1, border_color=GOLD_DIM)
        panel.place(relx=0.5, rely=0.5, anchor="center")
        panel.pack_propagate(False)

        # ── Title ─────────────────────────────────────────────────────
        ctk.CTkLabel(
            panel, text="Welcome, Tarnished",
            font=ctk.CTkFont(FONT_FAMILY, 24, weight="bold"),
            text_color=GOLD
        ).pack(pady=(28, 2))

        ctk.CTkLabel(
            panel, text="FIRST TIME SETUP",
            font=ctk.CTkFont(FONT_FAMILY, 11, weight="bold"),
            text_color=GOLD_DIM
        ).pack(pady=(0, 8))

        ctk.CTkFrame(panel, height=1, fg_color="#332A15").pack(
            fill="x", padx=80, pady=(0, 16))

        # Description Line 1
        ctk.CTkLabel(
            panel, text="Click below to locate your game's",
            font=ctk.CTkFont(FONT_FAMILY, 12),
            text_color=TEXT_SUB, justify="center"
        ).pack(pady=(0, 2))

        # Target file (Bold & Highlighted)
        ctk.CTkLabel(
            panel, text="start_protected_game.exe",
            font=ctk.CTkFont(FONT_FAMILY, 13, weight="bold"),
            text_color=GOLD_DIM, justify="center"
        ).pack(pady=(0, 2))

        # Description Line 3
        ctk.CTkLabel(
            panel, text="The original will be safely renamed.",
            font=ctk.CTkFont(FONT_FAMILY, 12),
            text_color=TEXT_SUB, justify="center"
        ).pack(pady=(0, 20))

        # Button
        ctk.CTkButton(
            panel, text="Browse & Install",
            font=ctk.CTkFont(FONT_FAMILY, 15, weight="bold"),
            height=44, corner_radius=8,
            fg_color=GOLD, hover_color=GOLD_HOVER,
            text_color=DARK_BG,
            command=self._install
        ).pack(fill="x", padx=40)

    # ══════════════════════════════════════════════════════════════════
    #  Launcher UI
    # ══════════════════════════════════════════════════════════════════
    def _build_launcher_ui(self):

        # ── Right-side panel ──────────────────────────────────────────
        panel = ctk.CTkFrame(self, width=PANEL_W, height=WINDOW_H,
                             fg_color=PANEL_SOLID,
                             corner_radius=0)
        panel.place(relx=1.0, anchor="ne", y=0)
        panel.pack_propagate(False)

        # Thin gold accent line on left edge of panel
        accent = ctk.CTkFrame(panel, width=2, height=WINDOW_H, fg_color=GOLD_DIM)
        accent.place(x=0, y=0)

        # ── Header ───────────────────────────────────────────────────
        header = ctk.CTkFrame(panel, fg_color="transparent")
        header.pack(fill="x", padx=18, pady=(28, 0))

        ctk.CTkLabel(
            header, text="LAUNCH",
            font=ctk.CTkFont(FONT_FAMILY, 20, weight="bold"),
            text_color=GOLD
        ).pack(side="left")

        ctk.CTkButton(
            header, text="+ Add",
            width=56, height=26, corner_radius=4,
            font=ctk.CTkFont(FONT_FAMILY, 11, weight="bold"),
            fg_color=GOLD_DIM, hover_color=GOLD,
            text_color="#1A1A0A",
            command=self._add_custom_exe
        ).pack(side="right")

        # Subtitle
        ctk.CTkLabel(
            panel, text="Choose your path",
            font=ctk.CTkFont(FONT_FAMILY, 11),
            text_color=TEXT_SUB, anchor="w"
        ).pack(fill="x", padx=20, pady=(2, 0))

        # Divider
        ctk.CTkFrame(panel, height=1, fg_color=GOLD_DIM).pack(
            fill="x", padx=18, pady=(10, 14))

        # ── Button area ──────────────────────────────────────────────
        self._btn_frame = ctk.CTkScrollableFrame(
            panel, fg_color="transparent",
            scrollbar_button_color=GOLD_DIM,
            scrollbar_button_hover_color=GOLD)
        self._btn_frame.pack(expand=True, fill="both",
                             padx=8, pady=(0, 8))

        self._refresh_buttons()

    # ──────────────────────────────────────────────────────────────────
    def _make_btn(self, parent, label, sub, cmd, available=True):
        """Create a styled launch card using a frame."""
        title_color = TEXT_MAIN if available else TEXT_SUB
        sub_color = TEXT_SUB if available else "#444444"
        border_c = CARD_BORDER if available else "#1E1E1E"

        card = ctk.CTkFrame(parent, height=58, corner_radius=8,
                            fg_color=CARD_BG,
                            border_width=1, border_color=border_c)
        card.pack(pady=4, fill="x", padx=4)
        card.pack_propagate(False)

        # Gold left accent strip
        accent = ctk.CTkFrame(card, width=3, height=40,
                              fg_color=GOLD_DIM if available else "#2A2A2A",
                              corner_radius=2)
        accent.pack(side="left", padx=(10, 8), pady=8)

        # Text container
        text_frame = ctk.CTkFrame(card, fg_color="transparent")
        text_frame.pack(side="left", fill="both", expand=True, pady=6)

        title_lbl = ctk.CTkLabel(
            text_frame, text=label,
            font=ctk.CTkFont(FONT_FAMILY, 14, weight="bold"),
            text_color=title_color, anchor="w")
        title_lbl.pack(fill="x")

        sub_lbl = ctk.CTkLabel(
            text_frame, text=sub,
            font=ctk.CTkFont(FONT_FAMILY, 10),
            text_color=sub_color, anchor="w")
        sub_lbl.pack(fill="x")

        # Make everything clickable + hover effect
        def on_enter(e):
            card.configure(fg_color=CARD_HOVER)
        def on_leave(e):
            card.configure(fg_color=CARD_BG)
        def on_click(e):
            cmd()
        def on_right_click(e):
            # If they right click, prompt to clear the saved path
            if messagebox.askyesno("Reset", f"Reset the path for '{label}'?"):
                cfg = self._load_config()
                if label == "Offline Mode" and "offline_override" in cfg:
                    del cfg["offline_override"]
                elif label == "Seamless Co-op" and "seamless_override" in cfg:
                    del cfg["seamless_override"]
                self._save_config(cfg)
                self._refresh_buttons()

        for widget in [card, accent, text_frame, title_lbl, sub_lbl]:
            widget.bind("<Enter>", on_enter)
            widget.bind("<Leave>", on_leave)
            widget.bind("<Button-1>", on_click)
            widget.bind("<Button-3>", on_right_click)

        return card

    # ──────────────────────────────────────────────────────────────────
    def _refresh_buttons(self):
        for w in self._btn_frame.winfo_children():
            w.destroy()

        cfg = self._load_config()

        # 1 — Vanilla
        self._make_btn(
            self._btn_frame,
            "Elden Ring", "Launch via EAC  (Online)",
            self._launch_real)

        # 2 — Offline
        offline = cfg.get("offline_override")
        if not offline or not os.path.exists(offline):
            m = sorted(glob.glob(os.path.join(
                self.base_dir, "EldenRingOfflineLauncher-*.exe")),
                reverse=True)
            offline = m[0] if m else None

        self._make_btn(
            self._btn_frame,
            "Offline Mode",
            "Ready" if offline else "Click to locate",
            lambda o=offline: self._launch_or_locate("offline_override", o),
            available=bool(offline))

        # 3 — Seamless Co-op
        seamless = cfg.get("seamless_override")
        if not seamless or not os.path.exists(seamless):
            d = os.path.join(self.base_dir, "ersc_launcher.exe")
            seamless = d if os.path.exists(d) else None

        self._make_btn(
            self._btn_frame,
            "Seamless Co-op",
            "Ready" if seamless else "Click to locate",
            lambda s=seamless: self._launch_or_locate("seamless_override", s),
            available=bool(seamless))

        # 4 — Convergence
        convergence = cfg.get("convergence_override")
        if not convergence or not os.path.exists(convergence):
            c = os.path.join(self.base_dir, "Start_Convergence.bat")
            convergence = c if os.path.exists(c) else None

        if convergence:
            self._make_btn(
                self._btn_frame,
                "The Convergence",
                "Ready",
                lambda c=convergence: self._launch_or_locate("convergence_override", c),
                available=True)

        # 5+ — Custom
        for i, app in enumerate(cfg.get("custom_apps", [])):
            nm = app.get("name", "Unknown")
            p = app.get("path", "")
            fp = p if os.path.isabs(p) else os.path.join(self.base_dir, p)
            ok = os.path.exists(fp)
            cmd = (lambda x=fp: self._launch_custom(x)) if ok \
                else (lambda idx=i: self._relocate_custom(idx))
            self._make_btn(
                self._btn_frame, nm,
                "Ready" if ok else "Click to locate",
                cmd, available=ok)

        # Spacer + bottom row
        ctk.CTkFrame(self._btn_frame, height=1,
                     fg_color="#1E1E1E").pack(
            fill="x", padx=10, pady=(16, 8))

        # Bottom row: Uninstall (left-ish) + Endorse (right-ish)
        bottom = ctk.CTkFrame(self._btn_frame, fg_color="transparent")
        bottom.pack(fill="x", padx=8, pady=(0, 4))

        ctk.CTkButton(
            bottom, text="Uninstall",
            width=100, height=28, corner_radius=6,
            font=ctk.CTkFont(FONT_FAMILY, 10),
            fg_color=RED_BTN, hover_color=RED_HOVER,
            text_color=RED_TEXT,
            command=self._uninstall
        ).pack(side="left")

        endorse_lbl = ctk.CTkLabel(
            bottom, text="♥ Endorse this Mod",
            font=ctk.CTkFont(FONT_FAMILY, 10),
            text_color=GOLD_DIM, cursor="hand2")
        endorse_lbl.pack(side="right")
        endorse_lbl.bind("<Button-1>",
                         lambda e: webbrowser.open(NEXUS_URL))
        endorse_lbl.bind("<Enter>",
                         lambda e: endorse_lbl.configure(text_color=GOLD))
        endorse_lbl.bind("<Leave>",
                         lambda e: endorse_lbl.configure(text_color=GOLD_DIM))

    # ══════════════════════════════════════════════════════════════════
    #  Config
    # ══════════════════════════════════════════════════════════════════
    def _load_config(self):
        if os.path.exists(self.config_path):
            try:
                with open(self.config_path, "r") as f:
                    return json.load(f)
            except Exception:
                pass
        return {"custom_apps": []}

    def _save_config(self, cfg):
        with open(self.config_path, "w") as f:
            json.dump(cfg, f, indent=4)

    # ══════════════════════════════════════════════════════════════════
    #  Actions
    # ══════════════════════════════════════════════════════════════════
    def _launch_or_locate(self, key, path):
        if path and os.path.exists(path):
            self._launch_custom(path)
        else:
            fp = filedialog.askopenfilename(
                title="Locate Executable",
                filetypes=[("Executable & Batch Files", "*.exe;*.bat")])
            if fp:
                cfg = self._load_config()
                cfg[key] = fp
                self._save_config(cfg)
                self._refresh_buttons()

    def _relocate_custom(self, idx):
        fp = filedialog.askopenfilename(
            title="Locate Executable",
            filetypes=[("Executable & Batch Files", "*.exe;*.bat")])
        if fp:
            cfg = self._load_config()
            cfg["custom_apps"][idx]["path"] = fp
            self._save_config(cfg)
            self._refresh_buttons()

    def _add_custom_exe(self):
        fp = filedialog.askopenfilename(
            title="Select Executable",
            filetypes=[("Executable & Batch Files", "*.exe;*.bat")])
        if not fp:
            return
        dlg = ctk.CTkInputDialog(
            text="Enter a name for this button:", title="Button Name")
        name = dlg.get_input()
        if not name:
            return
        rel = os.path.relpath(fp, self.base_dir)
        final = rel if not rel.startswith("..") else fp
        cfg = self._load_config()
        cfg.setdefault("custom_apps", []).append(
            {"name": name, "path": final})
        self._save_config(cfg)
        self._refresh_buttons()

    # ── Install ───────────────────────────────────────────────────────
    def _install(self):
        fp = filedialog.askopenfilename(
            title="Select start_protected_game.exe",
            filetypes=[("Executable Files",
                        "start_protected_game.exe")])
        if not fp:
            return
        if os.path.basename(fp).lower() != "start_protected_game.exe":
            messagebox.showerror(
                "Error", "Please select 'start_protected_game.exe'")
            return

        target_dir = os.path.dirname(fp)
        backup = os.path.join(
            target_dir, "real_start_protected_game.exe")
        uninstaller = os.path.join(
            target_dir, "Uninstall_Launcher.bat")
            
        try:
            if os.path.exists(backup) or os.path.exists(uninstaller):
                messagebox.showerror(
                    "Already Installed",
                    "The Elden Ring AIO Launcher is already installed in this directory!\n\n"
                    "Please launch the game directly through Steam to use the launcher.\n\n"
                    "Installation aborted to prevent overwriting your original game files.")
                return
            os.rename(fp, backup)
            dest = os.path.join(
                target_dir, "start_protected_game.exe")
            shutil.copy2(self.current_file, dest)
            
            # Create external uninstaller
            bat_path = os.path.join(target_dir, "Uninstall_Launcher.bat")
            with open(bat_path, "w") as f:
                f.write('@echo off\n')
                f.write('echo Uninstalling Elden Ring AIO Launcher...\n')
                f.write('if exist "start_protected_game.exe" (\n')
                f.write('    if exist "real_start_protected_game.exe" (\n')
                f.write('        del /f /q "start_protected_game.exe"\n')
                f.write('        ren "real_start_protected_game.exe" "start_protected_game.exe"\n')
                f.write('    )\n')
                f.write(')\n')
                f.write('del /f /q "launcher_config.json" "launcher_debug.log" "launcher_error.log" "launch_delay_temp.bat" "launcher_proxy.vbs"\n')
                f.write('echo Launcher successfully uninstalled. Game restored to Vanilla.\n')
                f.write('pause\n')
                f.write('start /b "" cmd /c del "%~f0"&exit /b\n')

            messagebox.showinfo(
                "Success",
                "Launcher installed!\n\n"
                "Press Play in Steam to open this launcher.\n"
                "(An 'Uninstall_Launcher.bat' file was also created in your game folder)")
            self.destroy()
        except PermissionError:
            messagebox.showerror(
                "Permission Error",
                "Access denied.\nTry running as Administrator.")
            if os.path.exists(backup) and not os.path.exists(
                    os.path.join(target_dir,
                                 "start_protected_game.exe")):
                os.rename(backup, fp)
        except Exception as e:
            if os.path.exists(backup) and not os.path.exists(
                    os.path.join(target_dir,
                                 "start_protected_game.exe")):
                os.rename(backup, fp)
            messagebox.showerror("Error", str(e))

    # ── Uninstall ─────────────────────────────────────────────────────
    def _uninstall(self):
        if not messagebox.askyesno(
                "Uninstall",
                "Restore original start_protected_game.exe\n"
                "and remove this launcher?"):
            return
        try:
            tmp = os.path.join(
                self.base_dir, "uninstall_temp_launcher.exe")
            if os.path.exists(tmp):
                os.remove(tmp)
            os.rename(self.current_file, tmp)

            os.rename(self.real_exe_path,
                      os.path.join(self.base_dir,
                                   "start_protected_game.exe"))

            messagebox.showinfo(
                "Done", "Launcher uninstalled. Game restored.")
            
            # Write a self-deleting batch script for cleanup to bypass cmd.exe quoting hell
            cleanup_bat = os.path.join(self.base_dir, "cleanup_launcher_internal.bat")
            with open(cleanup_bat, "w") as f:
                f.write('@echo off\n')
                f.write('ping 127.0.0.1 -n 5 > nul\n') # wait 4 seconds for Nuitka to close
                f.write(f'del /f /q "{tmp}" launcher_config.json launcher_debug.log launcher_error.log Uninstall_Launcher.bat launch_delay_temp.bat launcher_proxy.vbs\n')
                f.write('start /b "" cmd /c del "%~f0"&exit /b\n')

            subprocess.Popen([cleanup_bat], 
                             stdin=subprocess.DEVNULL,
                             stdout=subprocess.DEVNULL,
                             stderr=subprocess.DEVNULL,
                             creationflags=0x08000000, 
                             close_fds=True,
                             cwd=self.base_dir)

            self.destroy()
        except Exception as e:
            messagebox.showerror("Error", f"Uninstall failed:\n{e}")

    # ── Launch ────────────────────────────────────────────────────────
    def _launch_custom(self, path):
        if getattr(self, '_launched', False): return
        self._launched = True
        if os.path.exists(path):
            # 1. We MUST completely close our launcher so Co-op doesn't abort.
            # 2. We MUST escape Steam's Job Object, or Steam kills Co-op when we close.
            # 3. We MUST set the correct CWD, or Co-op crashes with Error 3.
            # 4. We MUST NOT drop a .vbs or .bat file, because Windows Defender flags it as a Wacatac dropper.
            # Solution: We run an in-memory PowerShell command that talks directly to the Windows Desktop
            # COM object to flawlessly launch the mod natively outside the Job Object with the exact right CWD.
            import subprocess
            ps_cmd = f"$shell = New-Object -ComObject Shell.Application; $shell.ShellExecute('{path}', '', '{os.path.dirname(path)}', 'open', 1)"
            subprocess.Popen(["powershell", "-WindowStyle", "Hidden", "-Command", ps_cmd], creationflags=0x08000000)
            self.destroy()

    def _launch_real(self):
        if getattr(self, '_launched', False): return
        self._launched = True
        if os.path.exists(self.real_exe_path):
            os.environ["SteamAppId"] = "1245620"
            os.startfile(self.real_exe_path, cwd=self.base_dir)
            self.destroy()


if __name__ == "__main__":
    try:
        app = EldenRingLauncher()
        app.mainloop()
    except Exception:
        # Write crash log next to the exe so we can debug
        try:
            log_dir = os.path.dirname(os.path.abspath(sys.argv[0]))
            log_path = os.path.join(log_dir, "launcher_error.log")
            with open(log_path, "w") as f:
                f.write(f"sys.argv: {sys.argv}\n")
                f.write(f"sys.executable: {sys.executable}\n")
                f.write(f"cwd: {os.getcwd()}\n\n")
                traceback.print_exc(file=f)
        except Exception:
            pass
        raise
