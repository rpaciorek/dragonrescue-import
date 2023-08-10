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
	* dragons (without XP - see TODO)
	* stables
		* **Note:** import stables from original dragonrescue dumps doesn't work and won't work, due to the lack of dragon-stables mapping in original data – you must move in dragons to stables manually
		* "only stables" (`--mode=stables`) import can be used to manage order of stables by edit XML file.
	* avatar (without XP)
	* hideout
		* **Note:** import hideout from original dragonrescue dumps doesn't work and won't work, due to the lack of hideout in original data
	* farm
		* **Note:** due to the lack some data in original dragonrescue dumps, import farm (in some cases) can be limited only to main (default) farm
	* inventory (**experimental** - can broke your account; blueprint not working, battle backpack not working correctly)

### What doesn't work – TODO

* import dragons XP (need (more) support for XP system in emus)
* import battle items (need support for Dragon Tactics (battle) items in emus)


## Usage

### Build

```
cd src
dotnet build
```

### Running

#### import

```
./dragonrescue-import --userApiUrl="USER_API_URL" --contentApiUrl="CONTENT_API_URL" \
                      --username="username" --password="password" --viking="viking_name" \
                      import --file "PATH_TO_GetAllActivePetsByuserId.xml"
```

**See `./dragonrescue-import import --help` for more options and details.**

#### export

```
./dragonrescue-import --userApiUrl="USER_API_URL" --contentApiUrl="CONTENT_API_URL"\
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
* IMPORT/EXPORT are operation mode
* "username" and "password" are login data for emu account
* "viking_name" is emu in-game name
* "PATH_TO_GetAllActivePetsByuserId.xml" is path to GetAllActivePetsByuserId.xml file from dragonrescue dump (e.g.  `../../mydragons/eba07882-0ae8-4965-9c39-07f409a1c415-GetAllActivePetsByuserId.xml`).
  In the directory containing this file there should be dragon pictures exported by dragonrescue.
