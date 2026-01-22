#!/usr/bin/env bash
set -Eeuo pipefail
IFS=$'\n\t'

# ---------- Safety / logging ----------
trap 'echo "Error on line $LINENO. Exiting." >&2' ERR

if [[ $EUID -ne 0 ]]; then
  echo "This script must be run as root." >&2
  exit 1
fi

export DEBIAN_FRONTEND=noninteractive

MARKER_DIR="/var/lib/lancommander"
MARKER_FILE="$MARKER_DIR/setup.done"

# ---------- One-time guard ----------
if [[ -f "$MARKER_FILE" ]]; then
  echo "Setup already completed previously ($MARKER_FILE exists)."
  echo "Skipping installation steps."
else
  mkdir -p "$MARKER_DIR"
fi

# ---------- Helpers ----------
ensure_user() {
  local name="$1"
  local home="$2"
  # Use nologin shell for service accounts
  if id -u "$name" >/dev/null 2>&1; then
    echo "User '$name' already exists. Skipping creation."
  else
    useradd -m -d "$home" -s /usr/sbin/nologin "$name"
    echo "Created user '$name' with home '$home'."
  fi
}

ensure_dir_owned() {
  local path="$1"
  local owner="$2"
  local group="$3"
  install -d -m 0755 -o "$owner" -g "$group" "$path"
}

apt_update_once() {
  # Run apt-get update only once per invocation when needed
  if [[ -z "${_APT_UPDATED:-}" ]]; then
    apt-get update
    _APT_UPDATED=1
  fi
}

apt_install() {
  apt_update_once
  apt-get install -y --no-install-recommends "$@"
}

# ---------- SteamCMD ----------
install_steamcmd() {
  echo "Installing SteamCMD..."

  apt_install wget ca-certificates software-properties-common lib32gcc-s1 lib32stdc++6

  # Create SteamCMD directory in /app/Data/Steam for persistence
  STEAMCMD_DIR="/app/Data/Steam"
  mkdir -p "$STEAMCMD_DIR"
  
  # Create .steam directory for credential persistence
  mkdir -p "$STEAMCMD_DIR/.steam/steamcmd"
  
  # Set permissions to allow the app to use SteamCMD
  chmod -R 755 "$STEAMCMD_DIR"

  if [[ -x "$STEAMCMD_DIR/steamcmd.sh" ]]; then
    echo "SteamCMD already present. Skipping download."
  else
    echo "Downloading SteamCMD..."
    tmpdir="$(mktemp -d)"
    (
      cd "$tmpdir"
      wget -qO steamcmd_linux.tar.gz "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz"
      tar -xzf steamcmd_linux.tar.gz -C "$STEAMCMD_DIR"
    )
    rm -rf "$tmpdir"
    chmod +x "$STEAMCMD_DIR/steamcmd.sh"
    echo "SteamCMD installed to $STEAMCMD_DIR."
  fi
  
  # Ensure .steam directory exists for credential persistence
  mkdir -p "$STEAMCMD_DIR/.steam/steamcmd"
  chmod -R 755 "$STEAMCMD_DIR/.steam"
}

# ---------- WINE ----------
install_wine() {
  echo "Installing WINE..."

  apt_install wine wine32 wine64 libwine fonts-wine winetricks cabextract unzip wget ca-certificates

  ensure_user "wine" "/home/wine"
  ensure_dir_owned "/home/wine/.wine" "wine" "wine"

  # Initialize wine prefix once (headless-friendly)
  if [[ -f "/home/wine/.wine/system.reg" ]]; then
    echo "WINE prefix already initialized. Skipping winecfg."
  else
    echo "Initializing WINE prefix..."
    # Suppress noisy logs and run a minimal init
    su -s /bin/bash -c 'WINEDEBUG=-all WINEARCH=win64 wineboot -u || true' wine
  fi

  echo "WINE setup complete."
}

# ---------- Conditional execution (only if first run or explicitly requested again) ----------
if [[ ! -f "$MARKER_FILE" ]]; then
  if [[ "${STEAMCMD:-0}" == "1" ]]; then
    echo "STEAMCMD=1 detected, installing SteamCMD..."
    install_steamcmd
  else
    echo "STEAMCMD not set to 1, skipping SteamCMD installation."
  fi

  if [[ "${WINE:-0}" == "1" ]]; then
    echo "WINE=1 detected, installing WINE..."
    install_wine
  else
    echo "WINE not set to 1, skipping WINE installation."
  fi

  # Mark as completed
  date -Is > "$MARKER_FILE"
  echo "Setup steps completed. Marker written to $MARKER_FILE"
else
  # Even if previously completed, allow user to force re-run parts by setting REINSTALL=1
  if [[ "${REINSTALL:-0}" == "1" ]]; then
    echo "REINSTALL=1 set — re-running requested installers if toggled."
    if [[ "${STEAMCMD:-0}" == "1" ]]; then install_steamcmd; fi
    if [[ "${WINE:-0}" == "1" ]]; then install_wine; fi
    date -Is > "$MARKER_FILE"
  fi
fi

# ---------- Start app ----------
echo "Switching to /app..."
cd /app

echo "Starting LANCommander Server..."
exec dotnet LANCommander.Server.dll --docker