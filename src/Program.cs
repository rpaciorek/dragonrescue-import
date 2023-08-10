using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using dragonrescue.Api;
using dragonrescue.Util;
using dragonrescue.Schema;
using System.CommandLine;

class Program {
    enum ImportModes {
        dragons, stables, inventory, avatar, hideout, farm
    }
    
    enum ImportRoomModes {
        auto, replace, add
    }
    
    static async Task<int> Main(string[] args) {
        var rootCommand = new RootCommand(string.Format("SoD account data import/export tool.\n\nSee `{0} command --help` for command details.", System.AppDomain.CurrentDomain.FriendlyName));
        
        /// global options
        
        rootCommand.AddGlobalOption(
            new Option<string?>(
                name: "--userApiUrl",
                description: "SoD user API URL, for example:\n" + 
                " \"http://localhost:5000\" for local hosted SoDOff (with default settings)\n" + 
                " \"http://localhost:5321\" for local hosted Project Edge (with default settings)",
                parseArgument: result => {
                    Config.URL_USER_API = result.Tokens.Single().Value;
                    return Config.URL_USER_API;
                }
            ) {IsRequired = true}
        );
        rootCommand.AddGlobalOption(
            new Option<string?>(
                name: "--contentApiUrl",
                description: "SoD content API URL, for example:\n" + 
                " \"http://localhost:5000\" for local hosted SoDOff (with default settings)\n" + 
                " \"http://localhost:5320\" for local hosted Project Edge (with default settings)",
                parseArgument: result => {
                    Config.URL_CONT_API = result.Tokens.Single().Value;
                    return Config.URL_CONT_API;
                }
            ) {IsRequired = true}
        );
        
        var loginUser = new Option<string?>(
            name: "--username",
            description: "Login username"
        ) {IsRequired = true};
        var loginPassword = new Option<string?>(
            name: "--password",
            description: "Login password"
        ) {IsRequired = true};
        var loginViking = new Option<string?>(
            name: "--viking",
            description: "Viking (in-game) name / sub profile name"
        ) {IsRequired = true};
        
        rootCommand.AddGlobalOption(loginUser);
        rootCommand.AddGlobalOption(loginPassword);
        rootCommand.AddGlobalOption(loginViking);
        
        // import command
        
        var inputFile = new Option<string?>(
            name: "--file",
            description: "Input file (dragons or stables) - see --mode option for details.",
            parseArgument: result => {
                string? filePath = result.Tokens.Single().Value;
                if (!File.Exists(filePath)) {
                    result.ErrorMessage = "File does not exist";
                    return null;
                } else {
                    return filePath;
                }
            }
        ) {IsRequired = true};
        
        var importMode = new Option<ImportModes>(
            name: "--mode",
            description: 
                "Import mode:\n" +
                " * dragons (default) – import dragons and stables (if available).\n" +
                "   --file option argument is path to GetAllActivePetsByuserId.xml file from dragonrescue* dump\n"+
                "   (e.g. '../../mydragons/eba07882-0ae8-4965-9c39-07f409a1c415-GetAllActivePetsByuserId.xml')\n" +
                " * stables – only stables will be imported – can be used to organise / change order of stables.\n" +
                "   --file option argument is path to Stables.xml file from dragonrescue-import dump.\n" +
                " * inventory – only viking inventory, stables will be omitting until provide --stables-mode=add option\n" +
                "   --file option argument is path to GetCommonInventory.xml file from dragonrescue* dump.\n" +
                "   WARNING: item will be added, not replaced! So repeated use import multiply items quantity.\n" +
                "   WARNING: this is experimental feature, it can broke your account easily\n" +
                "   WARNING: importing battle backpack not working correctly\n" +
                " * avatar – only viking avatar data\n" +
                "   --file option argument is path to VikingProfileData.xml or GetDetailedChildList.xml file from dragonrescue* dump.\n" +
                "   if file contain multiple viking's profiles, then will imported profile with name provided by --import-name\n" +
                " * hideout – only viking hideout data\n" +
                "   --file option argument is path to GetUserItemPositions_MyRoomINT.xml file from dragonrescue-import dump.\n" +
                " * farm – only viking farm data\n" +
                "   --file option argument is path to GetUserRoomList.xml file from dragonrescue* dump.\n",
            getDefaultValue: () => ImportModes.dragons
        );
        
        var importRoomMode = new Option<ImportRoomModes>(
            aliases: new[] {"--stables-mode", "--room-mode"},
            description: 
                "Specifies the mode of importing stables / farm rooms - adding new ones or replacing existing ones.\n"+
                "Note: farm room replace does *not* delete rooms that are not in imported data, only reuse old room when possible.\n"+
                "Default is auto: replace for --mode=stable or --mode=farm, add for --mode=dragons\n",
            getDefaultValue: () => ImportRoomModes.auto
        );

        var importName = new Option<string?>(
            name: "--import-name",
            description: "Viking (in-game) name / sub profile name to import (used with --mode=avatar). When not set use value of --viking."
        );
        
        var skipInventory = new Option<bool>(
            name: "--skip-inventory",
            description: "Skip inventory update on hideout and farm import."
        );
        
        var importCommand = new Command("import", "Import profile into SoD.") {
            inputFile,
            importMode,
            importRoomMode,
            importName,
            skipInventory,
        };
        importCommand.SetHandler(
            async (username, password, viking, mode, roomMode, path, importName, skipInventory) => {
                switch (mode) {
                    case ImportModes.dragons:
                        await ImportDragons(username, password, viking, path, (roomMode == ImportRoomModes.replace));
                        break;
                    case ImportModes.stables:
                        await ImportOnlyStables(username, password, viking, path, (roomMode == ImportRoomModes.auto || roomMode == ImportRoomModes.replace));
                        break;
                    case ImportModes.inventory:
                        await ImportInventory(username, password, viking, path, (roomMode != ImportRoomModes.add));
                        break;
                    case ImportModes.avatar:
                        if (importName == null)
                            importName = viking;
                        await ImportAvatar(username, password, viking, path, importName);
                        break;
                    case ImportModes.hideout:
                        await ImportHideout(username, password, viking, path, !skipInventory);
                        break;
                    case ImportModes.farm:
                        await ImportFarm(username, password, viking, path, (roomMode == ImportRoomModes.auto || roomMode == ImportRoomModes.replace), !skipInventory);
                        break;
                }
            },
            loginUser, loginPassword, loginViking, importMode, importRoomMode, inputFile, importName, skipInventory
        );
        rootCommand.AddCommand(importCommand);
        
        
        // export command
        
        var outDir = new Option<string?>(
            name: "--path",
            description: "Path to directory to write data",
            parseArgument: result => {
                string? dirPath = result.Tokens.Single().Value;
                if (!Directory.Exists(dirPath)) {
                    Directory.CreateDirectory(dirPath);
                }
                return dirPath;
            }
        ) {IsRequired = true};
        
        var exportCommand = new Command("export", "Export profile from SoD.") {
            outDir,
        };
        exportCommand.SetHandler(
            async (username, password, viking, path) => {
                await Export(username, password, viking, path);
            },
            loginUser, loginPassword, loginViking, outDir
        );
        rootCommand.AddCommand(exportCommand);

        return await rootCommand.InvokeAsync(args);
    }
    
    
    /* IMPORT FUNCTIONS */
    
