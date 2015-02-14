# SSImporter
System Shock's assets importer for Unity. Requires Unity 5.0.

## Todo
- Object positioning still off (Level 9, grates and doors)
- Object creation code remake and runtime support
- Fix/try different approach on decals
  - MaterialPropertyBlock is not serialized
  - Change to mesh projection instead of plane
- Animated screens
- Animated tile textures
- Palette rotated textures
  - Each palette chunk (5 colors) is rotated at different speed. Use multi layered material (one layer per chunk)?
- Change SpriteRenderer to MeshRenderer and plane mesh
- Screens with text
- Model 0-material override with animated texture material. (Just like animated screens)
- Combine tiles to create uniform collision mesh from level geometry.
- Code to weld and optimize level mesh.
- Directional sprites
  - Enemies
- Enemy animations
- UI graphics import
- Sound import
- Intro/Ending movie import
- Look into MAC resource files
	- Higher resolution UI graphics?
	- Quick time Intro/Ending videos
- Mini map
- Mini games http://pastebin.com/epfmWNQJ
- Trigger graph
- Music
- Code cleanup/refactoring
- Make import automated (Wizard)

## Thanks
- Makers of THE UNOFFICIAL SYSTEM SHOCK SPECIFICATIONS 
- ToxicFrog (https://github.com/ToxicFrog/ss1edit) 
- Makers of The System Shock Hack Project (http://tsshp.sourceforge.net/) 
- Makers of eXtendable Wad Editor (http://www.doomworld.com/xwe/) 
- Makers of SYSTEMSHOCK-Portable (https://www.systemshock.org/index.php?topic=211.0) 