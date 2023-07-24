using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using dragonrescue.Api;
using dragonrescue.Util;
using dragonrescue.Schema;
using System.CommandLine;

class Program {
    static async Task<int> Main(string[] args) {
        var rootCommand = new RootCommand(string.Format("SoD account data import/export tool.\n\nSee `{0} command --help` for command details.", System.AppDomain.CurrentDomain.FriendlyName));
        
        /// global options
        
        rootCommand.AddGlobalOption(
            new Option<string?>(
                name: "--userApiUrl",
                description: "SoD user API URL, \n\"http://localhost:5000\" for local hosted SoDOff, \n\"http://localhost:5321\" for local hosted Project Edge",
                parseArgument: result => {
                    Config.URL_USER_API = result.Tokens.Single().Value;
                    return Config.URL_USER_API;
                }
            ) {IsRequired = true}
        );
        rootCommand.AddGlobalOption(
            new Option<string?>(
                name: "--contentApiUrl",
                description: "SoD content API URL, \n\"http://localhost:5000\" for local hosted SoDOff, \n\"http://localhost:5320\" for local hosted Project Edge",
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
                " * dragons (default) – Import dragons and stables (if available).\n" +
                "   --file option argument is path to GetAllActivePetsByuserId.xml file from dragonrescue* dump\n"+
                "   (e.g. '../../mydragons/eba07882-0ae8-4965-9c39-07f409a1c415-GetAllActivePetsByuserId.xml')\n" +
                " * stables – only stables will be imported – can be used to organise / change order of stables.\n" +
                "   --file option argument is path to Stables.xml file from dragonrescue-import dump.",
            getDefaultValue: () => ImportModes.dragons
        );
        
        var importStablesMode = new Option<ImportStablesModes>(
            name: "--stables-mode",
            description: 
                "Specifies the mode of importing stables - adding new ones or replacing existing ones.\n"+
                "Default is auto: replace for --mode=stable, add for --mode=dragons",
            getDefaultValue: () => ImportStablesModes.auto
        );

        var importCommand = new Command("import", "Import profile into SoD.") {
            inputFile,
            importMode,
            importStablesMode,
        };
        importCommand.SetHandler(
            async (username, password, viking, mode, stablesMode, path) => {
                switch (mode) {
                    case ImportModes.dragons:
                        await Import(username, password, viking, path, (stablesMode == ImportStablesModes.replace));
                        break;
                    case ImportModes.stables:
                        await ImportOnlyStables(username, password, viking, path, (stablesMode == ImportStablesModes.auto || stablesMode == ImportStablesModes.replace));
                        break;
                }
            },
            loginUser, loginPassword, loginViking, importMode, importStablesMode, inputFile
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
    
    enum ImportModes {
        dragons, stables
    }
    enum ImportStablesModes {
        auto, replace, add
    }

    static async System.Threading.Tasks.Task Import(string username, string password, string viking, string path, bool replaceStables = false) {
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
        var res = await StablesApi.SetStables(client, apiToken, stablesXml, new Dictionary<string, string>(), replaceStables);
        Console.WriteLine(res);
    }
    
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
        FileUtil.WriteToChildFile(path, profile.ID, "VikingProfileData.xml", XmlUtil.SerializeXml(profile));
       
        Console.WriteLine("Fetching inventory ...");
        string childInventory = await InventoryApi.GetCommonInventory(client, apiToken);
        FileUtil.WriteToChildFile(path, profile.ID, "GetCommonInventory.xml", childInventory);
        
        try {
            Console.WriteLine("Fetching rooms (farms) ...");
            string rooms = await FarmApi.GetUserRoomList(client, apiToken, profile.ID);
            
            FileUtil.WriteToChildFile(path, profile.ID, "GetUserRoomList.xml", rooms);

            UserRoomResponse roomsObject = XmlUtil.DeserializeXml<UserRoomResponse>(rooms);
            foreach (UserRoom room in roomsObject.UserRoomList) {
                if (room.RoomID is null) continue;
                Console.WriteLine("Fetching item positions for room {0} ...", room.RoomID);
                string itemPositions = await FarmApi.GetUserItemPositions(client, apiToken, profile.ID, room.RoomID);
                FileUtil.WriteToChildFile(path, profile.ID, String.Format("{0}-{1}", room.RoomID, "GetUserItemPositions.xml"), itemPositions);
            }
        } catch {
            Console.WriteLine("Error while exporting farms ... do your emu have farms support?");
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
