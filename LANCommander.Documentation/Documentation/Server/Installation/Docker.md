---
id: docker
title: Docker Deployment
sidebar_label: Docker
description: Run LANCommander using Docker or Docker Compose
---

LANCommander can be run completely inside Docker containers, making it easy to deploy on a home server, NAS, or cloud host.

This guide walks through:

- A quick “one-shot” `docker run` example  
- A recommended `docker-compose.yml` setup  

---

## Prerequisites

Before you begin, you should have:

- A machine running Linux or Windows with:
  - Docker Engine 20.x+ installed  
  - (Optional but recommended) Docker Compose v2+
- A location on disk where LANCommander can store:
  - Application data (database, configs, etc.)  
  - Game/asset storage

Example host paths:

- `/srv/lancommander/data` – application data
- `/srv/lancommander/games` – game archives / installs

---

## Quick Start with `docker run`

For quick testing or a small single-host deployment, you can run the LANCommander server with a single `docker run` command.

```bash
docker run -d \
  --name lancommander \
  -p 1337:1337 \
  -p 35891:35891 \
  -p 213:213 \
  -v ./LANCommander:/app/Data \
  lancommander/lancommander:latest
```

## Recommended: Docker Compose
For a more maintainable setup (especially if you add additional services later like a database, cache, or sidecar worker), use Docker Compose.

Sample `docker-compose.yml`:

```yaml
services:
  lancommander:
    image: lancommander/lancommander:latest
    container_name: lancommander
    environment:
      - PUID=1000
      - PGID=1000
      - TZ=Etc/UTC
      # Uncomment the line below to install SteamCMD
      # - STEAMCMD=1
      # Uncomment the line below to install WINE
      # - WINE=1
    volumes:
      - /path/to/app/data:/app/Data
    ports:
      - 1337:1337/tcp   # Webinterface
      - 35891:35891/udp # Beacon Broadcast
      - 213:213/udp # IPX Relay
    restart: unless-stopped
```