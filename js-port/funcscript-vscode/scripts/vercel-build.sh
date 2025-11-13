#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
EDITOR_DIR="${PROJECT_ROOT}/../funcscript-editor"
STUDIO_DIR="${PROJECT_ROOT}/../funcscript-studio"
PUBLIC_DIR="${FSTUDIO_OUTPUT_DIR:-${PROJECT_ROOT}/public}"

log() {
  echo "[fsstudio-build] $*"
}

log "Installing FuncScript editor dependencies"
pushd "${EDITOR_DIR}" >/dev/null
npm ci --ignore-scripts
npm run build
popd >/dev/null

log "Installing FuncScript Studio dependencies"
pushd "${STUDIO_DIR}" >/dev/null
npm ci --ignore-scripts
log "Building FuncScript Studio"
PUBLIC_PATH=./ npm run build
popd >/dev/null

log "Copying FuncScript Studio dist -> ${PUBLIC_DIR}"
rm -rf "${PUBLIC_DIR}"
mkdir -p "${PUBLIC_DIR}"
cp -a "${STUDIO_DIR}/dist/." "${PUBLIC_DIR}/"

log "Build artifacts ready"
