#!/usr/bin/env bash
# Runtime globals are initialized by deploy.sh before these functions execute.
# shellcheck disable=SC2154
project=car-expense-calculator
source_repository=https://github.com/Extender92/CarExpenseCalculator

fail() { echo "Fel: $*" >&2; if [[ -n "${work:-}" ]]; then failed 1; fi; exit 1; }
failed() {
  local status=$1
  trap - ERR INT TERM
  echo "Uppdateringen avbröts under: $step. Ingen automatisk återställning eller image-städning utförs." >&2
  echo "Privat diagnostik finns i $work. Appen kan vara stoppad om underhållssteget påbörjats." >&2
  [[ "$status" == 130 || "$status" == 143 ]] || status=1
  exit "$status"
}
require_tools() {
  for tool in bash curl jq tar sha256sum docker flock realpath mktemp cmp sort cut stat readlink cp mv ln env grep tail awk; do
    command -v "$tool" >/dev/null || fail "Verktyget $tool saknas."
  done
}
download() {
  curl --fail --silent --show-error --location --connect-timeout 15 --max-time 180 \
    --max-filesize 1048576 --retry 3 --retry-delay 2 --retry-max-time 240 --output "$2" "$1" 2>"$work/download.log"
}
asset_url() {
  jq -er --arg name "$1" '[.assets[] | select(.name == $name) | .browser_download_url] |
    if length == 1 and (.[0] | test("^https://|^http://(localhost|127\\.0\\.0\\.1):[0-9]+/"))
    then .[0] else error("Invalid release asset") end' "$work/release.json"
}
validate_archive() {
  local names details
  [[ $(stat -c %s "$1") -le 1048576 ]] || fail 'Installationspaketet är för stort.'
  names=$(tar -tzf "$1")
  [[ "$(printf '%s\n' "$names" | sort)" == "$(printf '%s\n' .env.example compose.unraid.yaml deploy-lib.sh deploy.sh manifest.json update.sh | sort)" ]] || fail 'Installationspaketet innehåller oväntade filer.'
  details=$(tar --numeric-owner -tvzf "$1")
  while IFS= read -r entry; do
    [[ "$entry" == -* ]] || fail 'Installationspaketet får bara innehålla vanliga filer.'
  done <<< "$details"
  awk '{ size += $3 } END { exit size > 1048576 }' <<< "$details" || fail 'Installationspaketets uppackade innehåll är för stort.'
}
validate_manifest() {
  jq -e --arg version "$2" '
    .formatVersion == 1 and .version == $version and .architecture == "linux/amd64" and
    (.commit | test("^[a-f0-9]{40}$")) and
    (.images | keys == ["api","codex-extractor","web"]) and
    ([.images | to_entries[] | .value | type == "string"] | all) and
    ([.images | to_entries[] | .value | test("^[a-z0-9][a-z0-9.:/-]*/car-expense-calculator-(api|web|codex-extractor)@sha256:[a-f0-9]{64}$")] | all) and
    (.files | keys == [".env.example","compose.unraid.yaml","deploy-lib.sh","deploy.sh","update.sh"]) and
    ([.files[] | test("^[a-f0-9]{64}$")] | all)
  ' "$1" >/dev/null || fail 'Versionsmanifestet är ogiltigt eller stöds inte.'
  for service in api web codex-extractor; do
    jq -e --arg s "$service" '.images[$s] | contains("/car-expense-calculator-" + $s + "@sha256:")' "$1" >/dev/null
  done
}
validate_files() {
  local name hash
  while IFS=$'\t' read -r name hash; do
    [[ "$(sha256sum "$1/$name" | cut -d ' ' -f 1)" == "$hash" ]] || fail "Kontrollsumman för $name stämmer inte."
  done < <(jq -r '.files | to_entries[] | [.key,.value] | @tsv' "$1/manifest.json")
}
configure_compose() {
  api_image=$(jq -r .images.api "$bundle/manifest.json")
  web_image=$(jq -r .images.web "$bundle/manifest.json")
  extractor_image=$(jq -r '.images["codex-extractor"]' "$bundle/manifest.json")
}
compose() {
  env -u WEB_PORT -u POSTGRES_DB -u POSTGRES_USER -u POSTGRES_PASSWORD -u CODEX_HOME_PATH \
    -u CODEX_MODEL -u CODEX_REASONING_EFFORT -u COMPARISON_MAX_REQUEST_BYTES \
    CEC_API_IMAGE="$api_image" CEC_WEB_IMAGE="$web_image" CEC_EXTRACTOR_IMAGE="$extractor_image" \
    docker compose --project-name "$project" --project-directory "$installation" \
    --env-file "$installation/.env" -f "$bundle/compose.unraid.yaml" "$@"
}
validate_config() {
  jq -e --arg api "$api_image" --arg web "$web_image" --arg extractor "$extractor_image" '
    .name == "car-expense-calculator" and
    (.services | keys == ["api","codex-extractor","web"]) and
    ([.services[] | has("build") | not] | all) and
    .services.api.image == $api and .services.web.image == $web and .services["codex-extractor"].image == $extractor and
    (.services.api.ports // [] | length == 0) and (.services["codex-extractor"].ports // [] | length == 0) and
    (.services.web.ports | length == 1) and .services.web.ports[0].target == 80 and
    (.services.api.environment.ConnectionStrings__Postgres | startswith("Host=postgresql18;")) and
    .networks["car-expense-network"].external == true and
    .networks["car-expense-network"].name == "car-expense-network" and
    .services["codex-extractor"].volumes[0].type == "bind" and
    .services["codex-extractor"].volumes[0].target == "/var/lib/codex"
  ' "$work/config.json" >/dev/null || fail 'Compose-konfigurationen bryter mot installationsgränserna.'
  port=$(jq -r .services.web.ports[0].published "$work/config.json")
  [[ "$port" =~ ^[0-9]+$ && "$port" -ge 1 && "$port" -le 65535 ]] || fail 'Webbporten är ogiltig.'
  codex_path=$(jq -r '.services["codex-extractor"].volumes[0].source' "$work/config.json")
  [[ "$codex_path" == /* && "$codex_path" != / ]] || fail 'Codex-sökvägen måste vara absolut.'
}
inspect_containers() {
  local ids
  ids=$(docker ps -aq "$@") || return 1
  if [[ -z "$ids" ]]; then printf '[]\n'; else
    # Docker IDs contain no shell metacharacters; splitting the ID list is intentional.
    readarray -t container_ids <<< "$ids"
    docker inspect "${container_ids[@]}"
  fi
}
capture_existing() {
  inspect_containers --filter "label=com.docker.compose.project=$project" >"$work/existing.json"
  jq -e '[.[] | .Config.Labels["com.docker.compose.service"] | IN("api","web","codex-extractor")] | all' "$work/existing.json" >/dev/null || fail 'Projektet innehåller okända tjänster.'
  jq -e --arg path "$codex_path" '[.[] | select(.Config.Labels["com.docker.compose.service"] == "codex-extractor") |
    [.Mounts[] | select(.Destination == "/var/lib/codex")] |
    length == 1 and .[0].Type == "bind" and .[0].Source == $path] | all' "$work/existing.json" >/dev/null || fail 'Codex-monteringens sökväg har ändrats. Kontrollera .env.'
}
verify_image() {
  docker image inspect "$2" >"$work/image-$1.json"
  jq -e --arg component "$1" --arg source "$source_repository" --arg commit "$(jq -r .commit "$bundle/manifest.json")" '
    length == 1 and .[0].Os == "linux" and .[0].Architecture == "amd64" and
    .[0].Config.Labels["org.opencontainers.image.source"] == $source and
    .[0].Config.Labels["org.opencontainers.image.revision"] == $commit and
    .[0].Config.Labels["se.car-expense-calculator.component"] == $component
  ' "$work/image-$1.json" >/dev/null || fail "Image-identiteten för $1 stämmer inte."
}
remember_images() {
  local ids service
  [[ -f "$state/images.json" ]] || printf '[]\n' >"$state/images.json"
  # Include verified app-labelled images from earlier interrupted pulls.
  docker image ls --quiet --no-trunc --filter "label=org.opencontainers.image.source=$source_repository" >"$work/discovered-ids" 2>"$work/image-inventory.log"
  # Old locally built tags without labels are reported, not assumed safe to delete.
  for service in api web codex-extractor; do
    docker image ls --quiet --no-trunc --filter "reference=car-expense-calculator-$service:*" >>"$work/discovered-ids" 2>>"$work/image-inventory.log"
  done
  ids=$(awk 'NF' "$work/discovered-ids" | sort -u)
  printf '[]\n' >"$work/discovered-images.json"
  if [[ -n "$ids" ]]; then
    readarray -t discovered_ids <<< "$ids"
    docker image inspect "${discovered_ids[@]}" >"$work/discovered-images.json" 2>>"$work/image-inventory.log"
  fi
  jq -s '.[0] + [.[1][] | {id:.Image, reference:.Config.Image, legacy:true}] +
    [.[2:][][] | {id:.Id, reference:(.RepoDigests[0] // .RepoTags[0]), legacy:false}] |
    group_by(.id) | map((map(select(.legacy == true))[0]) // .[0])' "$state/images.json" "$work/existing.json" \
    "$work/image-api.json" "$work/image-web.json" "$work/image-codex-extractor.json" "$work/discovered-images.json" >"$state/images.next.json"
  mv -f -- "$state/images.next.json" "$state/images.json"
}
running_matches() {
  inspect_containers --filter "label=com.docker.compose.project=$project" >"$work/running.json"
  for service in api web codex-extractor; do
    local id
    id=$(jq -r '.[0].Id' "$work/image-$service.json")
    jq -e --arg s "$service" --arg id "$id" '[.[] | select(.Config.Labels["com.docker.compose.service"] == $s)] |
      length == 1 and .[0].Image == $id and .[0].State.Running == true and
      ((.[0].State.Health.Status // "healthy") == "healthy")' "$work/running.json" >/dev/null || return 1
  done
}
check_health() {
  for endpoint in health/live health/ready system/status; do
    curl --fail --silent --show-error --connect-timeout 5 --max-time 10 --retry 10 --retry-delay 2 --retry-connrefused \
      "http://127.0.0.1:$port/api/$endpoint" >"$work/health-${endpoint##*/}.json" 2>"$work/health.log"
    jq -e '.status == "healthy"' "$work/health-${endpoint##*/}.json" >/dev/null
  done
  jq -e '.database == "available"' "$work/health-status.json" >/dev/null
  if jq -e '.integrations.codexListingExtractionConfigured == true' "$work/health-status.json" >/dev/null; then
    echo 'API och PostgreSQL fungerar. Codex-inloggningen är tillgänglig.'
  else
    echo 'API och PostgreSQL fungerar. Codex-inloggningen behöver kontrolleras separat.'
  fi
}
activate_bundle() {
  local destination previous
  destination="$state/runtime-$version"
  previous=$(readlink "$state/current" || true)
  if [[ ! -d "$destination" ]]; then
    mkdir "$work/runtime"
    cp -- "$bundle/"* "$work/runtime/"
    cp -- "$bundle/.env.example" "$work/runtime/"
    mv -- "$work/runtime" "$destination"
  fi
  validate_files "$destination"
  ln -sfn "runtime-$version" "$state/current.next"
  mv -Tf -- "$state/current.next" "$state/current"
  cp -- "$bundle/manifest.json" "$state/current.next.json"
  mv -f -- "$state/current.next.json" "$state/current.json"
  # The launcher remains stable; replace the visible Compose link atomically.
  ln -sfn .deploy-state/current/compose.unraid.yaml "$installation/compose.next"
  mv -Tf -- "$installation/compose.next" "$installation/compose.unraid.yaml"
  if [[ "$previous" =~ ^runtime-build-[0-9]+-[0-9]+$ && "$previous" != "runtime-$version" ]]; then
    rm -rf -- "${state:?}/$previous"
  fi
}
clean_images() {
  local current_ids all_containers available_ids failed_cleanup=0 id reference legacy inspect tags component
  current_ids=$(jq -s '[.[][] | .Id]' "$work/image-api.json" "$work/image-web.json" "$work/image-codex-extractor.json") || return 1
  all_containers=$(inspect_containers 2>>"$work/cleanup.log") || return 1
  jq -e 'type == "array"' <<< "$all_containers" >/dev/null || return 1
  available_ids=$(docker image ls --quiet --no-trunc) || return 1
  jq -er '.[] | [.id,.reference,.legacy] | @tsv' "$state/images.json" >"$work/cleanup-candidates.tsv" || return 1
  while IFS=$'\t' read -r id reference legacy; do
    [[ "$id" =~ ^sha256:[a-f0-9]{64}$ ]] || { failed_cleanup=1; continue; }
    if jq -e --arg id "$id" 'index($id) != null' <<< "$current_ids" >/dev/null; then continue; fi
    if jq -e --arg id "$id" 'any(.[]; .Image == $id)' <<< "$all_containers" >/dev/null; then
      echo "Behåller image som används av en container: $id" >&2; failed_cleanup=1; continue
    fi
    if ! grep -Fxq -- "$id" <<< "$available_ids"; then continue; fi
    inspect=$(docker image inspect "$id" 2>/dev/null) || { echo "Kunde inte kontrollera image: $id" >&2; failed_cleanup=1; continue; }
    component=$(jq -r '.[0].Config.Labels["se.car-expense-calculator.component"] // ""' <<< "$inspect")
    if [[ ! "$component" =~ ^(api|web|codex-extractor)$ ]]; then
      [[ "$legacy" == true && "$reference" =~ ^car-expense-calculator-(api|web|codex-extractor):local$ ]] || {
        echo "Kan inte säkert identifiera äldre image: $id" >&2; failed_cleanup=1; continue;
      }
    else
      jq -e --arg source "$source_repository" '.[0].Config.Labels["org.opencontainers.image.source"] == $source' <<< "$inspect" >/dev/null || {
        echo "Behåller image med annan repository-märkning: $id" >&2; failed_cleanup=1; continue;
      }
    fi
    tags=$(jq -r '.[0].RepoTags[]?' <<< "$inspect")
    if [[ -n "$tags" ]] && ! jq -e 'all(.[0].RepoTags[]; test("(^|/)car-expense-calculator-(api|web|codex-extractor):"))' <<< "$inspect" >/dev/null; then
      echo "Behåller image med andra repositorynamn: $id" >&2; failed_cleanup=1; continue
    fi
    if [[ -n "$tags" ]]; then
      while IFS= read -r tag; do
        docker image rm "$tag" >>"$work/cleanup.log" 2>&1 || { echo "Kunde inte ta bort image: $id ($tag)" >&2; failed_cleanup=1; }
      done <<< "$tags"
    else
      docker image rm "$id" >>"$work/cleanup.log" 2>&1 || { echo "Kunde inte ta bort image: $id" >&2; failed_cleanup=1; }
    fi
  done <"$work/cleanup-candidates.tsv"
  [[ "$failed_cleanup" == 0 ]]
}
remove_work() {
  [[ "$work" == "$state"/pending.* && ! -L "$work" ]] || fail 'Osäker temporär sökväg.'
  rm -rf -- "$work"
}
