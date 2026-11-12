# syntax=docker/dockerfile:1.7
# El lanzador de robots: UN contenedor para todos los robots de la máquina. Arranca cada robot solo mientras tiene trabajo en Viriato
# y lo deja apagarse cuando vacía su cola. Los robots no van dentro de la imagen: se montan en /robots (ver docs/lanzador-de-robots.md).
#
# Se construye desde la carpeta backend/ (el contexto es backend/), con una de dos variantes:
#   docker build -f sdk/Viriato.Rpa.Lanzador/Dockerfile --target ligero    -t viriato-lanzador:ligero .
#   docker build -f sdk/Viriato.Rpa.Lanzador/Dockerfile --target navegador -t viriato-lanzador:navegador --platform linux/amd64 .
#
#   ligero     solo .NET: para robots que no abren navegador.
#   navegador  además Microsoft Edge, una pantalla virtual por robot (Xvfb + openbox) y lo necesario para verla y grabarla
#              (x11vnc, noVNC, ffmpeg). Edge para Linux solo existe para amd64.

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/Viariato.ApiContracts/Viariato.ApiContracts.csproj src/Viariato.ApiContracts/
COPY sdk/Viriato.Rpa.Lanzador/Viriato.Rpa.Lanzador.csproj sdk/Viriato.Rpa.Lanzador/
RUN dotnet restore sdk/Viriato.Rpa.Lanzador/Viriato.Rpa.Lanzador.csproj
COPY src/Viariato.ApiContracts/ src/Viariato.ApiContracts/
COPY sdk/Viriato.Rpa.Lanzador/ sdk/Viriato.Rpa.Lanzador/
RUN dotnet publish sdk/Viriato.Rpa.Lanzador/Viriato.Rpa.Lanzador.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0 AS ligero
# Sin privilegios: ni el lanzador ni los robots que arranca necesitan ser root.
RUN useradd --create-home --uid 1000 robot \
 && mkdir -p /robots /home/robot/.cache/selenium \
 && chown -R robot:robot /home/robot
WORKDIR /lanzador
COPY --from=build /app .
USER robot
# El catálogo se relee cuando cambia; en una carpeta montada los avisos del sistema de archivos no llegan, así que se mira cada poco.
ENV LANZADOR_CATALOGO=/robots/robots.json \
    DOTNET_USE_POLLING_FILE_WATCHER=1
ENTRYPOINT ["dotnet", "Viriato.Rpa.Lanzador.dll"]

FROM ligero AS navegador
USER root
RUN apt-get update \
 && apt-get install -y --no-install-recommends ca-certificates curl gnupg xvfb xauth openbox fonts-liberation ffmpeg x11vnc novnc websockify \
 && curl -fsSL https://packages.microsoft.com/keys/microsoft.asc | gpg --dearmor -o /usr/share/keyrings/microsoft-edge.gpg \
 && echo "deb [arch=amd64 signed-by=/usr/share/keyrings/microsoft-edge.gpg] https://packages.microsoft.com/repos/edge stable main" \
      > /etc/apt/sources.list.d/microsoft-edge.list \
 && apt-get update \
 && apt-get install -y --no-install-recommends microsoft-edge-stable \
 && rm -rf /var/lib/apt/lists/*
# Edge dentro de un contenedor arranca sin su «sandbox» (el contenedor ya es el aislamiento). Selenium lanza el binario tal cual,
# así que el binario es un envoltorio que añade esas opciones.
RUN mv /opt/microsoft/msedge/msedge /opt/microsoft/msedge/msedge.real
COPY sdk/Viriato.Rpa.Lanzador/edge-wrapper.sh /opt/microsoft/msedge/msedge
RUN chmod +x /opt/microsoft/msedge/msedge
USER robot
EXPOSE 6080
