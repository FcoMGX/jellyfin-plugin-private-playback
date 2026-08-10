# Jellyfin 10.11.11 research

Status: source-level research completed for the playback and user-data paths described below. Runtime integration results are recorded separately in `TEST_RESULTS.md` and must not be inferred from this document.

## Reproducible source baseline

| Component | Exact revision | Purpose |
| --- | --- | --- |
| Jellyfin Server | tag `v10.11.11`, commit `1fbd8739292cce610231be93daf43368733edf63` | Primary server implementation and public plugin contracts |
| jellyfin-web | tag `v10.11.11`, commit `35c0793ece3adbd247eab290ae1effab851f3d37` | Client locale selection and watched-state controls |
| Plugin template | commit `7a9dbdafcced0bf6ccf1ca5aa404e404c76d5b04` | Project structure and dashboard-page pattern |
| Webhook plugin | commit `616010ce5e554079f8100bc4a44a89bd54144c93` | Official event-consumer and hosted-service examples |
| Trakt plugin | commit `681bf0e1fee77bfbcac1917752ac058d9a78147d` | Official `UserDataSaved` and session-event consumer |
| Playback Reporting plugin | commit `e89999555f7d2d0d6d8d1cd793a6e36a893cd19d` | Independent playback-history persistence behaviour |

The server tag resolves exactly to the published 10.11.11 release. Its `global.json` requests .NET SDK 9.0 with latest-minor roll-forward, and every relevant server project targets `net9.0`. Official NuGet packages `Jellyfin.Controller` and `Jellyfin.Model` both publish version `10.11.11` for `net9.0`; the plugin pins those exact versions and excludes their runtime assets, as required by the official template guidance.

Primary sources:

