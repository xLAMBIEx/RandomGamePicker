# Random Game Picker (WPF, .NET 9)


A tiny WPF app that scans your Desktop for game shortcuts (`.lnk`) and lets you:


- Include/exclude titles to form a subset (the random pool)
- Roll a random game and launch it
- Add more shortcuts or executables manually (via dialog or drag & drop)
- Remove missing shortcuts and save your list to `%APPDATA%/RandomGamePicker/games.json`


## Build & Run
1. Ensure you have the **.NET 9 SDK** and **Windows** workload for WPF.
2. Open a terminal in the project folder and run:
```bash
dotnet build
dotnet run
```


## Tips
- You can drag & drop `.lnk` or `.exe` files onto the window to add them.
- If you keep your game shortcuts on the Desktop, click **Rescan Desktop** to import them.
- Use **Only show included** + the **Search** box to quickly manage big libraries.


---


Enjoy the roll! 🎲
