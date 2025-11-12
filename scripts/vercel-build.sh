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

log "Installing and building @tewelde/funcscript-editor"
pushd js-port/funcscript-editor >/dev/null
npm ci --ignore-scripts
npm run build
popd >/dev/null

log "Installing dependencies for FuncScript Studio"
pushd js-port/funcscript-studio >/dev/null
npm ci --ignore-scripts
log "Building FuncScript Studio demo"
PUBLIC_PATH=./ npm run build
popd >/dev/null

log "Building MkDocs site into ./public"
python3 -m mkdocs build --strict --clean --site-dir public

log "Bundling FuncScript Studio into the published site"
mkdir -p public/web/funcscript-studio
cp -a js-port/funcscript-studio/dist/. public/web/funcscript-studio/

log "Build complete"
