# Lanzador de Pandora's Tower: carga Lanzador.cs (la ventana) con Add-Type.
# Solo hace falta si no se pudo compilar PandorasTower.exe (lanzador.sh lo
# compila con el csc.exe de Windows); el acceso directo usa el .exe si existe.
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$src = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot 'Lanzador.cs')
Add-Type -TypeDefinition $src -Language CSharp -ReferencedAssemblies System.Windows.Forms, System.Drawing
[PandoraLauncher.Program]::Run($root, [string[]]@())
