#!/bin/sh
# Edge, tal como lo arranca Selenium dentro del contenedor del lanzador. Selenium no deja pasar opciones desde el robot, así que
# este envoltorio añade las que hacen falta en un contenedor (que ya es el aislamiento, y cuyo /dev/shm es pequeño).
#
# Aceptar un certificado caducado no va aquí: lo decide el robot al abrir Edge (SeleniumUtilities.StartDriver), normalmente con un
# parámetro de su proceso en Viriato, para poder quitarlo sin tocar el robot.

exec /opt/microsoft/msedge/msedge.real --no-sandbox --disable-dev-shm-usage --disable-gpu "$@"
