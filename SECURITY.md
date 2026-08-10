# Security policy

## Supported version

Security fixes are prepared only for the current `0.9.x` beta line targeting Jellyfin Server 10.11.11. No compatibility is implied for another Jellyfin ABI.

## Reporting a vulnerability

Do not include access tokens, passwords, private URLs, media names or real user data in a public issue. If this project is hosted on a forge with private security advisories, use that facility. Otherwise contact the distributor privately and provide a minimal reproduction against synthetic data.

Useful reports identify the plugin version, exact Jellyfin version, affected endpoint or call path, expected/actual persistent UserData and whether another plugin is installed. Redact authentication headers and server identifiers.

## Security boundaries

- Only Jellyfin's elevated-administrator policy can access plugin status and cleanup endpoints. Built-in plugin-configuration routes retain Jellyfin's own administrator checks.
- All enforcement is keyed by the authenticated `User` object supplied by Jellyfin; a policy is never global.
- The plugin opens no sockets, makes no outbound requests, executes no commands and stores no playback-history database.
- User names are display-only and inserted with DOM `textContent`.
- Corrupt, oversized, duplicate, empty-ID or future-schema configuration results in an empty policy registry and normal Jellyfin behaviour.
- Security/authentication logs remain untouched.

An inactive status is not a privacy guarantee. Administrators must verify active enforcement after installation or upgrade.
