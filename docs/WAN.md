# WAN sin relay de pago

Parsec Enterprise vende un relay global. Eso cuesta dinero; Lumen no opera uno.

Dos casas con internet distinto no se ven. Sin Tailscale (o UPnP de verdad, sin CGNAT) el código 192.168.x no existe en la otra red.

## Orden recomendado

1. **LAN** — UDP directo, mDNS de Moonlight + beacon Lumen (`47991/udp`).
2. **Tailscale o WireGuard** — el stream va P2P por la overlay. Es lo más simple en internet.
3. **UPnP** — el launcher pone `upnp = enabled` en Apollo para abrir 47984–48010.
4. **STUN** — por defecto `stun:stun.l.google.com:19302` (solo descubrimiento de IP pública).
5. **TURN self-hosted** — si el NAT es simétrico. Coturn en un VPS barato: `turn:tu-servidor:3478`. Pon el URI en Ajustes.

El enlace de invitación es `lumen://connect?host=IP&port=47989`. Pégalo o escribe la IP en Conectar.

SSO, SCIM, panel admin y audit logs de Parsec Teams no están en alcance.
