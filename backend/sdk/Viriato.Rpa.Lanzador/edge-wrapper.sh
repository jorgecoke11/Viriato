#!/bin/sh
# Edge, tal como lo arranca Selenium dentro del contenedor del lanzador. Selenium no deja pasar opciones desde el robot, así que
# este envoltorio añade las que hacen falta en un contenedor (que ya es el aislamiento, y cuyo /dev/shm es pequeño).
#
# EDGE_IGNORAR_ERRORES_DE_CERTIFICADO=true (apagado por defecto) hace que Edge acepte certificados no válidos. Solo para un portal
# cuyo certificado está caducado y no se puede arreglar: el usuario y la contraseña viajan por una conexión SIN verificar.
extra=""
if [ "${EDGE_IGNORAR_ERRORES_DE_CERTIFICADO:-false}" = "true" ]; then
    extra="--ignore-certificate-errors"
fi

# shellcheck disable=SC2086
exec /opt/microsoft/msedge/msedge.real --no-sandbox --disable-dev-shm-usage --disable-gpu $extra "$@"
