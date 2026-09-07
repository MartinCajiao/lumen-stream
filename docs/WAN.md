# WAN sin relay de pago

Parsec Enterprise vende un relay global. Eso cuesta dinero; Lumen no opera uno.

Dos casas con internet distinto no se ven. Sin Tailscale (o UPnP de verdad, sin CGNAT) el código 192.168.x no existe en la otra red.

## Orden recomendado (de menos a más autonomía)

1. **LAN** — UDP directo, mDNS de Moonlight + beacon Lumen (`47991/udp`).
2. **IPv6** — si las dos conexiones tienen IPv6, el código IPv6 va directo **sin instalar nada**. Lumen lo detecta y lo comparte solo. Es el camino autónomo.
3. **Relay Lumen (self-hosted)** — si hay doble NAT/CGNAT y no hay IPv6. El launcher lleva el cliente del relay embebido: **no instalas nada en las PCs**. Solo hace falta un servidor relay público (ver abajo). El stream va por ese relay.
4. **UPnP** — el launcher pone `upnp = enabled` en Apollo para abrir 47984–48010. Solo sirve si tu router da directo a internet (sin CGNAT).
5. **STUN** — por defecto `stun:stun.l.google.com:19302` (solo descubre la IP pública).
6. **Tailscale o WireGuard** — overlay VPN. Simple, pero instala una app externa.

El launcher siempre muestra la IP pública cuando STUN la descubre. Si UPnP no abrió los puertos (apagado en el router o CGNAT), el código se marca **Internet (prueba)**: vale si tienes un redireccionamiento manual de puertos; si no, usa IPv6, el relay Lumen, o Tailscale.

### Detección de doble NAT (automática)

Al compartir, Lumen le pregunta al router su IP WAN (UPnP `GetExternalIPAddress`) y la compara con la IP pública (STUN):

- **Iguales** → el router da directo a internet; los puertos mapeados valen.
- **WAN privada** (192.168.x en el WAN del router) → **doble NAT**: hay otro NAT delante (módem del ISP o CGNAT). El código público nunca entra.
- **WAN pública distinta** → **CGNAT** del ISP. Mismo resultado.

Cuando hay doble NAT/CGNAT, Lumen prueba en este orden: **IPv6** (si hay), **relay Lumen** (si lo configuraste en Ajustes), y si no, muestra la tarjeta de Tailscale.

### Relay Lumen (autónomo, sin apps externas)

Lumen lleva el cliente del relay embebido en el launcher. Las dos PCs (host y cliente) se conectan **saliente** al relay — no necesitan abrir puertos, ni instalar Tailscale. El host publica un código `relay:123456@tu-relay:47991` que el cliente pega tal cual.

Solo hace falta **un** servidor relay público con una IP. Despliégalo gratis una vez:

**Oracle Cloud Free Tier (VM gratuita para siempre, con IP pública):**

```bash
# En la VM (Linux x64):
sudo apt install -y dotnet-sdk-8.0
git clone https://github.com/MartinCajiao/lumen-stream.git
cd lumen-stream
dotnet publish src/Lumen.Relay -c Release -r linux-x64 --self-contained -o relay
./relay/lumen-relay 47991 "tu-secreto-compartido"
# Abre el puerto 47991/tcp en el firewall de Oracle (Security List).
```

**Fly.io (gratis, con IP pública):**

```powershell
# localmente
fly launch --image mcr.microsoft.com/dotnet/sdk:8.0 --no-deploy
# sube el binario lumen-relay y expón 47991/tcp
```

Luego en **Ajustes** del launcher (en las dos PCs): `relay = ip-de-tu-vm:47991:tu-secreto-compartido`.

A partir de ahí, al compartir con doble NAT el código sale como `relay:...` y cruza cualquier CGNAT. El relay solo ve bytes cifrados (GameStream ya va cifrado entre Apollo y Moonlight).

> Nota de latencia: el relay suma el viaje extra a la VM. Para juego competitivo, busca una VM en tu región. Para jugar normal, va fino. IPv6 (sin relay) sigue siendo lo más rápido cuando esté disponible.

El enlace de invitación es `lumen://connect?host=IP&port=47989`. Pégalo o escribe la IP en Conectar.

SSO, SCIM, panel admin y audit logs de Parsec Teams no están en alcance.
