# Security audit notes

## Attack surface

The plugin adds one authenticated dashboard page, ordinary plugin configuration, one elevated read endpoint and one elevated destructive endpoint. It adds no listener, external request, uploaded file, command execution, dynamic assembly load, raw SQL or custom credential store.

## Review results

| Concern | Review/control | Result |
| --- | --- | --- |
| Authorization | Controller class uses Jellyfin `RequiresElevation`; integration normal user received 403 | Verified |
| IDOR | Elevated caller only; route ID resolves through `IUserManager`; missing user returns 404; operation remains one user | Verified by unit/integration |
| CSRF | Dashboard writes through authenticated Jellyfin `ApiClient`; no anonymous/cookie-only custom endpoint | No independent bypass identified |
| XSS | User/config strings assigned with `textContent`; no `innerHTML`, `eval` or generated untrusted markup | Verified static tests |
| Injection/traversal/SSRF | Fixed routes/resources; GUID route constraint; no user-controlled path, command, SQL or outbound URL | No applicable sink identified |
| Deserialization | Jellyfin XML serializer into fixed configuration type; strict schema/entry validation; failure publishes normal policies | Verified invalid/corrupt cases |
| DoS | 2,048-entry and 256-character display-name limits; fixed 512 locks; no per-event I/O beyond core | Controls present |
| Race conditions | Pre-transaction baseline/filter under `(user,item)` stripe; maintenance shares stripe; concurrent API/runtime tests | Verified within covered cases |
| Secrets/logging | No tokens/passwords/item names/positions logged by plugin; focused repository scan passed | Verified within scan scope |
| Security logging | Decorator affects only UserData; authentication/activity/security paths untouched | Verified architectural boundary and real login |
| Fail-safe | ABI/config/baseline failures retain or delegate normal Jellyfin behaviour | Verified guard/corruption; baseline-null branch unit-covered indirectly by build only |
| Supply chain | Exact locks, no bundled dependency, official CLI vulnerability query returned none | Verified at execution-time snapshot |

## Residual risks

- A future Jellyfin ABI can change private implementation behaviour; exact activation guard prevents silent compatibility but creates availability-of-privacy dependence on the status check.
- Another plugin can replace the same service. Private Playback refuses ambiguous decoration rather than guessing order.
- Reporting/scrobbling plugins and Jellyfin technical logs can contain activity outside core UserData.
- A client may render an optimistic watched icon until refreshing server state.
- SonarQube Hotspots were not server-reviewed in this environment.
- The custom secret scan does not inspect Git history and is less comprehensive than a dedicated secret-scanning service.

No critical/blocker issue was found by the executed local review, but that statement is limited to these checks and is not a Sonar Quality Gate or a guarantee of absence of vulnerabilities.
