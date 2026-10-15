<#
  Mecha Keys - replace the Steam Workshop preview with the animated preview.gif via SteamCMD.

  tModLoader's in-game publisher can only upload a static PNG, so run this AFTER every
  in-game publish/update (each in-game publish puts the static icon_workshop.png back).

  Only the preview image is changed. The .tmod, title, description, tags and tModLoader's
  version metadata are not touched, because the VDF has no contentfolder/title/description.

  Usage (double-click upload_preview.bat, or):
    powershell -ExecutionPolicy Bypass -File upload_preview.ps1 [-SteamUser name] [-PublishedFileId 123] [-SteamCmd path] [-ChangeNote "text"]
#>
param(
	[string]$SteamUser = "",
	[string]$PublishedFileId = "",
	[string]$SteamCmd = "",
	[string]$ChangeNote = ""
)

$ErrorActionPreference = "Stop"
$AppId = "1281930"            # tModLoader
$ModName = "MechaKeys"
$Here = Split-Path -Parent $MyInvocation.MyCommand.Path
$Gif = Join-Path $Here "preview.gif"
$IdFile = Join-Path $Here "publishedfileid.txt"
$Vdf = Join-Path $Here "workshop_preview.generated.vdf"

function Fail($msg) { Write-Host ""; Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------- preview file
if (-not (Test-Path $Gif)) { Fail "preview.gif not found next to this script." }
$size = (Get-Item $Gif).Length
if ($size -ge 1MB) { Fail "preview.gif is $([math]::Round($size / 1KB)) KB; Steam previews must be under 1 MB." }
Write-Host "Preview: $Gif ($([math]::Round($size / 1KB)) KB)"

# ---------------------------------------------------------------- Steam folders
function Get-SteamRoots {
	$roots = @()
	try { $roots += (Get-ItemProperty "HKCU:\Software\Valve\Steam" -ErrorAction Stop).SteamPath } catch {}
	$roots += "C:\Steam", "${env:ProgramFiles(x86)}\Steam", "$env:ProgramFiles\Steam"
	$libs = @()
	foreach ($r in $roots) {
		if (-not $r -or -not (Test-Path $r)) { continue }
		$libs += $r
		$lf = Join-Path $r "steamapps\libraryfolders.vdf"
		if (Test-Path $lf) {
			foreach ($m in [regex]::Matches((Get-Content $lf -Raw), '"path"\s+"([^"]+)"')) {
				$libs += $m.Groups[1].Value -replace '\\\\', '\'
			}
		}
	}
	return $libs | ForEach-Object { $_ -replace '/', '\' } | Select-Object -Unique
}

# ---------------------------------------------------------------- published file id
if (-not $PublishedFileId -and (Test-Path $IdFile)) {
	$PublishedFileId = (Get-Content $IdFile -Raw).Trim()
}
if (-not $PublishedFileId) {
	# tModLoader keeps a copy of your own published mod in the workshop content folder
	foreach ($lib in Get-SteamRoots) {
		$content = Join-Path $lib "steamapps\workshop\content\$AppId"
		if (-not (Test-Path $content)) { continue }
		$hit = Get-ChildItem $content -Recurse -Filter "$ModName.tmod" -ErrorAction SilentlyContinue | Select-Object -First 1
		if ($hit) {
			$rel = $hit.FullName.Substring($content.Length).TrimStart('\')
			$PublishedFileId = $rel.Split('\')[0]
			Write-Host "Found published item $PublishedFileId in $content"
			break
		}
	}
}
if (-not $PublishedFileId) {
	Write-Host ""
	Write-Host "Could not find the Workshop item ID automatically."
	Write-Host "Open your mod's Workshop page; the ID is the number after ?id= in the address."
	Write-Host "(Publish the mod once from tModLoader first if you have not yet.)"
	$PublishedFileId = Read-Host "Published file ID"
}
$PublishedFileId = $PublishedFileId.Trim()
if ($PublishedFileId -notmatch '^\d+$') { Fail "'$PublishedFileId' is not a valid Workshop item ID." }
Set-Content -Path $IdFile -Value $PublishedFileId -Encoding ASCII

# ---------------------------------------------------------------- steamcmd
$candidates = @($SteamCmd, $env:STEAMCMD, (Join-Path $Here "steamcmd\steamcmd.exe"), "C:\steamcmd\steamcmd.exe", "C:\SteamCMD\steamcmd.exe") | Where-Object { $_ }
$SteamCmd = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $SteamCmd) {
	Write-Host ""
	$answer = Read-Host "steamcmd.exe was not found. Download it from Valve into SteamWorkshop\steamcmd now? (y/n)"
	if ($answer -notmatch '^[yY]') { Fail "Install SteamCMD (https://developer.valvesoftware.com/wiki/SteamCMD) and pass -SteamCmd <path>, or set the STEAMCMD environment variable." }
	$dir = Join-Path $Here "steamcmd"
	New-Item -ItemType Directory -Force -Path $dir | Out-Null
	$zip = Join-Path $dir "steamcmd.zip"
	Invoke-WebRequest "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip" -OutFile $zip
	Expand-Archive $zip -DestinationPath $dir -Force
	Remove-Item $zip
	$SteamCmd = Join-Path $dir "steamcmd.exe"
}
Write-Host "SteamCMD: $SteamCmd"

# ---------------------------------------------------------------- vdf
$previewPath = (Resolve-Path $Gif).Path -replace '\\', '/'
$lines = @(
	'"workshopitem"',
	'{',
	"`t`"appid`"`t`t`"$AppId`"",
	"`t`"publishedfileid`"`t`"$PublishedFileId`"",
	"`t`"previewfile`"`t`"$previewPath`""
)
if ($ChangeNote) { $lines += "`t`"changenote`"`t`"$($ChangeNote -replace '"', "'")`"" }
$lines += '}'
Set-Content -Path $Vdf -Value $lines -Encoding ASCII
Write-Host "VDF: $Vdf"
Get-Content $Vdf | ForEach-Object { Write-Host "    $_" }

# ---------------------------------------------------------------- upload
if (-not $SteamUser) { $SteamUser = Read-Host "Steam account name (the account that owns the Workshop item)" }
if (-not $SteamUser) { Fail "A Steam account name is required." }

Write-Host ""
Write-Host "Starting SteamCMD. Enter your password and Steam Guard code if it asks." -ForegroundColor Cyan
& $SteamCmd +login $SteamUser +workshop_build_item "$Vdf" +quit
$code = $LASTEXITCODE
Write-Host ""
if ($code -eq 0) {
	Write-Host "Done. Check https://steamcommunity.com/sharedfiles/filedetails/?id=$PublishedFileId" -ForegroundColor Green
	Write-Host "(Steam can take a few minutes to show the new preview.)"
} else {
	Fail "SteamCMD exited with code $code. Scroll up for its message."
}
