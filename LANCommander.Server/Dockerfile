FROM mcr.microsoft.com/dotnet/sdk:9.0.1 AS base
WORKDIR /app

EXPOSE 1337
EXPOSE 35891/udp

COPY . ./

RUN dotnet restore
RUN dotnet publish -o out

COPY published/ ./

ENTRYPOINT ["dotnet", "LANCommander.Server.dll"]