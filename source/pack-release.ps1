$ErrorActionPreference = 'Stop'
# A full step is major.minor (0.1). The tag matches app.manifest (v0.1.0).
# The app shows v0.1 when the patch is 0.
# Velopack CLI. Keep this version aligned with the Velopack package in build.ps1.
$vpkVersion = '1.2.161'
$root = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
$tag = $env:RELEASE_TAG
if ([string]::IsNullOrWhiteSpace($tag)) { throw 'Set RELEASE_TAG to the release tag, for example v0.1.0.' }
if ($tag -notmatch '^v(\d+\.\d+\.\d+)$') { throw "Tag '$tag' must look like v0.1.0." }
$version = $Matches[1]
$identity = [string]([xml](Get-Content -LiteralPath (Join-Path $PSScriptRoot 'app.manifest') -Raw)).assembly.assemblyIdentity.version
$manifestVersion = ([Version]$identity).ToString(3)
if ($version -ne $manifestVersion) { throw "Tag $tag does not match app.manifest version $identity. The pack version stays $manifestVersion." }

$exe = Join-Path $root 'Chest.exe'
if (-not (Test-Path -LiteralPath $exe)) { throw 'Chest.exe is missing. Run source/build.ps1 first.' }
$stage = Join-Path $root 'publish'
if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Path $stage | Out-Null
Copy-Item -LiteralPath $exe -Destination (Join-Path $stage 'Chest.exe')
foreach ($dll in @('Velopack.dll', 'Newtonsoft.Json.dll')) {
    $path = Join-Path $root $dll
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing $dll beside Chest.exe. Run source/build.ps1 first." }
    Copy-Item -LiteralPath $path -Destination (Join-Path $stage $dll)
}
$config = Join-Path $PSScriptRoot 'Chest.exe.config'
Copy-Item -LiteralPath $config -Destination (Join-Path $stage 'Chest.exe.config')
Copy-Item -LiteralPath (Join-Path $root 'assets') -Destination (Join-Path $stage 'assets') -Recurse
foreach ($doc in @('README.md', 'LICENSE.txt', 'THIRD_PARTY.txt', 'CHANGELOG.md')) {
    Copy-Item -LiteralPath (Join-Path $root $doc) -Destination (Join-Path $stage $doc)
}
$display = $version
if ($version -match '^(\d+)\.(\d+)\.0$') { $display = "$($Matches[1]).$($Matches[2])" }
$changelog = [IO.File]::ReadAllText((Join-Path $root 'CHANGELOG.md'))
$match = [regex]::Match($changelog, "(?ms)^## (?:$([regex]::Escape($version))|$([regex]::Escape($display)))\r?\n(.*?)(?=^## |\z)")
if (-not $match.Success) { throw "CHANGELOG.md has no section for $version." }
$notesPath = Join-Path $root 'release-notes.md'
[IO.File]::WriteAllText($notesPath, ("## v{0}`r`n{1}`r`n" -f $display, $match.Groups[1].Value.Trim()))

$out = Join-Path $root 'release-out'
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Recurse -Force }
$vpk = Join-Path $env:USERPROFILE ".dotnet\tools\vpk.exe"
if (-not (Test-Path -LiteralPath $vpk)) { $vpk = 'vpk' }
$splash = Join-Path $root 'assets\branding\chest-splash.png'
if (-not (Test-Path -LiteralPath $splash)) { throw "Missing splash image $splash." }
& $vpk pack `
    --packId Chest `
    --packVersion $version `
    --packDir $stage `
    --mainExe Chest.exe `
    --packTitle 'Chest' `
    --packAuthors Zenity `
    --icon (Join-Path $root 'assets\branding\chest.ico') `
    --splashImage $splash `
    --framework net48 `
    --releaseNotes $notesPath `
    --outputDir $out
if ($LASTEXITCODE -ne 0) { throw "vpk $vpkVersion pack failed." }
Get-ChildItem -LiteralPath $out | ForEach-Object { Write-Output $_.FullName }
