# DragonRescue Import

Client side (game API) importer / exporter for SoD emulators. Based on [original dragonrescue code](https://github.com/hictooth/dragonrescue) by hictooth.

Allows import data from dragonrescue dump into emu and export data from emu in the same format.
So allows also move dragons between emu servers.


This is early alpha version (proof of concept) – only dragons and stables import/export are supported.

* Due to the lack/incomplete of support for the XP system in current emus version, XP points are not imported.
* Due to the lack of data dragon-stables mapping will not be imported for original dragonrescue dumps. – You must move in dragon to stable manually.
* Use on own risk – in case of bug can broke your current account / viking.

## Build

```
cd src
dotnet build
```

## Running

### import

```
./dragonrescue-import --userApiUrl="USER_API_URL" --contentApiUrl="CONTENT_API_URL" --username="username" --password="password" --viking="viking_name" import --file "PATH_TO_GetAllActivePetsByuserId.xml"
```

See `./dragonrescue-import import --help` for more options and details. Including *stables-only* import option allowing you to modify the order of stables by editing XML.

### export

```
./dragonrescue-import --userApiUrl="USER_API_URL" --contentApiUrl="CONTENT_API_URL" --username="username" --password="password" --viking="viking_name" export --path "export_dir"
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
