# Permisos: quién puede hacer qué

Cada permiso es `módulo.acción`. Se declaran **en un solo sitio** (`backend/src/Viariato.Shared/Authorization/Permissions.cs`); al arrancar, el
servidor los crea en la base de datos y **le da todos al rol Admin**. Declarar uno nuevo es lo único que hace falta para que un
administrador lo tenga. El resto de roles se editan en *Configuración → Roles*.

Hay dos clases: los de **usar** la plataforma y los de **gestionarla**.

## Para usar

| Permiso | Qué permite |
|---|---|
| `casos.read` | Ver el panel, el listado y un caso (de los procesos a los que estás asignado). |
| `casos.crear` | Crear un caso eligiendo un creador (botón «+» del panel, *Listado → Nuevo caso*). |
| `casos.manage` | Operar un caso: pausar, reanudar, reprocesar, cambiar sus datos, añadir documentos y evidencias. |
| `casos.review` | Resolver las revisiones humanas. |
| `casos.cancelar` | Cancelar un caso o una ejecución (también la acción masiva «Cancelar»). |
| `casos.prioridad` | Cambiar el puesto de una ejecución en la cola de su servicio. |
| `casos.masivas` | Marcar casos en el Listado y usar las acciones sobre varios a la vez. Cada acción pide además su propio permiso (cancelar → `casos.cancelar`). |
| `casos.descargar` | Descargar en un zip los documentos de muchos casos ([detalle](descarga-masiva-de-documentos.md)). |
| `flujos.parametros` | Cambiar desde el panel los parámetros del proceso que el administrador abrió ([detalle](parametros-del-proceso.md)). |

Todo esto está además limitado por la **asignación al proceso**: sin ella el servidor responde como si el proceso no existiera.

## Para gestionar

| Permiso | Qué permite |
|---|---|
| `flujos.read` | Ver la configuración de los procesos. |
| `flujos.manage` | Crear y configurar procesos (versiones, pasos, estados, tipos, asignaciones, almacenamiento, tipos de documento) y **crear un caso a mano** sin creador. |
| `flujos.creadores` | Configurar los creadores de caso de un proceso ([detalle](creadores-de-caso.md)). |
| `rpa.manage` | Equipos, servicios y despliegues. |
| `rpa.despacho` | El despacho de cada equipo, sus plantillas y su cola ([detalle](despacho-por-instancias.md)). |
| `rpa.credenciales` | Las credenciales que piden los robots. Aparte del resto de la flota porque son secretos. |
| `users.read`, `users.manage`, `roles.manage` | Usuarios, roles y permisos. |
| `markets.manage` | Mercados. |

## Los roles

- **Admin**: todos los permisos, siempre (se completan solos al arrancar).
- **User**: el rol que recibe quien se registra. Empieza con los permisos de **usar** (`Permissions.ParaElRolUser`: `casos.read`, `casos.manage`,
  `casos.review`, `casos.crear`, `casos.cancelar`, `casos.prioridad`, `casos.masivas`, `casos.descargar`, `flujos.parametros`) y ninguno de gestionar. Un
  administrador puede cambiarlo como cualquier rol.

### Cómo se reparten al actualizar

Al arrancar, a `User` se le dan los permisos por defecto **solo** si no tiene ninguno (un rol que nadie ha configurado) y, después, cada
permiso **nuevo** de esa lista cuando se estrena. Lo que un administrador quitó o añadió **no se toca**: un permiso que ya existía nunca se
devuelve. (Vaciar el rol por completo lo deja sin nada hasta que alguien le ponga algo; al siguiente arranque recuperaría los por defecto.)

## Añadir un permiso

1. Una constante en `Permissions` y en `All` (con un comentario de qué guarda).
2. `RequireAuthorization(Permissions.Lo)` en el endpoint (o `Permiso` en una acción masiva).
3. Si es de uso, añadirlo a `ParaElRolUser`.
4. En el front, `can('modulo.accion')` donde se muestra el botón o el enlace del menú.

Los tests de `PermisosFlowTests` prueban cada puerta: un usuario normal usa la plataforma y no puede configurarla, y cada permiso fino se pide de
verdad.
