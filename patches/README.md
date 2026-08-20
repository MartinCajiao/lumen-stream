# Patches

## moonlight-qt

`0001-first-class-high-refresh.patch` añade 90/120/144/165/200/240 al combo de FPS.

```powershell
git -C client apply ../patches/moonlight-qt/0001-first-class-high-refresh.patch
```

En este árbol el cambio ya está en `client/app/gui/SettingsView.qml`.

## Apollo

No hace falta parche de código: Lumen escribe `sunshine.conf` + `apps.json` con virtual display, `dd_refresh_rate_option = auto`, privacy (`ensure_only_display`) y `native_pen_touch`.
