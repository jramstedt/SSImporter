# SSImporter
System Shock's assets importer for Unity. Requires Unity 5.1.

## Instructions  
###Notes
**This project is still under heavy development. Anything can change!**

You will get error messages about enums, they are harmless. Example of one is provided below.
`Unsupported enum type 'SystemShock.Resource.Enemy.TraitFlags' used for field 'Trait' in class 'Enemy'`

Prefab importing can take couple of minutes.

Before running map import, please restart Unity. Font importing is broken in Unity and new fonts will only work after restart.

Map importing will take really long time for the first time after you start Unity.
Unity does somekind of internal loading when assets are used for the first time.
Following map imports in same session are fast.

Map importing currently imports only one map. Map can be changed by editing Assets/SSImporter/Editor/MapImport.cs at line 38

You can use character prefabs from standard assets.
They will have to be resized to match the level scale. 0.7 for height seems to work nicely. Don't forget to change speed settings too.

###Procedure   
1. Create new Unity project (3D, No asset packages needed)
2. Clone repository to Unity projects Assets folder. (or copy SSImporter directory)
   Unity should now import and compile everything.
3. Run every importer in order from Assets/SystemShock menu.
Starting from 0. Set RES Path. It should ask for folder. Select your RES folder.
Run 1. Create Object Factory
Run 2. ...
 
## Todo
- Palette rotated textures
  - Each palette chunk (5 colors) is rotated at different speed. Use multi layered material (one layer per chunk)?
- Bitmap upsampling (some sprites seems to be upsampled)
- Combine tiles to create uniform collision mesh from level geometry.
- Code to weld and optimize level & decal mesh.
- Sprite library inspector (preview sprites)
- Sound import
- Intro/Ending movie import
- Look into MAC resource files
	- Higher resolution UI graphics?
	- Quick time Intro/Ending videos
- Mini map
- Mini games http://pastebin.com/epfmWNQJ
- Trigger graph
- Music (Port https://github.com/CoderLine/alphaSynth to Unity?)
- Code cleanup/refactoring
- Make import automated (Wizard)

## Thanks
- Makers of THE UNOFFICIAL SYSTEM SHOCK SPECIFICATIONS 
- ToxicFrog (https://github.com/ToxicFrog/ss1edit) 
- Makers of The System Shock Hack Project (http://tsshp.sourceforge.net/) 
- Makers of eXtendable Wad Editor (http://www.doomworld.com/xwe/) 
- Makers of SYSTEMSHOCK-Portable (https://www.systemshock.org/index.php?topic=211.0) 