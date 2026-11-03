[README.md](https://github.com/user-attachments/files/34309548/README.md)
# Pandora's Tower for PC — unofficial native port

Turns **your own copy** of Pandora's Tower for Wii into a native Windows and
Linux program through static recompilation with
[wiikit](https://github.com/vs-sr-dev/wiikit), with its own launcher,
60 fps and more (interpolated), high resolutions and PC controls.

**[Download the latest version](https://github.com/RecompPort/pandoras-tower-pc/releases/latest)** ·
[What's new](CHANGELOG.md) · [Full guide](GUIDE.md) ·
[Report a problem](https://github.com/RecompPort/pandoras-tower-pc/issues/new/choose) ·
[Español](#español)

> **Notice.** This repository contains no game files: not its executable,
> its graphics, sounds or texts, no official artwork and no Wii encryption
> keys. Everything is generated on your PC from the disc image of a copy you
> legally own. A fan project, not affiliated with Nintendo, Ganbarion or
> XSEED Games. Requests, links or game files are not accepted in the issues.

## What's new in 1.1

- **Performance you can read**: with **Measure performance** the port now
  measures the graphics card's own time and, when you close the game, says
  whether the card or the processor holds it back, against the frame rate you
  chose (movies, loading screens and the paused menu are left out).
- **Frame rates that fit your screen** are marked in the launcher and in the
  in-game menu (60 and 90 on a 180 Hz screen), so the picture stays even.
- **A new installer** in the launcher's style.
- The numbering starts again at **1.1** (the earlier versions went from 1.0
  to 1.8). Everything is in the [changelog](CHANGELOG.md).

## What it does

- **Native port**: the game's executable is recompiled to C++ and built for
  PC; the ISO is read directly, without extracting it, and the installer can
  keep a copy of it in the install folder, so the original can be moved or
  deleted. North American, European and Japanese versions.
- **Graphics**: internal resolution from x1 to x5, anisotropic filtering, the
  game's fog, shaders that are saved and prepared at startup, and texture
  packs, Dolphin's too, in PNG or DDS (BC7).
- **Frame rate**: the original 30, or 60, 90, 120, the monitor's rate or
  unlimited, with interpolated frames (the game keeps its 30, so everything
  moves as on the Wii). The rates that divide your screen's refresh are
  marked.
- **Picture**: fullscreen, exclusive or windowed; no black bars, neither the
  scenes' nor the movies' nor in the transitions, in 16:9 fullscreen at all
  times.
- **Controls**: gamepad (as a Classic Controller) or mouse and keyboard (as a
  Wii Remote and Nunchuk: the mouse points, a key or mouse button shakes the
  remote), or a gamepad with a gyroscope (DualSense, DualShock 4, Switch Pro,
  Joy-Con) or a real Wii Remote as the Wii Remote and Nunchuk (the gyroscope
  aims), every button remappable, and the game's button
  icons switchable to an Xbox-style or PlayStation-style gamepad's, or to keys
  and mouse.
- **Launcher** in seven languages (English, Spanish, German, Italian, French,
  Japanese and Chinese) with every option, save backup and shortcut, crash
  reports and a check for new versions; the Wii
  Remote wrist strap screen is skipped. The installer and the files are in
  English; the language is chosen in the launcher.
- **In-game menu** (Esc, or Start and Back together on a gamepad): pauses the
  game and changes edge smoothing, the fps counter, the frame rate, the volume
  and the gyroscope sensitivity on the fly. The game can also pause on its own
  when you switch windows.
- **Extras**: eight achievements of the port's own (not the game's), F9 for a
  screenshot, edge smoothing (FXAA), and **Measure performance**, which ends
  with a summary of what to change if it runs slowly.

## Requirements

- Windows 10 or 11, or Linux (x86-64; Ubuntu/Debian, Fedora, Arch, openSUSE or
  similar), and a graphics card with OpenGL 4.5 (almost any PC from the last
  ten years; on Linux, the Mesa or NVIDIA drivers). macOS is not supported: see
  below.
- **The ISO of your copy of the game: North American (`SX3EXJ`), European
  (`SX3P01`) or Japanese (`SX3J01`)**, dumped from your own disc (for example
  with CleanRip on your Wii). A WBFS or RVZ is converted to ISO with Dolphin
  (right click on the game > Convert File > ISO).
- An Internet connection during the installation and about 6 GB free (half
  of it for the build tools), plus the size of your ISO if you keep a copy of
  the game.

## How to play

1. Download **`PandorasTower-PC-v1.1-Setup.exe`** from
   [Releases](https://github.com/RecompPort/pandoras-tower-pc/releases) and
   open it. As a fan program without a digital signature, Windows may warn
   ("Windows protected your PC"): press **More info → Run anyway**.
2. Follow the wizard: accept the notice, choose the folder (by default
   `C:\PandorasTower`) and the ISO of your copy of the game, and press
   **Install**. On its own it:
   - downloads and installs [MSYS2](https://www.msys2.org) (the free build
     tools) if you don't have it, with the compiler;
   - copies the game into the install folder, if you leave that ticked;
   - recompiles and builds the game on your PC (about 5 minutes on a current
     PC; the bar shows it);
   - creates the **Pandora's Tower** shortcuts on the desktop and in the Start
     menu, and registers it in *Installed apps* so it can be uninstalled.
3. Open **Pandora's Tower**, choose the language and the options (graphics,
   frame rate, controls) and press **Play**.

To update to a new version, open its installer and choose the same folder:
it rebuilds what is needed and keeps your saves and options. With another
ISO, it adds another version of the game.

The installer carries nothing from the game: only the files of this
repository (you can build it yourself with `tools/installer/build.sh`). If
you prefer no installer, download the code (`Code → Download ZIP`), unzip it
in a folder with no spaces or accents and open `Install.cmd`, which does the
same in a text window.

### On Linux

1. Download `pandoras-tower-pc-v1.1-linux.tar.gz` from
   [Releases](https://github.com/RecompPort/pandoras-tower-pc/releases) (or
   the code) and unpack it, for example in `~/Games/pandoras-tower-pc`.
2. In a terminal, in that folder:
   ```
   ./install-linux.sh
   ```
   It installs what building needs with your distribution's package manager
   (it asks first, and asks for your password through `sudo`), asks for your
   ISO (with a file chooser if there is one) and whether to keep a copy of the
   game in the folder, builds the game (a few minutes; the first time SDL3 is
   also built if your distribution doesn't have it) and creates **Pandora's
   Tower** in the applications menu and on the desktop. `./install-linux.sh
   GAME.iso --copy-game --yes` does it all without asking.
3. Open **Pandora's Tower**: the same launcher as on Windows, running on Mono.
   Without Mono, the shortcut starts the game directly (`./run.sh`, which
   takes all the options as arguments: see the [guide](GUIDE.md)).

On Linux the launcher's gamepad navigation and the reading of the gamepad's
buttons when mapping them use XInput and don't work; the game itself reads
every gamepad (through SDL), and the launcher is used with the mouse and the
keyboard.

### macOS

Not supported for now. The renderer is written for OpenGL 4.5 and macOS stops
at OpenGL 4.1 (Apple deprecated OpenGL in favour of Metal), so the game would
not start. It needs the renderer ported to OpenGL 4.1 or to Metal. On an
Intel Mac, Windows through Boot Camp works.

### Your own icons and art

The `art` folder of the installation is for your own images, which are never
uploaded: `icon.png` (the launcher and its shortcuts), `game.png` (the game's
window, the taskbar and Task Manager), `background.png` and `logo.png` (the
launcher). Without them, the icon of the game's own Wii menu banner, read from
your disc, is used. Run the installer again on the same folder to apply them
(on Linux, `./launcher.sh` and `./setup.sh`).

<details>
<summary>Manual installation (in the MSYS2 CLANG64 terminal)</summary>

```
pacman -S --needed git mingw-w64-clang-x86_64-clang mingw-w64-clang-x86_64-cmake mingw-w64-clang-x86_64-ninja mingw-w64-clang-x86_64-python mingw-w64-clang-x86_64-python-pycryptodome mingw-w64-clang-x86_64-sdl3
git clone https://github.com/RecompPort/pandoras-tower-pc.git
cd pandoras-tower-pc
./setup.sh "/c/path/to/your/Pandora's Tower.iso"
./launcher.sh
```

`setup.sh` checks that your executable is exactly the one of an analysed
version, recompiles it and builds the port; `launcher.sh` creates the
launcher and the shortcut. Every detail and option is in the
[guide](GUIDE.md).
</details>

## How it works

The game's executable (`main.dol`) is translated to C++ with wiikit's
recompiler, which also provides the runtime (graphics, audio, controllers,
disc). This project adds what is specific to Pandora's Tower:

- `tools/names-manual.tsv`: SDK and game functions identified, each with its
  evidence in the code;
- `tools/names-cache/`: the names of every function of each version, already
  worked out (only addresses and names), so that analysis isn't repeated on
  every PC;
- `tools/pandora.cpp`: the game layer (controls, 60 fps, wrist strap screen);
- `tools/patches/`: improvements to wiikit's runtime (audio, interpolation,
  reading the ISO, fog, textures, controllers…), which `setup.sh` applies;
- `tools/launcher/`: the launcher (C#, built with the compiler that comes with
  Windows) and its art, original to this project.

## Status

Version 1.1 ([what changed](CHANGELOG.md)): playable in the three versions of
the game, on Windows and Linux.

**Known limits**
- The image has to be an ISO (an RVZ or WBFS is converted with Dolphin).
- No own icon has been identified yet for the Wii Remote's +, −, 1, 2 and
  D-pad when playing with mouse and keyboard (they show the game's).
- A real Wii Remote aims only with a MotionPlus (no sensor bar), and hasn't
  been tested much yet.
- macOS is not supported (see above).

## Frequently asked questions

- **It runs slowly.** Lower the resolution (x2 is already the resolution of a
  1080p screen) and leave the frame rate at 60, or pick one marked "fits your
  screen". On a laptop, check that the
  game uses the powerful graphics card (the `video:` line of
  `build/run.err`). If it still runs slowly, turn on **Measure performance**
  in the launcher and play a couple of minutes: when you close the game it
  says how long the graphics card takes for each picture, whether it or the
  processor holds it back, and what to change (also whether your frame rate
  fits your screen's refresh: 90 on a 180 Hz screen, not 120). If that
  doesn't help, open an issue with your `build/run.err` and
  `build/perf-summary.txt`.
- **The first time a place is seen, it stops for a moment.** Its shaders are
  being prepared; it only happens once (they are kept).
- **Which image works?** Your own copy's ISO: `SX3EXJ` (North America),
  `SX3P01` (Europe) or `SX3J01` (Japan). A WBFS or RVZ: convert it to ISO with
  Dolphin (right click → Convert File → ISO).
- **Can I delete the ISO?** Yes, if you let the installer keep a copy of the
  game in the install folder.
- **Where are my saves?** `build/nand`; the launcher backs them up (**Game →
  Folders → Back up saves**).
- **Steam Deck:** install the Linux version (Desktop mode, `./install-linux.sh`),
  add **Pandora's Tower** to Steam as a non-Steam game and choose **Gamepad with
  gyroscope** in the launcher to aim with the Deck's gyroscope. For the game
  to read the gyroscope itself, turn Steam Input off for it (its controller
  settings in Steam). Not tested on a Deck yet.
- **It closed with an error.** The launcher writes a report in `crash-reports/`:
  attach it to a new issue.

## How to report a problem

Open an [issue](https://github.com/RecompPort/pandoras-tower-pc/issues/new/choose)
with what happened, your PC and `build/run.err` (or the crash report). Never
attach or link game files.

## License

The code, the scripts and the art of this project: [MIT](LICENSE).
Third-party components and trademarks: [THIRD-PARTY.md](THIRD-PARTY.md).

---

### Español

Port nativo no oficial de Pandora's Tower (Wii) para PC, hecho por
recompilación estática con [wiikit](https://github.com/vs-sr-dev/wiikit).
**No incluye archivos del juego, arte oficial ni claves de la Wii**: todo se
genera en tu equipo a partir de la ISO de tu propia copia legal
(norteamericana `SX3EXJ`, europea `SX3P01` o japonesa `SX3J01`). Sin relación
con Nintendo, Ganbarion ni XSEED Games.

**Qué ofrece**: resolución interna de x1 a x5, 60, 90, 120 fps o los de tu
monitor (interpolados: el juego sigue a sus 30), sin bandas negras, paquetes
de texturas en HD, mando, ratón y teclado, mando con giroscopio o Wiimote
real, menú dentro del juego (Esc), logros propios del port y un lanzador en
siete idiomas, también español.

**Novedades de la 1.1**: medición del rendimiento con el tiempo de la propia
gráfica y un resumen claro al cerrar el juego, los fps que encajan con tu
pantalla marcados (60 y 90 en una de 180 Hz) y un instalador nuevo con el
estilo del lanzador. La numeración empieza de nuevo en la 1.1.

**Requisitos**: Windows 10/11 o Linux, una gráfica con OpenGL 4.5, conexión
a Internet al instalar y unos 6 GB libres. macOS no (su OpenGL se queda en
la 4.1).

**Cómo jugar en Windows**: descarga `PandorasTower-PC-v1.1-Setup.exe` de
[Releases](https://github.com/RecompPort/pandoras-tower-pc/releases/latest)
y ábrelo (no está firmado: en el aviso de SmartScreen elige *Más información
→ Ejecutar de todas formas*). El asistente instala MSYS2 si hace falta, puede
guardar una copia del juego en la carpeta de instalación (para poder borrar
la ISO), compila el juego en tu equipo (unos 5 minutos en un PC actual, más
5–10 para descargar MSYS2 la primera vez) y crea los accesos directos y el
desinstalador. Para actualizar, abre el instalador nuevo y elige la misma
carpeta: tus partidas y opciones se conservan.

**En Linux**: descomprime `pandoras-tower-pc-v1.1-linux.tar.gz` y ejecuta
`./install-linux.sh` en esa carpeta.

El instalador está en inglés; el idioma se elige en el lanzador. Para avisar
de un problema, abre un
[issue](https://github.com/RecompPort/pandoras-tower-pc/issues/new/choose)
con `build/run.err` (o el informe de `crash-reports/`); nunca adjuntes
archivos del juego. Código y arte original del port: MIT.
