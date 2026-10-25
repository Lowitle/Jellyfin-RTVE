# RTVE Play para Jellyfin

Plugin no oficial para **Jellyfin 10.11** que replica el catálogo a la carta de [RTVE Play](https://www.rtve.es/play/)
dentro de una biblioteca de Jellyfin. El plugin **no descarga vídeo**: crea archivos `.strm` que apuntan a
las URLs de streaming de RTVE, junto con los metadatos `.nfo`, las carátulas y los subtítulos en SRT.

> **Aviso legal**: este proyecto no está afiliado, patrocinado ni respaldado por RTVE. Todo el contenido
> reproducido sigue alojado en los servidores de RTVE y sujeto a sus condiciones de uso. Úsalo únicamente
> con contenido al que tengas derecho de acceso.

## Instalación rápida

Añade este repositorio en **Panel de control → Plugins → Repositorios → +** y instala **RTVE Play** desde el
catálogo (a partir de ahí se actualiza sola con cada versión publicada):

```
https://raw.githubusercontent.com/Lowitle/Jellyfin-RTVE/main/manifest.json
```

## Características

- **Programas configurables**: se indica el id numérico (`111570`), el slug de la página (`saber-vivir`)
  o la URL completa del programa, y el plugin resuelve el resto.
- **Catálogo navegable**: el botón **Ver todos los programas** lista el catálogo paginado de RTVE
  (solo programas de televisión) con casillas para añadir varios de golpe.
- **Series y episodios**: agrupa los vídeos de cada programa en una serie, con temporadas y numeración
  de episodios (usa `seasonNumber`/`episode` de RTVE cuando existen y, si no, ordena por fecha).
- **MP4 progresivo con fallback a HLS**: intenta primero la URL MP4 (`PreferMp4`), lo que permite *seek*
  y transcodificación directa en Jellyfin; si no hay, usa la playlist HLS.
- **Metadatos**: `tvshow.nfo` y `<episodio>.nfo` con título, sinopsis, fecha de emisión, duración,
  géneros y director; `poster.jpg` y `fanart.jpg` desde las imágenes del programa.
- **Subtítulos** (opcionales): descarga los WebVTT de RTVE y los convierte a SRT junto a cada episodio
  (`*.es.srt`, `*.en.srt`…). Por defecto no se descarga ninguno: se marcan con casillas uno o varios
  idiomas, o **todos los idiomas disponibles** para bajar cada pista que publique RTVE.
- **Comprobación de la carpeta de destino**: el botón **Comprobar carpeta** dice si Jellyfin puede
  escribir en ella y, si no, propone carpetas escribibles del servidor.
- **Biblioteca en un clic**: el botón **Crear biblioteca «RTVE Play»** da de alta la carpeta de destino
  como biblioteca de tipo *Programas de TV* (o añade la carpeta a la biblioteca si ya existe), sin pasar
  por el asistente de Jellyfin.
- **Filtro de DRM opcional**: desactivado por defecto, porque RTVE marca con `hasDRM` buena parte del
  catálogo aunque después sirva el MP4/HLS sin cifrar.
- **Reintentos**: las descargas repiten la petición cuando RTVE responde 429/5xx (límite de peticiones)
  con una espera creciente, antes de dar un subtítulo o un catálogo por perdido.
- **Tarea programada** `Actualizar RTVE Play` con intervalo configurable y ejecución al arrancar, además
  de un botón **Ejecutar ahora** en la página de configuración.

## Requisitos

- Jellyfin **10.11.x** (compilado contra `Jellyfin.Controller` 10.11).
- Una carpeta escribible por Jellyfin para el plugin, añadida como biblioteca de tipo **Programas de TV**.
  Lo recomendable es una **biblioteca propia** (ver *Carpeta de destino: biblioteca separada*); el botón
  **Crear biblioteca «RTVE Play»** de la página de configuración la da de alta por ti.
- Acceso de red a `www.rtve.es` / `api.rtve.es`. El catálogo de RTVE Play está geobloqueado fuera de
  España para algunos contenidos: las URLs se generan igualmente, pero la reproducción puede fallar.

## Instalación

### Opción A: repositorio de plugins propio (recomendada)

El repositorio incluye un `manifest.json` que Jellyfin puede leer como repositorio de plugins, de forma que
el plugin se instala y **se actualiza** desde el catálogo, sin copiar archivos a mano.

1. El proyecto ya está publicado en <https://github.com/Lowitle/Jellyfin-RTVE> y la release **v1.0.0.0** está
   disponible (ver *Publicación* para entender el flujo).
2. En Jellyfin: **Panel de control → Plugins → Repositorios → +** y añade
   `https://raw.githubusercontent.com/Lowitle/Jellyfin-RTVE/main/manifest.json`
   (si tu rama por defecto fuera `master`, usa `master`).
3. Guarda, refresca el catálogo e instala **RTVE Play** (categoría *General*).
4. Reinicia Jellyfin cuando lo pida.

> El `manifest.json` versionado lo mantiene el workflow de release: `owner` es `Lowitle` y `versions[]` se
> rellena con cada tag publicado (`sourceUrl`, `checksum` MD5 y `timestamp`).

### Opción B: paquete compilado

1. Genera el paquete (ver *Compilación*), o descarga el `.zip` de la sección de *releases*.
2. Descomprime el contenido en una carpeta dentro del directorio de plugins de Jellyfin:
   - Linux: `/var/lib/jellyfin/plugins/RTVE Play_1.0.0.0/`
   - Windows: `C:\ProgramData\Jellyfin\Server\plugins\RTVE Play_1.0.0.0\`
   - Docker: `/config/plugins/RTVE Play_1.0.0.0/`
3. Reinicia Jellyfin. El plugin aparece en **Panel de control → Plugins**.

### Opción C: compilación manual

```powershell
dotnet build -c Release
# o, para generar también dist/rtveplay_1.0.0.0.zip:
pwsh ./package.ps1
```

Copia `Jellyfin.Plugin.RtvePlay/bin/Release/net9.0/Jellyfin.Plugin.RtvePlay.dll` al directorio de plugins
en una carpeta llamada `RTVE Play_1.0.0.0` y reinicia Jellyfin.

## Publicación

- `build.yaml` es el manifiesto para el [repositorio oficial de plugins de Jellyfin](https://github.com/jellyfin/jellyfin-plugin-repository)
  (`guid`, `version`, `targetAbi`, `framework`, `artifacts`). Debe mantenerse sincronizado con
  `Directory.Build.props` (`Version`) y con `Plugin.PluginId`. `owner` ya es `Lowitle`; solo hay que
  cambiarlo si algún día se envía al repositorio oficial.
- `manifest.json` (raíz) es el **repositorio de plugins propio**: un array JSON con `guid`, `name`,
  `description`, `overview`, `owner`, `category` y `versions[]` (`version`, `changelog`, `targetAbi`,
  `sourceUrl`, `checksum` MD5 del `.zip`, `timestamp`).
- `.github/workflows/build.yaml` compila, ejecuta las pruebas y sube `dist/rtveplay_<versión>.zip` como
  artefacto en cada push a `main`.
- `.github/workflows/release.yaml` se dispara con un tag `v*` (o a mano con *Run workflow*):
  1. comprueba que el tag coincide con `Version` de `Directory.Build.props`;
  2. ejecuta `dotnet test` y `package.ps1`;
  3. `scripts/Update-Manifest.ps1` calcula el MD5, añade la versión al `manifest.json` y lo commitea;
  4. crea la release con el `.zip` adjunto.

Publicación inicial (ya realizada el 25/10/2026):

```powershell
git init
git add .
git commit -m "RTVE Play 1.0.0.0"
git branch -M main
git remote add origin https://github.com/Lowitle/Jellyfin-RTVE.git
git push -u origin main
git tag v1.0.0.0
git push origin v1.0.0.0
```

El tag lanzó el workflow **Release**: build + 141 pruebas, release `v1.0.0.0` con `rtveplay_1.0.0.0.zip` y
commit automático del `manifest.json` (`checksum` MD5 `926CE03042BAD6AF413E533CBA058404`). La versión
**1.0.1.0** añade los subtítulos multi-idioma, el listado paginado de programas y la comprobación de la
carpeta de destino; la **1.0.2.0** añade el botón **Crear biblioteca**, deja el filtro de DRM desactivado
por defecto (con migración automática de la configuración guardada) y reintenta las descargas ante los
`429`/`5xx` de RTVE.

Para una versión nueva: sube `Version` en `Directory.Build.props` y `version` en `build.yaml`, haz commit y
publica el tag (`git tag v1.0.2.0; git push origin v1.0.2.0`). El manifiesto y la release se actualizan solos.

## Configuración

La página del plugin está en **Panel de control → Plugins → RTVE Play**.

### Programas

| Campo | Descripción |
| --- | --- |
| **Id, slug o URL del programa** | Acepta `111570`, `saber-vivir` o `https://www.rtve.es/play/videos/saber-vivir/`. |
| **Añadir programa** | Resuelve la referencia contra la API de RTVE y la guarda con su título. |
| **Buscar por título** | Filtra el catálogo por ese texto en el panel del listado (solo programas de televisión: los de radio de RTVE Play se descartan). |
| **Ver todos los programas** | Lista el catálogo completo **paginado**: marca las casillas que quieras y pulsa **Añadir seleccionados**. También hay «Seleccionar la página» y botones *Anterior* / *Siguiente*. |

Los programas ya añadidos aparecen marcados en el listado como *ya añadido* y no se pueden volver a
seleccionar. Recuerda pulsar **Guardar** al terminar.

### Biblioteca y opciones

| Campo | Por defecto | Descripción |
| --- | --- | --- |
| **Carpeta de destino** | *(vacío)* | Ruta absoluta tal y como la ve el servidor. Debe ser una carpeta propia añadida como biblioteca de tipo *Programas de TV* (ver abajo). |
| **Comprobar carpeta** | — | Comprueba que la carpeta existe y que Jellyfin puede escribir en ella; si falla, propone carpetas escribibles del servidor. |
| **Crear biblioteca «RTVE Play»** | — | Crea (o reutiliza) una biblioteca de tipo *Programas de TV* apuntando a la carpeta de destino. Si la carpeta ya está en una biblioteca de series, no hace nada; si pertenece a una de otro tipo, avisa del conflicto. |
| **Intervalo de actualización (horas)** | `12` | Periodicidad de la tarea programada (1–168). |
| **Máximo de vídeos por programa** | `0` | Limita cuántos episodios se replican por programa; `0` = sin límite. |
| **Preferir MP4 progresivo** | activado | Usa MP4 directo cuando existe (permite *seek*); si no, HLS. |
| **Omitir vídeos protegidos con DRM** | desactivado | Solo si algún vídeo concreto no se reproduce: RTVE marca con `hasDRM` casi todo el catálogo aunque después sirve un MP4/HLS **sin cifrar**, así que activarlo deja programas enteros sin episodios. |
| **Escribir metadatos .nfo** | activado | Genera `tvshow.nfo` y un `.nfo` por episodio. |
| **Borrar episodios que ya no están en el catálogo** | desactivado | Elimina `.strm`/`.nfo`/`.srt` sobrantes; úsalo solo si la carpeta es exclusiva del plugin. |

### Subtítulos

| Campo | Por defecto | Descripción |
| --- | --- | --- |
| **Descargar subtítulos** | activado | Descarga los WebVTT de RTVE y los convierte a SRT junto al `.strm`. Sin idiomas marcados no se escribe ningún `.srt`. |
| **Todos los idiomas disponibles de cada vídeo** | desactivado | Descarga todas las pistas que publique RTVE para cada episodio (ignora las casillas de idioma). |
| **Español / Inglés / Catalán / Gallego / Euskera** | ninguno | Se pueden marcar **varios**: se escribe un `.srt` por idioma (`*.es.srt`, `*.en.srt`…). Las casillas se deshabilitan si no se descargan subtítulos o si está activo «todos los idiomas». |

El botón **Ejecutar ahora** lanza la tarea sin esperar al siguiente ciclo.

### Carpeta de destino: biblioteca separada

El plugin solo escribe archivos (`.strm`, `.nfo`, `.jpg`, `.srt`); no inyecta nada en la base de datos de
Jellyfin. Por eso lo más limpio es darle una carpeta propia y añadirla como **biblioteca aparte** de tipo
*Programas de TV* (por ejemplo «RTVE Play»), en vez de reutilizar la carpeta de tu biblioteca de series:

- Puedes borrar, mover o reconstruir la carpeta sin tocar el resto de tu biblioteca.
- **Borrar episodios que ya no están en el catálogo** es seguro, porque esa carpeta solo contiene lo que
  genera el plugin.
- El botón **Crear biblioteca «RTVE Play»** de la página de configuración da de alta esa biblioteca
  apuntando a la carpeta de destino (o añade la carpeta a la biblioteca «RTVE Play» si ya existía).
- Jellyfin la indexa igual que cualquier otra biblioteca: carátulas, `tvshow.nfo`, episodios y subtítulos.
- **Docker**: monta un volumen dedicado (p. ej. `-v /srv/rtve-play:/media/rtve-play`) y escribe en
  *Carpeta de destino* la ruta **del contenedor** (`/media/rtve-play`), no la del host. La ruta debe ser
  escribible por el usuario de Jellyfin.
- **Instalación nativa**: cualquier ruta escribible, p. ej. `D:\Media\rtve-play` o `/srv/media/rtve-play`.
- Si prefieres que conviva con el resto de medios, una subcarpeta dentro del volumen de medios
  (`/media/rtve-play`) también sirve: solo tienes que añadirla como biblioteca independiente para no
  mezclarla con tus series.

### La tarea falla con «Access to the path ... is denied»

```
[ERR] Error executing Scheduled Task
System.UnauthorizedAccessException: Access to the path '/media/RTVE Play/' is denied.
```

Significa que la *Carpeta de destino* **no existe o no es escribible dentro del contenedor**. El plugin
no escribe en el host: escribe en la ruta que ve Jellyfin, así que esa ruta tiene que estar montada.

1. Pulsa **Comprobar carpeta** en la página del plugin: el resultado dice si existe, si es escribible y,
   si no lo es, qué carpetas del servidor sí admiten escritura.
2. Añade un volumen dedicado en el `docker-compose.yml` (o en `docker run`):

   ```yaml
   services:
     jellyfin:
       volumes:
         - /srv/rtve-play:/media/rtve-play      # host:contenedor
   ```

3. Escribe en *Carpeta de destino* la ruta **del contenedor** (`/media/rtve-play`), sin la del host.
4. Comprueba los permisos del usuario de Jellyfin (`PUID`/`PGID` en la imagen oficial):

   ```bash
   mkdir -p /srv/rtve-play
   sudo chown -R 1000:1000 /srv/rtve-play
   ```

5. Si la carpeta cuelga de un recurso de red (SMB/NFS), verifica que el montaje esté activo y permita
   crear archivos: algunos montajes se publican en solo lectura (`ro`).

En una instalación nativa basta con que el usuario que ejecuta Jellyfin tenga permiso de escritura en la
carpeta; el mensaje de la tarea indica ahora la ruta comprobada y las carpetas alternativas detectadas.

## Estructura generada

```
<Carpeta de destino>/
└── Saber Vivir/
    ├── tvshow.nfo
    ├── poster.jpg
    ├── fanart.jpg
    └── Season 1/
        ├── Saber Vivir - S01E01 - Título del episodio.strm
        ├── Saber Vivir - S01E01 - Título del episodio.nfo
        └── Saber Vivir - S01E01 - Título del episodio.es.srt   (uno por idioma marcado)
```

Los nombres se sanean (caracteres prohibidos, longitud máxima) y se desambiguan con `[2]`, `[3]`… cuando
dos episodios comparten título. Los archivos solo se reescriben si el contenido cambia.

## Tarea programada

- Nombre: **Actualizar RTVE Play** (categoría *RTVE Play*, clave `RtvePlayRefresh`).
- Disparadores: al arrancar el servidor y por intervalo (`RefreshIntervalHours`).
- También visible en **Panel de control → Programadas**, donde se puede ver el progreso y el resultado.

## Cómo funciona

1. `RtveClient` resuelve la referencia del programa (slug o `DC.identifier` de su página, o el id tal cual)
   y pagina sus vídeos con `/api/programas/{id}/videos?type=39816`.
2. Cada vídeo se resuelve con el módulo ZTNR de RTVE (`crypto` + `ztnr`), que devuelve las variantes
   MP4/HLS; `ZtnrResolver.SelectPreferred` elige la mejor según `PreferMp4`.
3. Los WebVTT de cada idioma marcado se descargan y se convierten a SRT con `VttToSrt` (un `.srt` por
   idioma; con «todos los idiomas» se guarda cada pista publicada).
4. `StrmLibrary` escribe los `.strm`, `.nfo` y `.srt`, y se notifica a `ILibraryMonitor` para que
   Jellyfin refresque la carpeta del programa.

## Limitaciones conocidas

- **DRM**: RTVE marca con `hasDRM` buena parte del catálogo aunque el MP4 progresivo y la playlist HLS que
  sirve van **sin cifrar**, así que el filtro de DRM está **desactivado** por defecto (una configuración
  antigua se migra sola a partir de la versión 2 del esquema). Los directos y algunos contenidos de
  terceros sí pueden requerir DRM real: si un vídeo concreto no arranca, activa el filtro.
- **Límite de peticiones**: RTVE responde a veces `429`/`503`. Las descargas se reintentan tres veces con
  espera creciente; si aun así falla un subtítulo, el aviso del log incluye la URL y se recupera en la
  siguiente sincronización.
- **Geobloqueo**: fuera de España parte del catálogo devuelve error al reproducir.
- **API no oficial**: RTVE puede cambiar los endpoints o el esquema de datos sin previo aviso.
- **DASH**: se ignora (`mpd`); solo se usan MP4 progresivo y HLS.
- Las URLs de los `.strm` pueden caducar: vuelve a ejecutar la tarea para regenerarlas.

## Desarrollo

```powershell
dotnet build                     # compila plugin y tests (Jellyfin.Plugin.RtvePlay.sln)
dotnet test                      # 121 pruebas (xunit)
pwsh ./package.ps1               # genera dist/rtveplay_1.0.0.0.zip + SHA256
# Windows PowerShell 5.1: powershell -ExecutionPolicy Bypass -File .\package.ps1
```

El arnés `scratch/LiveSmoke` (fuera de la solución) ejecuta contra la API real de RTVE la resolución de
programas, la paginación de vídeos, los streams, los subtítulos y la búsqueda:

```powershell
dotnet run --project scratch/LiveSmoke/LiveSmoke.csproj -c Release -- saber-vivir
```

Estructura de `Jellyfin.Plugin.RtvePlay`: `Rtve/` (cliente y resolución de streams), `Library/` (nombres,
NFO, `.strm`), `Sync/` (sincronización), `Tasks/`, `Api/` (endpoints del plugin) y `Configuration/`.

## Licencia

GPL-3.0-or-later. Consulta [LICENSE](LICENSE).
