import re
import threading
import tkinter as tk
from tkinter import ttk, messagebox, filedialog
from pathlib import Path
from deep_translator import GoogleTranslator


DEFAULT_REGEX = r'(<sys:String\b[^>]*>)(.*?)(</sys:String>)'
DEFAULT_BATCH_SIZE = 40


def get_languages():
    try:
        languages = GoogleTranslator().get_supported_languages(as_dict=True)
        items = sorted(languages.items(), key=lambda x: x[0].lower())
        return {"Auto Detect": "auto", **{name.title(): code for name, code in items}}
    except Exception:
        return {
            "Auto Detect": "auto",
            "English": "en",
            "French": "fr",
            "Spanish": "es",
            "German": "de",
            "Italian": "it",
            "Portuguese": "pt",
            "Dutch": "nl",
            "Polish": "pl",
            "Russian": "ru",
            "Japanese": "ja",
            "Korean": "ko",
            "Chinese (Simplified)": "zh-CN",
            "Chinese (Traditional)": "zh-TW",
            "Arabic": "ar",
        }


def chunked(items, size):
    for i in range(0, len(items), size):
        yield items[i:i + size]


def translate_matches_batched(
    text,
    pattern,
    source_language,
    target_language,
    group_number,
    batch_size,
    progress_callback=None
):
    regex = re.compile(pattern, re.DOTALL)
    matches = list(regex.finditer(text))

    if not matches:
        return text, 0, 0

    values = []

    for match in matches:
        try:
            value = match.group(group_number)
        except IndexError:
            raise ValueError(f"Regex does not contain capture group {group_number}.")

        if value.strip():
            values.append(value)

    unique_values = list(dict.fromkeys(values))

    if not unique_values:
        return text, len(matches), 0

    translator = GoogleTranslator(
        source=source_language,
        target=target_language
    )

    translations = {}
    batches = list(chunked(unique_values, batch_size))
    total_batches = len(batches)

    for batch_index, batch in enumerate(batches, start=1):
        if progress_callback:
            progress_callback(
                batch_index,
                total_batches,
                len(unique_values)
            )

        translated_batch = translator.translate_batch(batch)

        if isinstance(translated_batch, str):
            translated_batch = [translated_batch]

        if not translated_batch or len(translated_batch) != len(batch):
            translated_batch = [
                translator.translate(item)
                for item in batch
            ]

        for original, translated in zip(batch, translated_batch):
            translations[original] = translated if translated is not None else original

    pieces = []
    cursor = 0

    for match in matches:
        start, end = match.span(group_number)
        original = match.group(group_number)

        pieces.append(text[cursor:start])

        if original.strip():
            pieces.append(translations.get(original, original))
        else:
            pieces.append(original)

        cursor = end

    pieces.append(text[cursor:])

    return "".join(pieces), len(matches), len(unique_values)


