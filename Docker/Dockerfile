# syntax=docker/dockerfile:1

FROM ghcr.io/linuxserver/baseimage-ubuntu:jammy

# set version label
ARG BUILD_DATE
ARG VERSION
ARG TARGETPLATFORM
LABEL build_version="LANCommander version:- ${VERSION} Build-date:- ${BUILD_DATE}, container based on github.com/LinuxServer/baseimage-ubuntu"
LABEL maintainer="bashNinja, pathartl"

RUN case "$TARGETPLATFORM" in \
      "linux/amd64") export BUILD_ARCH="linux-x64" ;; \
      "linux/arm64") export BUILD_ARCH="linux-arm64" ;; \
      *) echo "Unknown TARGETPLATFORM: $TARGETPLATFORM" && exit 1 ;; \
    esac && \
    echo "Using BUILD_ARCH=$BUILD_ARCH" && \
    cp -r ./build/$BUILD_ARCH /app/LANCommander

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
