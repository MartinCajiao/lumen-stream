# Release notes

## Lumen Stream v0.4.3 (2026-09-07)

### Conectar ahora empareja Y streamea solo — sin PIN

- **Auto-emparejamiento**. Antes, al pulsar Conectar salía el rollo del PIN (lo ves en Moonlight, lo escribes en el otro PC, vuelves a pulsar). Ahora, si las dos PCs tienen Lumen, el host **auto-aprueba** el PIN fijo `1234` mientras comparte, y el cliente lo manda con `moonlight pair --pin 1234`. Lumen sondea, ve que emparejó, y **lanza el stream él solo**. Un botón y ya estás dentro.
- Si el host no es Lumen (Sunshine puro) o no está compartiendo, a los 15s cae al flujo manual del PIN (sigue funcionando).
- El host arranca un watcher en segundo plano que aprueba el PIN `1234` mientras comparte; se apaga al dejar de compartir.
- Nota honesta: en tu propia wifi esto es seguro (tú eres el dueño). En una wifi pública, cualquiera que adivine `1234` podría emparejar — no compartas en redes ajenas.

### Apollo + Moonlight ya vienen DENTRO de Lumen.exe (de v0.4.2)
- `Lumen.exe` lleva Apollo y Moonlight embebidos (133 MB). No se descargan la primera vez. Un solo archivo.

### Tests
- 76 tests pasando (nuevos: auto-pair args, PIN fijo, IPv6 entre corchetes).

## Lumen Stream v0.4.2 (2026-09-07)

### Apollo + Moonlight ya vienen DENTRO de Lumen.exe

- **Un solo archivo, todo integrado.** `Lumen.exe` ahora lleva Apollo y Moonlight embebidos (133 MB). Ya no se descargan la primera vez: abres Lumen y ya. El usuario no instala Apollo ni Moonlight a mano, ni los ve — van dentro.
- Al primer Compartir, Lumen extrae Apollo y Moonlight en segundo plano a `%AppData%\LumenStream\deps`. Moonlight no pide permiso. Apollo pide **un** aviso de Windows (UAC) la primera vez, porque su driver de pantalla virtual lo exige Windows — no es de Lumen, no se puede ocultar.
- Sigue habiendo dos archivos para el usuario: **`Lumen.exe`** (portable, ya con todo dentro) y **`Lumen-Setup.exe`** (instalador, para quien quiera acceso directo y autocompartir al iniciar sesión).

### Tests
- 71 tests pasando.

## Lumen Stream v0.4.1 (2026-09-07)

### Sin fricción: adiós al login

- **No más usuario ni contraseña**. Lumen entra directo a la pantalla de Compartir/Conectar. La pantalla de "crear cuenta / iniciar sesión" ya no existe — era una joda que no aportaba nada en un launcher de un solo usuario.
- El botón "Cerrar sesión" ahora es "Cambiar nombre" y te deja dentro (nunca te saca al muro de login).
- Sigue habiendo dos archivos para el usuario: **`Lumen.exe`** (portable) y **`Lumen-Setup.exe`** (instalador). El Setup instala Apollo + Moonlight solo; el portable los baja solito al primer Compartir. El usuario no instala Apollo ni Moonlight a mano.

### Tests
- 71 tests pasando.

## Lumen Stream v0.4.0 (2026-09-07)

### La app ya no depende de apps externas para cruzar CGNAT

- **IPv6 autónomo**: si las dos conexiones tienen IPv6, Lumen comparte el código IPv6 y va directo **sin instalar nada**. Antes el código IPv6 solo se compartía si UPnP abría puertos — un bug, porque IPv6 no necesita UPnP. Arreglado.
- **Relay Lumen embebido**: el launcher lleva el cliente del relay dentro. Las dos PCs se conectan **saliente** al relay — no abren puertos, no instalan Tailscale. El host publica `relay:123456@tu-relay:47991` y el cliente lo pega tal cual. Cruza cualquier CGNAT.
- **Servidor relay propio** (`Lumen.Relay`): un binario `lumen-relay` que despliegas gratis en una VM con IP pública (Oracle Cloud Free Tier o Fly.io). Un solo puerto TCP. Ver `docs/WAN.md`.
- **Ajustes en la UI**: campo "relay Lumen (host:puerto:secreto)" en el launcher. No hay que editar JSON a mano.
- **Detección de doble NAT** (de v0.3.1): compara la IP WAN del router con la IP pública y, si hay CGNAT, prueba IPv6 → relay → tarjeta de Tailscale.

