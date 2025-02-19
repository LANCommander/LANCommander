# syntax=docker/dockerfile:1

FROM ghcr.io/linuxserver/baseimage-ubuntu:jammy

# set version label
ARG BUILD_DATE
ARG VERSION
ARG TARGETPLATFORM
LABEL build_version="LANCommander version:- ${VERSION} Build-date:- ${BUILD_DATE}, container based on github.com/LinuxServer/baseimage-ubuntu"
LABEL maintainer="bashNinja, pathartl"

# Debug - List contents of build directory
RUN ls -la /

# Create app directory
RUN mkdir -p /app/LANCommander

# Set architecture-specific build directory
COPY ./build/linux-x64/* /app/LANCommander/
COPY ./build/linux-arm64/* /app/LANCommander/

# Debug - List contents after copy
RUN ls -la /app/LANCommander/

# Remove unused architecture
RUN case "$TARGETPLATFORM" in \
      "linux/amd64") rm -rf /app/LANCommander/linux-arm64 ;; \
      "linux/arm64") rm -rf /app/LANCommander/linux-x64 ;; \
      *) echo "Unknown TARGETPLATFORM: $TARGETPLATFORM" && exit 1 ;; \
    esac

# Debug - List contents after architecture cleanup
RUN ls -la /app/LANCommander/

# copy local files
COPY root/ /

# install app
RUN \
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
