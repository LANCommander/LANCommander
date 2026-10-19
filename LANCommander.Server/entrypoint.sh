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

# ---------- SteamCMD ----------
install_steamcmd() {
  echo "Installing SteamCMD..."

  if [[ "${LANCOMMANDER_FULL_IMAGE:-0}" == "1" ]]; then
    STEAMCMD_DIR="/app/Data/Steam"
    mkdir -p "$STEAMCMD_DIR/.steam/steamcmd"
    if [[ ! -x "$STEAMCMD_DIR/steamcmd.sh" ]]; then
      cp -a /opt/steamcmd/. "$STEAMCMD_DIR/"
      chmod -R 755 "$STEAMCMD_DIR"
    fi
    echo "Full image detected; SteamCMD is available."
    return 0
  fi

  apt-get update
  apt-get install -y --no-install-recommends wget ca-certificates software-properties-common lib32gcc-s1 lib32stdc++6

  STEAMCMD_DIR="/app/Data/Steam"
  mkdir -p "$STEAMCMD_DIR/.steam/steamcmd"
  chmod -R 755 "$STEAMCMD_DIR"

  if [[ -x "$STEAMCMD_DIR/steamcmd.sh" ]]; then
    echo "SteamCMD already present."
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
  fi

  mkdir -p "$STEAMCMD_DIR/.steam/steamcmd"
  chmod -R 755 "$STEAMCMD_DIR/.steam"
}

# ---------- WINE ----------
install_wine() {
  echo "Installing WINE..."

  if [[ "${LANCOMMANDER_FULL_IMAGE:-0}" != "1" ]]; then
    if ! dpkg --print-foreign-architectures | grep -qx i386; then
      dpkg --add-architecture i386
    fi

    apt-get update
    apt-get install -y --no-install-recommends wine wine32:i386 wine64 libwine fonts-wine cabextract unzip wget curl ca-certificates
  fi

  if [[ -x "/usr/local/bin/winetricks" ]]; then
    echo "winetricks already installed. Skipping download."
  else
    curl -fsSL "https://raw.githubusercontent.com/Winetricks/winetricks/master/src/winetricks" -o /usr/local/bin/winetricks
    chmod +x /usr/local/bin/winetricks
  fi

  ensure_user "wine" "/home/wine"
  ensure_dir_owned "/home/wine/.wine" "wine" "wine"

  if [[ -f "/home/wine/.wine/system.reg" ]]; then
    echo "WINE prefix already initialized. Skipping winecfg."
  else
    echo "Initializing WINE prefix..."
    su -s /bin/bash -c 'WINEDEBUG=-all WINEARCH=win64 wineboot -u || true' wine
  fi

  echo "WINE setup complete."
}

# ---------- Conditional execution (only if first run or explicitly requested again) ----------
if [[ ! -f "$MARKER_FILE" ]]; then
  if [[ "${STEAMCMD:-0}" == "1" ]]; then
    install_steamcmd
  fi

  if [[ "${WINE:-0}" == "1" ]]; then
    install_wine
  fi

  date -Is > "$MARKER_FILE"
  echo "Setup steps completed. Marker written to $MARKER_FILE"
else
  if [[ "${REINSTALL:-0}" == "1" ]]; then
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