#!/usr/bin/env bash
#
# setup-wsl.sh — provision the interoperability peers for the HermodSSH test harness.
#
# Installs the third-party SSH/SFTP implementations that the interop suite (PLAN.md, section 11)
# drives our client and server against: OpenSSH, Dropbear, TinySSH, PuTTY tools, curl and the
# Python peers (AsyncSSH, Paramiko). Intended to run inside a WSL2 Debian/Ubuntu (or any
# Debian-family Linux). Idempotent: safe to re-run.
#
# Usage:   ./setup-wsl.sh            # install everything
#          ./setup-wsl.sh --check    # only report what is present, install nothing
#
# Requires sudo for the apt packages. The Python peers are installed into a local virtual
# environment (.venv-interop next to this script), so nothing is installed system-wide for those.

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VENV_DIR="${SCRIPT_DIR}/.venv-interop"

CHECK_ONLY=0
if [[ "${1:-}" == "--check" ]]; then
    CHECK_ONLY=1
fi

APT_PACKAGES=(
    openssh-server      # sshd — the reference server
    openssh-client      # ssh, sftp, ssh-keygen, ssh-keyscan, ssh-agent
    dropbear-bin        # dropbear / dbclient — embedded-world peer
    tinysshd            # radically minimal server (ed25519 + chacha20 + sntrup761)
    putty-tools         # plink / psftp / puttygen
    curl                # SFTP via libssh/libssh2 — a different stack
    socat               # run inetd-style tinysshd on a socket
    openssl             # key/cert plumbing helpers
    ca-certificates
    python3
    python3-venv
    python3-pip
)

log()  { printf '\033[1;34m[setup]\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m[warn ]\033[0m %s\n' "$*"; }

# ---------------------------------------------------------------------------------------------------

if ! command -v apt-get >/dev/null 2>&1; then
    warn "apt-get not found — this script targets Debian/Ubuntu (WSL2). Install the peers manually."
    exit 1
fi

if [[ "${CHECK_ONLY}" -eq 0 ]]; then
    log "Installing interop peers via apt (sudo required) ..."
    sudo apt-get update -qq
    sudo DEBIAN_FRONTEND=noninteractive apt-get install -y -qq "${APT_PACKAGES[@]}"

    log "Creating the Python virtual environment for AsyncSSH / Paramiko ..."
    if [[ ! -d "${VENV_DIR}" ]]; then
        python3 -m venv "${VENV_DIR}"
    fi
    # shellcheck disable=SC1091
    source "${VENV_DIR}/bin/activate"
    pip install --quiet --upgrade pip
    pip install --quiet asyncssh paramiko
    deactivate
fi

# ---------------------------------------------------------------------------------------------------

log "Installed peer versions:"

report() {
    local name="$1"; shift
    if command -v "$1" >/dev/null 2>&1; then
        printf '  %-12s %s\n' "${name}" "$("$@" 2>&1 | head -n1)"
    else
        printf '  %-12s \033[1;33mMISSING\033[0m\n' "${name}"
    fi
}

# OpenSSH prints its version on stderr; grab it via -V.
report "OpenSSH"  ssh -V
report "sshd"     sh -c 'command -v sshd >/dev/null && $(command -v sshd) -\? 2>&1 | head -n1 || echo n/a'
report "Dropbear" dropbear -V
report "dbclient" dbclient -V
report "plink"    plink -V
report "curl"     curl --version
report "socat"    socat -V

if [[ -d "${VENV_DIR}" ]]; then
    # shellcheck disable=SC1091
    source "${VENV_DIR}/bin/activate"
    report "AsyncSSH" python3 -c 'import asyncssh; print(asyncssh.__version__)'
    report "Paramiko" python3 -c 'import paramiko; print(paramiko.__version__)'
    deactivate
else
    printf '  %-12s (python venv not created — run without --check)\n' "AsyncSSH/…"
fi

if command -v go >/dev/null 2>&1; then
    report "Go" go version
else
    warn "Go toolchain not found (optional): needed only for the golang.org/x/crypto/ssh harness."
fi

log "Done. The interop suite reaches this shell from NUnit via 'wsl.exe -e'."
