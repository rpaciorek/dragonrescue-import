using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Xml;
using dragonrescue.Api;
using dragonrescue.Util;
using dragonrescue.Schema;

string[] commandlineArgs = Environment.GetCommandLineArgs();
if (commandlineArgs.Length < 7) {
    Console.WriteLine("Not enough args.\nUsage: ./dragonrescue-import <USER_API_URL> <CONTENT_APU_URL> <username> <password> <viking_name> <input_file>");
    Environment.Exit(0);
}
Config.URL_USER_API = commandlineArgs[1];
Config.URL_CONT_API = commandlineArgs[2];
string username = commandlineArgs[3];
string password = commandlineArgs[4];
string viking = commandlineArgs[5];
string inPath = commandlineArgs[6];

// open file and read filelist from basedir

XmlDocument dragonsXml = new XmlDocument();
dragonsXml.PreserveWhitespace = true;
dragonsXml.Load(inPath);

List<string> dragonsFiles = Directory.GetFiles(Path.GetDirectoryName(inPath)).ToList();

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
    
    break;
}
