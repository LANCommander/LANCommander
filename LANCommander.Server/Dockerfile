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
    xz-utils \
  && rm -rf /var/lib/apt/lists/*

# A static LGPL ffmpeg build rather than Debian's "ffmpeg" package, which is a GPL build.
# No libx264 here - MediaToolService probes for an encoder and uses libopenh264.
# Only ffmpeg itself: ffprobe is never invoked by the server.
RUN case "$(dpkg --print-architecture)" in \
    arm64) ffmpeg_arch="linuxarm64" ;; \
    *) ffmpeg_arch="linux64" ;; \
  esac \
  && ffmpeg_tmp="$(mktemp -d)" \
  && curl -fsSL "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-${ffmpeg_arch}-lgpl.tar.xz" -o "$ffmpeg_tmp/ffmpeg.tar.xz" \
  && tar -xf "$ffmpeg_tmp/ffmpeg.tar.xz" -C "$ffmpeg_tmp" \
  && find "$ffmpeg_tmp" -type f -name ffmpeg -perm -u+x -exec install -m 0755 {} /usr/local/bin/ffmpeg \; \
  && rm -rf "$ffmpeg_tmp" \
  && test -x /usr/local/bin/ffmpeg

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