using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using dragonrescue.Api;
using dragonrescue.Util;
using dragonrescue.Schema;

string[] commandlineArgs = Environment.GetCommandLineArgs();
if (commandlineArgs.Length < 7) {
    Console.WriteLine("Not enough args.");
    Console.WriteLine("Usage: ./dragonrescue-import <USER_API_URL> <CONTENT_APU_URL> IMPORT <username> <password> <viking_name> <input_file>");
    Console.WriteLine("   or: ./dragonrescue-import <USER_API_URL> <CONTENT_APU_URL> EXPORT <username> <password> <viking_name> <output_dir>");
    Environment.Exit(0);
}
Config.URL_USER_API = commandlineArgs[1];
Config.URL_CONT_API = commandlineArgs[2];
string runningmode = commandlineArgs[3];
string username = commandlineArgs[4];
string password = commandlineArgs[5];
string viking = commandlineArgs[6];
string path = commandlineArgs[7];

// open file and read filelist from basedir

XmlDocument dragonsXml = new XmlDocument();
XmlDocument stablesXml = new XmlDocument();
List<string> dragonsFiles = new List<string>();

if (runningmode == "IMPORT") {
    dragonsXml.PreserveWhitespace = true;
    dragonsXml.Load(path);

    try {
        stablesXml.Load(File.OpenText(Path.GetDirectoryName(path) + "/" + dragonsXml["ArrayOfRaisedPetData"]["RaisedPetData"]["uid"].InnerText + "-Stables.xml"));
    } catch (FileNotFoundException) {
        stablesXml = null;
        Console.WriteLine("Can't open stables file (this is normal for original SoD data) ... ignoring");
    }
    
    dragonsFiles.AddRange(Directory.GetFiles(Path.GetDirectoryName(path)).ToList());
}


// connect to server and login as viking
(var client, var apiToken, var userID) = await LoginApi.DoVikingLogin(username, password, viking);

// process dragons XML (do import)
if (runningmode == "IMPORT") {
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
                string? imgFile = dragonsFiles.Find(x => x.EndsWith($"{vikingUID}_EggColor_{dragonIP}.jpg"));
                if (imgFile is not null) {
                    imgData = Convert.ToBase64String(System.IO.File.ReadAllBytes(imgFile));
                }
                
                // crate dragon on server
                
                var res = await DragonApi.CreateDragonFromXML(client, apiToken, userID, raisedPetData, imgData);
                
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
        var res = await StablesApi.SetStables(client, apiToken, stablesXml, dragonsIDMap, true);
        Console.WriteLine(res);
    }
} else if (runningmode == "IMPORT_S") {
    stablesXml.Load(path);
    var res = await StablesApi.SetStables(client, apiToken, stablesXml, new Dictionary<string, string>(), true);
    Console.WriteLine(res);
} else if (runningmode == "EXPORT") {
    Console.WriteLine("Fetching dragons ...");
    var pets = await DragonApi.GetAllActivePetsByuserId(client, apiToken, userID);
    FileUtil.WriteToChildFile(path, userID, "GetAllActivePetsByuserId.xml", pets);

    Console.WriteLine("Fetching dragons achievements ...");
    var petAchievements = await DragonApi.GetPetAchievementsByUserID(client, apiToken, userID);
    FileUtil.WriteToChildFile(path, userID, "GetPetAchievementsByUserID.xml", petAchievements);
    
    Console.WriteLine("Fetching dragons stables ...");
    var dragonsStables = await StablesApi.GetStables(client, apiToken);
    FileUtil.WriteToChildFile(path, userID, "Stables.xml", dragonsStables);
    
    for (int i = 0; i < 500; i++) { // hard limit of 500 for this scrape, hopefully no one has more than that?
        Console.WriteLine(string.Format("Fetching image slot {0} ...", i));
        string imageData = await ImageApi.GetImageData(client, apiToken, i);
        ImageData imageDataObject = XmlUtil.DeserializeXml<ImageData>(imageData);
        if (imageDataObject is null || string.IsNullOrWhiteSpace(imageDataObject.ImageURL)) break;
        //FileUtil.WriteToChildFile(path, userID, String.Format("{0}-{1}", i, "GetImageData.xml"), imageData);

        // now get the image itself
        Console.WriteLine(string.Format("Downloading image {0} ...", i));
        string imageUrl = imageDataObject.ImageURL;
        string filename = $"{userID}_EggColor_{i}.jpg";
        FileUtil.DownloadFile(path, filename, imageUrl);
    }
} else {
    Console.WriteLine(string.Format("Unsupported mode, should be IMPORT or EXPORT, not {0}", runningmode));
}