    static async System.Threading.Tasks.Task ImportDragons(string username, string password, string viking, string path, bool replaceStables = false) {
        XmlDocument dragonsXml = new XmlDocument();
        dragonsXml.PreserveWhitespace = true;
        dragonsXml.Load(path);

        XmlDocument stablesXml = new XmlDocument();
        try {
            stablesXml.Load(File.OpenText(Path.GetDirectoryName(path) + "/" + dragonsXml["ArrayOfRaisedPetData"]["RaisedPetData"]["uid"].InnerText + "-Stables.xml"));
        } catch (FileNotFoundException) {
            stablesXml = null;
            Console.WriteLine("Can't open stables file (this is normal for original SoD data) ... ignoring");
        }
        
        List<string> inputFiles = new List<string>();
        inputFiles.AddRange(Directory.GetFiles(Path.GetDirectoryName(path)).ToList());
        
        // connect to server and login as viking
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(username, password, viking);
        
        // process dragons XML (do import)
        var dragonsIDMap = new Dictionary<string, string>();
        for (int j = 0; j < dragonsXml.ChildNodes.Count; j++) {
            for (int i = 0; i < dragonsXml.ChildNodes[j].ChildNodes.Count; i++) {
                var raisedPetData = dragonsXml.ChildNodes[j].ChildNodes[i];
                if (raisedPetData.HasChildNodes && raisedPetData.Name == "RaisedPetData") {
                    var vikingUID = raisedPetData["uid"].InnerText;
                    var dragonID  = raisedPetData["id"].InnerText;
                    var dragonEID = raisedPetData["eid"].InnerText;
                    var dragonIP  = raisedPetData["ip"].InnerText;
                    
                    // read image data if available
                    
                    string? imgData = null;
                    string? imgFile = inputFiles.Find(x => x.EndsWith($"{vikingUID}_EggColor_{dragonIP}.jpg"));
                    if (imgFile is not null) {
                        imgData = Convert.ToBase64String(System.IO.File.ReadAllBytes(imgFile));
                    }
                    
                    // crate dragon on server
                    
                    var res = await DragonApi.CreateDragonFromXML(client, apiToken, profile.ID, raisedPetData, imgData);
                    
                    // add to IDs map for update stables XML
                    
                    dragonsIDMap.Add(dragonID, raisedPetData["id"].InnerText);
                    
                    // check results
                    
                    XmlDocument resXml = new XmlDocument();
                    resXml.LoadXml(res);
                    if (resXml["SetRaisedPetResponse"]["RaisedPetSetResult"].InnerText == "1") {
                        Console.WriteLine(string.Format("{0} moved to new server successfully (new id is {1} / {2})", raisedPetData["n"].InnerText, raisedPetData["id"].InnerText, raisedPetData["eid"].InnerText));
                    } else {
                        Console.WriteLine(string.Format("Error while moving {0} to new server: {1}", raisedPetData["n"].InnerText, res));
                    }
                }
            }
        }
        if (stablesXml != null) {
            Console.WriteLine("Importing stables ...");
            var res = await StablesApi.SetStables(client, apiToken, stablesXml, dragonsIDMap, replaceStables);
            Console.WriteLine(res);
        }
    }
    
