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
        lib32stdc++6 \
        libc6-i386 \
        libstdc++6:i386 \
        libgcc-s1:i386 \
        lib32tinfo6 \
        libtinfo6:i386 \
        libcurl4-gnutls-dev:i386
    
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
    
    # Create symlink for easy access
    ln -sf /home/steam/steamcmd/steamcmd.sh /usr/local/bin/steamcmd
    
    echo "SteamCMD installed successfully!"
}

# Check if SteamCMD should be installed
if [ "$STEAMCMD" = "1" ]; then
    echo "STEAMCMD=1 detected, installing SteamCMD..."
    install_steamcmd
else
    echo "STEAMCMD not set to 1, skipping SteamCMD installation"
fi

# Start the LANCommander server
echo "Starting LANCommander Server..."
exec dotnet LANCommander.Server.dll --docker 