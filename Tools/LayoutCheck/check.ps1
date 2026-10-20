# Layout checks for the zine PDFs. Run from anywhere; paths are relative to the vault root.
#
#   .\Tools\LayoutCheck\check.ps1 render "UnchartedWaters/Setting Guide/0[5-9]*.md"
#       PDF + per-page PNGs into %TEMP%\LayoutCheck, with a page count per file.
#
#   .\Tools\LayoutCheck\check.ps1 width "UnchartedWaters/Setting Guide/*.md"
#       Lists \smalltables cells that would wrap (default limit 312pt; -Limit to change).
param(
    [Parameter(Mandatory)][ValidateSet('render', 'width')][string]$Mode,
    [Parameter(Mandatory)][string[]]$Files,
    [float]$Limit = 312,
    [string]$OutDir = (Join-Path $env:TEMP 'LayoutCheck')
)

$vaultRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
# @() keeps a single match an array; a bare string would splat one character per argument.
$paths = @(foreach ($f in $Files) {
    Get-ChildItem -Path (Join-Path $vaultRoot $f) -File | ForEach-Object FullName
})
if (-not $paths) { Write-Error "No files matched: $Files"; exit 1 }

$project = Join-Path $PSScriptRoot 'LayoutCheck.csproj'
if ($Mode -eq 'render') {
    dotnet run --project $project -- render $vaultRoot $OutDir @paths
} else {
    dotnet run --project $project -- width $Limit @paths
}
exit $LASTEXITCODE
