# Release notes

## Lumen Stream v0.3.1 (2026-09-07)

### El porqué de "ni con el código funciona"
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
