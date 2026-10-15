# Animated Workshop preview (SteamCMD)

tModLoader's in-game **Publish** button can only upload a static PNG (`icon_workshop.png`).
Steam itself accepts an animated GIF as a Workshop preview, so this folder holds everything
needed to swap the preview for `preview.gif` with SteamCMD.

This folder is listed in `buildIgnore`, so none of it ends up inside the `.tmod`.

| File | What it is |
| --- | --- |
| `preview.gif` | 512×512 animated preview (≈310 KB; Steam's limit is 1 MB). The keycap clicks once per switch colour. |
| `preview_small.gif` | 128×128 version, for a README or forum post. |
| `upload_preview.bat` | Double-click this. Runs `upload_preview.ps1`. |
| `upload_preview.ps1` | Finds your Workshop item ID and SteamCMD, writes the VDF, runs the upload. |
| `workshop_preview.template.vdf` | The same VDF by hand, if you prefer to run SteamCMD yourself. |

## Steps

1. **Publish from tModLoader first, as usual.** Workshop → Develop Mods → Mecha Keys → Build, then Publish.
   This uploads the `.tmod` together with the version, side and dependency metadata that only tModLoader
   can write. SteamCMD can't set those, so don't upload the mod content itself through SteamCMD.
2. **Run `upload_preview.bat`.** The script will:
   - find the Workshop item ID (it looks for your own published `MechaKeys.tmod` under
     `steamapps\workshop\content\1281930\<id>\`, or asks for the number after `?id=` on the Workshop page)
     and saves it to `publishedfileid.txt` for next time,
   - find `steamcmd.exe` (`-SteamCmd <path>`, the `STEAMCMD` environment variable, `SteamWorkshop\steamcmd\`
     or `C:\steamcmd\`), or offer to download it from Valve,
   - write `workshop_preview.generated.vdf` with only `appid`, `publishedfileid` and `previewfile`,
   - run `steamcmd +login <you> +workshop_build_item <vdf> +quit`. Enter your password and Steam Guard code when asked.
3. **Run it again after every in-game publish.** Each tModLoader publish puts the static `icon_workshop.png` back.

Command line options:

```
powershell -ExecutionPolicy Bypass -File upload_preview.ps1 -SteamUser yourname -PublishedFileId 1234567890 -SteamCmd C:\steamcmd\steamcmd.exe -ChangeNote "New animated preview"
```

`-ChangeNote` is optional. Without it, no change-log entry is added.

## Doing it by hand

Copy `workshop_preview.template.vdf`, replace `YOUR_WORKSHOP_ITEM_ID`, and check the `previewfile` path. Then run:

```
steamcmd +login yourname +workshop_build_item "C:\path\to\workshop_preview.vdf" +quit
```

Leave out `contentfolder`, `title`, `description` and `visibility`. Any field you leave out stays as it is on
the Workshop. A `contentfolder` line would replace the mod files that tModLoader uploaded.

## Regenerating the art

`tools/make_icons.py` (Python + Pillow) redraws `icon.png`, `icon_small.png`, `icon_workshop.png`,
`Assets/Textures/LogoSheet.png`, `Assets/Textures/LogoAccent.png` and both GIFs:

```
python tools/make_icons.py . SteamWorkshop
```
