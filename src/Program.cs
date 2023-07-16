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
List<string> dragonsFiles = new List<string>();

if (runningmode == "IMPORT") {
    dragonsXml.PreserveWhitespace = true;
    dragonsXml.Load(path);

    dragonsFiles.AddRange(Directory.GetFiles(Path.GetDirectoryName(path)).ToList());
}

// connect to server

Console.WriteLine(string.Format("Logging into School of Dragons as '{0}' with password '{1}'...", username, password));

using HttpClient client = new();
string loginInfo = await LoginApi.LoginParent(client, username, password);

ParentLoginInfo loginInfoObject = XmlUtil.DeserializeXml<ParentLoginInfo>(loginInfo);
if (loginInfoObject.Status != MembershipUserStatus.Success) {
    Console.WriteLine("Login error. Please check username and password.");
    Environment.Exit(1);
}

Console.WriteLine("Fetching child profiles...");
string children = await LoginApi.GetDetailedChildList(client, loginInfoObject.ApiToken);
UserProfileDataList childrenObject = XmlUtil.DeserializeXml<UserProfileDataList>(children);
Console.WriteLine(string.Format("Found {0} child profiles.", childrenObject.UserProfiles.Length));

foreach (UserProfileData profile in childrenObject.UserProfiles) {
    if (viking != profile.AvatarInfo.UserInfo.FirstName && viking != profile.AvatarInfo.AvatarData.DisplayName) {
        Console.WriteLine(string.Format("Skip child profile: {0} ({1}).", profile.AvatarInfo.UserInfo.FirstName, profile.AvatarInfo.AvatarData.DisplayName));
        continue;
    }
    
    Console.WriteLine(string.Format("Selecting profile {0} ({1}, {2})...", profile.AvatarInfo.UserInfo.FirstName, profile.AvatarInfo.AvatarData.DisplayName, profile.ID));
    var childApiToken = await LoginApi.LoginChild(client, loginInfoObject.ApiToken, profile.ID);
    
    // process dragons XML (do import)
    if (runningmode == "IMPORT") {
        for (int j = 0; j < dragonsXml.ChildNodes.Count; j++) {
            for (int i = 0; i < dragonsXml.ChildNodes[j].ChildNodes.Count; i++) {
                var raisedPetData = dragonsXml.ChildNodes[j].ChildNodes[i];
                if (raisedPetData.HasChildNodes && raisedPetData.Name == "RaisedPetData") {
                    
                    // read image data if available
                    
                    var imgUID = raisedPetData["uid"].InnerText;
                    var imgIP  = raisedPetData["ip"].InnerText;
                    string? imgData = null;
                    string? imgFile = dragonsFiles.Find(x => x.EndsWith($"{imgUID}_EggColor_{imgIP}.jpg"));
                    if (imgFile is not null) {
                        imgData = Convert.ToBase64String(System.IO.File.ReadAllBytes(imgFile));
                    }
                    
                    // crate dragon on server
                    
                    var res = await DragonApi.CreateDragonFromXML(client, childApiToken, profile.ID, raisedPetData, imgData);
                    
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
    } else if (runningmode == "EXPORT") {
        Console.WriteLine("Fetching dragons ...");
        string pets = await DragonApi.GetAllActivePetsByuserId(client, childApiToken, profile.ID);
        FileUtil.WriteToChildFile(path, profile.ID, "GetAllActivePetsByuserId.xml", pets);

        for (int i = 0; i < 500; i++) { // hard limit of 500 for this scrape, hopefully no one has more than that?
            Console.WriteLine(string.Format("Fetching image slot {0} ...", i));
            string imageData = await ImageApi.GetImageData(client, childApiToken, i);
            ImageData imageDataObject = XmlUtil.DeserializeXml<ImageData>(imageData);
            if (imageDataObject is null || string.IsNullOrWhiteSpace(imageDataObject.ImageURL)) break;
            //FileUtil.WriteToChildFile(path, profile.ID, String.Format("{0}-{1}", i, "GetImageData.xml"), imageData);

            // now get the image itself
            Console.WriteLine(string.Format("Downloading image {0} ...", i));
            string imageUrl = imageDataObject.ImageURL;
            string filename = $"{profile.ID}_EggColor_{i}.jpg";
            FileUtil.DownloadFile(path, filename, imageUrl);
        }
    } else {
        Console.WriteLine(string.Format("Unsupported mode, should be IMPORT or EXPORT, not {0}", runningmode));
    }
    
    break;
}
