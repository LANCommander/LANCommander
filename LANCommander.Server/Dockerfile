FROM mcr.microsoft.com/dotnet/sdk:9.0 AS base
WORKDIR /app

EXPOSE 1337
EXPOSE 213/udp
EXPOSE 35891/udp

ARG TARGETARCH
COPY ./published-${TARGETARCH} /app
COPY entrypoint.sh /app/entrypoint.sh
COPY steamcmd.sh /usr/local/bin/steamcmd

# Make entrypoint script executable
RUN chmod +x /app/entrypoint.sh
RUN chmod +x /usr/local/bin/steamcmd

ENTRYPOINT ["/app/entrypoint.sh"]