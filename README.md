# Repair Shop for Quest

The Quest version of caleb's Repair Shop for A Township Tale. On a server that runs Repair Shop, a Quest player sees the shop, its boards and its workers, and can hand in worn tools and pay for repairs, side by side with PC players.

Ported with caleb's permission. The original mod, its art and its design are his: https://github.com/calebv2/ATT-Repair-Shop-Standalone

## What you need

- A Quest with LemonLoader (the Township Tale Quest kit).
- A server that runs Repair Shop 0.2.30 with SyncLib 1.0.4. Nothing changes on the server: it keeps caleb's `RepairShop.dll` and `SyncLib.dll`.
- PC players on the same server keep caleb's PC client and SyncLib, as before.

## Install

Close the game. Put `RepairShopQuest.dll` in the headset's Mods folder:

```
/sdcard/Android/data/com.Alta.ATownshipTale/files/MelonLoader/com.Alta.ATownshipTale/Mods/
```

Start the game and join a server that runs Repair Shop. The mod builds the shop only when the server's item list has it.

It works next to Portal Gun for Quest: both read the same item list from the server.

## Use

Put a worn tool in the intake, read the quote on the board, drop the coins in the tray and press PAY. Come back when the board says it is ready.

## Differences from the PC version

- caleb's custom shaders have no Quest version, so the shop's art is drawn with the game's own Quest shader. Textures are at most 1024 pixels.
- Labels use the game's TextMeshPro font.
- The dock guards of caleb's PC client are not in this version. The server keeps all of its own.
- Everything else follows the server, which is caleb's.

## Building

`build.ps1 -RepairShop <path>` takes `Client\RepairShop.dll` from caleb's 0.2.30 release, bakes its art into `res\repairshop.pack` with `tools\bake`, then builds the Quest DLL into `build\`.

Put LemonLoader's `net8` folder and its generated `Il2CppAssemblies` in `lemon\`, and `AssetsTools.NET.dll` from the PC game's `MelonLoader\net6` folder in `lemon\net6\`.

## Credits

Repair Shop is by caleb (calebv2). The worker animation in `src\Workers` is his code, carried over from his PC client. Quest port by Shadow.

This is an unofficial, fan made mod for A Township Tale. It is not affiliated with or endorsed by the owners or developers of A Township Tale.
