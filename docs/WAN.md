# WAN sin relay de pago

Parsec Enterprise vende un relay global. Eso cuesta dinero; Lumen no opera uno.

Dos casas con internet distinto no se ven. Sin Tailscale (o UPnP de verdad, sin CGNAT) el código 192.168.x no existe en la otra red.

## Orden recomendado

1. **LAN** — UDP directo, mDNS de Moonlight + beacon Lumen (`47991/udp`).
2. **Tailscale o WireGuard** — el stream va P2P por la overlay. Es lo más simple en internet.
3. **UPnP** — el launcher pone `upnp = enabled` en Apollo para abrir 47984–48010.
4. **STUN** — por defecto `stun:stun.l.google.com:19302` (solo descubrimiento de IP pública).
5. **TURN self-hosted** — si el NAT es simétrico. Coturn en un VPS barato: `turn:tu-servidor:3478`. Pon el URI en Ajustes.

El launcher siempre muestra la IP pública cuando STUN la descubre. Si UPnP no abrió los puertos (apagado en el router o CGNAT), el código se marca **Internet (prueba)**: vale si tienes un redireccionamiento manual de puertos; si no, instala Tailscale en las dos PCs.

### Detección de doble NAT (automática)

Al compartir, Lumen le pregunta al router su IP WAN (UPnP `GetExternalIPAddress`) y la compara con la IP pública (STUN):

- **Iguales** → el router da directo a internet; los puertos mapeados valen.
- **WAN privada** (192.168.x en el WAN del router) → **doble NAT**: hay otro NAT delante (módem del ISP o CGNAT). El código público nunca entra. Lumen muestra el código de la wifi y la tarjeta de Tailscale.
- **WAN pública distinta** → **CGNAT** del ISP. Mismo resultado.

Cuando hay doble NAT/CGNAT, Lumen ofrece el botón **Instalar Tailscale (gratis)**: baja el MSI oficial, lo abre, y cuando Tailscale conecta (misma cuenta en las dos PCs) el código cambia solo a 100.x.

El enlace de invitación es `lumen://connect?host=IP&port=47989`. Pégalo o escribe la IP en Conectar.

SSO, SCIM, panel admin y audit logs de Parsec Teams no están en alcance.
