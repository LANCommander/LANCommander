FROM mcr.microsoft.com/dotnet/sdk:9.0 AS base
WORKDIR /app

EXPOSE 1337
EXPOSE 35891/udp

COPY ./published /app

ENTRYPOINT ["dotnet", "LANCommander.Server.dll"]