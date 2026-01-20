#!/bin/bash
# Set STEAMCONFIG to persist credentials in /app/Data/Steam/.steam
export STEAMCONFIG="/app/Data/Steam/.steam"
exec /app/Data/Steam/steamcmd.sh "$@"