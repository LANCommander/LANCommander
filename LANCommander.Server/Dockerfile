FROM mcr.microsoft.com/dotnet/sdk:9.0 AS base
WORKDIR /app

EXPOSE 1337
EXPOSE 213/udp
EXPOSE 35891/udp

ARG TARGETARCH
COPY ./published-${TARGETARCH} /app
COPY entrypoint.sh /app/entrypoint.sh
COPY steamcmd.sh /usr/local/bin/steamcmd

RUN apt-get update \
  && apt-get install -y --no-install-recommends \
    ca-certificates \
    curl \
    ffmpeg \
  && rm -rf /var/lib/apt/lists/*

RUN case "$(dpkg --print-architecture)" in \
    amd64) ytdlp_asset="yt-dlp_linux" ;; \
    arm64) ytdlp_asset="yt-dlp_linux_aarch64" ;; \
    armhf) ytdlp_asset="yt-dlp_linux_armv7l" ;; \
    *) ytdlp_asset="yt-dlp_linux" ;; \
  esac \
  && curl -fsSL "https://github.com/yt-dlp/yt-dlp/releases/latest/download/${ytdlp_asset}" -o /usr/local/bin/yt-dlp \
  && chmod +x /usr/local/bin/yt-dlp

RUN chmod +x /app/entrypoint.sh /usr/local/bin/steamcmd

ENTRYPOINT ["/app/entrypoint.sh"]