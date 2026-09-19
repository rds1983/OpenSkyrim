# OpenSkyrim

A from-scratch rewrite of **The Elder Scrolls V: Skyrim** in **C#**.

## Technology Stack

The engine is built on the following open-source libraries:

| Library            | Purpose                                              | Repo                                                     |
| ------------------ | ---------------------------------------------------- | -------------------------------------------------------- |
| FNA                | Cross-platform audio, input, and drawing (XNA reimplementation) | https://github.com/FNA-XNA/FNA                 |
| FontStashSharp     | Dynamic sprite-font rendering / text layout          | https://github.com/FontStashSharp/FontStashSharp          |
| XNAssets           | Loading and processing of game assets                | https://github.com/rds1983/XNAssets                       |
| DigitalRiseModel   | 3D model loading                                     | https://github.com/rds1983/DigitalRiseModel               |
| Nursia             | 3D scene rendering and navigation                    | https://github.com/rds1983/Nursia                         |
| Mutagen            | Parsing, creating, and editing Bethesda plugin files (*.esp / *.esm) | https://github.com/Mutagen-Modding/Mutagen

## Repository Layout

| Project    | Description                                             |
| ---------- | ------------------------------------------------------- |
| `NifViewer`| Console tool that scans a Skyrim installation and lists all `.nif` files in a Myra-based `ListView` GUI |

## Mutagen Notes

For the project’s findings on working with Bethesda data files and Mutagen-based archive scanning, see [MUTAGEN_NOTES.md](MUTAGEN_NOTES.md).

## NIF Reference

The project used code and format knowledge from the [nifscope / NifSkope repository](https://github.com/niftools/nifskope) to implement its own NIF parser for Gamebryo assets and mesh extraction.

## Roadmap

TBD — planning in progress.

## License

GPL-3.0. See [LICENSE](LICENSE).