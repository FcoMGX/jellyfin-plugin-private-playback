#!/usr/bin/env bash
set -euo pipefail

project_root=$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)
jellyfin_bin_dir=${JELLYFIN_BIN_DIR:?Set JELLYFIN_BIN_DIR to the extracted Jellyfin binary directory.}
if [[ -n "${FFMPEG_PATH:-}" ]]; then
    ffmpeg_path="$FFMPEG_PATH"
else
    ffmpeg_path=$(command -v ffmpeg || true)
fi
[[ -n "$ffmpeg_path" && -x "$ffmpeg_path" ]] || {
    printf 'FFmpeg was not found. Install FFmpeg or set FFMPEG_PATH.\n' >&2
    exit 127
}
plugin_zip=${PRIVATE_PLAYBACK_PLUGIN_ZIP:-}
test_root=${PRIVATE_PLAYBACK_TEST_ROOT:-$(mktemp -d)}
base_url=http://127.0.0.1:8096
plugin_id=bb23ffd1-026a-4598-8133-e77ae50ccad7
server_pid=
run_number=0

cleanup() {
    if [[ -n "$server_pid" ]] && kill -0 "$server_pid" 2>/dev/null; then
        kill -TERM "$server_pid" 2>/dev/null || true
        for _ in $(seq 1 50); do
            kill -0 "$server_pid" 2>/dev/null || break
            sleep 0.1
        done
        kill -KILL "$server_pid" 2>/dev/null || true
        wait "$server_pid" 2>/dev/null || true
    fi
}
trap cleanup EXIT

fail() {
    printf 'FAIL: %s\n' "$1" >&2
    exit 1
}

pass() {
    printf 'PASS: %s\n' "$1"
}

api() {
    local method=$1
    local path=$2
    local token=${3:-}
    local data=${4:-}
    local args=(-fsS --noproxy '*' --connect-timeout 3 --max-time 30 -X "$method" -H 'Accept: application/json')
    if [[ -n "$token" ]]; then
        args+=(-H "X-Emby-Token: $token")
    fi
    if [[ -n "$data" ]]; then
        args+=(-H 'Content-Type: application/json' --data "$data")
    fi
    curl "${args[@]}" "$base_url$path"
}

auth() {
    local username=$1
    local password=$2
    local device_id=$3
    curl -fsS --noproxy '*' --connect-timeout 3 --max-time 30 \
        -H 'Content-Type: application/json' \
        -H "Authorization: MediaBrowser Client=\"PrivatePlaybackTests\", Device=\"Integration\", DeviceId=\"$device_id\", Version=\"1.0.0\"" \
        --data "{\"Username\":\"$username\",\"Pw\":\"$password\"}" \
        "$base_url/Users/AuthenticateByName"
}

start_server() {
    run_number=$((run_number + 1))
    local console_log="$test_root/server-console-$run_number.log"
    HTTP_PROXY=http://127.0.0.1:9 \
    HTTPS_PROXY=http://127.0.0.1:9 \
    ALL_PROXY=http://127.0.0.1:9 \
    http_proxy=http://127.0.0.1:9 \
    https_proxy=http://127.0.0.1:9 \
    all_proxy=http://127.0.0.1:9 \
    NO_PROXY=localhost,127.0.0.1,::1 \
    no_proxy=localhost,127.0.0.1,::1 \
    "$jellyfin_bin_dir/jellyfin" \
        -d "$test_root/data" \
        -c "$test_root/config" \
        -C "$test_root/cache" \
        -l "$test_root/log" \
        -w "$jellyfin_bin_dir/jellyfin-web" \
        --ffmpeg "$ffmpeg_path" \
        --nonetchange \
        >"$console_log" 2>&1 &
    server_pid=$!

    for _ in $(seq 1 90); do
        if grep -q 'Startup complete' "$console_log" \
            && curl -fsS --noproxy '*' --connect-timeout 1 --max-time 2 "$base_url/System/Info/Public" >/dev/null 2>&1; then
            return
        fi
        if ! kill -0 "$server_pid" 2>/dev/null; then
            tail -80 "$console_log" >&2
            fail "Jellyfin exited during startup"
        fi
        sleep 1
    done
    fail "Jellyfin did not become ready"
}

