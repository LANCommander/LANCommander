FROM mcr.microsoft.com/dotnet/sdk:9.0 AS base
WORKDIR /app

EXPOSE 1337
EXPOSE 35891/udp

COPY . ./

RUN dotnet restore
RUN dotnet publish -o out

RUN ls -lah ./

COPY ./published/ ./

ENTRYPOINT ["dotnet", "LANCommander.Server.dll"]