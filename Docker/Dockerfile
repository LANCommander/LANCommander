# syntax=docker/dockerfile:1

FROM ghcr.io/linuxserver/baseimage-ubuntu:jammy

# set version label
ARG BUILD_DATE
ARG VERSION
LABEL build_version="LANCommander version:- ${VERSION} Build-date:- ${BUILD_DATE}, container based on github.com/LinuxServer/baseimage-ubuntu"
LABEL maintainer="bashNinja, pathartl"

# copy local files
COPY root/ /
COPY ./build/linux-x64 /app/LANCommander

# install app
RUN \
  echo "**** install packages ****" && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
      jq \
      libicu70 \
      sqlite3 \
      unzip \
      python3 && \
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
    ln -s /config/internal.log /app/LANCommander/internal.log && \
  echo "**** cleanup ****" && \
  apt-get -y autoremove && \
  apt-get clean && \
  rm -rf \
    /tmp/* \
    /var/lib/apt/lists/* \
    /var/tmp/*

# ports and volumes
EXPOSE 1337/tcp
EXPOSE 35891/udp

VOLUME /config
