#!/bin/bash
set -e

# Function to install SteamCMD
install_steamcmd() {
    echo "Installing SteamCMD..."
    
    # Update package list
    apt-get update
    
    # Install required dependencies
    apt-get install -y \
        wget \
        ca-certificates \
        software-properties-common \
        lib32gcc-s1 \
        lib32stdc++6
    
    # Create steam user and directory
    useradd -m -d /home/steam steam
    mkdir -p /home/steam/steamcmd
    chown -R steam:steam /home/steam/steamcmd
    
    # Download and install SteamCMD
    cd /home/steam/steamcmd
    wget -qO- "https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz" | tar -xz
    
    # Set permissions
    chown -R steam:steam /home/steam/steamcmd
    chmod +x /home/steam/steamcmd/steamcmd.sh

    cd -
    
    echo "SteamCMD installed successfully!"
}

# Function to install WINE
install_wine() {
    echo "Installing WINE..."
    
    # Update package list
    apt-get update
    
    # Install WINE and dependencies
    apt-get install -y \
        wine \
        wine32 \
        wine64 \
        libwine \
        fonts-wine \
        winetricks \
        cabextract \
        unzip \
        wget \
        ca-certificates
    
    # Create wine user and directory
    useradd -m -d /home/wine wine
    mkdir -p /home/wine/.wine
    chown -R wine:wine /home/wine/.wine
    
    # Set up WINE environment
    su - wine -c "winecfg /v" || true
    
    echo "WINE installed successfully!"
}

# Check if SteamCMD should be installed
if [ "$STEAMCMD" = "1" ]; then
    echo "STEAMCMD=1 detected, installing SteamCMD..."
    install_steamcmd
else
    echo "STEAMCMD not set to 1, skipping SteamCMD installation"
fi

# Check if WINE should be installed
if [ "$WINE" = "1" ]; then
    echo "WINE=1 detected, installing WINE..."
    install_wine
else
    echo "WINE not set to 1, skipping WINE installation"
fi

cd /app

# Start the LANCommander server
echo "Starting LANCommander Server..."
exec dotnet LANCommander.Server.dll --docker 