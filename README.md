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
	* inventory (**experimental** - can broke your account; blueprint not working, battle backpack not working correctly)

### What doesn't work – TODO

* import battle items (need support for Dragon Tactics (battle) items in emus)


## Usage

### Build

```
dotnet build
```

Use  To build single file executable package use: `dotnet publish --runtime linux-x64` (for 64bit Linux) or `dotnet publish --runtime win-x86` (for 32bit Windows), etc.

### Running

#### import

```
bin/dragonrescue-import --userApiUrl="USER_API_URL" --contentApiUrl="CONTENT_API_URL" \
                        --username="username" --password="password" --viking="viking_name" \
                        import --file "PATH_TO_GetAllActivePetsByuserId.xml"
```

**See `./dragonrescue-import import --help` for more options and details.**

#### export

```
bin/dragonrescue-import --userApiUrl="USER_API_URL" --contentApiUrl="CONTENT_API_URL"\
                        --username="username" --password="password" --viking="viking_name"\
                        export --path "export_dir"
```

Where:

* "USER_API_URL" is:
	* "http://localhost:5000" for local hosted [Sodoff](https://github.com/Spirtix/sodoff)
	* "http://localhost:5321" for local hosted [Edge](https://github.com/SkySwimmer/Edge)
* "CONTENT_API_URL" is:
	* "http://localhost:5000" for local hosted Sodoff
	* "http://localhost:5320" for local hosted Edge
* "username" and "password" are login data for emu account
* "viking_name" is emu in-game name
* "PATH_TO_GetAllActivePetsByuserId.xml" is path to GetAllActivePetsByuserId.xml file from dragonrescue dump (e.g.  `../../mydragons/eba07882-0ae8-4965-9c39-07f409a1c415-GetAllActivePetsByuserId.xml`).
  In the directory containing this file there should be dragon pictures exported by dragonrescue.
