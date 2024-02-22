# syntax=docker/dockerfile:1

FROM ghcr.io/linuxserver/baseimage-ubuntu:jammy

# set version label
ARG BUILD_DATE
ARG VERSION="0.6.0"
LABEL build_version="LANCommander version:- ${VERSION} Build-date:- ${BUILD_DATE}, container based on github.com/LinuxServer/baseimage-ubuntu"
LABEL maintainer="bashNinja"

# copy local files
COPY root/ /

# grab app
ADD "https://github.com/LANCommander/LANCommander/releases/download/v${VERSION}/LANCommander-Linux-x64-v${VERSION}.zip" "/tmp"

# install app
RUN \
  echo "**** install packages ****" && \
    apt-get update && \
    apt-get install -y --no-install-recommends \
      jq \
      libicu70 \
      sqlite3 \
      unzip && \
  echo "**** setup lancommander ****" && \
    unzip "/tmp/LANCommander-Linux-x64-v${VERSION}.zip" -d /tmp && \
    tar -xvf "/tmp/LANCommander-Linux-x64-v${VERSION}.tar" -C /tmp && \
    mv /tmp/_Build_Linux/ /app/LANCommander && \
    chmod +x /app/LANCommander/LANCommander && \
    ls -alh /app/LANCommander && \
  echo "**** setup symlinks ****" && \
    ln -s /config/Uploads/ /app/LANCommander/Uploads && \
    ln -s /config/Updates/ /app/LANCommander/Updates && \
    ln -s /config/Saves/ /app/LANCommander/Saves && \
    ln -s /config/Media/ /app/LANCommander/Media && \
    ln -s /config/Logs/ /app/LANCommander/Logs && \
    ln -s /config/Backups/ /app/LANCommander/Backups && \
    ln -s /config/Icon/ /app/LANCommander/Icon && \
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
EXPOSE 1337

VOLUME /config
