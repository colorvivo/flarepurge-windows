# Contributing to FlarePurge for Windows

Thanks for your interest in improving FlarePurge! This document covers how to build, what we accept, and how to submit changes.

## How this repository works

FlarePurge is developed in a **private core repository** and published here as the
public, MIT-licensed edition. That keeps the app consistent across every platform
(Windows today; macOS / iOS / Android next) and lets us ship coordinated releases.

What this means for you as a contributor:

- **Issues are very welcome here** — bug reports, feature ideas (within scope) and
  translation fixes. This is the right place to report anything.
- **Pull requests are welcome too.** When we accept one, the change is applied to the
  core and then published back here. In some cases we may re-implement your change in
  the core to keep the platforms aligned — you will always be credited, and the PR
  thread will explain what happened.
- The public repository is a faithful mirror of the core: **it may be updated with a
  squashed "sync" commit** rather than merging your branch directly. This is normal
  and does not mean your contribution was ignored.

## Scope — one job, done well

FlarePurge exists to **purge Cloudflare cache** and nothing else. Please keep this in mind before opening a feature request or PR:

- ✅ In scope: anything that makes cache purging faster, clearer, safer, or more accessible; bug fixes; translations; tests; performance.
- ❌ Out of scope: DNS management, WAF rules, analytics dashboards, page rules, or any other Cloudflare feature. Those belong in different tools.

PRs that broaden the app beyond cache purging will be declined regardless of quality — it's a product decision, not a comment on your work.

## Development setup

You need **Windows 10/11** with:

- **.NET 10 SDK** (pinned in `global.json`)
- **Visual Studio 2022** with the **Windows App SDK / WinUI** workload (for the `FlarePurge.App` project)

```powershell
git clone https://github.com/colorvivo/flarepurge-windows.git
cd flarepurge-windows

dotnet restore
dotnet build src/FlarePurge.Core/FlarePurge.Core.csproj
dotnet test  src/FlarePurge.Tests/FlarePurge.Tests.csproj
```

The `FlarePurge.Core` project and its tests build with the plain `dotnet` CLI on any platform. The `FlarePurge.App` (WinUI 3) project requires Windows + `msbuild`.

## Coding conventions

- **C# 13**, nullable enabled, `TreatWarningsAsErrors`. Keep the build warning-clean.
- Formatting and naming rules live in `.editorconfig` — respect them.
- MVVM via **CommunityToolkit.Mvvm** (`ObservableObject`, `RelayCommand`). No code-behind business logic.
- Networking through the existing `HttpClient` + certificate-pinning path. Don't add new HTTP stacks.
- **No analytics, no tracking, no third-party telemetry SDKs.** This is a hard rule.
- Add or update tests (`xUnit` + `FluentAssertions`) for any behavior change. Keep the suite green.

## Translations

The app ships 21 languages as `.resw` resource files under `src/FlarePurge.App/Strings/<locale>/`. Native-speaker corrections and completing the locales still marked `<!-- TODO: translate -->` are very welcome — one locale per PR keeps review easy.

## Submitting a PR

1. Fork and branch from `main`.
2. Keep changes focused; one concern per PR.
3. Ensure `dotnet test` passes and the build is warning-free.
4. Describe the *why*, not just the *what*, in the PR body.

## Reporting bugs

Open an issue with: what you did, what you expected, what happened, your Windows version, and the app version (Settings → About). Never paste a real Cloudflare API token into an issue.

## Security

Found a security issue? Please **do not** open a public issue. Email the maintainer privately instead. API tokens, cert pinning, and credential storage are the sensitive areas.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
