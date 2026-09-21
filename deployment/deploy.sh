#!/usr/bin/env bash
# Runtime is deliberately independent of Git, Node and .NET.
set -Eeuo pipefail
umask 077
runtime=$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd -P)
# shellcheck source=deployment/deploy-lib.sh
source "$runtime/deploy-lib.sh"
require_tools
installation=$(realpath -e -- "${1:?Installationskatalog saknas}")
shift
[[ $# == 0 ]] || fail 'Kommandot tar inga argument.'
[[ -f "$installation/.env" && ! -L "$installation/.env" ]] || fail 'En vanlig .env-fil krävs i installationskatalogen.'
state="$installation/.deploy-state"
[[ ! -L "$state" ]] || fail 'Tillståndskatalogen får inte vara en symbolisk länk.'
mkdir -p -- "$state"
chmod 700 -- "$state"
exec 9>"$state/update.lock"
flock -n 9 || fail 'En uppdatering pågår redan.'
work=$(mktemp -d "$state/pending.XXXXXXXX")
step='förkontroll'
trap 'failed "$?"' ERR
trap 'failed 130' INT
trap 'failed 143' TERM
docker info >"$work/docker-info.log" 2>&1
docker compose version >"$work/compose-version.log" 2>&1
[[ $(docker info --format '{{.OSType}}/{{.Architecture}}') =~ ^linux/(x86_64|amd64)$ ]] || fail 'Denna version kräver Linux x86_64.'
docker inspect postgresql18 >"$work/postgres.json" 2>"$work/postgres.log"
jq -e 'length == 1 and .[0].State.Running == true and .[0].NetworkSettings.Networks["car-expense-network"] != null' "$work/postgres.json" >/dev/null
docker network inspect car-expense-network >"$work/network.json" 2>"$work/network.log"
docker exec postgresql18 pg_isready --quiet >"$work/postgres-ready.log" 2>&1
echo 'Hämtar senaste publicerade versionen…'
step='hämtning av versionsinformation'
release_api=${CEC_RELEASE_API:-https://api.github.com/repos/Extender92/CarExpenseCalculator/releases/latest}
[[ "$release_api" == https://* || "$release_api" == http://127.0.0.1:*/* || "$release_api" == http://localhost:*/* ]] || fail 'Versionsadressen måste använda HTTPS.'
download "$release_api" "$work/release.json"
jq -e '.draft == false and .prerelease == false and (.tag_name | test("^build-[0-9]+-[0-9]+$"))' "$work/release.json" >/dev/null
version=$(jq -r .tag_name "$work/release.json")
archive_url=$(asset_url unraid-bundle.tar.gz)
checksum_url=$(asset_url unraid-bundle.tar.gz.sha256)
step='hämtning och kontroll av installationspaket'
download "$archive_url" "$work/bundle.tar.gz"
download "$checksum_url" "$work/bundle.sha256"
expected=$(cat "$work/bundle.sha256")
[[ "$expected" =~ ^([a-f0-9]{64})[[:space:]]+unraid-bundle.tar.gz$ ]] || fail 'Kontrollsummefilen har fel format.'
[[ "${BASH_REMATCH[1]}" == "$(sha256sum "$work/bundle.tar.gz" | cut -d ' ' -f 1)" ]] || fail 'Installationspaketets kontrollsumma stämmer inte.'
validate_archive "$work/bundle.tar.gz"
mkdir "$work/bundle"
tar --extract --gzip --file "$work/bundle.tar.gz" --directory "$work/bundle" --no-same-owner --no-same-permissions
bundle="$work/bundle"
validate_manifest "$bundle/manifest.json" "$version"
validate_files "$bundle"
if [[ -f "$state/current.json" ]]; then
  current_version=$(jq -er .version "$state/current.json")
  if [[ "$current_version" == "$version" ]]; then
    cmp -s "$state/current.json" "$bundle/manifest.json" || fail 'Den publicerade versionens innehåll har ändrats.'
  else
    newest=$(printf '%s\n' "$current_version" "$version" | sort -V | tail -n 1)
    [[ "$newest" == "$version" ]] || fail 'Den publicerade versionen är äldre än den installerade. Automatisk nedgradering stöds inte.'
  fi
fi
step='kontroll av konfiguration'
configure_compose
compose config --format json >"$work/config.json" 2>"$work/config.log"
validate_config
capture_existing
step='hämtning av images'
for service in api web codex-extractor; do
  reference=$(jq -r --arg service "$service" '.images[$service]' "$bundle/manifest.json")
  echo "Hämtar $service…"
  docker pull "$reference" >"$work/pull-$service.log" 2>&1
  verify_image "$service" "$reference"
done
remember_images
if [[ -f "$state/current.json" ]] && cmp -s "$state/current.json" "$bundle/manifest.json" && running_matches; then
  step='hälsokontroll av aktuell version'
  check_health
  echo "Version $version är redan aktuell. Ingen omstart eller migration behövdes."
else
  step='stopp av appen'
  echo 'Stoppar appen och uppdaterar databasschemat…'
  compose stop web api codex-extractor >"$work/stop.log" 2>&1
  step='databasmigration'
  compose run --rm --no-deps api migrate >"$work/migrate.log" 2>&1
  step='start av den nya versionen'
  compose up --detach --no-build --wait --wait-timeout 120 codex-extractor api web >"$work/start.log" 2>&1
  step='kontroll av körande images'
  running_matches
  step='hälsokontroll av den nya versionen'
  check_health
fi
step='registrering av aktuell version'
activate_bundle
step='städning av äldre app-images'
if clean_images; then
  echo "Uppdateringen är klar: $version ($(jq -r .commit "$state/current.json"))."
  remove_work
else
  echo "Appen är uppdaterad till $version, men städning är ofullständig." >&2
  echo "Diagnostik finns i $work. Kör ./update.sh igen för att försöka städa." >&2
  exit 2
fi
