<#
.SYNOPSIS
    Publie une nouvelle version de WorldBuilderCoop : compile, met à jour mod.json,
    committe le .dll dans dist/, crée un tag et pousse. La GitHub Action prend
    alors le relais (release + asset), puis le BP-Mods-Registry référence le mod.

.EXAMPLE
    .\publish.ps1 -Version 1.0.1
    .\publish.ps1 -Version 1.0.2 -Message "Fix sync joueurs" -NoBuild

.NOTES
    À lancer en local : c'est la seule machine qui possède les DLL du jeu
    nécessaires à la compilation.
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[A-Za-z0-9.]+)?$')]
    [string]$Version,

    [string]$Message = "",

    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    # Réutilise le dernier .dll déjà compilé au lieu de relancer msbuild.
    [switch]$NoBuild
)

$ErrorActionPreference = "Stop"
$ScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $ScriptRoot

function Fail($msg) { Write-Host "ERREUR: $msg" -ForegroundColor Red; exit 1 }
function Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }

$Project = Join-Path $ScriptRoot "WorldBuilderCoop\WorldBuilderCoop.csproj"
$ModJson = Join-Path $ScriptRoot "mod.json"
$DistDll = Join-Path $ScriptRoot "dist\WorldBuilderCoop.dll"
$Tag = "v$Version"

if (-not (Test-Path $Project)) { Fail "csproj introuvable : $Project" }
if (-not (Test-Path $ModJson)) { Fail "mod.json introuvable : $ModJson" }

# --- Garde-fous git ---
$branch = (git rev-parse --abbrev-ref HEAD).Trim()
if ($branch -ne "main") { Fail "Tu es sur la branche '$branch', bascule sur 'main' d'abord." }
if (git tag -l $Tag) { Fail "Le tag $Tag existe déjà. Choisis une autre version." }

# --- Compilation ---
if ($NoBuild) {
    Step "Build ignoré (-NoBuild)"
    $built = Join-Path $ScriptRoot "WorldBuilderCoop\bin\$Configuration\WorldBuilderCoop.dll"
    if (-not (Test-Path $built)) {
        $built = Get-ChildItem -Path (Join-Path $ScriptRoot "WorldBuilderCoop\bin") -Recurse -Filter "WorldBuilderCoop.dll" -ErrorAction SilentlyContinue |
                 Sort-Object LastWriteTime -Descending | Select-Object -First 1 -ExpandProperty FullName
    }
    if (-not $built) { Fail "Aucun .dll compilé trouvé. Compile dans Visual Studio ou enlève -NoBuild." }
}
else {
    Step "Recherche de MSBuild"
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    if (-not (Test-Path $vswhere)) { Fail "vswhere introuvable. Installe Visual Studio, ou utilise -NoBuild." }
    $msbuild = & $vswhere -latest -prerelease -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
               Select-Object -First 1
    if (-not $msbuild) { Fail "MSBuild introuvable via vswhere. Utilise -NoBuild." }

    Step "Compilation $Configuration"
    & $msbuild $Project /p:Configuration=$Configuration /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { Fail "Échec de la compilation." }
    $built = Join-Path $ScriptRoot "WorldBuilderCoop\bin\$Configuration\WorldBuilderCoop.dll"
    if (-not (Test-Path $built)) { Fail "DLL attendu introuvable après build : $built" }
}
Write-Host "    DLL : $built" -ForegroundColor DarkGray

# --- Mise à jour de mod.json (remplacement ciblé pour préserver le formatage) ---
Step "mod.json → version $Version"
$content = Get-Content $ModJson -Raw
$new = [regex]::Replace($content, '("version"\s*:\s*")[^"]*(")', "`${1}$Version`${2}")
if ($new -eq $content -and $content -notmatch "`"version`"\s*:\s*`"$([regex]::Escape($Version))`"") {
    Fail "Champ 'version' introuvable dans mod.json."
}
Set-Content -Path $ModJson -Value $new -Encoding UTF8 -NoNewline

# --- Copie de l'asset dans dist/ ---
Step "Copie → dist\WorldBuilderCoop.dll"
New-Item -ItemType Directory -Force -Path (Split-Path $DistDll) | Out-Null
Copy-Item $built $DistDll -Force

# --- Commit + tag + push ---
Step "Commit, tag $Tag et push"
$commitMsg = if ($Message) { "Release $Tag - $Message" } else { "Release $Tag" }
git add -A
git add -f $DistDll
git commit -m $commitMsg | Out-Null
git tag -a $Tag -m $commitMsg
git push origin main --follow-tags
if ($LASTEXITCODE -ne 0) { Fail "git push a échoué." }

Write-Host ""
Write-Host "OK ! $Tag poussé." -ForegroundColor Green
Write-Host "  - Release en cours : https://github.com/Expected333/WorldBuilderCoop/actions" -ForegroundColor Gray
Write-Host "  - Le mod apparaîtra dans le registry dans l'heure (ou déclenche-le manuellement) :" -ForegroundColor Gray
Write-Host "    https://expected333.github.io/BP-Mods-Registry/index.json" -ForegroundColor Gray
