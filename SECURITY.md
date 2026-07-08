# Security Policy

FlarePurge handles Cloudflare API tokens, so we take security seriously. Thank you
for helping keep users safe.

## Supported versions

Security fixes are provided for the **latest released version** of FlarePurge for
Windows. Older versions are not patched — please update to the current release
available from [flarepurge.com](https://flarepurge.com).

## Reporting a vulnerability

**Please do not open a public issue for security vulnerabilities.**

Report privately through either channel:

- **GitHub Security Advisories** — go to the **Security** tab of this repository and
  choose **"Report a vulnerability"** (private vulnerability reporting). This is the
  preferred channel.
- **Website contact** — use the contact channel at
  [flarepurge.com](https://flarepurge.com) if you cannot use GitHub.

Please include: a description of the issue, steps to reproduce, the affected version
(Settings → About), your Windows version, and any relevant logs — **with all
Cloudflare API tokens redacted.**

We aim to acknowledge reports within a few business days and to coordinate a fix and
disclosure timeline with you.

## Scope

The most sensitive areas of the app, and the ones we care most about, are:

- **Token storage** — API tokens are kept in the Windows Credential Locker
  (`PasswordVault`, backed by DPAPI). Reports of tokens leaking to disk, logs,
  crash dumps, telemetry or the clipboard are high priority.
- **Certificate pinning** — the app pins the SPKI SHA-256 of `api.cloudflare.com`.
  Reports of pin bypass or downgrade are in scope.
- **Network egress** — by design the app only contacts `api.cloudflare.com` and
  `flarepurge.com/status.json`. Any unexpected outbound connection is a valid report.
- **Privilege / packaging** — MSIX manifest capabilities, `runFullTrust` usage.

## Our security guarantees

- **No analytics, no tracking, no third-party telemetry SDKs.** Ever.
- API tokens are never written to disk in plain text and are never transmitted
  anywhere except Cloudflare's own API.
- Use a **scoped Cloudflare API token** (`Zone:Read` + `Zone Cache Purge`) — never
  your Global API Key. FlarePurge never asks for it.

## A note on how fixes ship

FlarePurge is developed in a private core repository and mirrored here as the public,
MIT-licensed edition. Accepted security fixes are applied to the core and then
published to this repository as part of the next release. This lets us patch every
platform (Windows, and the upcoming macOS / iOS / Android editions) consistently.
