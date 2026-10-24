# Jellyfin-RTVE — Resumen del proyecto

## Objetivo
Plugin de Jellyfin que integra el catálogo a la carta de RTVE Play para que los programas aparezcan y se reproduzcan dentro de Jellyfin.

## Origen de los datos (verificado el 24/10/2026)
Stream anónimo (sin login ni cookies). Catálogo público y rico:

1. Catálogo:
```
GET https://api.rtve.es/api/videos.json?size=N
GET https://www.rtve.es/api/programas/{id}/videos?type=39816&page=N&size=M
```
Campos por item: `id`, `title`/`longTitle`, `description`, `thumbnail`, `duration` (ms), temporada/episodio, `programInfo`, y flags `hasDRM`, `requireLogged`, `geolocalizado`, `country`.
El campo `qualities[].filePath` es una plantilla con placeholders:
`https://ztnr.rtve.es/ztnr/res/_PROVIDER_/video/<preset>/_TOKEN_`

2. Resolución de la URL real (token):
```
GET http://www.rtve.es/ztnr/movil/thumbnail/{rtveplayw|default}/videos/{id}.png?q=v2
```
Devuelve un **PNG en base64** con las URLs cifradas en chunks `tEXt` (cifrado casero). Hay que portar el algoritmo `_decrypt_url` de yt-dlp a C#: decodificar base64, saltar 8 bytes, recorrer chunks, en `tEXt` separar por `#` (alfabeto) y por `%%` (url_data), reconstruir con el alfabeto y el contador e/b.
Resultado: **m3u8 HLS** (también puede dar `mpd` o `mp4`).

3. Verificado en vivo:
- El m3u8 HLS está **en claro**: `#EXTM3U`, `#EXT-X-VERSION:3`, segmentos `.ts`, `#EXT-X-ENDLIST`, **sin `#EXT-X-KEY`** (incluso en un item con `hasDRM:true`; el DRM está en la variante DASH, no en HLS).
- URL m3u8 sin firma ni `expires`: `https://rtvehlsvodlote7.rtve.es/mediavodv2/resources/.../xxxxx.m3u8?idasset=<id>&hls_client_manifest_version=3` (aparentemente estable; a validar permanencia).
- Barrido de 30 items: 6 `hasDRM` (20%), 8 `requireLogged` (pero el token NO lo exige), 4 `geolocalizado`.
- Subtítulos: `GET https://www.rtve.es/api/videos/{id}/subtitulos` → VTT es/en/gl/ca/eu.

## Qué hace el plugin
Resuelve catálogo + token y expone el contenido en Jellyfin. Al ser **HLS (no MP4)**, se plantean dos variantes:
- **(A) Variante canal/proxy:** `IChannel` + resolución al vuelo de `MediaSourceInfo` (mejor para seek/transcode y para no cachear URLs). **Recomendada.**
- **(B) Variante `.strm`:** escribir `.strm` apuntando al m3u8 (más simple, pero HLS en `.strm` tiene seek/transcode menos limpios).

Filtra/verifica por item: saltar si solo hay DASH-DRM (no tocar DRM). Subtítulos VTT → SRT (o servirlos como pista externa).

## Decisiones de diseño
- **Sin credenciales:** el token es anónimo; no hace falta cuenta de RTVE (el flag `requireLogged` no bloquea el token).
- **DRM:** no reproducir contenido solo-DASH-DRM; usar el HLS en claro. Verificar por item.
- **Numeración/metadatos:** usar temporada/episodio del catálogo cuando exista; `.nfo` propios; metadatos online desactivados.
- **Config UI:** lista de programas (slug o id), carpeta destino (variante B), intervalo, filtrar DRM sí/no, idioma de subtítulos, botón "actualizar ahora".
- **Descifrado:** port de `_decrypt_url` a C# (~40 líneas).

## Entorno de destino
- Jellyfin Server "Lowyfin" **10.11.6** (Debian, Linux, x64) en un LXC de Proxmox, IP `192.168.87.170:8096`.
- Datos en `/var/lib/jellyfin`, plugins en `/var/lib/jellyfin/plugins`.
- Ya existe un árbol `.strm` montado `/mnt/media/strm/media/...` y bibliotecas (Series, Infantil, etc.).
- **Target del plugin:** `net9.0`; paquetes `Jellyfin.Controller` y `Jellyfin.Model` **10.11.6**; `targetAbi` **10.11.0.0**.

## Estructura prevista del código
- Namespace `Jellyfin.Plugin.RtvePlay`: `Plugin.cs`, `PluginConfiguration`, `configPage.html`.
- `RtveClient` (catálogo paginado, subtítulos).
- `ZtnrResolver` (descargar PNG base64 y descifrar `_decrypt_url` → m3u8).
- `VttToSrt`.
- Canal/provider (variante A) o `ScheduledTask` + `EntryPoint` (variante B).
- `meta.json` (GUID + targetAbi).

## Trampas y riesgos
- **Token ofuscado en PNG:** puede cambiar; port de `_decrypt_url`; mantener a la par que yt-dlp.
- **HLS en lugar de MP4:** seek/transcode menos limpio; segmentos `.ts`; `TARGETDURATION` variable (incluso 1 s).
- **DRM (Widevine) en DASH** de parte del contenido (deportes): no tocarlo; filtrar.
- `requireLogged` en la UI (no bloquea el token, pero a vigilar si cambian).
- Geo variable por programa (`geolocalizado` true/false).
- Permanencia de la URL m3u8 sin caducidad: a validar (PoC de 48 h).
- Ancho de banda: 1080p ~4,5 Mbps; el `.strm` no ocupa disco pero consume en cada reproducción.
- API no-oficial; puede cambiar.

## Privacidad / arquitectura de red
Usuarios = cuentas de Jellyfin; RTVE no ve usuarios (stream anónimo). Jellyfin hace de proxy (el cliente solo habla con Jellyfin). El geobloqueo depende de la IP del servidor y del flag `geolocalizado` por programa.

## Publicación en GitHub (previsto)
Nombre `Jellyfin.Plugin.RtvePlay`. Licencia **GPLv3**. README con instalación/config y aviso de no afiliado con RTVE/CRTVE. CI (dotnet 9.0) que publica el `.zip`. `manifest.json` (`targetAbi 10.11.0.0`). Sin secretos.

## Referencias
- Extractor de referencia: `yt_dlp/extractor/rtve.py` (`RTVEBaseIE._decrypt_url`, `RTVEProgramIE`, `RTVEALaCartaIE`).
- Herramienta local: `C:\localapps\yt-dlp\yt-dlp.exe` (2026.08.19), verificado resolviendo RTVE Play sin cookies.