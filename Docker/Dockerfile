# syntax=docker/dockerfile:1

FROM ghcr.io/linuxserver/baseimage-ubuntu:jammy

# set version label
ARG BUILD_DATE
ARG VERSION
ARG TARGETPLATFORM
LABEL build_version="LANCommander version:- ${VERSION} Build-date:- ${BUILD_DATE}, container based on github.com/LinuxServer/baseimage-ubuntu"
LABEL maintainer="bashNinja, pathartl"

# Create app directory
RUN mkdir -p /app/LANCommander
# RUN mkdir -p /build/linux-x64
# RUN mkdir -p /build/linux-arm64

# Set architecture-specific build directory
# COPY ./build/linux-x64/* /build/linux-x64
# COPY ./build/linux-arm64/* /build/linux-arm64

# Remove unused architecture
# RUN case "$TARGETPLATFORM" in \
#       "linux/amd64") cp -r /build/linux-x64/* /app/LANCommander ;; \
#       "linux/arm64") cp -r /build/linux-arm64/* /app/LANCommander ;; \
#       *) echo "Unknown TARGETPLATFORM: $TARGETPLATFORM" && exit 1 ;; \
#     esac && \
#     rm -rf /build

COPY ./build/linux-x64 /app/LANCommander

# copy local files
COPY root/ /

# install app
RUN \
  echo "**** install packages ****" && \
    apt-get update && \
    apt-get -y install libicu-dev && \
  echo "**** setup lancommander ****" && \
    chmod +x /app/LANCommander/LANCommander.Server && \
    ls -alh /app/LANCommander && \
  echo "**** setup symlinks ****" && \
    ln -s /config/Uploads/ /app/LANCommander/Uploads && \
    ln -s /config/Updates/ /app/LANCommander/Updates && \
    ln -s /config/Launcher/ /app/LANCommander/Launcher && \
    ln -s /config/Saves/ /app/LANCommander/Saves && \
    ln -s /config/Media/ /app/LANCommander/Media && \
    ln -s /config/Logs/ /app/LANCommander/Logs && \
    ln -s /config/Backups/ /app/LANCommander/Backups && \
    ln -s /config/Settings.yml /app/LANCommander/Settings.yml && \
    ln -s /config/LANCommander.db /app/LANCommander/LANCommander.db && \
    ln -s /config/internal.log /app/LANCommander/internal.log

# ports and volumes
EXPOSE 1337/tcp
EXPOSE 35891/udp

VOLUME /config
