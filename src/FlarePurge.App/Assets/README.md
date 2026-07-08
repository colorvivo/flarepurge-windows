# Assets

Full MSIX asset pack for FlarePurge Windows. Generated from the Apple design
master (`flarepurge/Apple/design/flarepurge-icono-master.png`) via
`scripts/generate_windows_icons.sh`. All PNGs are opaque, sRGB, 8-bit/channel,
no EXIF. Background `#0D0E1A`, accent `#F38020`. Layout matches the macOS app
icon 1:1.

**Do not regenerate from the Windows VM.** Run the generator on the Mac and
`git pull` on the VM to consume the result.

## Logo scales (Windows 11 DPI variants)

Every MSIX logo ships in five scales. The base file (no suffix) is a copy of
`.scale-100` per Microsoft guidance; Windows prefers the scale closest to the
monitor DPI and falls back to the base.

| Family | scale-100 | scale-125 | scale-150 | scale-200 | scale-400 | Composition |
|---|---|---|---|---|---|---|
| `Square44x44Logo` | 44×44 | 55×55 | 66×66 | 88×88 | 176×176 | Flame only, centered |
| `Square150x150Logo` | 150×150 | 188×188 | 225×225 | 300×300 | 600×600 | Flame + wordmark stacked |
| `Wide310x150Logo` | 310×150 | 388×188 | 465×225 | 620×300 | 1240×600 | Flame left, wordmark right |
| `StoreLogo` | 50×50 | 63×63 | 75×75 | 100×100 | 200×200 | Flame + wordmark stacked |
| `SplashScreen` | 620×300 | 775×375 | 930×450 | 1240×600 | 2480×1200 | Flame + wordmark centered in wide canvas |

## Target-size variants for Square44

Used by Windows in taskbar compact, Jump List, and Start search hover contexts.
Flame-only composition (wordmark is illegible below ~44 px).

| File | Size |
|---|---|
| `Square44x44Logo.targetsize-16.png` | 16×16 |
| `Square44x44Logo.targetsize-24.png` | 24×24 |
| `Square44x44Logo.targetsize-32.png` | 32×32 |
| `Square44x44Logo.targetsize-48.png` | 48×48 |
| `Square44x44Logo.targetsize-256.png` | 256×256 |

## Executable icon

`AppIcon.ico` is the multi-resolution icon embedded into `FlarePurge.App.exe`
via `<ApplicationIcon>Assets\AppIcon.ico</ApplicationIcon>` in the csproj.
Windows Explorer, Alt-Tab, and the title bar use this file — NOT the PNGs
above. Resolutions bundled: 16, 24, 32, 48, 64, 128, 256.

## Regenerate

Requires ImageMagick 7 on macOS (`brew install imagemagick`). From the repo
root on the Mac:

```bash
bash scripts/generate_windows_icons.sh
```

The script defaults to the Apple branding master and this Assets directory.
Override with `--branding-master <path>`, `--icon-master <path>`, or
`--output <dir>`.

## Microsoft Store — pending (v1.1+)

The following are deliberately NOT included in this pack; they require the
app to be functional or belong to a later sprint:

- Store screenshots — captured from the running app in Sprint 3+.
- Hero image 2400×1200 — Store feature graphic.
- Tray icon (`.ico` 16×16 monochrome) — added in Sprint 2 alongside the
  system tray implementation.
- `-altform-unplated` / `-altform-lightunplated` — high-contrast themes for
  accessibility, v1.1+.
