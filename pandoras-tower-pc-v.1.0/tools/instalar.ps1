# Instalador de Pandora's Tower para PC: hace de una vez todo lo de la guía.
#   - comprueba que la carpeta no tenga espacios ni acentos (si los tiene, se copia a C:\pandora-port)
#   - instala MSYS2 (las herramientas de compilación, gratuitas) si no está
#   - instala el compilador y las librerías
#   - pide la ISO de tu copia del juego y compila el port (setup.sh)
#   - prepara el lanzador y el acceso directo del escritorio (lanzador.sh)
# Se abre con Instalar.cmd. Se puede repetir: para actualizar el port tras
# descargar una versión nueva, o para añadir otra versión del juego.
# No descarga ningún archivo del juego: todo sale de tu ISO, en tu equipo.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$Host.UI.RawUI.WindowTitle = "Pandora's Tower - instalación"
Add-Type -AssemblyName System.Windows.Forms
[System.Windows.Forms.Application]::EnableVisualStyles()

function Paso([string]$t) { Write-Host ''; Write-Host "== $t" -ForegroundColor Yellow }
function Aviso([string]$t, [string]$icono = 'Information') {
    [void][System.Windows.Forms.MessageBox]::Show($t, "Pandora's Tower", 'OK', $icono)
}
function Pregunta([string]$t) {
    return [System.Windows.Forms.MessageBox]::Show($t, "Pandora's Tower", 'YesNo', 'Question') -eq 'Yes'
}
function Fallo([string]$t) {
    Write-Host ''
    Write-Host "ERROR: $t" -ForegroundColor Red
    Write-Host 'Lo que ha pasado está en build\instalar.log (adjúntalo si pides ayuda).'
    Aviso "No se pudo terminar la instalación:`n`n$t`n`nLo que ha pasado está en build\instalar.log." 'Error'
    try { Stop-Transcript | Out-Null } catch { }
    exit 1
}

# ---- 1. la carpeta: las herramientas de compilación no admiten espacios ni acentos
if ($root -match '[^A-Za-z0-9_\-\.:\\]') {
    $dest = 'C:\pandora-port'
    $msg = "La carpeta del port tiene espacios, acentos u otros signos:`n$root`n`n" +
           "Las herramientas de compilación no los admiten. ¿Lo copio a $dest y sigo desde allí?"
    if (-not (Pregunta $msg)) { Fallo "mueve la carpeta a una ruta sin espacios ni acentos (por ejemplo $dest) y vuelve a abrir Instalar.cmd." }
    Write-Host "Copiando el port a $dest..."
    & robocopy.exe $root $dest /E /NFL /NDL /NJH /NJS /NP /XD build wiikit | Out-Null
    if ($LASTEXITCODE -ge 8) { Fallo "no se pudo copiar el port a $dest." }
    & (Join-Path $dest 'tools\instalar.ps1')
    exit $LASTEXITCODE
}

New-Item -ItemType Directory -Force -Path (Join-Path $root 'build') | Out-Null
try { Start-Transcript -Path (Join-Path $root 'build\instalar.log') -Force | Out-Null } catch { }
Write-Host "Pandora's Tower para PC - instalación en $root"
Write-Host 'No cierres esta ventana: compilar el juego lleva unos 5 minutos (si hay que descargar MSYS2, de 5 a 10 más).'

# ---- 2. MSYS2
$msys = 'C:\msys64'
$bash = Join-Path $msys 'usr\bin\bash.exe'
if (-not (Test-Path $bash)) {
    Paso 'MSYS2: las herramientas de compilación (gratuitas, unos 500 MB)'
    if (-not (Pregunta "Para compilar el juego en tu equipo hace falta MSYS2 (gratuito, de msys2.org).`n`n¿Lo descargo e instalo en $msys?")) {
        Fallo "sin MSYS2 no se puede compilar. Instálalo desde https://www.msys2.org en $msys y vuelve a abrir Instalar.cmd."
    }
    $inst = Join-Path $env:TEMP 'msys2-x86_64-latest.exe'
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $ProgressPreference = 'SilentlyContinue'
        Write-Host 'Descargando MSYS2...'
        Invoke-WebRequest -UseBasicParsing -Uri 'https://github.com/msys2/msys2-installer/releases/latest/download/msys2-x86_64-latest.exe' -OutFile $inst
    } catch { Fallo "no se pudo descargar MSYS2 ($($_.Exception.Message)). Comprueba la conexión, o instálalo a mano desde https://www.msys2.org." }
    Write-Host 'Instalando MSYS2...'
    Start-Process -FilePath $inst -ArgumentList 'in', '--confirm-command', '--accept-messages', '--root', 'C:/msys64' -Wait
    if (-not (Test-Path $bash)) { Fallo "MSYS2 no se instaló. Instálalo a mano desde https://www.msys2.org en $msys y vuelve a abrir Instalar.cmd." }
}

