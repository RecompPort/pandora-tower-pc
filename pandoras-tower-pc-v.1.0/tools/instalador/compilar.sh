#!/usr/bin/env bash
# Compila el instalador de Windows: dist/PandorasTower-PC-vX-Setup.exe
#
#   tools/instalador/compilar.sh
#
# Lleva dentro los archivos del port (los del repositorio, como port.zip) y
# nada del juego: el juego se compila en el equipo de cada uno desde su ISO.
# Sirve en Windows (terminal MSYS2 CLANG64, con el csc.exe de .NET Framework 4)
# y en Linux o macOS (con mcs, de Mono). Para el icono y el manifiesto hace
# falta llvm-rc o windres (en MSYS2: pacman -S mingw-w64-clang-x86_64-llvm).
set -euo pipefail
cd "$(dirname "$0")/../.." || exit 1
ROOT="$(pwd)"
I="$ROOT/tools/instalador"
die() { printf '\nERROR: %s\n' "$*" >&2; exit 1; }
PY="$(command -v python3 || command -v python || true)"
[ -n "$PY" ] || die "hace falta Python 3"

VERS="$(sed -n 's/.*const string Version = "\([0-9.]*\)".*/\1/p' "$I/Instalador.cs")"
[ -n "$VERS" ] || die "no encuentro la versión en Instalador.cs"
OUT="$ROOT/dist/PandorasTower-PC-v$VERS-Setup.exe"
T="$ROOT/build/instalador"
rm -rf "$T"; mkdir -p "$T" "$ROOT/dist"

echo "== port.zip (versión $VERS)"
# los archivos del repositorio: los de git si es un clon; si no, todo menos lo que
# nunca se publica (lo mismo que deja fuera .gitignore)
if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    git ls-files -z --cached --others --exclude-standard > "$T/lista"
else
    "$PY" - "$T/lista" <<'PYEOF'
import os, sys, fnmatch
skip_dirs = {'.git', 'build', 'wiikit', 'dist', '__pycache__'}
skip_files = ['PandorasTower.exe', 'controles.txt', 'lanzador.ini', 'LEEME.md', 'gxtrace_*.txt',
              'tools/lanzador/logo.png', 'tools/lanzador/icono.ico', 'tools/lanzador/emblema.png',
              'tools/lanzador/pandora.png', 'tools/lanzador/lanzador.json']
disc = ('.iso', '.wbfs', '.rvz', '.wia', '.gcz', '.ciso', '.dol', '.bnr')
out = []
for d, dirs, files in os.walk('.'):
    rel = os.path.relpath(d, '.').replace(os.sep, '/')
    dirs[:] = [x for x in dirs if x != '__pycache__']
    if rel == '.':
        dirs[:] = [x for x in dirs if x not in skip_dirs]
        rel = ''
    for f in files:
        p = (rel + '/' + f) if rel else f
        if any(fnmatch.fnmatch(p, s) for s in skip_files) or f.lower().endswith(disc + ('.pyc',)):
            continue
        if (p.startswith('arte/') or p.startswith('texturas/')) and not p.endswith('/LEEME.txt'):
            continue
        out.append(p)
open(sys.argv[1], 'wb').write(b'\0'.join(x.encode() for x in sorted(out)))
PYEOF
fi
"$PY" - "$T/lista" "$T/port.zip" <<'PYEOF'
import sys, zipfile, os
names = [n for n in open(sys.argv[1], 'rb').read().decode().split('\0') if n]
skip = ('.github/', 'tools/instalador/', 'dist/')
names = sorted(n for n in names if not n.startswith(skip) and os.path.isfile(n))
for n in names:
    low = n.lower()
    if low.endswith(('.iso', '.wbfs', '.rvz', '.wia', '.gcz', '.ciso', '.dol', '.bnr')) or low.endswith('wiiboot.exe'):
        sys.exit('ERROR: ' + n + ' no se puede publicar')
