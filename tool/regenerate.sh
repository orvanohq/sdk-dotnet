#!/bin/sh
# Spec 0014 RGN-1 to RGN-3: regenerates this repo's generated code from the orvano commit in orvano-contract.json.
#   tool/regenerate.sh                fetch the pinned commit into .orvano/, check the contract hash, run the generator,
#                                     and bring PublicAPI.Unshipped.txt up to date
#   tool/regenerate.sh --pin <sha>    move the pin to that commit first (writes orvano-contract.json)
#   tool/regenerate.sh --fetch-only   fetch and check, no generator
# CI runs it and then `git diff --exit-code` (the drift check). It needs git, Node 24, pnpm (through corepack), and
# the .NET SDK on PATH.
set -eu

root=$(cd "$(dirname "$0")/.." && pwd)
checkout="$root/.orvano"
pin_file="$root/orvano-contract.json"
target=csharp-server

fail() {
  echo "regenerate: $1" >&2
  exit "${2:-1}"
}

is_commit() {
  printf '%s' "$1" | grep -Eq '^[0-9a-f]{40}$'
}

# Fetches one orvano commit into .orvano/ with no token (the repo is public), unless it is already there.
fetch() {
  if [ ! -d "$checkout/.git" ]; then
    git init -q "$checkout"
  fi
  if [ "$(git -C "$checkout" rev-parse HEAD 2>/dev/null || true)" = "$1" ]; then
    return
  fi
  git -C "$checkout" fetch -q --depth 1 https://github.com/orvanohq/orvano "$1"
  git -C "$checkout" checkout -q --detach FETCH_HEAD
}

# The public contract's version and SHA-256 at the checked out commit, as "<version> <sha256>" (RGN-1).
contract_facts() {
  # shellcheck disable=SC2016 # the script is JavaScript; its ${} is a template literal, not the shell's
  node -e '
    const { createHash } = require("node:crypto");
    const bytes = require("node:fs").readFileSync(process.argv[1]);
    const version = JSON.parse(bytes.toString("utf8")).info.version;
    console.log(`${version} ${createHash("sha256").update(bytes).digest("hex")}`);
  ' "$checkout/api/openapi.json"
}

# One field of orvano-contract.json.
pin_field() {
  node -e 'console.log(JSON.parse(require("node:fs").readFileSync(process.argv[1], "utf8"))[process.argv[2]] ?? "")' \
    "$pin_file" "$1"
}

fetch_only=false
while [ $# -gt 0 ]; do
  case "$1" in
    --pin)
      if [ $# -lt 2 ] || ! is_commit "$2"; then
        fail "--pin needs a 40 character lowercase commit SHA." 2
      fi
      fetch "$2"
      facts=$(contract_facts)
      printf '{\n  "commit": "%s",\n  "apiVersion": "%s",\n  "contractSha256": "%s"\n}\n' \
        "$2" "${facts% *}" "${facts#* }" >"$pin_file"
      shift 2
      ;;
    --fetch-only)
      fetch_only=true
      shift
      ;;
    *)
      fail "unknown argument $1." 2
      ;;
  esac
done

commit=$(pin_field commit)
is_commit "$commit" || fail "orvano-contract.json needs a 40 character lowercase commit."
fetch "$commit"

# RGN-3: the pin must describe the contract at its commit.
facts=$(contract_facts)
api_version=$(pin_field apiVersion)
contract_sha=$(pin_field contractSha256)
if [ "${facts% *}" != "$api_version" ] || [ "${facts#* }" != "$contract_sha" ]; then
  fail "orvano-contract.json says $api_version (sha256 $contract_sha), but api/openapi.json at $commit is ${facts% *} (sha256 ${facts#* }). Run tool/regenerate.sh --pin $commit."
fi
if [ "$fetch_only" = true ]; then
  exit 0
fi

(cd "$checkout" && ORVANO_SKIP_HOOKS=1 pnpm install --silent --frozen-lockfile --filter @orvano/sdk-generator...)
node "$checkout/api/generator/src/cli.ts" --target "$target" --contract "$checkout/api/openapi.json" \
  --out "$root" --commit "$commit"
# GNR-9: the repo's formatter after the generator. The generator's output is already clean, so this changes nothing.
dotnet format whitespace "$root/Orvano.Server.slnx" --include-generated \
  --include src/Orvano.Server/Generated tests/Orvano.Server.ConformanceTests/Generated
# Spec 0002 LIB-10: the generated public API goes into PublicAPI.Unshipped.txt, or CI's build fails on RS0016 and RS0017.
node "$root/tool/sync-public-api.mjs"
echo "regenerate: Generated/ matches orvano $(printf '%s' "$commit" | cut -c1-7) (contract $api_version)."
