#!/usr/bin/env bash

set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

log() {
  echo "[vercel-build] $*"
}

log "Installing MkDocs dependencies"
python3 -m pip install --upgrade pip --user
python3 -m pip install --user -r docs/requirements.txt

log "Building MkDocs site into ./public"
python3 -m mkdocs build --strict --clean --site-dir public

log "Hydrating FuncScript Studio into ./public/fsstudio"
FSTUDIO_OUTPUT_DIR="${ROOT_DIR}/public/fsstudio" \
  bash js-port/funcscript-vscode/scripts/vercel-build.sh

log "Build complete"