### Por qué esto sí es autónomo
- IPv6: cero dependencias, cero relay, latencia mínima. Va cuando el ISP da IPv6.
- Relay Lumen: el cliente va embebido (no instalas nada). Solo necesitas **un** servidor relay público, que es tuyo (gratis en Oracle/Fly). No es una app de terceros en tus PCs.

### Lo que sigue siendo física (honesto)
Dos PCs detrás de CGNAT simétrico, sin IPv6, no se ven directo sin un relay. Eso no lo arregla ninguna app — ni Lumen, ni Parsec. Por eso existe el relay. Parsec cobra por el suyo; Lumen te da el código para que tengas el tuyo gratis.

### Tests
- 71 tests pasando (nuevos: relay end-to-end TCP, parsing de códigos relay, IPv6 sin UPnP).

### Artefactos
- **Lumen.exe** — launcher portable (win-x64, self-contained)
- **Lumen-Setup.exe** — instalador (Apollo + Moonlight + firewall)
- **lumen-relay** — servidor relay para desplegar en una VM gratis (linux-x64 / win-x64, opcional)

## Lumen Stream v0.3.1 (2026-09-07)

### El porqué de "ni con el codigo funciona"
- **Detección de doble NAT/CGNAT**: al compartir, Lumen compara la IP WAN del router con la IP pública. Si el router está detrás de otro NAT (CGNAT del ISP o doble router), el código público **nunca** iba a entrar — ahora Lumen lo detecta, lo dice en claro, y muestra el código de la wifi en vez de uno que no sirve.
- **Botón Tailscale**: cuando hay doble NAT aparece la tarjeta "Desde otra casa no va a entrar (todavía)" con instalador de Tailscale en un clic. Cuando Tailscale conecta en las dos PCs (misma cuenta), el código cambia solo a 100.x y eso sí cruza casas.
- Si el código pegado es 100.x y este PC no tiene Tailscale, sale la misma tarjeta.

### Verificado en vivo
- Emparejado PIN completo probado de punta a punta (login + /api/pin + lista de apps).
- Detección NAT probada en red con doble NAT real.

### Tests
- 61 tests pasando (nuevos: clasificación NAT, Tailscale).

## Lumen Stream v0.3.0 (2026-08-21)

### 200 FPS + fallback automático a Hz del panel
- Preset **200 Hz** de primera clase en Sunshine, Moonlight UI y docs
- `FpsPreset.Resolve` clampa los presets fijos al Hz máximo del panel del cliente (p.ej. 200 → 144 si la pantalla no pasa de 144)

### Fixes de conectividad LAN/WAN
- **Emparejado real**: consulta `/api/serverinfo` con el certificado del cliente antes de streamear; elimina el dead-end de "Conectar otra vez"
- **Moonlight portable**: escribe `Moonlight.conf` junto al exe cuando detecta `portable.dat` (los ajustes ya no se ignoran)
- **Firewall**: reglas para el launcher y el beacon UDP 47991; avisa si falla por falta de admin
- **Diagnóstico de puerto**: detecta conflictos en 47989 antes de relanzar Apollo
- **WAN honesta**: muestra la IP pública como "Internet (prueba)" cuando UPnP falla, con aviso de CGNAT/Tailscale

### Tests
- 56 tests pasando (nuevos: `ConnectionFixesTests`, `WanAndDiscoveryTests` actualizado)

### Artefactos
- **Lumen.exe** (93 MB, single-file, self-contained, win-x64) — launcher portable
- **Lumen-Setup.exe** (68 MB) — instalador que pone Apollo + Moonlight y abre el firewall
