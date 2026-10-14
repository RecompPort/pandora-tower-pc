# shellcheck shell=bash
# copy_dlls EXE: copia junto a EXE las DLL de MSYS2 CLANG64 que usa (SDL3,
# libc++...), para que arranque fuera de la terminal. Solo en Windows: en
# otro sistema no hace nada. La usan setup.sh (tras compilar) y lanzador.sh.
copy_dlls() {
    command -v cygpath >/dev/null 2>&1 || return 0
    local exe="$1" dir n=0 dll
    dir="$(dirname "$exe")"
    [ -f "$exe" ] || return 0
    local list
    if command -v ldd >/dev/null 2>&1; then
        list="$(ldd "$exe" 2>/dev/null | awk '$3 ~ /\/clang64\/bin\// {print $3}')"
    fi
    if [ -z "${list:-}" ]; then                     # sin ldd: las de siempre
        list=""
        for dll in SDL3.dll libc++.dll libunwind.dll libwinpthread-1.dll; do
            [ -f "/clang64/bin/$dll" ] && list+="/clang64/bin/$dll"$'\n'
        done
    fi
    while IFS= read -r dll; do
        [ -n "$dll" ] || continue
        cp -u "$dll" "$dir/" && n=$((n + 1))
    done <<< "$list"
    echo "DLL junto al juego: $n"
}