    static async System.Threading.Tasks.Task ImportOnlyStables(string username, string password, string viking, string path, bool replaceStables = true) {
        XmlDocument stablesXml = new XmlDocument();
        stablesXml.Load(path);
        
        // connect to server and login as viking
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(username, password, viking);
        
        // send stables to server
        Console.WriteLine("Importing stables ...");
        var res = await StablesApi.SetStables(client, apiToken, stablesXml, new Dictionary<string, string>(), replaceStables);
        Console.WriteLine(res);
    }
    
    static async System.Threading.Tasks.Task ImportInventory(string username, string password, string viking, string path, bool skipStables = true) {
        CommonInventoryData inventory = XmlUtil.DeserializeXml<CommonInventoryData>(System.IO.File.ReadAllText(path));
        
        var inventoryChanges = new Dictionary<int, int>();
        foreach (UserItemData userItem in inventory.Item) {
            if (skipStables && userItem.Item.AssetName.Length >= 12 && userItem.Item.AssetName.Substring(0,12) == "DragonStable")
                continue;
            
            //Console.WriteLine($"{userItem.ItemID} {userItem.Quantity} {userItem.ItemTier} {userItem.ItemStats}");
            // TODO support for DT items (items with non empty userItem.ItemTier and userItem.ItemStats)
            
            if (userItem.Item.BluePrint != null) continue;
            
            inventoryChanges[userItem.ItemID] = userItem.Quantity;
        }
        
        // connect to server and login as viking
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(username, password, viking);
        
        // new http client due to timeout
        HttpClient client2 = new HttpClient();
        client2.Timeout = TimeSpan.FromMinutes(10);
        
        // send inventory to server
        Console.WriteLine("Importing inventory ... please be patient ... it may take a while ...");
        var res = await InventoryApi.AddItems(client2, apiToken, inventoryChanges);
        Console.WriteLine(res);
    }
    
    static async System.Threading.Tasks.Task ImportHideout(string username, string password, string viking, string path, bool addToInventory = true) {
        string roomXml = System.IO.File.ReadAllText(path);
        
        // connect to server and login as viking
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(username, password, viking);
        
        // send hideout to server
        Console.WriteLine("Importing hideout ...");
        var res = await RoomApi.SetUserItemPositions(client, apiToken, profile.ID, "MyRoomINT", roomXml, addToInventory);
        Console.WriteLine(res);
    }
    
