param(
    [string]$Dll = "$PSScriptRoot\..\bin\Release\net462\Hufsa.dll",
    [string]$OutDir = "$PSScriptRoot\..\dist"
)
# Builds the Thunderstore zip: manifest, README, icon, and the mod (dll, loop, sounds) at the root, which
# mod managers install to BepInEx/plugins/<team>-Hufsa/. The version comes from thunderstore/manifest.json.
$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot\.."
$manifest = Get-Content "$root\thunderstore\manifest.json" -Raw | ConvertFrom-Json
$version = $manifest.version_number

$stage = Join-Path $OutDir "stage"
if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
New-Item -ItemType Directory -Force "$stage\sounds" | Out-Null

Copy-Item "$root\thunderstore\manifest.json", "$root\thunderstore\README.md", "$root\thunderstore\icon.png" $stage
if (Test-Path "$root\CHANGELOG.md") { Copy-Item "$root\CHANGELOG.md" $stage }
Copy-Item $Dll, "$root\Assets\hufsa_loop.mp3" $stage
Get-ChildItem "$root\Assets\sounds" -File | Where-Object Extension -in '.ogg', '.wav', '.mp3' | Copy-Item -Destination "$stage\sounds"

$zip = Join-Path $OutDir "Hufsa-$version.zip"
if (Test-Path $zip) { Remove-Item $zip }
# Written entry by entry with forward slashes: Windows PowerShell's Compress-Archive uses backslashes,
# which mod managers can choke on.
Add-Type -AssemblyName System.IO.Compression, System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    $base = (Resolve-Path $stage).Path.TrimEnd('\') + '\'
    foreach ($f in Get-ChildItem $stage -Recurse -File) {
        $entry = $f.FullName.Substring($base.Length).Replace('\', '/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $f.FullName, $entry)
    }
}
finally { $archive.Dispose() }
Remove-Item $stage -Recurse -Force
"Packaged $zip"
