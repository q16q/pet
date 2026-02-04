<a id="readme-top"></a>
<div align="center">
  <a href="https://github.com/q16q/pet">
    <img src=".assets/logo.png" alt="Logo" width="256" height="256">
  </a>
  <h3 align="center">PET mod loader for KoboldKare</h3>

  <p align="center">
    Featuring asynchronous mod loading!
    <br />
    <a href="https://github.com/q16q/pet/issues/new?labels=bug&template=bug-report---.md">Report Bug</a>
    &middot;
    <a href="https://github.com/q16q/pet/issues/new?labels=enhancement&template=feature-request---.md">Request Feature</a>
  </p>
</div>

>[!WARNING]
>PET is still in development; expect bugs, crashes and incompatibilities!

### About Project
**PET (Parallel-Engaging Threaded)** mod loader implementation is a collection of HarmonyX patches that allow KobaldKare to load multiple mods at once using asynchronous C# Tasks, rather than loading them one by one.


### Why?
The original mod loading process wasn’t fast enough to stop you from getting a coffee while the mods loaded.

### Installation
This project requires [BepInEx](https://github.com/BepInEx/BepInEx) to run.  
1. Download latest release from the [Releases tab](https://github.com/q16q/pet/releases/latest).
2. Put it into **KoboldKare/BepInEx/plugins**.

### Why BepInEx?
I’d love to make this plugin more accessible by publishing it in the Steam Workshop, but since these mods are just bundles of assets, I can’t include code in them or modify the mod loading process.

### Building from source
>[!WARNING]
>.NET SDK is required for building from source

Copy these libraries from `KoboldKare/KoboldKare_Data/Managed` directory into `lib/`:
- `UnityEngine.dll`
- `UnityEngine.CoreModule.dll`
- `UnityEngine.AssetBundleModule.dll`
- `UnityEngine.UI.dll`
- `UnityEngine.UIModule.dll`
- `UnityEngine.TextRenderingModule.dll`
- `Unity.ResourceManager.dll`
- `Unity.Addressables.dll`
- `naelstrof.Modding.dll`
- `com.rlabrecque.steamworks.net.dll`

Run _the command below_ in the project directory to build the dll in `bin/Release/net46/PET.dll`:
```
dotnet build -c Release
```