    static async System.Threading.Tasks.Task ImportFarm(string username, string password, string viking, string path, bool replaceRooms = true, bool addToInventory = true) {
        // read room list to import
        var roomsList = XmlUtil.DeserializeXml<UserRoomResponse>(System.IO.File.ReadAllText(path)).UserRoomList;
        
        // connect to server and login as viking
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(username, password, viking);
        
        Console.WriteLine("Importing farm ...");
        
        // get old rooms list
        List<UserRoom> rooms = null;
        if (replaceRooms) {
            var x =await RoomApi.GetUserRoomList(client, apiToken, profile.ID);
            Console.WriteLine(string.Format("bbbb {0}", x));
            rooms = XmlUtil.DeserializeXml<UserRoomResponse>(x).UserRoomList;
        }
        
        // for each imported room
        foreach (UserRoom room in roomsList) {
            if (room.RoomID is null) continue;
            
            // get room xml save file path
            string roomFile = null;
            string roomFileNew = path.Replace("-GetUserRoomList.xml", "-GetUserItemPositions_" + room.RoomID + ".xml");
            string roomFileOld = path.Replace("-GetUserRoomList.xml", "-" + room.RoomID + "-GetUserItemPositions.xml");
            if (File.Exists(roomFileNew)) {
                roomFile = roomFileNew;
            } else if (File.Exists(roomFileOld)) {
                roomFile = roomFileOld;
            } else {
                Console.WriteLine(string.Format("Can't find input file for room: {0}.\n  {1} nor {2} do not exist", room.RoomID, roomFileNew, roomFileOld));
                continue;
            }
            
            // get new RoomID
            string newRoomID = null;
            if (room.RoomID == "" || room.RoomID == "MyRoomINT" || room.RoomID == "StaticFarmItems") {
                // for special room use old RoomID
                newRoomID = room.RoomID;
            } else {
                // for farm extensions ...
                if (replaceRooms) {
                    // find RoomID to reuse and remove from old rooms list
                    var r = rooms.FirstOrDefault(x => x.ItemID == room.ItemID);
                    if (r != null) {
                        newRoomID = room.RoomID;
                        rooms.Remove(r);
                    }
                }
                if (newRoomID is null && room.ItemID != null) {
                    // if don't have room to reuse, then add new room to get RoomID
                    int inventoryID = await InventoryApi.AddItemAndGetInventoryId(client, apiToken, (int)room.ItemID, 1);
                    Console.WriteLine(string.Format("aaa {0}", inventoryID));
                    newRoomID = inventoryID.ToString();
                }
                if (newRoomID is null) {
                    // TODO try use old inventory to get ItemID based on room.RoomID == InventoryID
                    Console.WriteLine(string.Format("Room type could not be determined for room \"{0}\"", room.RoomID));
                    continue;
                }
            }
            
            // rename room (call SetUserRoom)
            if (!string.IsNullOrEmpty(room.Name)) {
                var res2 = await RoomApi.SetUserRoom(client, apiToken, newRoomID, room.Name);
            }
            
            Console.WriteLine(string.Format("Setting item positions for room \"{0}\" (old \"{1}\") using file {2} ...", newRoomID, room.RoomID, roomFile));
            var res = await RoomApi.SetUserItemPositions(client, apiToken, profile.ID, newRoomID, System.IO.File.ReadAllText(roomFile), addToInventory);
            Console.WriteLine(res);
        }
    }
    
    static async System.Threading.Tasks.Task ImportAvatar(string username, string password, string viking, string path, string importName) {
        XmlDocument avatarXmlDoc = new XmlDocument();
        avatarXmlDoc.Load(path);
        AvatarData avatar = null;
        
        if (avatarXmlDoc["ArrayOfUserProfileDisplayData"] != null) {
            foreach (XmlNode profileData in avatarXmlDoc["ArrayOfUserProfileDisplayData"].ChildNodes) {
                var tmpAvatar = XmlUtil.DeserializeXml<AvatarData>(profileData["Avatar"]["AvatarData"].OuterXml);
                if (tmpAvatar.DisplayName == importName) {
                    avatar = tmpAvatar;
                    break;
                }
            }
            if (avatar == null) {
                Console.WriteLine(string.Format("Can't find viking profile {0} in input file ({1})", importName, path));
                return;
            }
        } else {
            try {
                avatar = XmlUtil.DeserializeXml<AvatarData>(avatarXmlDoc["UserProfileDisplayData"]["Avatar"]["AvatarData"].OuterXml);
            } catch {
                Console.WriteLine(string.Format("Can't find valid viking profile in input file ({0})", path));
                return;
            }
        }
        
        // change imported name to current name
        avatar.DisplayName = viking;
        
        // connect to server and login as viking
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(username, password, viking);
        
        // send avatar data to server
        Console.WriteLine("Importing viking avatar ...");
        var res = await VikingApi.SetAvatar(client, apiToken, avatar);
        Console.WriteLine(res);
    }
    
    
    /* EXPORT FUNCTIONS */
    
