FROM mcr.microsoft.com/dotnet/sdk:9.0 AS base
WORKDIR /app

EXPOSE 1337
EXPOSE 213/udp
EXPOSE 35891/udp

COPY ./published /app
COPY entrypoint.sh /app/entrypoint.sh

# Make entrypoint script executable
RUN chmod +x /app/entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]