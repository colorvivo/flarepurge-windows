# Changelog

Historial de cambios de FlarePurge para Windows. Formato basado en [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/); versiones siguen [SemVer](https://semver.org/lang/es/).

## [1.0.2] — 2026-04-26

> Esta release contiene el trabajo que se preparó como 1.0.1 en local pero nunca se subió al Store. Se publica como 1.0.2 directamente para evitar colisiones de versión con el catálogo de Partner Center.


### Añadido
- **Cache de listado de zonas** por cuenta. La lista aparece al instante al abrir la app o cambiar de cuenta; se refresca en segundo plano silencioso. Persiste en `%LOCALAPPDATA%\FlarePurge\zones.v1.json` (sólo metadatos públicos — ningún token).
- **Recarga manual** de zonas: botón "Recargar" en la barra de estado + atajo **Ctrl+R**. Fuerza un fetch nuevo a `api.cloudflare.com`.
- **Indicador "Zonas actualizadas hace X"** en la barra de estado. Se refresca cada 30 s.
- **Etiqueta opcional al añadir una API key**. Útil para distinguir cuentas si guardas varias (por ejemplo "Cliente X" vs. "Personal"). Si se deja en blanco se usa el nombre devuelto por Cloudflare.
- **Renombrar etiqueta** de una cuenta guardada desde Ajustes → Cuentas API. Icono lápiz en cada fila.

### Traducciones
- **Infra i18n totalmente cableada**: la UI ahora consume las 21 traducciones del paquete en vez de mostrar cadenas hardcoded. Antes la infraestructura existía (21 idiomas × ~220 keys) pero ninguna vista la usaba — por eso el wizard aparecía en inglés y el selector de idioma no tenía efecto.
- **Wizard de token, About, Ajustes, Zonas, Detalle de zona, diálogos modales (purga, confirmación, selectiva, historial), menú de la bandeja y ventanas de resultado** pasan por el `ResourceLoader`. Cambiar el idioma en Ajustes ahora surte efecto al reiniciar la app.
- **Fugas en inglés corregidas** (botón "← Back", "Paste token", "Verify and save token", "Token saved.", overlines "BUILT WITH / BACKEND / TRACKING / TOKEN STORAGE / LANGUAGES / PRICE" de la ventana Acerca de, menú "Open FlarePurge / Quit" del tray, etc.).
- **116 keys nuevas** añadidas al catálogo (`action_back`, `zones_updatedRelative`, `time_*Ago`, `editLabel_title`, `settings_section_security/window`, `purgeConfirm_*`, `selective_*`, `history_*`, `bulk_*`, `tray_*`, etc.), con traducciones completas para EN/ES/ES-MX/CA/FR/IT/DE/PT-PT (+ NL/NB/SV en tiempo y configuración). 13 locales restantes (AR/EL/HE/HU/JA/KO/PL/RO/TH/ZH-Hans/...) caen al inglés con marca `<!-- TODO: translate -->` hasta que haya traducción nativa.

### Paridad con Mac
- **Anti re-render en `ApplyZones`**: el silent refresh ya no recrea los items del ListView si el payload entrante coincide con el actual (compara por Id / Name / Status / Plan / AccountName / NameServers / CreatedOn). Preserva selección y scroll entre refrescos. Copia directa del comportamiento de `ZoneListViewModel.load()` en SwiftUI.
- **Política de error silencioso alineada con Apple**: el silent refresh en background ya no surface-a errores 401/403 como banner de reauth. Sólo el Ctrl+R manual reporta fallos al usuario. Evita quitar el foco mientras trabajas.
- **`IAccountStore.RenameAccount(id, newLabel)` centralizado**: la vista de Ajustes ya no muta la lista ad-hoc, delega en el store (trim + no-op en sin cambios + guard de id desconocido). Paridad con `AppState.renameAccount` en SwiftUI.

### Corregido
- Crash al editar la etiqueta de una cuenta en Ajustes (*"Only a single ContentDialog can be open at any time"*). El renombrado ahora usa un Flyout anclado al botón en lugar de un diálogo anidado.

### Interno
- Se borra el cache de zonas de una cuenta al cerrar sesión o eliminarla.
- Helper `L.S(key)` / `L.Format(key, args…)` para consumo desde XAML (`{x:Bind loc:L.S('key'), Mode=OneTime}`) y code-behind.
- 14 tests nuevos totales (8 para `JsonZoneCacheStore`, 6 para `IAccountStore.RenameAccount`). **Suite: 205/205** en verde, 0 warnings.

---

## [1.0.0] — 2026-04-24

Primera release para Microsoft Store. Paridad funcional 1:1 con FlarePurge Mac v1.7.x.

### Añadido
- **Listar zonas** de Cloudflare con meta (plan, cuenta, nameservers, fecha).
- **Purgar todo** el caché de una zona en un clic.
- **Purga selectiva** por URLs (hasta 30 por lote, troceo automático) o por hosts.
- **Multi-cuenta**: añadir varias API keys, cambiar entre ellas, agrupación automática cuando hay zonas de varias cuentas Cloudflare bajo el mismo token.
- **Favoritos** por zona con purga masiva ("Purgar N zonas favoritas") y "Purgar todas las zonas de la cuenta X".
- **Historial de sesión** con timestamp, resultado y purge ID.
- **System tray dinámico**: menú con cuentas + favoritos + "Purgar todas".
- **Minimizar a la bandeja** al cerrar (opcional).
- **Ajustes**: tema (Auto/Claro/Oscuro, aplica en vivo), idioma (21 locales), confirmación antes de purgar.
- **Kill switch remoto** (`flarepurge.com/status.json`) para desactivar la app en caso de incidencia.
- **Atajos de teclado**: Ctrl+R (recargar), Ctrl+F (buscar), Ctrl+, (ajustes), Ctrl+1..9 (saltar a favorita N), Ctrl+Shift+P (purga rápida), Esc (atrás).
- **Accesibilidad baseline**: AutomationProperties, contrastes, targets táctiles ≥32 px.

### Seguridad
- Tokens guardados en **Windows Credential Vault** (DPAPI).
- **Cert pinning SPKI SHA-256** contra `api.cloudflare.com` (3 hashes GTS WE1 + WR1 + Root R4).
- **Sin analytics, sin tracking, sin SDKs de terceros**. Las únicas peticiones salientes van a `api.cloudflare.com` y a `flarepurge.com/status.json`.
- Scopes de token mínimos: `Zone:Read` + `Zone Cache Purge` (+ `User:Read` para validación inicial).

### Stack
- WinUI 3 · Windows App SDK 1.6 · C# 13 · .NET 10.
- Target Windows 10 21H2+ / Windows 11.
- 191/191 tests, 0 warnings con `TreatWarningsAsErrors=true`.