    static async System.Threading.Tasks.Task Export(string username, string password, string viking, string path) {
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(username, password, viking);
        
        Console.WriteLine("Fetching dragons ...");
        var pets = await DragonApi.GetAllActivePetsByuserId(client, apiToken, profile.ID);
        FileUtil.WriteToChildFile(path, profile.ID, "GetAllActivePetsByuserId.xml", pets);

        Console.WriteLine("Fetching dragons achievements ...");
        var petAchievements = await DragonApi.GetPetAchievementsByUserID(client, apiToken, profile.ID);
        FileUtil.WriteToChildFile(path, profile.ID, "GetPetAchievementsByUserID.xml", petAchievements);
        
        Console.WriteLine("Fetching dragons stables ...");
        var dragonsStables = await StablesApi.GetStables(client, apiToken);
        FileUtil.WriteToChildFile(path, profile.ID, "Stables.xml", dragonsStables);
        
        Console.WriteLine("Write viking avatar ...");
        FileUtil.WriteToChildFile(path, profile.ID, "VikingProfileData.xml", XmlUtil.SerializeXml(profile)); // viking XP is saved here
       
        Console.WriteLine("Fetching inventory ...");
        string childInventory = await InventoryApi.GetCommonInventory(client, apiToken);
        FileUtil.WriteToChildFile(path, profile.ID, "GetCommonInventory.xml", childInventory);
        
        try {
            Console.WriteLine("Fetching item positions for hideout ...");
            string itemPositions = await RoomApi.GetUserItemPositions(client, apiToken, profile.ID, "MyRoomINT");
            FileUtil.WriteToChildFile(path, profile.ID, "GetUserItemPositions_MyRoomINT.xml", itemPositions);
            
            Console.WriteLine("Fetching rooms (farms) ...");
            string rooms = await RoomApi.GetUserRoomList(client, apiToken, profile.ID);
            FileUtil.WriteToChildFile(path, profile.ID, "GetUserRoomList.xml", rooms);

            UserRoomResponse roomsObject = XmlUtil.DeserializeXml<UserRoomResponse>(rooms);
            foreach (UserRoom room in roomsObject.UserRoomList) {
                if (room.RoomID is null) continue;
                Console.WriteLine("Fetching item positions for room {0} ...", room.RoomID);
                itemPositions = await RoomApi.GetUserItemPositions(client, apiToken, profile.ID, room.RoomID);
                FileUtil.WriteToChildFile(path, profile.ID, String.Format("GetUserItemPositions_{0}.xml", room.RoomID), itemPositions);
            }
            
        } catch {
            Console.WriteLine("Error while exporting hideout / farms ... do your emu have hideout / farms support?");
        }
        
        for (int i = 0; i < 500; i++) { // hard limit of 500 for this scrape, hopefully no one has more than that?
            Console.WriteLine(string.Format("Fetching image slot {0} ...", i));
            string imageData = await ImageApi.GetImageData(client, apiToken, i);
            ImageData imageDataObject = XmlUtil.DeserializeXml<ImageData>(imageData);
            if (imageDataObject is null || string.IsNullOrWhiteSpace(imageDataObject.ImageURL)) break;
            //FileUtil.WriteToChildFile(path, profile.ID, String.Format("{0}-{1}", i, "GetImageData.xml"), imageData);

            // now get the image itself
            Console.WriteLine(string.Format("Downloading image {0} ...", i));
            string imageUrl = imageDataObject.ImageURL;
            string filename = $"{profile.ID}_EggColor_{i}.jpg";
            FileUtil.DownloadFile(path, filename, imageUrl);
        }
    }
}