- [Jellyfin Server 10.11.11 release](https://github.com/jellyfin/jellyfin/releases/tag/v10.11.11)
- [Jellyfin Server source at the exact commit](https://github.com/jellyfin/jellyfin/tree/1fbd8739292cce610231be93daf43368733edf63)
- [jellyfin-web source at the exact commit](https://github.com/jellyfin/jellyfin-web/tree/35c0793ece3adbd247eab290ae1effab851f3d37)
- [Official plugin template](https://github.com/jellyfin/jellyfin-plugin-template)
- [Official plugin documentation](https://jellyfin.org/docs/general/server/plugins/)
- [Jellyfin.Controller 10.11.11 on NuGet](https://www.nuget.org/packages/Jellyfin.Controller/10.11.11)
- [Jellyfin.Model 10.11.11 on NuGet](https://www.nuget.org/packages/Jellyfin.Model/10.11.11)

## End-to-end playback flow

```text
Client
  -> PlaystateController endpoint
  -> ISessionManager / SessionManager
  -> IUserDataManager / UserDataManager
  -> EF Core transaction against UserData
  -> in-memory UserData cache and BaseItem rehydration
  -> UserDataSaved event
  -> playback event publication
  -> subsequent item, Resume and Next Up queries
```

### API ingress

`Jellyfin.Api/Controllers/PlaystateController.cs` defines the current endpoints:

- `POST /Sessions/Playing` calls `ISessionManager.OnPlaybackStart`.
- `POST /Sessions/Playing/Progress` calls `ISessionManager.OnPlaybackProgress`.
- `POST /Sessions/Playing/Stopped` calls `ISessionManager.OnPlaybackStopped`.
- `POST /UserPlayedItems/{itemId}` marks an item played.
- `DELETE /UserPlayedItems/{itemId}` marks an item unplayed.

Legacy user-qualified routes delegate to the same implementations. `Jellyfin.Api/Controllers/ItemsController.cs` also exposes `POST /UserItems/{itemId}/UserData`, which can update `Played`, `PlaybackPositionTicks`, `PlayCount`, `LastPlayedDate`, favourites and ratings in one DTO. Consequently, protecting only the two dedicated played/unplayed routes would leave a direct API bypass.

### Playback start

`Emby.Server.Implementations/Session/SessionManager.cs`, `OnPlaybackStart(User, BaseItem)`:

1. obtains the mutable `UserItemData` object;
2. increments `PlayCount`;
3. sets `LastPlayedDate` to UTC now;
4. sets `Played` depending on the item's resume capabilities;
5. calls `IUserDataManager.SaveUserData` with reason `PlaybackStart`;
6. only after the save publishes the playback-start events.

This means an event-only plugin is already too late to prevent the start write.

### Progress and completion

`SessionManager.OnPlaybackProgress(User, BaseItem, PlaybackProgressInfo)` calls `IUserDataManager.UpdatePlayState`, optionally updates remembered audio/subtitle stream indexes, and saves with reason `PlaybackProgress` for real client check-ins. Automatically generated in-session progress updates do not save UserData.

`Emby.Server.Implementations/Library/UserDataManager.UpdatePlayState` applies the server's configured minimum/maximum resume thresholds. It writes `PlaybackPositionTicks`, and when the completion threshold is crossed it sets `Played=true` and position zero. Items without runtime or without resume support follow explicit branches in that method.

`SessionManager.OnPlaybackStopped(User, BaseItem, ...)` runs the same play-state update and saves with reason `PlaybackFinished`. If the client supplies no position, Jellyfin assumes completion, increments `PlayCount`, sets `Played` where supported, and clears the position.

### Persistence and event order

`Emby.Server.Implementations/Library/UserDataManager.SaveUserData`:

1. maps every `UserItemData` field to the EF Core `UserData` entity;
2. upserts all user-data keys in a database transaction;
3. calls `SaveChanges` and commits;
4. updates its `FastConcurrentLru` cache;
5. rehydrates `BaseItem.UserData`;
6. invokes `UserDataSaved` synchronously.

The persisted playback-relevant columns in `src/Jellyfin.Database/Jellyfin.Database.Implementations/Entities/UserData.cs` are:

- `PlaybackPositionTicks`;
- `PlayCount`;
- `LastPlayedDate`;
- `Played`.

The same row also stores `IsFavorite`, `Rating`, `AudioStreamIndex` and `SubtitleStreamIndex`. A privacy implementation must therefore preserve the non-playback fields while controlling the playback fields.

There is no pre-save `UserDataSaving` event. `UserDataSaved` is post-commit. Session playback events are also published after the corresponding UserData write. A “save then reset” plugin would introduce observable and crash-sensitive windows and could not reconstruct an earlier `LastPlayedDate` after the cache object had already been mutated.

## Manual watched/unwatched flow

`PlaystateController.UpdatePlayedStatus` calls `BaseItem.MarkPlayed` or `BaseItem.MarkUnplayed`.

`MediaBrowser.Controller/Entities/BaseItem.cs` performs the following:

- `MarkPlayed`: obtains UserData, ensures `PlayCount >= 1`, optionally increments it, clears the resume position, assigns `LastPlayedDate`, sets `Played=true`, then saves with reason `TogglePlayed`.
- `MarkUnplayed`: sets `PlayCount=0`, clears position and last-played date, sets `Played=false`, then saves with reason `TogglePlayed`.

Folder implementations recursively apply the operation to children. The generic `UpdateItemUserData` endpoint reaches the same public `IUserDataManager` abstraction. Intercepting that single abstraction therefore covers current, legacy, folder-recursive and generic UserData routes server-side.

## Derived behaviour

| Feature | Source of truth in 10.11.11 | Privacy implication |
| --- | --- | --- |
| Resume bar / percentage | `PlaybackPositionTicks`; percentage is calculated in `BaseItem.FillUserDataDtoValues` | Preserve the old position to prevent new resume state |
| Continue Watching | `ItemsController` queries `IsResumable=true`, which becomes `PlaybackPositionTicks > 0` in `BaseItemRepository` | Blocking position changes prevents new entries |
| Watched state | `Played` in the per-user `UserData` row | Must be protected for both playback and manual API calls |
| Season/series aggregate state | `Folder.FillUserDataDtoValues` counts child items with `Played=false`; it is derived, not a separate aggregate write | Protecting episode `Played` keeps aggregates coherent |
| Next Up | `TVSeriesManager` queries played episodes, resume position and last-played dates; `BaseItemRepository.GetNextUpSeriesKeys` groups episode `LastPlayedDate` | Maximum-privacy preset must protect watched state, position and history together |
| History ordering | `DatePlayed` and `PlayCount` map to `LastPlayedDate` and `PlayCount` in `OrderMapper` | These fields form one user-facing “play history” option |

`PlayedPercentage` and `UnplayedItemCount` appear in DTOs but are not independent persisted fields in the `UserData` entity. They are not exposed as independent plugin switches.

## Public extension points and selected mechanism

`MediaBrowser.Controller/Plugins/IPluginServiceRegistrator.cs` is the official pre-container service-registration extension point. `Emby.Server.Implementations/ApplicationHost.cs` registers the core `IUserDataManager` as a singleton and then calls `PluginManager.RegisterServices`. The exact 10.11.11 registration order therefore permits a plugin to replace that one descriptor with a decorator while retaining and constructing the original concrete implementation.

The selected decorator:

- is compiled only against public `IUserDataManager` and plugin contracts;
- activates only when the descriptor is the exact expected singleton core implementation for 10.11.11;
- otherwise leaves the descriptor untouched and reports enforcement unavailable;
- returns a copy of `UserItemData` for protected users so core code cannot mutate the original cached baseline;
- immediately before a save, obtains the unchanged baseline and restores each protected playback field;
- delegates one sanitised write only if an allowed field changed;
- forwards the original `UserDataSaved` event, so allowed writes keep normal notification semantics;
- holds no playback history of its own.

This prevents the prohibited values from entering the EF transaction. It is materially safer than post-event restoration and preserves pre-existing state without needing a per-item snapshot database.

## UI extension limitation

The official web-page mechanism (`IHasWebPages`, `PluginPageInfo` and `DashboardController`) exposes dashboard configuration resources. It does not expose a per-user extension hook for normal library/detail/play-state controls.

In jellyfin-web 10.11.11, `src/components/userdatabuttons/userdatabuttons.js` and `src/elements/emby-playstatebutton/emby-playstatebutton.js` create the watched button client-side and call `ApiClient.markPlayed`/`markUnplayed`. No server capability or plugin callback is consulted. A repository-wide search found no supported plugin contract to remove this control per user.

Therefore this plugin does not patch, transform or inject into jellyfin-web. The button may remain visible. Server-side enforcement returns the actual unchanged state, and a later UserData refresh corrects clients that optimistically toggled their local icon.

## Localisation mechanism

jellyfin-web `src/lib/globalize/index.js` obtains its selected language from user settings, normalises it, and writes it to `document.documentElement.lang`. Dashboard plugin pages run inside that document. The plugin reads this official reflected locale value, normalises regional variants, loads one embedded UTF-8 resource, and falls back to `en-GB` for unknown, empty, failed or incomplete resources. Traditional Chinese maps to `zh-TW`, which is the locale present in the 10.11.11 Jellyfin localisation tree.

## Other plugins

- Playback Reporting subscribes directly to `ISessionManager.PlaybackStart`, `PlaybackProgress` and `PlaybackStopped` and writes an independent playback database. It can still record a private user's activity.
- Webhook consumes playback events independently of UserData writes and can transmit those events when configured.
- Trakt subscribes to both session events and `UserDataSaved`; its own remote scrobbling can still occur.

The decorator suppresses `UserDataSaved` only when there is no allowed UserData change to persist. It does not suppress session events, alter another plugin's configuration, or touch another plugin's database. Administrators must exclude private users in those plugins separately.

## Evidence table

| Question | Exact source/symbol | Result | Design implication |
| --- | --- | --- | --- |
| Where is progress updated? | `SessionManager.OnPlaybackProgress`; `UserDataManager.UpdatePlayState` | Position and completion are calculated before `SaveUserData` | Intercept `IUserDataManager`, not playback events |
| What starts a play-history record? | `SessionManager.OnPlaybackStart(User, BaseItem)` | Start increments count and last-played date immediately | Start writes must also be sanitised |
| What endpoint marks played? | `PlaystateController.MarkPlayedItem` and legacy delegate | Calls `BaseItem.MarkPlayed` | `TogglePlayed` must be covered |
| Is there another API bypass? | `ItemsController.UpdateItemUserData` | Generic DTO may set all playback fields | Sanitize the DTO overload too |
| Is `UserDataSaved` pre-save? | `UserDataManager.SaveUserData` | It fires after transaction commit/cache update | Event-only reset rejected |
| What is actually persisted? | EF `UserData` entity | Four playback fields plus favourites, rating and stream preferences | Recompose, do not drop the whole row |
| What drives Continue Watching? | `ItemsController.GetResumeItems`; `BaseItemRepository` `IsResumable` filter | Position greater than zero | Progress option controls this effect |
| What drives Next Up? | `TVSeriesManager`; `GetNextUpSeriesKeys` | Played, position and last-played date | Full private preset protects all three concepts |
| Are season/series counters stored? | `Folder.FillUserDataDtoValues` | Child state is counted on demand | No invented aggregate switch |
| Can plugins register before DI is built? | `ApplicationHost` and `PluginManager.RegisterServices` | Yes, after core services are added | Exact-version decorator is feasible |
| Can a plugin officially hide watched buttons? | `IHasWebPages`/`PluginPageInfo`; jellyfin-web watched-button modules | No supported per-user control extension found | Document visible-button limitation; enforce server-side |
| Do external reporting plugins remain independent? | Playback Reporting, Webhook and Trakt event subscriptions | Yes | Explicit privacy limitation; never tamper with them |

## Scope of the source review

The complete source trees at the revisions above were obtained locally. Searches covered all relevant projects and symbols, followed by call tracing through the specific files named in this document. This was a systematic feature-focused review, not a claim that every line of either repository was read.
