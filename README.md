# DragonRescue Import

Client side (game API) importer of dragonrescue dump to SoD emulators. Based on [original dragonrescue code](https://github.com/hictooth/dragonrescue) by hictooth.

This is first public release (alpha stage / proof of concept) – only dragons import are supported.

* Due to the lack of support for the XP system in current emus version, XP points are not imported.
* Due to the lack of data dragon-stables mapping will not be imported. – You must move in dragon to stable manually.
* Use on own risk – in case of bug can broke your current account / viking.


## Running

```
cd src;
dotnet run --project dragonrescue.csproj "USER_API_URL" "CONTENT_API_URL" "username" "password" "viking_name" "PATH_TO_GetAllActivePetsByuserId.xml"
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