stop_server() {
    kill -TERM "$server_pid"
    for _ in $(seq 1 100); do
        kill -0 "$server_pid" 2>/dev/null || break
        sleep 0.1
    done
    if kill -0 "$server_pid" 2>/dev/null; then
        kill -KILL "$server_pid"
    fi
    wait "$server_pid" || true
    server_pid=
}

assert_private_baseline() {
    local json=$1
    jq -e \
        '(.PlaybackPositionTicks // .playbackPositionTicks) == 20000000
         and (.Played // .played // false) == false
         and (.PlayCount // .playCount) == 2
         and (.LastPlayedDate // .lastPlayedDate) == "2026-01-01T00:00:00.0000000Z"
         and (.IsFavorite // .isFavorite) == true
         and (.Rating // .rating) == 9' \
        <<<"$json" >/dev/null || fail "protected playback baseline changed"
}

wait_for_next_up() {
    local user_id=$1
    local token=$2
    local series=$3
    local expected_episode=$4
    local next_up
    local next_up_id
    for _ in $(seq 1 60); do
        next_up=$(api GET "/Shows/NextUp?UserId=$user_id&SeriesId=$series&Limit=5" "$token")
        next_up_id=$(jq -r '(.Items // .items)[0].Id // (.Items // .items)[0].id // empty' <<<"$next_up")
        [[ "$next_up_id" == "$expected_episode" ]] && return 0
        sleep 0.5
    done

    return 1
}

plugin_dll="$project_root/src/Jellyfin.Plugin.PrivatePlayback/bin/Release/net9.0/Jellyfin.Plugin.PrivatePlayback.dll"
[[ -f "$plugin_dll" ]] || fail "build the Release plugin before running integration tests"
[[ -x "$jellyfin_bin_dir/jellyfin" ]] || fail "Jellyfin executable was not found"
version_output=$("$jellyfin_bin_dir/jellyfin" --version 2>&1 || true)
[[ "$version_output" == *"10.11.11.0"* ]] || fail "Jellyfin must be exactly 10.11.11.0"

plugin_dir="$test_root/data/plugins/Private Playback_0.9.0.0"
media_dir="$test_root/media"
episode_dir="$test_root/television/Integration Show/Season 01"
mkdir -p "$plugin_dir" "$media_dir" "$episode_dir" "$test_root/config" "$test_root/cache" "$test_root/log"
if [[ -n "$plugin_zip" ]]; then
    [[ -f "$plugin_zip" ]] || fail "the requested installable ZIP does not exist"
    unzip -q "$plugin_zip" -d "$plugin_dir"
else
    cp "$plugin_dll" "$plugin_dir/"
    cp "$project_root/packaging/meta.json" "$plugin_dir/"
fi
[[ -f "$plugin_dir/Jellyfin.Plugin.PrivatePlayback.dll" ]] || fail "the staged package has no plugin assembly"
[[ -f "$plugin_dir/meta.json" ]] || fail "the staged package has no plugin manifest"
"$ffmpeg_path" -hide_banner -loglevel error -y \
    -f lavfi -i color=c=navy:size=320x180:rate=2 \
    -f lavfi -i sine=frequency=880:sample_rate=8000 \
    -t 360 -c:v libx264 -preset ultrafast -pix_fmt yuv420p -c:a aac -shortest \
    "$media_dir/private-playback-test.mp4"
cp "$media_dir/private-playback-test.mp4" "$episode_dir/Integration Show - S01E01.mp4"
cp "$media_dir/private-playback-test.mp4" "$episode_dir/Integration Show - S01E02.mp4"

start_server
public_info=$(api GET '/System/Info/Public')
jq -e '(.Version // .version) == "10.11.11"' <<<"$public_info" >/dev/null || fail "unexpected server version"

api GET '/Startup/User' >/dev/null
api POST '/Startup/User' '' '{"Name":"IntegrationAdmin","Password":"admin-pass"}' >/dev/null
api POST '/Startup/Configuration' '' '{"ServerName":"Private Playback Integration","UICulture":"en-GB","MetadataCountryCode":"GB","PreferredMetadataLanguage":"en"}' >/dev/null
api POST '/Startup/RemoteAccess' '' '{"EnableRemoteAccess":false,"EnableAutomaticPortMapping":false}' >/dev/null
api POST '/Startup/Complete' >/dev/null

admin_auth=$(auth IntegrationAdmin admin-pass admin-device)
admin_token=$(jq -er '.AccessToken // .accessToken' <<<"$admin_auth")
admin_id=$(jq -er '.User.Id // .user.id' <<<"$admin_auth")
private_user=$(api POST '/Users/New' "$admin_token" '{"Name":"PrivateUser","Password":"private-pass"}')
normal_user=$(api POST '/Users/New' "$admin_token" '{"Name":"NormalUser","Password":"normal-pass"}')
private_id=$(jq -er '.Id // .id' <<<"$private_user")
normal_id=$(jq -er '.Id // .id' <<<"$normal_user")

encoded_media=$(jq -rn --arg value "$media_dir" '$value | @uri')
encoded_television=$(jq -rn --arg value "$test_root/television" '$value | @uri')
api POST "/Library/VirtualFolders?name=Integration&collectionType=movies&paths=$encoded_media&refreshLibrary=true" "$admin_token" '{}' >/dev/null
api POST "/Library/VirtualFolders?name=IntegrationTV&collectionType=tvshows&paths=$encoded_television&refreshLibrary=true" "$admin_token" '{}' >/dev/null
item_id=
episode_one_id=
episode_two_id=
season_id=
series_id=
for _ in $(seq 1 90); do
    items=$(api GET "/Items?UserId=$admin_id&Recursive=true&IncludeItemTypes=Movie&Fields=Path" "$admin_token")
    item_id=$(jq -r '(.Items // .items)[0].Id // (.Items // .items)[0].id // empty' <<<"$items")
    episodes=$(api GET "/Items?UserId=$admin_id&Recursive=true&IncludeItemTypes=Episode&SortBy=IndexNumber&SortOrder=Ascending" "$admin_token")
    episode_one_id=$(jq -r 'first((.Items // .items)[] | select((.IndexNumber // .indexNumber) == 1) | (.Id // .id)) // empty' <<<"$episodes")
    episode_two_id=$(jq -r 'first((.Items // .items)[] | select((.IndexNumber // .indexNumber) == 2) | (.Id // .id)) // empty' <<<"$episodes")
    season_id=$(jq -r 'first((.Items // .items)[] | select((.IndexNumber // .indexNumber) == 1) | (.SeasonId // .seasonId)) // empty' <<<"$episodes")
    series_id=$(jq -r 'first((.Items // .items)[] | select((.IndexNumber // .indexNumber) == 1) | (.SeriesId // .seriesId)) // empty' <<<"$episodes")
    [[ -n "$item_id" && -n "$episode_one_id" && -n "$episode_two_id" && -n "$season_id" && -n "$series_id" ]] && break
    sleep 1
done
[[ -n "$item_id" ]] || fail "synthetic movie was not imported"
[[ -n "$episode_two_id" && -n "$season_id" && -n "$series_id" ]] || fail "synthetic television series was not imported"
pass "Jellyfin 10.11.11 loaded the plugin package and synthetic movie/episode media"

private_auth_a=$(auth PrivateUser private-pass private-device-a)
private_auth_b=$(auth PrivateUser private-pass private-device-b)
normal_auth=$(auth NormalUser normal-pass normal-device)
private_token_a=$(jq -er '.AccessToken // .accessToken' <<<"$private_auth_a")
private_token_b=$(jq -er '.AccessToken // .accessToken' <<<"$private_auth_b")
normal_token=$(jq -er '.AccessToken // .accessToken' <<<"$normal_auth")

api POST "/UserItems/$item_id/UserData" "$private_token_a" \
    '{"PlaybackPositionTicks":20000000,"Played":false,"PlayCount":2,"LastPlayedDate":"2026-01-01T00:00:00Z","IsFavorite":false,"Rating":5}' >/dev/null

plugin_configuration=$(jq -cn \
    --arg private_id "$private_id" \
    --arg normal_id "$normal_id" \
    '{SchemaVersion:1,UserPolicies:[
      {UserId:$private_id,LastKnownName:"PrivateUser",Mode:1,RememberProgress:false,RememberWatched:false,RecordHistory:false},
      {UserId:$normal_id,LastKnownName:"NormalUser",Mode:0,RememberProgress:true,RememberWatched:true,RecordHistory:true}
    ]}')
api POST "/Plugins/$plugin_id/Configuration" "$admin_token" "$plugin_configuration" >/dev/null
status=$(api GET '/PrivatePlayback/Status' "$admin_token")
jq -e '(.IsActive // .isActive) == true and (.ServerVersion // .serverVersion) == "10.11.11.0"' <<<"$status" >/dev/null \
    || fail "exact-version enforcement is not active"

non_admin_status=$(curl -sS --noproxy '*' --connect-timeout 3 --max-time 30 -o /dev/null -w '%{http_code}' -H "X-Emby-Token: $private_token_a" "$base_url/PrivatePlayback/Status")
[[ "$non_admin_status" == "403" ]] || fail "administrative endpoint accepted a normal user"
page=$(api GET '/web/ConfigurationPage?name=Private%20Playback' "$admin_token")
grep -q 'privatePlaybackConfigurationPage' <<<"$page" || fail "configuration page was not served"
pass "administrative API and configuration page are protected and available"

direct_result=$(api POST "/UserItems/$item_id/UserData" "$private_token_a" \
    '{"PlaybackPositionTicks":80000000,"Played":true,"PlayCount":9,"LastPlayedDate":"2026-02-02T00:00:00Z","IsFavorite":true,"Rating":9}')
assert_private_baseline "$direct_result"
manual_result=$(api POST "/UserPlayedItems/$item_id" "$private_token_a")
assert_private_baseline "$manual_result"
manual_pids=()
for index in $(seq 1 8); do
    if (( index % 2 == 0 )); then
        api POST "/UserPlayedItems/$item_id" "$private_token_a" >/dev/null &
    else
        api POST "/UserPlayedItems/$item_id" "$private_token_b" >/dev/null &
    fi
    manual_pids+=("$!")
done
for manual_pid in "${manual_pids[@]}"; do
    wait "$manual_pid"
done
private_after_manual_race=$(api GET "/UserItems/$item_id/UserData" "$private_token_b")
assert_private_baseline "$private_after_manual_race"
private_resume_before=$(api GET "/UserItems/Resume?UserId=$private_id&Limit=20" "$private_token_a")
private_resume_before_ids=$(jq -c '(.Items // .items) | map(.Id // .id) | sort' <<<"$private_resume_before")
pass "direct, repeated and concurrent manual watched changes are filtered server-side"

start_payload_a=$(jq -cn --arg item "$item_id" '{ItemId:$item,PositionTicks:20000000,PlayMethod:"DirectPlay",PlaySessionId:"private-a",CanSeek:true}')
start_payload_b=$(jq -cn --arg item "$item_id" '{ItemId:$item,PositionTicks:30000000,PlayMethod:"DirectPlay",PlaySessionId:"private-b",CanSeek:true}')
api POST '/Sessions/Playing' "$private_token_a" "$start_payload_a" >/dev/null
api POST '/Sessions/Playing' "$private_token_b" "$start_payload_b" >/dev/null
api POST '/Sessions/Playing/Progress' "$private_token_a" "${start_payload_a/20000000/90000000}" >/dev/null &
progress_a=$!
api POST '/Sessions/Playing/Progress' "$private_token_b" "${start_payload_b/30000000/100000000}" >/dev/null &
progress_b=$!
wait "$progress_a"
wait "$progress_b"
stop_payload_a=$(jq -cn --arg item "$item_id" '{ItemId:$item,PositionTicks:3500000000,PlaySessionId:"private-a"}')
stop_payload_b=$(jq -cn --arg item "$item_id" '{ItemId:$item,PositionTicks:115000000,PlaySessionId:"private-b"}')
api POST '/Sessions/Playing/Stopped' "$private_token_a" "$stop_payload_a" >/dev/null
api POST '/Sessions/Playing/Stopped' "$private_token_b" "$stop_payload_b" >/dev/null
private_after_sessions=$(api GET "/UserItems/$item_id/UserData" "$private_token_a")
assert_private_baseline "$private_after_sessions"
private_resume_after=$(api GET "/UserItems/Resume?UserId=$private_id&Limit=20" "$private_token_b")
private_resume_after_ids=$(jq -c '(.Items // .items) | map(.Id // .id) | sort' <<<"$private_resume_after")
[[ "$private_resume_after_ids" == "$private_resume_before_ids" ]] || fail "private playback changed Continue Watching"
pass "simultaneous partial/completed private sessions leave playback state and Continue Watching unchanged"

wait_for_next_up "$private_id" "$private_token_a" "$series_id" "$episode_one_id" \
    || fail "unexpected initial private Next Up episode"
episode_start=$(jq -cn --arg item "$episode_one_id" '{ItemId:$item,PositionTicks:0,PlayMethod:"DirectPlay",PlaySessionId:"private-episode",CanSeek:true}')
episode_stop=$(jq -cn --arg item "$episode_one_id" '{ItemId:$item,PositionTicks:3500000000,PlaySessionId:"private-episode"}')
api POST '/Sessions/Playing' "$private_token_a" "$episode_start" >/dev/null
api POST '/Sessions/Playing/Stopped' "$private_token_a" "$episode_stop" >/dev/null
private_episode_data=$(api GET "/UserItems/$episode_one_id/UserData" "$private_token_b")
jq -e \
    '(.PlaybackPositionTicks // .playbackPositionTicks) == 0
     and (.Played // .played // false) == false
     and (.PlayCount // .playCount) == 0
     and (.LastPlayedDate // .lastPlayedDate) == null' \
    <<<"$private_episode_data" >/dev/null || fail "completed private episode changed playback state"
wait_for_next_up "$private_id" "$private_token_b" "$series_id" "$episode_one_id" \
    || fail "private episode playback changed Next Up"
private_season=$(api GET "/Users/$private_id/Items/$season_id" "$private_token_a")
private_series=$(api GET "/Users/$private_id/Items/$series_id" "$private_token_a")
jq -e '(.UserData // .userData).UnplayedItemCount == 2' <<<"$private_season" >/dev/null \
    || fail "private season aggregate changed"
jq -e '(.UserData // .userData).UnplayedItemCount == 2' <<<"$private_series" >/dev/null \
    || fail "private series aggregate changed"
pass "completed private episode leaves episode, season, series and Next Up unchanged"

normal_start=$(jq -cn --arg item "$item_id" '{ItemId:$item,PositionTicks:0,PlayMethod:"DirectPlay",PlaySessionId:"normal-a",CanSeek:true}')
normal_progress=$(jq -cn --arg item "$item_id" '{ItemId:$item,PositionTicks:600000000,PlayMethod:"DirectPlay",PlaySessionId:"normal-a",CanSeek:true}')
normal_stop=$(jq -cn --arg item "$item_id" '{ItemId:$item,PositionTicks:600000000,PlaySessionId:"normal-a"}')
api POST '/Sessions/Playing' "$normal_token" "$normal_start" >/dev/null
api POST '/Sessions/Playing/Progress' "$normal_token" "$normal_progress" >/dev/null
api POST '/Sessions/Playing/Stopped' "$normal_token" "$normal_stop" >/dev/null
normal_data=$(api GET "/UserItems/$item_id/UserData" "$normal_token")
jq -e \
    '(.PlaybackPositionTicks // .playbackPositionTicks) == 600000000
     and (.Played // .played // false) == false
     and (.PlayCount // .playCount) >= 1
     and ((.LastPlayedDate // .lastPlayedDate) != null)' \
    <<<"$normal_data" >/dev/null \
    || fail "normal user playback state was not persisted: $(jq -c . <<<"$normal_data")"
normal_resume=$(api GET "/UserItems/Resume?UserId=$normal_id&Limit=20" "$normal_token")
jq -e --arg item "$item_id" 'any((.Items // .items)[]; (.Id // .id) == $item)' <<<"$normal_resume" >/dev/null \
    || fail "normal progress did not appear in Continue Watching"
normal_manual=$(api POST "/UserPlayedItems/$item_id" "$normal_token")
jq -e '(.Played // .played // false) == true and (.PlaybackPositionTicks // .playbackPositionTicks) == 0' \
    <<<"$normal_manual" >/dev/null || fail "normal manual watched change was not persisted"
normal_episode=$(api POST "/UserPlayedItems/$episode_one_id" "$normal_token")
jq -e '(.Played // .played // false) == true' <<<"$normal_episode" >/dev/null \
    || fail "normal episode watched change was not persisted"
wait_for_next_up "$normal_id" "$normal_token" "$series_id" "$episode_two_id" \
    || fail "normal Next Up did not advance to episode two"
normal_season=$(api GET "/Users/$normal_id/Items/$season_id" "$normal_token")
normal_series=$(api GET "/Users/$normal_id/Items/$series_id" "$normal_token")
jq -e '(.UserData // .userData).UnplayedItemCount == 1' <<<"$normal_season" >/dev/null \
    || fail "normal season aggregate did not change"
jq -e '(.UserData // .userData).UnplayedItemCount == 1' <<<"$normal_series" >/dev/null \
    || fail "normal series aggregate did not change"
pass "normal user persists progress/manual watched state and updates Continue Watching/Next Up aggregates"

preview=$(api GET "/PrivatePlayback/Users/$private_id/PlaybackData/Preview" "$admin_token")
jq -e '(.AffectedItemCount // .affectedItemCount) == 1' <<<"$preview" >/dev/null || fail "cleanup preview count was unexpected"
cleanup_result=$(api POST "/PrivatePlayback/Users/$private_id/PlaybackData/Clear" "$admin_token" '{"Confirmation":"CLEAR_PLAYBACK_DATA"}')
jq -e '(.ClearedItemCount // .clearedItemCount) == 1' <<<"$cleanup_result" >/dev/null || fail "cleanup did not clear one item"
cleanup_again=$(api POST "/PrivatePlayback/Users/$private_id/PlaybackData/Clear" "$admin_token" '{"Confirmation":"CLEAR_PLAYBACK_DATA"}')
jq -e '(.ClearedItemCount // .clearedItemCount) == 0' <<<"$cleanup_again" >/dev/null || fail "cleanup was not idempotent"
private_clean=$(api GET "/UserItems/$item_id/UserData" "$private_token_a")
jq -e \
    '(.PlaybackPositionTicks // .playbackPositionTicks) == 0
     and (.Played // .played // false) == false
     and (.PlayCount // .playCount) == 0
     and (.LastPlayedDate // .lastPlayedDate) == null
     and (.IsFavorite // .isFavorite) == true
     and (.Rating // .rating) == 9' \
    <<<"$private_clean" >/dev/null || fail "cleanup changed unrelated user data"
private_resume_clean=$(api GET "/UserItems/Resume?UserId=$private_id&Limit=20" "$private_token_a")
jq -e --arg item "$item_id" 'all((.Items // .items)[]; (.Id // .id) != $item)' <<<"$private_resume_clean" >/dev/null \
    || fail "cleanup left the item in Continue Watching"
pass "destructive cleanup previews, preserves unrelated fields and is idempotent"

configuration_file="$test_root/data/plugins/configurations/Jellyfin.Plugin.PrivatePlayback.xml"
[[ -f "$configuration_file" ]] || fail "plugin configuration was not persisted"
stop_server

start_server
admin_auth=$(auth IntegrationAdmin admin-pass admin-device-restart)
admin_token=$(jq -er '.AccessToken // .accessToken' <<<"$admin_auth")
private_auth_a=$(auth PrivateUser private-pass private-device-restart)
private_token_a=$(jq -er '.AccessToken // .accessToken' <<<"$private_auth_a")
normal_auth=$(auth NormalUser normal-pass normal-device-restart)
normal_token=$(jq -er '.AccessToken // .accessToken' <<<"$normal_auth")
api POST "/UserItems/$item_id/UserData" "$private_token_a" '{"PlaybackPositionTicks":33000000,"Played":true,"PlayCount":3}' >/dev/null
after_restart=$(api GET "/UserItems/$item_id/UserData" "$private_token_a")
jq -e '(.PlaybackPositionTicks // .playbackPositionTicks) == 0 and (.Played // .played // false) == false and (.PlayCount // .playCount) == 0' <<<"$after_restart" >/dev/null \
    || fail "policy did not survive restart"
private_episode_restart=$(api GET "/UserItems/$episode_one_id/UserData" "$private_token_a")
jq -e '(.Played // .played // false) == false and (.PlayCount // .playCount) == 0' <<<"$private_episode_restart" >/dev/null \
    || fail "private episode state changed after restart"
wait_for_next_up "$private_id" "$private_token_a" "$series_id" "$episode_one_id" \
    || fail "private Next Up changed after restart"
normal_episode_restart=$(api GET "/UserItems/$episode_one_id/UserData" "$normal_token")
jq -e '(.Played // .played // false) == true' <<<"$normal_episode_restart" >/dev/null \
    || fail "normal episode state was lost after restart"
pass "configuration, private protection, normal watched state and Next Up survive a real restart"
stop_server

cp "$configuration_file" "$configuration_file.valid"
printf '<PluginConfiguration><SchemaVersion>' >"$configuration_file"
start_server
private_auth_a=$(auth PrivateUser private-pass private-device-corrupt)
private_token_a=$(jq -er '.AccessToken // .accessToken' <<<"$private_auth_a")
api POST "/UserItems/$item_id/UserData" "$private_token_a" '{"PlaybackPositionTicks":33000000,"Played":false,"PlayCount":3}' >/dev/null
corrupt_result=$(api GET "/UserItems/$item_id/UserData" "$private_token_a")
jq -e '(.PlaybackPositionTicks // .playbackPositionTicks) == 33000000 and (.PlayCount // .playCount) == 3' <<<"$corrupt_result" >/dev/null \
    || fail "corrupt configuration did not fall back to normal behaviour"
grep -q 'configuration is invalid; all users will retain normal Jellyfin behavior' "$test_root/server-console-$run_number.log" \
    || fail "corrupt configuration was not logged"
pass "corrupt configuration fails safely to normal Jellyfin behaviour"
stop_server

mv "$configuration_file.valid" "$configuration_file"
start_server
private_auth_a=$(auth PrivateUser private-pass private-device-recovered)
private_token_a=$(jq -er '.AccessToken // .accessToken' <<<"$private_auth_a")
api POST "/UserItems/$item_id/UserData" "$private_token_a" '{"PlaybackPositionTicks":44000000,"Played":true,"PlayCount":4}' >/dev/null
recovered_result=$(api GET "/UserItems/$item_id/UserData" "$private_token_a")
jq -e '(.PlaybackPositionTicks // .playbackPositionTicks) == 33000000 and (.Played // .played // false) == false and (.PlayCount // .playCount) == 3' <<<"$recovered_result" >/dev/null \
    || fail "restored policy did not resume protection"
pass "restoring valid configuration resumes protection without data loss"

stop_server
grep -q 'Loaded plugin: Private Playback 0.9.0.0' "$test_root/server-console-1.log" || fail "plugin load evidence missing"
if grep -Eqi 'Private Playback.*(malfunctioned|failed to load|unhandled)' "$test_root"/server-console-*.log; then
    fail "plugin-related startup error found in logs"
fi

mv "$plugin_dir" "$test_root/removed-plugin"
start_server
if grep -q 'Loaded plugin: Private Playback' "$test_root/server-console-$run_number.log"; then
    fail "plugin still loaded after its installation directory was removed"
fi
private_auth_a=$(auth PrivateUser private-pass private-device-uninstalled)
private_token_a=$(jq -er '.AccessToken // .accessToken' <<<"$private_auth_a")
api POST "/UserItems/$item_id/UserData" "$private_token_a" '{"PlaybackPositionTicks":550000000,"Played":true,"PlayCount":5}' >/dev/null
uninstalled_result=$(api GET "/UserItems/$item_id/UserData" "$private_token_a")
jq -e \
    '(.PlaybackPositionTicks // .playbackPositionTicks) == 550000000
     and (.Played // .played // false) == true
     and (.PlayCount // .playCount) == 5' \
    <<<"$uninstalled_result" >/dev/null || fail "Jellyfin UserData was not operational after uninstall"
normal_auth=$(auth NormalUser normal-pass normal-device-uninstalled)
normal_token=$(jq -er '.AccessToken // .accessToken' <<<"$normal_auth")
normal_episode_uninstalled=$(api GET "/UserItems/$episode_one_id/UserData" "$normal_token")
jq -e '(.Played // .played // false) == true' <<<"$normal_episode_uninstalled" >/dev/null \
    || fail "normal user data changed after uninstall"
pass "server and existing per-user UserData remain operational after plugin uninstall"
stop_server

printf 'Integration test root: %s\n' "$test_root"
printf 'All Private Playback integration tests passed.\n'