with zipfile.ZipFile(sys.argv[2], 'w', zipfile.ZIP_DEFLATED, compresslevel=9) as z:
    for n in names:
        info = zipfile.ZipInfo('pandoras-tower-pc/' + n, (2026, 1, 1, 0, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = (0o755 if n.endswith('.sh') else 0o644) << 16
        z.writestr(info, open(n, 'rb').read())
print(len(names), 'archivos,', os.path.getsize(sys.argv[2]) // 1024, 'KB')
PYEOF

echo "== icono, manifiesto y versión"
V4="$(echo "$VERS.0.0.0" | cut -d. -f1-4 | tr . ,)"
RES="$T/instalador.res"
RC=""
for c in llvm-rc llvm-rc-18 llvm-rc-17 llvm-rc-16 llvm-rc-15 llvm-rc-14; do command -v "$c" >/dev/null 2>&1 && { RC="$c"; break; }; done
WINRES=()
if [ -n "$RC" ]; then
    "$RC" -I "$I" "-DVER=$V4" "-DVERS=\"$VERS\"" -fo "$RES" "$I/instalador.rc"
    WINRES=("-win32res:$RES")
elif command -v windres >/dev/null 2>&1; then
    (cd "$I" && windres "-DVER=$V4" "-DVERS=\\\"$VERS\\\"" -O res -o "$RES" instalador.rc)
    WINRES=("-win32res:$RES")
else
    echo "aviso: sin llvm-rc ni windres: el instalador solo llevará el icono (sin manifiesto, Windows puede pedir permisos de administrador)" >&2
    WINRES=("-win32icon:$ROOT/tools/lanzador/pandora.ico")
fi

echo "== $(basename "$OUT")"
cp "$I/lateral.jpg" "$ROOT/tools/lanzador/PirataOne-Regular.ttf" "$ROOT/tools/lanzador/pandora.ico" "$ROOT/LICENSE" "$T/"
SRC="$I/Instalador.cs"
CSC=""
if command -v cygpath >/dev/null 2>&1; then
    WIN="$(cygpath -u "${WINDIR:-C:\\Windows}")"
    for c in "$WIN/Microsoft.NET/Framework64/v4.0.30319/csc.exe" "$WIN/Microsoft.NET/Framework/v4.0.30319/csc.exe"; do
        [ -x "$c" ] && { CSC="$c"; break; }
    done
fi
w() { if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi; }
ARGS=(-target:winexe -optimize+ -platform:anycpu
      -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.IO.Compression.dll
      "-resource:$(w "$T/port.zip"),port.zip" "-resource:$(w "$T/lateral.jpg"),lateral.jpg"
      "-resource:$(w "$T/PirataOne-Regular.ttf"),PirataOne.ttf" "-resource:$(w "$T/pandora.ico"),pandora.ico"
      "-resource:$(w "$T/LICENSE"),LICENSE")
for r in "${WINRES[@]}"; do ARGS+=("${r%%:*}:$(w "${r#*:}")"); done
if [ -n "$CSC" ]; then
    MSYS2_ARG_CONV_EXCL='*' "$CSC" -nologo "${ARGS[@]}" -codepage:65001 "-out:$(w "$OUT")" "$(w "$SRC")"
elif command -v mcs >/dev/null 2>&1; then
    # contra las librerías de referencia de .NET Framework 4.5 (paquete mono-devel), no contra
    # las de Mono: estas tienen métodos que Windows no tiene (p. ej. string.TrimEnd(char)) y
    # el instalador fallaría allí con "Método no encontrado"
    API=""
    for d in "$(dirname "$(command -v mcs)")/../lib/mono/4.5-api" /usr/lib/mono/4.5-api /Library/Frameworks/Mono.framework/Versions/Current/lib/mono/4.5-api; do
        [ -f "$d/mscorlib.dll" ] && [ -f "$d/System.IO.Compression.dll" ] && { API="$(cd "$d" && pwd)"; break; }
    done
    [ -n "$API" ] || die "faltan las librerías de referencia de .NET Framework (4.5-api): instala mono-devel"
    MARGS=()
    for a in "${ARGS[@]}"; do
        case "$a" in -r:*) MARGS+=("-r:$API/${a#-r:}") ;; *) MARGS+=("$a") ;; esac
    done
    mcs -langversion:5 -nostdlib "-lib:$API" "-r:$API/mscorlib.dll" "-r:$API/System.dll" "-r:$API/System.Core.dll" \
        "${MARGS[@]}" "-out:$OUT" "$SRC"
else
    die "no hay compilador de C#: en Windows hace falta .NET Framework 4 (viene con Windows); en Linux, mono-mcs"
fi
ls -l "$OUT"
(cd "$(dirname "$OUT")" && sha256sum "$(basename "$OUT")" | tee "$(basename "$OUT").sha256")
echo "Listo: $OUT (súbelo como archivo de la versión en GitHub, Releases)"
