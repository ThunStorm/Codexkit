#!/bin/zsh
set -euo pipefail
project_dir=${0:A:h:h}
check_dir=$(mktemp -d /tmp/codex-meter-check.XXXXXX)
swiftc -parse-as-library -o "$check_dir/RefreshChecks" \
  "$project_dir/Sources/CodexMenuMeter/Domain.swift" \
  "$project_dir/Sources/CodexMenuMeter/RateLimitModels.swift" \
  "$project_dir/Sources/CodexMenuMeter/JSONRPCClient.swift" \
  "$project_dir/Sources/CodexMenuMeter/CodexProcessLocator.swift" \
  "$project_dir/Sources/CodexMenuMeter/AppState.swift" \
  "$project_dir/Scripts/RefreshChecks.swift"
"$check_dir/RefreshChecks"
