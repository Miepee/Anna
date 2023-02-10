# Anna
Anna (**A**M2R-tool for **N**ew and **N**ovel **A**ssets) is essentially a resource pack manager for AM2RLauncher profiles.
The tool lets you change Music, Language and Palette files in a straight forward way, without needing to deal with copying profiles back and forth or needing to create seperate modpacks for each profile you want certain resources to be changed.


Anna loads its resource packs from .apa files (**A**sset **P**acks for **A**nna), which are just renamed zip files. Here's how an example file could look like:
```
MyCoolResourcePack.apa
|- Music
  |- MusTitle.ogg
  |- MusAlpha.ogg
  |- MusFoobar.ogg
|- Language
  |- headers
    |- deutsch_a1_f26_b0_c3_d21_e23.png
  |- fonts
    |- Glasstown_NBP.ttf
  |- english.ini
  |- german.ini
|- Palette
  |- suits
    |- power.png
    |- fusion_power.png
```
The `Music`, `Language` and `Palette` folders from the pack will then be copied into the `asset_directory`, `asset_directory/lang` and `asset_directory/mods/palettes` respectively. Note, that for the `Music` folder, only `.ogg` files will be copied.
