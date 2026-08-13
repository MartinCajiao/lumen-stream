# Host Apollo

El launcher escribe `%AppData%\LumenStream\host\sunshine.conf` y `apps.json`.

- `virtual-display: true` — SudoVDA a la resolución/Hz del cliente.
- `dd_configuration_option = ensure_primary` o `ensure_only_display` (privacy).
- `dd_refresh_rate_option = auto` y `dd_resolution_option = auto`.
- `native_pen_touch` para Wacom.
- `fps = [30,60,90,120,144,165,240]`.

Arranca `apollo.exe` con esa conf. El log va a `%AppData%\LumenStream\logs\host.log` (overlay de captura).
