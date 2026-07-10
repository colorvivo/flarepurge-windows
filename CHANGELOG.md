# Changelog

Change history for **FlarePurge for Windows**. Format based on
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versions follow
[SemVer](https://semver.org/).

> **About this changelog.** FlarePurge is developed in a private core repository
> and released here as the public, MIT-licensed edition. This file mirrors the
> full release history of the core so the public builds are fully traceable.
> Some entries reference cross-platform parity work (macOS / Android) that lives
> in separate repositories.

## [1.7.3] — 2026-07-10 _(security hardening + version parity with macOS)_

> Version jump **1.0.x → 1.7.3** to align the number with the Apple/macOS and
> Android apps (single ecosystem version). A hardening release following a
> specialist security board review: solid fundamentals (real fail-closed cert
> pinning, secrets in the Credential Locker/DPAPI, verified "no tracking"),
> fixing a cluster of concurrency/crash/data-loss bugs — the Windows equivalents
> of what was found on Apple — plus a second pass on network, supply-chain and
> UX robustness.

### Fixed — concurrency and data loss
- **Favorites lost across accounts** (High): toggling a favorite on one account no
  longer wipes the favorites of the others. The global list was rebuilt only from
  the active account's zones; it now preserves favorites of non-visible zones.
- **Bulk purge crash** (High): the parallel work no longer reads the shared zone
  list from its worker threads while the UI mutates it during a background
  refresh — that caused an `InvalidOperationException` that killed the process. It
  now uses a snapshot.
- **Cache poisoning on account switch** (High): an in-flight silent refresh no
  longer paints or caches another account's zones if you switch accounts while it
  resolves (the token is resolved per request). The result is dropped if the active
  account changed.

### Fixed — robustness / crash-safety
- A 200 response with a malformed zone (missing or wrong-typed field) no longer
  crashes: it maps to a decoding error like everything else.
- On-disk state files (`accounts.v1.json`, `zones.v1.json`) locked by antivirus/
  backup or containing a null field no longer crash startup; they degrade to empty.
- **Crash-safe UI handlers** (High): async event handlers (purge, refresh, account
  switch, dialogs, settings/about/history) only caught Cloudflare API errors; any
  other exception (a concurrent dialog, an I/O failure, a vault limit) bubbled up
  through `async void` and **killed the process**. They now all go through a common
  wrapper that logs the error and shows a generic notice instead of exiting. A
  last-resort net was added: an unhandled exception is logged and the app stays
  alive rather than terminating.

### Fixed — UX
- The Settings **"Confirm bulk purge"** toggle now actually works: bulk operations
  were reading the single-zone confirm preference by mistake, so a whole-account
  purge could fire with no prompt.

### Security
- **TLS**: the pinned client no longer follows automatic redirects
  (`AllowAutoRedirect=false`) — a 3xx redirect could carry the request outside the
  pinned perimeter.
- **Masked token in the wizard**: the API-key field is now a `PasswordBox` (was a
  plain `TextBox`). The token is no longer visible on screen, in screenshots or in
  screen shares, and the system spell-checker/dictionary no longer persists it. An
  optional "peek" button lets you verify the paste.
- **Sanitized `crash.log`**: the crash log redacts identifiers and secrets (32-hex
  zone/account IDs, `Bearer …`, emails, long token-like sequences), caps message
  length and rotates by size (64 KB → `crash.log.1`). Prevents CWE-532 if a server
  error's text ever flows into an exception.
- **Supply chain** (S1/S2/S3): `packages.lock.json` generated and committed for all
  three projects; all NuGet dependencies pinned to exact versions (wildcard →
  exact; FluentAssertions held at 6.12.x due to the commercial licence of 7.x+);
  CI restore uses `--locked-mode`; GitHub Actions pinned by commit SHA instead of
  mutable tags. A compromised release of any package or action can no longer slip
  silently into the Store build.

### Fixed — network and input robustness (second pass)
- **Zone identifier validation** (N2): the zoneId is validated as URL-path-safe
  before being interpolated into the API path, so a tampered value (from disk cache
  or a server payload) cannot inject path segments.
- **Response size limit** (N3): API responses are read with a 16 MB cap
  (`LimitedReadStream`) so an oversized or malicious body cannot exhaust memory in
  the parser. The remote kill-switch `message` is truncated (500 chars) and its
  download bounded (64 KB).
- **Robust TLS error classification** (N5): a secure-connection failure is detected
  by the `HttpRequestError.SecureConnectionError` type instead of sniffing for
  "SSL/TLS/certificate" in the text (which breaks with localized OS messages).
- **Unreadable API error messages** (I3): the 16 `error.*` keys were missing from
  the `.resw` files, so any Cloudflare error showed the raw key (e.g.
  "error.notFound") instead of text. Real, localized messages are now shown
  (invalid/expired token, missing permission, rate limit, timeout, server error…).

### Fixed — UX and crash-safety (second pass)
- **Cancelable bulk purge** (C2): the bulk purge progress dialog (favorites /
  account) now has a **Cancel** button. Previously, with 429 rate limiting +
  Retry-After (up to 60 s × 4 per zone), the user was trapped with no way out; it
  now cancels in-flight requests and the summary reflects what completed.
- **Tray favorite purge with the wrong account** (C4): purging a favorite from the
  tray used the active account's token, causing 403/404 when the favorite belonged
  to another account. Each favorite now stores its owning account and purges with
  the correct token.
- **Single instance + collision-safe file writes** (C7): FlarePurge is now
  single-instance — a second launch redirects activation to the already-open
  instance (brings it to front, even from the tray) and exits, instead of running a
  second process that competes for the state files. Writes to
  `accounts.v1.json` / `zones.v1.json` use a per-process unique temp file (was a
  fixed `.tmp`) with cleanup on failure.
- **Failed selective purge shown as success** (X4): the zone-detail result banner
  now reflects the actual failure instead of always painting success.
- **Purge-history leak** (C5): the history dialog no longer leaks its `ListView`
  (or leaves handlers touching the UI from worker threads) — it unsubscribes when
  closed.
- **Token in memory** (G1): the wizard clears the token from the view-model as soon
  as it is saved to the vault, so it is not retained in memory.
- **Demo mode blocked in Release** (G3): demo-mode activation (fake data for Store
  screenshots) is compiled in Debug only; a Release/Store build can no longer be
  toggled into it via env var, argument or a `demo.flag` file.
- **Orphaned token cleanup** (G2): at startup, FlarePurge removes Credential Vault
  entries that no stored account references (e.g. after a partial corruption of
  `accounts.v1.json`). Safe by design: it **only acts when the accounts file was
  read with certainty**; if the read degrades (antivirus/backup lock) it deletes
  nothing, so live tokens are never lost. Scoped to FlarePurge's own resource,
  never other apps' credentials.
- **More accurate Store claims** (P1/D2): the listing (ES+EN) qualifies "tokens
  don't leave your device" (Windows may sync the Credential Vault if you have
  roaming/MSA sync on), clarifies "zero third-party SDKs" → no analytics/tracking
  SDKs, and discloses the startup beacon to `flarepurge.com/status.json` (kill
  switch, no identifiers, fail-open).

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
