# Mecha Keys tools

These are not packaged into the mod (`tools\*` is in `buildIgnore`).

- `synth_switches.py`: generates the original switch packs in `Bundled/Keyboards/` (gateron_yellow, oil_king,
  holy_panda, boba_u4, kailh_box_jade, kailh_box_navy, alps_skcm_blue, topre, buckling_spring, kailh_choc_red,
  silent_linear, membrane, typewriter, mouse_classic, mouse_optical, mouse_silent). Every clip is built from
  filtered noise, resonators and damped sines, so no recorded audio is involved. Needs Python 3 with numpy and
  scipy, plus ffmpeg (with libvorbis) on PATH.

      python tools/synth_switches.py Bundled/Keyboards --variants 32 --demo demo.ogg
      python tools/synth_switches.py Bundled/Keyboards --only topre typewriter

- `make_icons.py`: draws the pixel-art icon, the in-game logo frames and the animated Workshop GIFs (Pillow).

      python tools/make_icons.py . SteamWorkshop
