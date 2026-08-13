# Lumen Stream

Streaming de PC a PC, **gratis** y **GPL-3.0**. Un launcher que arranca [Apollo](https://github.com/ClassicOldSong/Apollo) (host) y [Moonlight](https://github.com/moonlight-stream/moonlight-qt) (cliente).

No es Parsec. El protocolo es GameStream, el de Sunshine/Moonlight.

**Sitio:** https://martincajiao.github.io/lumen-stream/  
**Descarga:** https://github.com/MartinCajiao/lumen-stream/releases/latest

## Uso

1. En el PC gamer: **Compartir este PC**.
2. En el otro: **Conectar**. Si pide PIN, lo pegas en el host.
3. Otra red: escribe el código (IP) que sale al compartir. Si el router no abre puertos, usa [Tailscale](https://tailscale.com/) en las dos.

## Compilar

```powershell
git clone --recurse-submodules https://github.com/MartinCajiao/lumen-stream.git
dotnet test LumenStream.sln
.\scripts\build-installer.ps1
```

## Licencia

GPL-3.0-or-later. Apollo y moonlight-qt conservan las suyas (GPL-3.0).