class RegexTranslatorApp(tk.Tk):
    def __init__(self):
        super().__init__()
        self.title("Regex Translator")
        self.geometry("1180x820")
        self.minsize(900, 650)

        self.bg = "#111318"
        self.panel = "#181b22"
        self.panel_alt = "#20242d"
        self.input_bg = "#0d0f14"
        self.fg = "#f2f3f5"
        self.muted = "#9ca3af"
        self.accent = "#6c8cff"
        self.accent_hover = "#7d99ff"
        self.border = "#2d3340"

        self.configure(bg=self.bg)

        self.style = ttk.Style(self)
        self.style.theme_use("clam")
        self.configure_styles()

        self.languages = get_languages()

        self.columnconfigure(0, weight=1)
        self.rowconfigure(3, weight=1)
        self.rowconfigure(6, weight=1)

        self.build_ui()

    def configure_styles(self):
        self.style.configure(
            "TFrame",
            background=self.bg
        )
        self.style.configure(
            "Panel.TFrame",
            background=self.panel
        )
        self.style.configure(
            "TLabel",
            background=self.bg,
            foreground=self.fg,
            font=("Segoe UI", 10)
        )
        self.style.configure(
            "Muted.TLabel",
            background=self.bg,
            foreground=self.muted,
            font=("Segoe UI", 9)
        )
        self.style.configure(
            "Panel.TLabel",
            background=self.panel,
            foreground=self.fg,
            font=("Segoe UI", 10)
        )
        self.style.configure(
            "TEntry",
            fieldbackground=self.input_bg,
            foreground=self.fg,
            insertcolor=self.fg,
            bordercolor=self.border,
            lightcolor=self.border,
            darkcolor=self.border,
            padding=8
        )
        self.style.map(
            "TEntry",
            fieldbackground=[("focus", self.input_bg)],
            bordercolor=[("focus", self.accent)]
        )
        self.style.configure(
            "TCombobox",
            fieldbackground=self.input_bg,
            background=self.panel_alt,
            foreground=self.fg,
            arrowcolor=self.fg,
            bordercolor=self.border,
            lightcolor=self.border,
            darkcolor=self.border,
            padding=6
        )
        self.style.map(
            "TCombobox",
            fieldbackground=[("readonly", self.input_bg)],
            foreground=[("readonly", self.fg)],
            background=[("readonly", self.panel_alt)],
            bordercolor=[("focus", self.accent)]
        )
        self.style.configure(
            "TButton",
            background=self.panel_alt,
            foreground=self.fg,
            bordercolor=self.border,
            lightcolor=self.border,
            darkcolor=self.border,
            padding=(12, 8),
            font=("Segoe UI", 10)
        )
        self.style.map(
            "TButton",
            background=[("active", "#2a303b"), ("disabled", "#181b22")],
            foreground=[("disabled", "#676d78")]
        )
        self.style.configure(
            "Accent.TButton",
            background=self.accent,
            foreground="#ffffff",
            bordercolor=self.accent,
            lightcolor=self.accent,
            darkcolor=self.accent,
            padding=(14, 8),
            font=("Segoe UI Semibold", 10)
        )
        self.style.map(
            "Accent.TButton",
            background=[("active", self.accent_hover), ("disabled", "#3b466a")],
            foreground=[("disabled", "#b0b7ca")]
        )
        self.style.configure(
            "TNotebook",
            background=self.bg,
            borderwidth=0
        )
        self.style.configure(
            "TNotebook.Tab",
            background=self.panel_alt,
            foreground=self.muted,
            padding=(14, 8),
            borderwidth=0
        )
        self.style.map(
            "TNotebook.Tab",
            background=[("selected", self.accent)],
            foreground=[("selected", "#ffffff")]
        )

    def build_ui(self):
        header = ttk.Frame(self, style="Panel.TFrame", padding=14)
        header.grid(row=0, column=0, sticky="ew", padx=12, pady=(12, 8))
        header.columnconfigure(1, weight=1)
        header.columnconfigure(3, weight=1)

        ttk.Label(
            header,
            text="Regex",
            style="Panel.TLabel"
        ).grid(
            row=0,
            column=0,
            sticky="w",
            padx=(0, 8)
        )

        self.regex_var = tk.StringVar(value=DEFAULT_REGEX)

        ttk.Entry(
            header,
            textvariable=self.regex_var
        ).grid(
            row=0,
            column=1,
            columnspan=3,
            sticky="ew"
        )

        ttk.Label(
            header,
            text="Capture group",
            style="Panel.TLabel"
        ).grid(
            row=1,
            column=0,
            sticky="w",
            padx=(0, 8),
            pady=(10, 0)
        )

        self.group_var = tk.StringVar(value="2")

        ttk.Entry(
            header,
            textvariable=self.group_var,
            width=8
        ).grid(
            row=1,
            column=1,
            sticky="w",
            pady=(10, 0)
        )

        ttk.Label(
            header,
            text="Batch size",
            style="Panel.TLabel"
        ).grid(
            row=1,
            column=2,
            sticky="w",
            padx=(0, 8),
            pady=(10, 0)
        )

        self.batch_var = tk.StringVar(value=str(DEFAULT_BATCH_SIZE))

        ttk.Entry(
            header,
            textvariable=self.batch_var,
            width=8
        ).grid(
            row=1,
            column=3,
            sticky="w",
            pady=(10, 0)
        )

        ttk.Label(
            header,
            text="Input language",
            style="Panel.TLabel"
        ).grid(
            row=2,
            column=0,
            sticky="w",
            padx=(0, 8),
            pady=(10, 0)
        )

        self.source_var = tk.StringVar(value="Auto Detect")

        self.source_combo = ttk.Combobox(
            header,
            textvariable=self.source_var,
            values=list(self.languages.keys()),
            state="readonly"
        )

        self.source_combo.grid(
            row=2,
            column=1,
            sticky="ew",
            pady=(10, 0),
            padx=(0, 12)
        )

        ttk.Label(
            header,
            text="Output language",
            style="Panel.TLabel"
        ).grid(
            row=2,
            column=2,
            sticky="w",
            padx=(0, 8),
            pady=(10, 0)
        )

        default_target = "French" if "French" in self.languages else next(
            key for key in self.languages if key != "Auto Detect"
        )

        self.target_var = tk.StringVar(value=default_target)

        self.target_combo = ttk.Combobox(
            header,
            textvariable=self.target_var,
            values=[
                key
                for key in self.languages.keys()
                if key != "Auto Detect"
            ],
            state="readonly"
        )

        self.target_combo.grid(
            row=2,
            column=3,
            sticky="ew",
            pady=(10, 0)
        )

        ttk.Label(
            self,
            text="Matched strings are deduplicated and translated in batches for much faster large-file translation.",
            style="Muted.TLabel"
        ).grid(
            row=1,
            column=0,
            sticky="w",
            padx=14,
            pady=(0, 8)
        )

        input_bar = ttk.Frame(self)
        input_bar.grid(
            row=2,
            column=0,
            sticky="ew",
            padx=12
        )
        input_bar.columnconfigure(0, weight=1)

        ttk.Label(
            input_bar,
            text="Input"
        ).grid(
            row=0,
            column=0,
            sticky="w"
        )

        ttk.Button(
            input_bar,
            text="Open File",
            command=self.open_file
        ).grid(
            row=0,
            column=1,
            sticky="e"
        )

        input_frame = tk.Frame(
            self,
            bg=self.border,
            highlightthickness=0,
            bd=0
        )

        input_frame.grid(
            row=3,
            column=0,
            sticky="nsew",
            padx=12,
            pady=(6, 10)
        )

        input_frame.columnconfigure(0, weight=1)
        input_frame.rowconfigure(0, weight=1)

        self.input_text = tk.Text(
            input_frame,
            wrap="none",
            undo=True,
            bg=self.input_bg,
            fg=self.fg,
            insertbackground=self.fg,
            selectbackground=self.accent,
            selectforeground="#ffffff",
            relief="flat",
            borderwidth=0,
            padx=10,
            pady=10,
            font=("Consolas", 10)
        )

        self.input_text.grid(
            row=0,
            column=0,
            sticky="nsew",
            padx=1,
            pady=1
        )

        input_scroll_y = ttk.Scrollbar(
            input_frame,
            orient="vertical",
            command=self.input_text.yview
        )

        input_scroll_y.grid(
            row=0,
            column=1,
            sticky="ns"
        )

        input_scroll_x = ttk.Scrollbar(
            input_frame,
            orient="horizontal",
            command=self.input_text.xview
        )

        input_scroll_x.grid(
            row=1,
            column=0,
            sticky="ew"
        )

        self.input_text.configure(
            yscrollcommand=input_scroll_y.set,
            xscrollcommand=input_scroll_x.set
        )

        actions = ttk.Frame(self)
        actions.grid(
            row=4,
            column=0,
            sticky="ew",
            padx=12
        )
        actions.columnconfigure(2, weight=1)

        self.translate_button = ttk.Button(
            actions,
            text="Translate",
            style="Accent.TButton",
            command=self.start_translation
        )

        self.translate_button.grid(
            row=0,
            column=0,
            sticky="w"
        )

        ttk.Button(
            actions,
            text="Clear Output",
            command=self.clear_output
        ).grid(
            row=0,
            column=1,
            sticky="w",
            padx=(8, 0)
        )

        ttk.Button(
            actions,
            text="Save Output",
            command=self.save_output
        ).grid(
            row=0,
            column=3,
            sticky="e"
        )

        self.status_var = tk.StringVar(value="Ready")

        ttk.Label(
            self,
            textvariable=self.status_var,
            style="Muted.TLabel"
        ).grid(
            row=5,
            column=0,
            sticky="w",
            padx=14,
            pady=(8, 4)
        )

        self.output_notebook = ttk.Notebook(self)
        self.output_notebook.grid(
            row=6,
            column=0,
            sticky="nsew",
            padx=12,
            pady=(0, 12)
        )

        self.outputs = {}

    def open_file(self):
        path = filedialog.askopenfilename(
            filetypes=[
                ("Text/XML/XAML files", "*.txt *.xml *.xaml"),
                ("All files", "*.*")
            ]
        )

        if not path:
            return

        try:
            content = Path(path).read_text(encoding="utf-8")
        except UnicodeDecodeError:
            content = Path(path).read_text(encoding="utf-8-sig")

        self.input_text.delete("1.0", "end")
        self.input_text.insert("1.0", content)

    def clear_output(self):
        for tab in self.output_notebook.tabs():
            self.output_notebook.forget(tab)

        self.outputs.clear()
        self.status_var.set("Ready")

    def start_translation(self):
        source_text = self.input_text.get("1.0", "end-1c")
        pattern = self.regex_var.get().strip()

        if not source_text:
            messagebox.showerror(
                "Missing input",
                "Paste or open some text first."
            )
            return

        if not pattern:
            messagebox.showerror(
                "Missing regex",
                "Enter a regex first."
            )
            return

        try:
            group_number = int(self.group_var.get())
        except ValueError:
            messagebox.showerror(
                "Invalid group",
                "Capture group must be a number."
            )
            return

        try:
            batch_size = int(self.batch_var.get())
            if batch_size < 1:
                raise ValueError
        except ValueError:
            messagebox.showerror(
                "Invalid batch size",
                "Batch size must be a positive number."
            )
            return

        try:
            compiled = re.compile(pattern, re.DOTALL)
            matches = list(compiled.finditer(source_text))
        except re.error as exc:
            messagebox.showerror(
                "Invalid regex",
                str(exc)
            )
            return

        if not matches:
            messagebox.showwarning(
                "No matches",
                "The regex matched nothing."
            )
            return

        try:
            matches[0].group(group_number)
        except IndexError:
            messagebox.showerror(
                "Missing capture group",
                f"The regex does not have capture group {group_number}."
            )
            return

        source_name = self.source_var.get()
        target_name = self.target_var.get()

        source_code = self.languages[source_name]
        target_code = self.languages[target_name]

        self.translate_button.config(state="disabled")
        self.clear_output()

        unique_count = len(
            dict.fromkeys(
                match.group(group_number)
                for match in matches
                if match.group(group_number).strip()
            )
        )

        self.status_var.set(
            f"Found {len(matches)} matches, {unique_count} unique strings."
        )

        threading.Thread(
            target=self.worker,
            args=(
                source_text,
                pattern,
                group_number,
                batch_size,
                source_code,
                target_code,
                source_name,
                target_name
            ),
            daemon=True
        ).start()

    def worker(
        self,
        source_text,
        pattern,
        group_number,
        batch_size,
        source_code,
        target_code,
        source_name,
        target_name
    ):
        try:
            def progress(batch_index, total_batches, unique_count):
                self.after(
                    0,
                    lambda: self.status_var.set(
                        f"Translating {source_name} → {target_name} | "
                        f"batch {batch_index}/{total_batches} | "
                        f"{unique_count} unique strings"
                    )
                )

            result, match_count, unique_count = translate_matches_batched(
                source_text,
                pattern,
                source_code,
                target_code,
                group_number,
                batch_size,
                progress
            )

            self.after(
                0,
                lambda: self.add_output_tab(
                    target_name,
                    result
                )
            )

            self.after(
                0,
                lambda: self.status_var.set(
                    f"Done. {match_count} matches, "
                    f"{unique_count} unique strings translated to {target_name}."
                )
            )

        except Exception as exc:
            self.after(
                0,
                lambda e=exc: messagebox.showerror(
                    "Translation failed",
                    str(e)
                )
            )

            self.after(
                0,
                lambda: self.status_var.set(
                    "Translation failed."
                )
            )

        finally:
            self.after(
                0,
                lambda: self.translate_button.config(
                    state="normal"
                )
            )

    def add_output_tab(self, language, result):
        frame = ttk.Frame(self.output_notebook)
        frame.rowconfigure(0, weight=1)
        frame.columnconfigure(0, weight=1)

        text = tk.Text(
            frame,
            wrap="none",
            undo=True,
            bg=self.input_bg,
            fg=self.fg,
            insertbackground=self.fg,
            selectbackground=self.accent,
            selectforeground="#ffffff",
            relief="flat",
            borderwidth=0,
            padx=10,
            pady=10,
            font=("Consolas", 10)
        )

        text.grid(
            row=0,
            column=0,
            sticky="nsew"
        )

        text.insert(
            "1.0",
            result
        )

        scrollbar_y = ttk.Scrollbar(
            frame,
            orient="vertical",
            command=text.yview
        )

        scrollbar_y.grid(
            row=0,
            column=1,
            sticky="ns"
        )

        scrollbar_x = ttk.Scrollbar(
            frame,
            orient="horizontal",
            command=text.xview
        )

        scrollbar_x.grid(
            row=1,
            column=0,
            sticky="ew"
        )

        text.configure(
            yscrollcommand=scrollbar_y.set,
            xscrollcommand=scrollbar_x.set
        )

        self.output_notebook.add(
            frame,
            text=language
        )

        self.outputs[language] = text

    def save_output(self):
        current = self.output_notebook.select()

        if not current:
            messagebox.showinfo(
                "Nothing to save",
                "Translate something first."
            )
            return

        frame = self.nametowidget(current)
        text_widget = None

        for child in frame.winfo_children():
            if isinstance(child, tk.Text):
                text_widget = child
                break

        if text_widget is None:
            return

        path = filedialog.asksaveasfilename(
            defaultextension=".xaml",
            filetypes=[
                ("XAML/XML", "*.xaml *.xml"),
                ("Text", "*.txt"),
                ("All files", "*.*")
            ]
        )

        if not path:
            return

        Path(path).write_text(
            text_widget.get("1.0", "end-1c"),
            encoding="utf-8"
        )


if __name__ == "__main__":
    RegexTranslatorApp().mainloop()
