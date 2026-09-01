# VelocityGrid 1.0 release checklist

## Automated gates

- Build Debug and Release for x64 with no errors.
- Run native and managed tests through Visual Studio Test Explorer.
- Confirm the packaged sample starts without a lost initial provider request.
- Run the scrolling, cache-revisit, GC-stress, and market-stream scenarios.
- Check bounded cache and working-set metrics against `performance.md`.

## Accessibility gates

- Navigate every supported command using only the keyboard.
- Verify the grid name, read-only help text, selection announcements, and provider-error announcement with Narrator.
- Repeat with a descriptive host-supplied `AutomationProperties.Name`.
- Test Windows contrast themes as well as WinUI light and dark themes.
- Confirm that positive/negative meaning remains available through icons or text when provider colours are suppressed.

## Reliability gates

- Resize, minimize/restore, close during requests, and repeatedly open/close the sample under a debugger.
- Exercise display/DPI changes and graphics-device recreation where test hardware permits.
- Inject provider cancellation, timeout, malformed-page, and exception cases; verify `DataError` and recovery UI.
- Soak the market stream while scrolling for at least one hour and check memory, stale requests, and update latency.

## Packaging and API gates

- Freeze the public managed and WinRT API names for 1.0.
- Choose final publisher and package identities; the sample currently retains a development identity.
- Produce and validate x64 and ARM64 packages on clean supported machines.
- Confirm MIT licence, notices, symbols/source indexing, version, and release notes.
- Validate sample permissions; it currently requests only `runFullTrust`.
