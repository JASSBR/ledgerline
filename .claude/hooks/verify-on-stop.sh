#!/usr/bin/env bash
# Stop: before Claude hands back, the side(s) it changed must still build and lint.
# Exit code 2 sends the failure back to Claude, which keeps working instead of ending on a red build.
set -uo pipefail
export PATH="$HOME/.dotnet:$PATH"
root="${CLAUDE_PROJECT_DIR:-$(pwd)}"
cd "$root" || exit 0

# Never loop: if this hook already blocked once in this turn, let Claude stop and report.
python3 -c 'import json,sys; sys.exit(0 if json.load(sys.stdin).get("stop_hook_active") else 1)' && exit 0

changed=$(git status --porcelain 2>/dev/null | awk '{print $2}')
[[ -z "$changed" ]] && exit 0

if grep -qE '\.(cs|csproj|props)$' <<<"$changed"; then
  if ! out=$(dotnet build ClaimFlow.slnx -v q -nologo 2>&1); then
    grep -E ' error ' <<<"$out" | sed "s|$root/||" | sort -u | head -20 >&2
    echo "dotnet build failed (analyzers treat warnings as errors). Fix before finishing." >&2
    exit 2
  fi
fi

if grep -qE '^web/src/' <<<"$changed"; then
  if ! out=$(cd web && npx --no-install ng lint 2>&1); then
    tail -30 <<<"$out" >&2
    echo "ng lint failed. Fix before finishing." >&2
    exit 2
  fi
fi

if grep -qE 'appsettings.*\.json$' <<<"$changed"; then
  echo "Reminder: appsettings changed — make sure no secret was added (secrets go in user-secrets / platform env vars)." >&2
fi
exit 0
