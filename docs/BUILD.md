# Compilar Apollo (host) y Moonlight (cliente)

Los submódulos viven en `host/` (Apollo) y `client/` (moonlight-qt). Lumen no sustituye esos binarios: los arranca con la config que genera el launcher.

## Submódulos

```powershell
git submodule update --init --depth 1
```

O `scripts/init-submodules.ps1`.

## Apollo (Windows)

Sigue `host/docs/` y el README de Apollo. Resumen típico:

1. Instala [CMake](https://cmake.org/), Visual Studio 2022 (C++), y las deps que pida Apollo (Boost, FFmpeg, etc.).
2. Compila el árbol `host/`.
Copia `apollo.exe` (y DLLs) a `src/Lumen.Launcher/bin/Debug/net8.0/vendor/apollo/` o `C:\Program Files\Apollo\`.

**Mejor:** `scripts/build-installer.ps1` o al pulsar Compartir Lumen instala Apollo/Moonlight solo. Ver [INSTALL.md](INSTALL.md).
4. Instala el driver **SudoVDA** que trae Apollo. Sin él el virtual display no aparece y 144 Hz no se siente si el monitor físico es 60 Hz.

El launcher pasa `sunshine.conf` generado en `%AppData%\LumenStream\host\sunshine.conf`.

## moonlight-qt (Windows)

1. Qt 6 + MSVC según `client/README.md`.
2. Aplica `patches/moonlight-qt/0001-first-class-high-refresh.patch` si el árbol está limpio (ya está aplicado en este clone).
3. Copia `Moonlight.exe` a `vendor/moonlight/` o Program Files.

Lumen también escribe las preferencias de Moonlight (FPS, bitrate, 4:4:4, overlay) en el registro de Windows, así que el Hz pedido no depende de abrir Ajustes.

## Launcher

```powershell
dotnet build LumenStream.sln -c Release
dotnet run --project src/Lumen.Launcher/Lumen.Launcher.csproj -c Release
```

Requisito: .NET 8 SDK.
