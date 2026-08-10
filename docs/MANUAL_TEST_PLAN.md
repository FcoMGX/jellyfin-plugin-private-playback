# Manual test plan

This plan uses a disposable Jellyfin 10.11.11 instance and synthetic media. Never point it at a production data directory. Record API responses before and after restart; a UI icon alone is not persistence evidence.

## Preparation

1. Install a clean official Jellyfin Server 10.11.11 and confirm `/System/Info/Public` reports `10.11.11`.
2. Stop Jellyfin, extract the verified plugin ZIP into a dedicated versioned plugin directory, then start Jellyfin.
3. In the plugin page, confirm protection is active and server version is `10.11.11.0`.
4. Create `UsuarioNormal` and `Invitado` with access to the same disposable movie and TV library.
5. Use FFmpeg-generated media longer than Jellyfin's minimum resume duration; include a two-episode series.
6. Authenticate each user and retain tokens only in local shell variables. Never paste tokens into a report.
7. Record the movie, episode, season and series IDs from the API.

Example read (replace placeholders locally):

```bash
curl --fail --silent --show-error \
  -H "X-Emby-Token: $INVITADO_TOKEN" \
  "$JELLYFIN_URL/UserItems/$ITEM_ID/UserData" | jq .
```

## Normal-user baseline

1. As `UsuarioNormal`, play the movie past the minimum resume threshold and stop below the watched threshold.
2. Query UserData and confirm a non-zero `PlaybackPositionTicks`, positive `PlayCount`, non-null `LastPlayedDate` and `Played=false`.
3. Query `/UserItems/Resume?UserId=...` and confirm the movie is present.
4. Mark the movie watched through `POST /UserPlayedItems/{itemId}` and confirm `Played=true` and position zero.
5. Mark episode 1 watched and confirm season/series unplayed counts decrease and `/Shows/NextUp` advances to episode 2.

## Fully private user

1. In the plugin page select `Invitado` → **Fully private** and save.
2. Query and record the baseline UserData for the movie and episode 1.
3. Partially play and seek in the movie, send progress, pause/resume if the client supports it, then stop.
4. Query UserData and confirm all four protected fields exactly equal the baseline.
5. Query Continue Watching and confirm it is unchanged from the baseline.
6. Play the movie past Jellyfin's automatic watched threshold and stop. Confirm the fields remain unchanged.
7. Play episode 1 past the watched threshold. Confirm episode `Played=false` when its baseline was false, season/series aggregate counts are unchanged, and Next Up still points to episode 1.

## Manual/API watched enforcement

1. While authenticated as `Invitado`, call `POST /UserPlayedItems/{movieId}`.
2. Immediately query `/UserItems/{movieId}/UserData` from a second invited-user session. Confirm `Played`, position, count and date equal the baseline.
3. Repeat the POST several times in parallel from two sessions and query again.
4. Attempt `DELETE /UserPlayedItems/{movieId}` and the generic `POST /UserItems/{movieId}/UserData` with changed playback fields. Confirm the configured categories remain unchanged.
5. Repeat the same watched POST as `UsuarioNormal`; confirm only that user's state changes.

## Restart and relogin

1. Stop Jellyfin through the normal service manager and wait for process exit.
2. Start Jellyfin and authenticate `Invitado` again.
3. Re-query movie, episode, season, series, Continue Watching and Next Up. Compare them with the private baseline.
4. Log out, log in again and repeat the UserData read.
5. Try marking watched once more, restart again and confirm the state still matches the baseline.

## Configuration lifecycle

1. Rename `Invitado`; confirm the policy remains because it is keyed by ID.
2. Create a new user; reopen the page and confirm it appears dynamically with normal mode.
3. Delete a disposable configured user; reopen the page, confirm the stale entry does not break rendering, select Normal and save to remove it.
4. Back up then deliberately corrupt the disposable plugin XML while Jellyfin is stopped. Start it and confirm protection reports/falls back to normal behaviour rather than modifying all users.
5. Restore the valid XML, restart and confirm protection resumes.

## Explicit cleanup

1. Create known progress, watched, count/date, favourite, rating and stream-choice data before protection.
2. Enable protection and confirm old values remain.
3. Preview cleanup and record the affected count.
4. Clear only after the explicit confirmation prompt.
5. Confirm the four playback fields are cleared while favourite/rating/stream choices remain.
6. Run cleanup again and confirm zero changes.

## Client and media regression

Using each client/media path that matters to the deployment, verify login, browse, Direct Play, Direct Stream, transcode, pause, seek, resume within the current session, audio/subtitle changes and playback stop. Verify server UserData separately. Record clients and versions actually tested; do not generalize from this plan.

## Other plugins

In a disposable clone, install the exact intended versions of Playback Reporting, Webhook and/or Trakt. Confirm no DI/startup errors and recognize that their independent session stores or remote scrobbles can still record `Invitado`. Do not delete their databases to manufacture a pass.

## Uninstall

1. Stop Jellyfin and move the plugin binary directory outside the plugin path.
2. Start Jellyfin and confirm the plugin no longer loads.
3. Verify login, browsing, normal UserData writes and both users' remaining fields.
4. Confirm no automatic data deletion occurred. Configuration XML may be archived separately while stopped.
