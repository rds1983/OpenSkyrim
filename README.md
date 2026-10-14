# OpenSkyrim

A from-scratch rewrite of **The Elder Scrolls V: Skyrim** in **C#**.

## Technology Stack

The engine is built on the following open-source libraries:

| Library            | Purpose                                              | Repo                                                     |
| ------------------ | ---------------------------------------------------- | -------------------------------------------------------- |
| FNA                | Cross-platform audio, input, and drawing (XNA reimplementation) | https://github.com/FNA-XNA/FNA                 |
| Myra               | UI toolkit used for the in-game GUI                  | https://github.com/rds1983/Myra                           |
| FontStashSharp     | Dynamic sprite-font rendering / text layout          | https://github.com/FontStashSharp/FontStashSharp          |
| XNAssets           | Loading and processing of game assets                | https://github.com/rds1983/XNAssets                       |
| DigitalRiseModel   | 3D model loading                                     | https://github.com/rds1983/DigitalRiseModel               |
| Nursia             | 3D scene rendering and navigation                    | https://github.com/rds1983/Nursia                         |
| Mutagen            | Parsing, creating, and editing Bethesda plugin files (*.esp / *.esm) | https://github.com/Mutagen-Modding/Mutagen
| Nifly              | Parsing Gamebryo/NetImmerse `.nif` model files and extracting mesh + texture data | https://github.com/ousnius/NiflySharp |

## Repository Layout

| Project             | Description                                                                 |
| ------------------- | --------------------------------------------------------------------------- |
| `OpenSkyrim.Viewer` | FNA + Myra + Nursia tool for inspecting a Skyrim Special Edition installation (see below) |

## OpenSkyrim.Viewer

`OpenSkyrim.Viewer` is a desktop tool (FNA + Myra + Nursia) for inspecting a **Skyrim Special
Edition** installation. It currently:

- Mounts the game's `Data` folder, both loose files and `.bsa` archives, and lists every
  `.nif` model found there.
- Lists every interior cell and every worldspace cell ("locations") from `Skyrim.esm`.
- Renders the selected location's placed objects as a 3D scene, applying each placement's
  transform and loading DDS textures on demand.
- Renders an individual `.nif` model when a model entry is selected.
- Resolves the location's player-start marker and places the camera there on load.
- Lets you select an object either from the object list or by clicking it in the scene
  (ray/triangle picking); the selection is highlighted with a box and stays in sync between
  the two.
- Hides Creation Kit-only design-time markers (X markers, room markers and the like), which
  the game never renders.

### Building and Running

Requires the .NET 8 SDK. Build the solution:

```
dotnet build OpenSkyrim.slnx
```

The FNA native libraries are not checked into the repository (see `.gitignore`); place them
under `OpenSkyrim.Viewer/lib/native` so they are copied to the output directory. Run the
viewer by passing the Skyrim installation folder:

```
dotnet run --project OpenSkyrim.Viewer -- "D:\SteamLibrary\steamapps\common\Skyrim Special Edition"
```

## Mutagen Notes

For the project’s findings on working with Bethesda data files and Mutagen-based archive scanning, see [MUTAGEN_NOTES.md](MUTAGEN_NOTES.md).

## Nifly Notes

For the project’s findings on loading `.nif` files and extracting mesh and texture data with Nifly, see [NIFLY_NOTES.md](NIFLY_NOTES.md).

## Myra Notes

For the project’s findings on building OpenSkyrim.Viewer’s UI with MyraPad / `myra-export`, see [MYRA_NOTES.md](MYRA_NOTES.md).

## Roadmap

TBD — planning in progress.

## License

GPL-3.0. See [LICENSE](LICENSE).
