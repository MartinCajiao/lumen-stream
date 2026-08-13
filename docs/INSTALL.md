# Instalar Lumen (sin pelearte con Apollo)

## Lo fácil

```powershell
.\scripts\build-installer.ps1
```

Sale `dist\Lumen-Setup.exe`. Ejecútalo **una vez** (internet). Instala Apollo y Moonlight. Luego abre `dist\Lumen\Lumen.exe`.

Si tienes [WiX](https://wixtoolset.org/), el mismo script genera `dist\Lumen.msi`.

## En la app

1. Crear cuenta (usuario + contraseña, sin email).
2. En el PC gamer: **Compartir este PC**. Sale un código (IP).
3. En el otro PC: **Conectar** (misma wifi) o **Añadir** pegando el código.
4. Si Moonlight pide PIN, lo escribes en el host.

No hace falta instalar Apollo a mano. Si falta, al compartir Lumen lo descarga. A distancia hace falta UPnP o Tailscale; no hay relay mundial.
