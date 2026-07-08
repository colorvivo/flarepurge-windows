# Changelog

Change history for **FlarePurge for Windows**. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow
[SemVer](https://semver.org/).

> **About this changelog.** FlarePurge is developed in a private core repository
> and released here as the public, MIT-licensed edition. This file mirrors the
> full release history of the core so the public builds are fully traceable.
> Some entries reference cross-platform parity work (macOS / Android) that lives
> in separate repositories.

## [1.0.2] — 2026-04-26

> This release contains the work that was prepared as 1.0.1 locally but never
> shipped to the Store. It is published directly as 1.0.2 to avoid version
> collisions with the Partner Center catalog.

### Added
- **Per-account zone list cache.** The list appears instantly when opening the
  app or switching accounts, then refreshes silently in the background. Persisted
  at `%LOCALAPPDATA%\FlarePurge\zones.v1.json` (public metadata only — never any token).
- **Manual zone reload:** "Reload" button in the status bar + **Ctrl+R** shortcut.
  Forces a fresh fetch to `api.cloudflare.com`.
- **"Zones updated X ago" indicator** in the status bar, refreshed every 30 s.
- **Optional label when adding an API key.** Useful to tell accounts apart when
  you store several (e.g. "Client X" vs. "Personal"). Blank falls back to the
  name returned by Cloudflare.
- **Rename the label** of a saved account from Settings → API Accounts (pencil
  icon on each row).

### Translations
- **i18n infrastructure fully wired.** The UI now consumes the 21 bundled
  translations instead of hardcoded strings. Previously the infrastructure
  existed (21 languages × ~220 keys) but no view used it — which is why the
  wizard showed in English and the language selector had no effect.
- **Token wizard, About, Settings, Zones, Zone detail, modal dialogs (purge,
  confirmation, selective, history), tray menu and result windows** now go
  through `ResourceLoader`. Changing the language in Settings now takes effect
  on app restart.
- **English leaks fixed** ("← Back", "Paste token", "Verify and save token",
  "Token saved.", the "BUILT WITH / BACKEND / TRACKING / TOKEN STORAGE /
  LANGUAGES / PRICE" overlines in About, the "Open FlarePurge / Quit" tray menu,
  etc.).
- **116 new keys** added to the catalog, with complete translations for
  EN/ES/ES-MX/CA/FR/IT/DE/PT-PT (+ NL/NB/SV for time and configuration strings).
  The remaining 13 locales (AR/EL/HE/HU/JA/KO/PL/RO/TH/ZH-Hans/…) fall back to
  English and are marked `<!-- TODO: translate -->` until a native translation lands.

### Cross-platform parity
- **Anti re-render in `ApplyZones`:** the silent refresh no longer rebuilds the
  ListView items when the incoming payload matches the current one (compared by
  Id / Name / Status / Plan / AccountName / NameServers / CreatedOn). Preserves
  selection and scroll between refreshes.
- **Silent-error policy aligned across platforms:** the background silent refresh
  no longer surfaces 401/403 errors as a reauth banner. Only a manual Ctrl+R
  reports failures to the user, so focus is never stolen while you work.
- **Centralized `IAccountStore.RenameAccount(id, newLabel)`:** the Settings view
  no longer mutates the list ad-hoc; it delegates to the store (trim + no-op on
  no change + unknown-id guard).

### Fixed
- Crash when editing an account label in Settings (*"Only a single ContentDialog
  can be open at any time"*). Renaming now uses a Flyout anchored to the button
  instead of a nested dialog.

### Internal
- An account's zone cache is cleared on sign-out or removal.
- `L.S(key)` / `L.Format(key, args…)` helper for consumption from XAML
  (`{x:Bind loc:L.S('key'), Mode=OneTime}`) and code-behind.
- 14 new tests total (8 for `JsonZoneCacheStore`, 6 for
  `IAccountStore.RenameAccount`). **Suite: 205/205** green, 0 warnings.

---

## [1.0.0] — 2026-04-24

First Microsoft Store release. 1:1 functional parity with FlarePurge for Mac v1.7.x.

### Added
- **List Cloudflare zones** with metadata (plan, account, nameservers, date).
- **Purge everything** — clear a zone's entire cache in one click.
- **Selective purge** by URL (up to 30 per batch, automatic chunking) or by host.
- **Multi-account:** add several API keys, switch between them, automatic grouping
  when a token spans multiple Cloudflare accounts.
- **Favorites** per zone with bulk purge ("Purge N favorite zones") and "Purge all
  zones in account X".
- **Session history** with timestamp, result and purge ID.
- **Dynamic system tray:** menu with accounts + favorites + "Purge all".
- **Minimize to tray** on close (optional).
- **Settings:** theme (Auto/Light/Dark, applied live), language (21 locales),
  confirm-before-purge.
- **Remote kill switch** (`flarepurge.com/status.json`) to disable the app in case
  of an incident.
- **Keyboard shortcuts:** Ctrl+R (reload), Ctrl+F (search), Ctrl+, (settings),
  Ctrl+1..9 (jump to favorite N), Ctrl+Shift+P (quick purge), Esc (back).
- **Accessibility baseline:** AutomationProperties, contrast, touch targets ≥32 px.

### Security
- Tokens stored in the **Windows Credential Vault** (DPAPI).
- **Certificate pinning** (SPKI SHA-256) against `api.cloudflare.com`
  (3 hashes: GTS WE1 + WR1 + Root R4).
- **No analytics, no tracking, no third-party SDKs.** The only outbound requests
  go to `api.cloudflare.com` and `flarepurge.com/status.json`.
- Minimal token scopes: `Zone:Read` + `Zone Cache Purge` (+ `User:Read` for
  initial validation).

### Stack
- WinUI 3 · Windows App SDK 1.6 · C# 13 · .NET 10.
- Target Windows 10 21H2+ / Windows 11.
- 191/191 tests, 0 warnings with `TreatWarningsAsErrors=true`.
