#!/usr/bin/env bash
# PostToolUse (Edit|Write): formats only the file Claude just touched, so diffs stay clean without a full-solution pass.
set -euo pipefail
export PATH="$HOME/.dotnet:$PATH"

file=$(python3 -c 'import json,sys; print(json.load(sys.stdin).get("tool_input",{}).get("file_path",""))')
[[ -z "$file" || ! -f "$file" ]] && exit 0
root="${CLAUDE_PROJECT_DIR:-$(pwd)}"

case "$file" in
  *.cs)
    # --folder skips MSBuild evaluation: whitespace/style formatting of one file in about a second.
    dotnet format whitespace "$root" --folder --include "$file" >/dev/null 2>&1 || true
    ;;
  "$root"/web/src/*.ts|"$root"/web/src/*.html|"$root"/web/src/*.scss)
    (cd "$root/web" && npx --no-install prettier --write --log-level silent "$file") || true
    ;;
esac
exit 0
