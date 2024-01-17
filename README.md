# DragonRescue Import

Client side (game API) importer / exporter for SoD emulators. Based on [original dragonrescue code](https://github.com/hictooth/dragonrescue) by hictooth.

Allows import data from dragonrescue dump into emu and export data from emu in the same format.
So allows also move dragons between emu servers.

Use import with caution – in case of bug can broke your current account / viking.

## Status

### What works

* export
	* dragons (with XP)
	* stables
	* avatar (with XP)
	* hideout
	* farm
	* inventory
* import
	* dragons (with XP)
	* stables
		* **Note:** import stables from original dragonrescue dumps doesn't work and won't work, due to the lack of dragon-stables mapping in original data – you must move in dragons to stables manually
		* "only stables" (`--mode=stables`) import can be used to manage order of stables by edit XML file.
	* avatar (with XP)
	* hideout
		* **Note:** import hideout from original dragonrescue dumps doesn't work and won't work, due to the lack of hideout in original data
	* farm
		* **Note:** due to the lack some data in original dragonrescue dumps, import farm (in some cases) can be limited only to main (default) farm
	* inventory
		* **Warning:** inventory contains many invisible items (for example affecting quests or causing dragons duplication); inventory import may broke your account

## Usage

### Build

* to build command line version, run `dotnet build` in `src` dir 
* to build GUI version, run `dotnet build` in `gui`
* to build redistributable packages (gui and commandline version for 64 bit Linux and 32 bit Windows) use `build.sh` script

### Running

#### gui

* run `gui/bin/dragonrescuegui`
* select mode (export or import)
* fill server data form
* select output dir (export) or mode and input file (import)
* click export or import button
* read messages in text window

#### export

```
src/bin/dragonrescue-import --userApiUrl="User API URL" --contentApiUrl="Content API URL"\
                            --username="Username" --password="Password" --viking="Viking name"\
                            export --path "export_dir"
```

#### import

```
src/bin/dragonrescue-import --userApiUrl="User API URL" --contentApiUrl="Content API URL" \
                            --username="Username" --password="Password" --viking="Viking name" \
                            import --file "PATH_TO_GetAllActivePetsByuserId.xml"
```

**See `./dragonrescue-import import --help` for more options and details.**


#### arguments description

In all modes (gui/command line and import/export) you need provide access data for SoD API server:

* "User API URL" is:
	* "http://localhost:5000" for local hosted [Sodoff](https://github.com/Spirtix/sodoff)
	* "http://localhost:5321" for local hosted [Edge](https://github.com/SkySwimmer/Edge)
* "Content API URL" is:
	* "http://localhost:5000" for local hosted Sodoff
	* "http://localhost:5320" for local hosted Edge
* "Username" and "Password" are login data for emu account
* "Viking name" is emu in-game name

Also for import you need provide file path coresponding to selected mode, for import:

* dragons → "PATH_TO_GetAllActivePetsByuserId.xml" is path to GetAllActivePetsByuserId.xml file from dragonrescue dump (e.g.  `../../mydragons/eba07882-0ae8-4965-9c39-07f409a1c415-GetAllActivePetsByuserId.xml`).
  The directory containing this file should also contain dragon pictures and some other xml files (like `*GetPetAchievementsByUserID.xml`) exported by dragonrescue.
* avatar → "...-GetDetailedChildList.xml" or "...-VikingProfileData.xml.xml"
* farm → "...-GetUserRoomList.xml"
* hideout → "...-GetUserItemPositions_MyRoomINT.xml"
* inventory → "...-GetCommonInventory.xml"


## Other tools

* Alternative GUI is available on: https://github.com/B1ackDaemon/dragonrescue-helper
