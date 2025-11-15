FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /script

RUN mkdir -p /app

COPY entrypoint.sh /app/entrypoint.sh
COPY steamcmd.sh /usr/local/bin/steamcmd

COPY . .
RUN dotnet publish LANCommander.Server/LANCommander.Server.csproj -c Debug -o /app

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

EXPOSE 1337
EXPOSE 213/udp
EXPOSE 35891/udp

# Make entrypoint script executable
RUN chmod +x /app/entrypoint.sh
RUN chmod +x /usr/local/bin/steamcmd

RUN mkdir -p /vsdbg
RUN curl -sSL https://aka.ms/getvsdbgsh | bash /dev/stdin -v latest -l /vsdbg

ENTRYPOINT ["/app/entrypoint.sh"]