# <img src="https://i.imgur.com/UGF4WmA.png" width=32> Image Map

Image Map converts images into Minecraft maps using the latest map colors. It works with both Java Edition and Bedrock Edition worlds.

This repository is a fork of [tryashtar/image-map](https://github.com/tryashtar/image-map) with additional world discovery, preview, inventory, and command-line features.

## Features

- Java Edition and Bedrock Edition world support.
- Automatic detection of Java, Bedrock UWP/Preview, Bedrock GDK, Education Edition, NetEase, OptiCraft, and custom world folders.
- Import a single image, a folder of images, dropped files, or an image from the clipboard.
- Split an image into a map grid and preview the result before importing.
- Rotate, flip, change the background, and adjust scaling, fill mode, color matching, and dithering.
- Preview existing and imported maps.
- Change map IDs, delete maps, and export maps as images.
- Generate structure blocks for map walls, with optional glowing or invisible item frames where the world version supports them.
- Add maps and structure blocks directly to a player inventory.
- Automatically pack large imports into chests when there are not enough free inventory slots.
- Undo and redo map changes.
- Batch import, export, list, delete, and ID changes from the command-line interface.
- Optional command-line logging of the first and last map IDs created by an import.

## Requirements

- Windows 10 or Windows 11 for the WPF GUI.
- .NET 6 Desktop Runtime for framework-dependent GUI builds, or .NET 6 Runtime for the CLI.
- Self-contained builds do not require a separately installed .NET runtime.
- Java worlds should be closed before writing. Bedrock worlds should also be closed in Minecraft before writing.

Always back up a world before modifying it.

The publish targets are `win-x64`, `win-x86`, `win-arm64`, and `linux-x64`. The Linux target contains the CLI only because the GUI uses WPF. Windows ARM32 is not currently supported.

## Using the GUI

### Worlds tab

The Worlds tab lists detected Java and Bedrock worlds. Select a world to open it, or double-click it to open the world and switch to the Maps tab.

The **Java Worlds** and **Bedrock Worlds** buttons open a folder picker with automatically detected locations. The picker supports:

- Checking one or more world folders.
- **Browse...** to add a folder manually.
- **Select All**.
- **Replace current list** to replace the current selection instead of adding to it.
- Holding <kbd>Shift</kbd> while opening the picker to add folders without replacing the existing list.

Typical detected locations include:

- Java: `%APPDATA%\.minecraft\saves`
- Bedrock UWP: `%LOCALAPPDATA%\Packages\Microsoft.MinecraftUWP_8wekyb3d8bbwe\LocalState\games\com.mojang\minecraftWorlds`
- Bedrock Preview UWP: `%LOCALAPPDATA%\Packages\Microsoft.MinecraftWindowsBeta_8wekyb3d8bbwe\LocalState\games\com.mojang\minecraftWorlds`
- Bedrock GDK: `%APPDATA%\Minecraft Bedrock\Users\<user>\games\com.mojang\minecraftWorlds`
- Bedrock Preview GDK: `%APPDATA%\Minecraft Bedrock Preview\Users\<user>\games\com.mojang\minecraftWorlds`
- Education Edition, NetEase, OptiCraft, legacy Win32Craft folders, and cloned UWP packages.

You can also drag and drop a world folder directly onto the program, or pass a world folder as the first command-line argument to `ImageMap.exe`. Additional arguments are treated as images to open.

Right-click a world to open its folder in Explorer.

### Maps tab

The Maps tab has two sides:

- **Importing** on the left: images waiting to be converted and sent to the world.
- **In World** on the right: maps that already exist in the selected world.

Open images with **Open**, by dragging and dropping them, or by pasting them with <kbd>Ctrl+V</kbd>. Imported images go through a preview window where you can adjust:

- Grid width and height.
- Rotation and horizontal/vertical flipping.
- Background color.
- Scaling: automatic, pixel art, or bicubic.
- Fill mode: uniform, stretch, or crop.
- Color algorithm: Good Fast, Euclidean, CIEDE2000, CIE76, CMC, or Oklab.
- Dithering: none, Floyd-Steinberg, or Burks.

The preview window can confirm or discard one image, or confirm/discard all queued images. Imported maps are assigned IDs automatically unless you change them. Maps that would overwrite an existing ID are marked with a warning.

Use the context menu on an imported or existing map for **Preview**, **Change ID**, **Export image**, and other actions. Existing maps can also be deleted, opened as files in Java Edition, or added to an inventory.

**Create Items** adds the imported maps to the selected player inventory as map items when an inventory is selected. If they do not fit in the free slots, they are packed into chests automatically.

**Create structures** adds the generated structure blocks to the selected inventory. Structure generation arranges selected maps in a grid and writes the structure data to the world. Glowing and invisible item frames are available only for versions that support them.

When an import does not fit even after chest packing, the program asks whether to:

- Continue and save the maps/structures without changing the inventory.
- Pause so you can free space or change the selection.

**Send All to World** writes the maps and any enabled structures. Status messages below the button report whether the inventory write succeeded, failed, or was skipped.

Other map controls:

- **Show empty maps** includes maps with no image data.
- <kbd>Ctrl+Z</kbd> undoes supported map changes.
- <kbd>Ctrl+Shift+Z</kbd> redoes them.
- <kbd>Delete</kbd> discards selected imported maps or deletes selected world maps.
- The **Maps** tab context menu has **Close** to unload the current world.

### Supported map colors

For Java Edition, the program detects the world version and uses the colors available in that version:

Beta 1.8+  
<img src="https://i.imgur.com/weiQZGR.png" height=48>&nbsp;  
13w42a  
<img src="https://i.imgur.com/VEaqAYI.png" height=48>&nbsp;  
13w43a (1.7)+  
<img src="https://i.imgur.com/WmNcx1u.png" height=48>&nbsp;  
1.8.1-pre1+  
<img src="https://i.imgur.com/xYQd4m3.png" height=48>&nbsp;  
17w17a (1.12)+  
<img src="https://i.imgur.com/lLU3Dyc.png" height=48>&nbsp;  
1.16 pre-6+  
<img src="https://i.imgur.com/groRMjk.png" height=48>&nbsp;  
21w15a  
<img src="https://i.imgur.com/WW3wsiG.png" height=48>&nbsp;  
21w16a (1.17)+  
<img src="https://i.imgur.com/EA39eVT.png" height=48>&nbsp;

As of 1.21.11, no additional map colors have been added to Java Edition.

## Command-line interface

`ImageMap-cmd` converts images without opening the GUI. It supports Java worlds and Bedrock worlds. A world is detected as Bedrock when it contains a `db` folder; otherwise it is treated as a Java world.

### Usage

```text
ImageMap-cmd <world folder> <actions...>
```

Actions are processed from left to right. Put `--inventory` before `--import` if the imported structure should go into that inventory.

Examples:

```text
ImageMap-cmd "C:\worlds\MyWorld" --list
ImageMap-cmd "C:\worlds\MyWorld" --import "C:\images\picture.png"
ImageMap-cmd "C:\worlds\MyWorld" --import "C:\images" --size 3,2 --scaling pixel --fill crop --algorithm oklab --dithering floyd
ImageMap-cmd "C:\worlds\MyWorld" --inventory "Local player" --import "C:\images\picture.png"
ImageMap-cmd "C:\worlds\MyWorld" --export 12,"C:\out\map12.png" 13,"C:\out\map13.png"
ImageMap-cmd "C:\worlds\MyWorld" --delete 12 13
ImageMap-cmd "C:\worlds\MyWorld" --change-id 12,100 13,101
ImageMap-cmd "C:\worlds\MyWorld" --import "C:\images" --log
```

### Actions

- `--inventory <name>` selects a player inventory for structure items. The GUI shows the available inventory names. In the CLI, this adds the generated structure block to the inventory; the maps themselves are still written to the world.
- `--import <file or folder> [options]` imports one image or every top-level file in a folder. The folder scan is not recursive.
- `--export <id,path> [<id,path> ...]` exports existing maps to image files.
- `--list` prints all map IDs currently used by the world.
- `--delete <id> [<id> ...]` removes maps from the world.
- `--change-id <from,to> [<from,to> ...]` moves maps to new IDs.
- `-l`, `--log` writes the first and last map IDs created during the run to `ImageMapCMD_logged_IDs.txt` next to the executable.

### Import options

- `--size <width>,<height>` sets the map grid size. Default: `1,1`.
- `--scaling pixel|bicubic` sets the image resampler. Default: `pixel`.
- `--fill uniform|stretch|crop` controls how the image fits the target size. Default: `uniform`.
- `--algorithm good|euclidean|ciede2000|cie76|cmc|oklab` selects the color matching algorithm. Default: `good`.
- `--dithering none|floyd|burks` selects dithering. Default: `none`.
- `--background <color>` sets the background color. ImageSharp color names and hex colors are accepted. Default: transparent.
- `--id <id>` sets the first map ID. Without it, the next free ID is selected automatically.

### Logging

The log file contains the first ID, the last ID, and `done`:

```text
168
172
done
```

For example, if the world already had 167 maps and the run created 5 new maps, the range is `168` through `172`.

The log records the first and last IDs created by `--import` in the current run. If no maps were created, the log file is not updated. The file is placed next to `ImageMap-cmd.exe`, so the executable folder must be writable.

## Building and publishing

The solution requires the .NET 6 SDK or newer and the Windows Desktop/WPF components.

```text
git clone --recursive https://github.com/Max-RM/image-map-l.git
cd image-map-l
dotnet build ImageMap4.sln -c Release
```

The repository includes PowerShell scripts for publishing:

```text
scripts\Publish-All.ps1
scripts\Publish-All.ps1 -Rids win-x64
scripts\Publish-All.ps1 -SelfContained
scripts\Package-All.ps1
```

Framework-dependent builds are written to `Publish\ImageMap_<rid>`. Self-contained builds are written to `Publish\ImageMap_SelfContained_<rid>`. `Package-All.ps1` creates ZIP archives from the framework-dependent directories in `Archives`.

The GUI is published only for Windows targets. The `linux-x64` target publishes the CLI only.

## Notes

- Close the world in Minecraft before writing. Java worlds are locked while open.
- Bedrock worlds are written through LevelDB. Make a backup before importing large batches.
- Unsupported or unrecognized world versions may not open, or may not support every item/structure feature.
- Glowing frames, invisible frames, chests, and structures depend on the world version.
- Very large Bedrock imports may be packed into nested chests so the entire batch fits in one inventory slot.

## Credits

Original Image Map by [tryashtar](https://github.com/tryashtar/image-map).