# una orden en la terminal CLANG64 de MSYS2, en la carpeta del port; devuelve su código.
# La orden va en un archivo (build\paso.sh), no en la línea de comandos: así las comillas
# y los apóstrofos de las rutas (Pandora's Tower) llegan tal cual a bash.
$env:MSYSTEM = 'CLANG64'
$env:CHERE_INVOKING = '1'
$env:PT_ROOT = $root
$paso = Join-Path $root 'build\paso.sh'
function Msys([string]$cmd) {
    $text = "cd `"`$(cygpath -u `"`$PT_ROOT`")`" || exit 1`n$cmd`n"
    [System.IO.File]::WriteAllText($paso, $text, (New-Object System.Text.UTF8Encoding($false)))
    & $bash -l ($paso -replace '\\', '/') | Out-Host
    return $LASTEXITCODE
}

# ---- 3. el compilador y las librerías (si ya están todos, no se toca nada)
$pk = 'git mingw-w64-clang-x86_64-clang mingw-w64-clang-x86_64-cmake mingw-w64-clang-x86_64-ninja ' +
      'mingw-w64-clang-x86_64-python mingw-w64-clang-x86_64-python-pycryptodome mingw-w64-clang-x86_64-sdl3'
if ((Msys "pacman -Q $pk >/dev/null 2>&1") -eq 0) {
    Write-Host 'Las herramientas de compilación ya están instaladas.'
} else {
    Paso 'Actualizando MSYS2'
    # la primera actualización puede reiniciar el propio pacman: por eso, dos veces
    [void](Msys 'pacman -Syu --noconfirm')
    [void](Msys 'pacman -Syu --noconfirm')
    Paso 'Instalando el compilador y las librerías'
    if ((Msys "pacman -S --needed --noconfirm $pk") -ne 0) { Fallo 'no se pudieron instalar los paquetes de MSYS2 (¿hay conexión a Internet?).' }
}

# ---- 4. la ISO y la compilación
$compiled = (Test-Path (Join-Path $root 'build\source.txt')) -or (Test-Path (Join-Path $root 'build\jp\source.txt'))
$iso = $null
if (-not $compiled -or (Pregunta "El juego ya está compilado: ahora se recompila con esta versión del port.`n`n¿Quieres añadir además otra ISO (otra versión del juego)?")) {
    Paso 'Elige la ISO de tu copia del juego'
    $dlg = New-Object System.Windows.Forms.OpenFileDialog
    $dlg.Title = "Elige la ISO de Pandora's Tower (de tu copia del juego)"
    $dlg.Filter = 'Imagen ISO de Wii (*.iso)|*.iso'
    if ($dlg.ShowDialog() -ne 'OK') {
        if (-not $compiled) { Fallo "hace falta la ISO de tu copia del juego (una WBFS o RVZ se convierte a ISO con Dolphin)." }
    } else { $iso = $dlg.FileName }
}
Paso 'Compilando el juego (unos 5 minutos en un PC actual)'
if ($iso) {
    $env:PT_ISO = $iso
    if ((Msys './setup.sh "$(cygpath -u "$PT_ISO")"') -ne 0) { Fallo 'falló la compilación (setup.sh): mira las últimas líneas de esta ventana.' }
}
if ($compiled -or -not $iso) {
    if ((Msys './setup.sh') -ne 0) { Fallo 'falló la compilación (setup.sh): mira las últimas líneas de esta ventana.' }
}

# ---- 5. el lanzador y el acceso directo
Paso 'Preparando el lanzador y el acceso directo del escritorio'
if ((Msys './lanzador.sh') -ne 0) { Fallo 'no se pudo preparar el lanzador (lanzador.sh).' }

Write-Host ''
Write-Host "Listo. Abre «Pandora's Tower» desde el escritorio." -ForegroundColor Green
try { Stop-Transcript | Out-Null } catch { }
if (Pregunta "Instalación terminada.`n`nPara jugar, abre «Pandora's Tower» desde el escritorio (o Jugar.cmd en la carpeta del port).`n`n¿Lo abro ahora?") {
    Start-Process -FilePath (Join-Path $root 'Jugar.cmd') -WorkingDirectory $root
}
