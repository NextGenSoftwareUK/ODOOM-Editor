ExtractOquakeSprites - Import Quake 1 sprites for OASIS STAR display pack
============================================================================

This tool reads Quake 1 id1/pak0.pak, extracts sprites from progs/*.mdl models,
and writes PNGs named by Doom thing type into the UDB OASIS Sprites folder.
The editor then uses these for OQUAKE assets (keys, weapons, health, armor,
ammo, monsters).

TWO EXTRACTION MODES
  Skin crop (default):
    - Reads the raw MDL skin texture and crops the front half
    - Fast; works well for keys, weapons, and items

  MDL 3D render (--render):
    - Parses MDL vertices, UV coordinates, and triangle mesh
    - Software-rasterizes frame 0 in a front-facing orthographic projection
    - Applies UV-mapped skin texture with Lambert diffuse shading
    - Produces a proper "in-game screenshot" sprite for monsters
    - Keys always use skin crop even in --render mode (skin crop is better for keys)

REQUIREMENTS
  - Quake 1 game data: a folder containing pak0.pak (e.g. from Steam, GOG,
    or a full Quake 1 install). The folder is usually named "id1".

USAGE
  ExtractOquakeSprites.exe [id1_path] [output_sprites_path]
  ExtractOquakeSprites.exe --render  [id1_path] [output_sprites_path]
  ExtractOquakeSprites.exe --list    [id1_path]   list progs/*.mdl in pak

  Verbose output (pak entry count, skip reasons) is always on.

  id1_path            Folder that contains pak0.pak (default: C:\Source\vkQuake\id1)
  output_sprites_path Where to write 5xxx.png, etc. (default: UDB Assets\...\OASIS\Sprites)

EXAMPLES
  "C:\Program Files (x86)\Steam\steamapps\common\Quake\id1"
  "C:\GOG Games\Quake\id1"
  ExtractOquakeSprites.exe --render "C:\Program Files (x86)\Steam\steamapps\common\Quake\id1"

After running, open your map in Ultimate Doom Builder; OQUAKE thing types
that have a PNG in the Sprites folder will show the Quake sprite in 2D/3D.








