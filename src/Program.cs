using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Xml;
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
string loginInfo = await LoginParent(client, username, password);

ParentLoginInfo loginInfoObject = XmlUtil.DeserializeXml<ParentLoginInfo>(loginInfo);
if (loginInfoObject.Status != MembershipUserStatus.Success) {
    Console.WriteLine("Login error. Please check username and password.");
    Environment.Exit(1);
}

Console.WriteLine("Fetching child profiles...");
string children = await GetDetailedChildList(client, loginInfoObject.ApiToken);
UserProfileDataList childrenObject = XmlUtil.DeserializeXml<UserProfileDataList>(children);
Console.WriteLine(string.Format("Found {0} child profiles.", childrenObject.UserProfiles.Length));

foreach (UserProfileData profile in childrenObject.UserProfiles) {
    if (viking != profile.AvatarInfo.UserInfo.FirstName && viking != profile.AvatarInfo.AvatarData.DisplayName) {
        Console.WriteLine(string.Format("Skip child profile: {0} ({1}).", profile.AvatarInfo.UserInfo.FirstName, profile.AvatarInfo.AvatarData.DisplayName));
        continue;
    }
    
    Console.WriteLine(string.Format("Selecting profile {0} ({1}, {2})...", profile.AvatarInfo.UserInfo.FirstName, profile.AvatarInfo.AvatarData.DisplayName, profile.ID));
    var childApiToken = await LoginChild(client, loginInfoObject.ApiToken, profile.ID);
    
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
                
                var res = await CreateDragonFromXML(client, childApiToken, profile.ID, raisedPetData, imgData);
                
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


static async Task<string> LoginParent(HttpClient client, string UserName, string Password) {
    ParentLoginData loginData = new ParentLoginData {
        UserName = UserName,
        Password = Password,
        Locale = "en-US"
    };

    var loginDataString = XmlUtil.SerializeXml(loginData);
    var loginDataStringEncrypted = TripleDES.EncryptUnicode(loginDataString, Config.KEY);

    var formContent = new FormUrlEncodedContent(new[] {
        new KeyValuePair<string, string>("apiKey", "b99f695c-7c6e-4e9b-b0f7-22034d799013"),
        new KeyValuePair<string, string>("parentLoginData", loginDataStringEncrypted)
    });

    var response = await client.PostAsync(Config.URL_USER_API + "/v3/AuthenticationWebService.asmx/LoginParent", formContent);
    var bodyRaw = await response.Content.ReadAsStringAsync();
    var bodyEncrypted = XmlUtil.DeserializeXml<string>(bodyRaw);
    var bodyDecrypted = TripleDES.DecryptUnicode(bodyEncrypted, Config.KEY);
    return bodyDecrypted;
    //return XmlUtil.DeserializeXml<ParentLoginInfo>(bodyDecrypted);
}


static async Task<string> GetDetailedChildList(HttpClient client, string apiToken) {
    var formContent = new FormUrlEncodedContent(new[] {
        new KeyValuePair<string, string>("apiKey", "b99f695c-7c6e-4e9b-b0f7-22034d799013"),
        new KeyValuePair<string, string>("parentApiToken", apiToken)
    });

    var response = await client.PostAsync(Config.URL_USER_API + "/ProfileWebService.asmx/GetDetailedChildList", formContent);
    var bodyRaw = await response.Content.ReadAsStringAsync();
    return bodyRaw;
    //return XmlUtil.DeserializeXml<UserProfileDataList>(bodyRaw);
}


static async Task<string> LoginChild(HttpClient client, string apiToken, string childUserId) {
    var childUserIdEncrypted = TripleDES.EncryptUnicode(childUserId, Config.KEY);

    var ticks = DateTime.UtcNow.Ticks.ToString();
    var locale = "en-US";
    var signature = Md5.GetMd5Hash(string.Concat(new string[]
        {
            ticks,
            Config.KEY,
            apiToken,
            childUserIdEncrypted,
            locale
        }));

    var formContent = new FormUrlEncodedContent(new[] {
        new KeyValuePair<string, string>("apiKey", "b99f695c-7c6e-4e9b-b0f7-22034d799013"),
        new KeyValuePair<string, string>("parentApiToken", apiToken),
        new KeyValuePair<string, string>("ticks", ticks),
        new KeyValuePair<string, string>("signature", signature),
        new KeyValuePair<string, string>("childUserID", childUserIdEncrypted),
        new KeyValuePair<string, string>("locale", locale),
    });

    var response = await client.PostAsync(Config.URL_USER_API + "/AuthenticationWebService.asmx/LoginChild", formContent);
    var bodyRaw = await response.Content.ReadAsStringAsync();
    var bodyEncrypted = XmlUtil.DeserializeXml<string>(bodyRaw);
    return TripleDES.DecryptUnicode(bodyEncrypted, Config.KEY);
}




static async Task<string> CreatePet(HttpClient client, string apiToken, string request) {
    var formContent = new FormUrlEncodedContent(new[] {
        new KeyValuePair<string, string>("apiKey", "b99f695c-7c6e-4e9b-b0f7-22034d799013"),
        new KeyValuePair<string, string>("apiToken", apiToken),
        new KeyValuePair<string, string>("request", request),
    });

    var response = await client.PostAsync(Config.URL_CONT_API + "/V2/ContentWebService.asmx/CreatePet", formContent);
    var bodyRaw = await response.Content.ReadAsStringAsync();
    response.Dispose();
    return bodyRaw;
}


static async Task<string> SetRaisedPet(HttpClient client, string apiToken, string request) {
    var formContent = new FormUrlEncodedContent(new[] {
        new KeyValuePair<string, string>("apiKey", "b99f695c-7c6e-4e9b-b0f7-22034d799013"),
        new KeyValuePair<string, string>("apiToken", apiToken),
        new KeyValuePair<string, string>("request", request),
    });

    var response = await client.PostAsync(Config.URL_CONT_API + "/v3/ContentWebService.asmx/SetRaisedPet", formContent);
    var bodyRaw = await response.Content.ReadAsStringAsync();
    response.Dispose();
    return bodyRaw;
}

static async Task<string> SetImage(HttpClient client, string apiToken, int imageSlot, string image) {
    var formContent = new FormUrlEncodedContent(new[] {
        new KeyValuePair<string, string>("apiKey", "b99f695c-7c6e-4e9b-b0f7-22034d799013"),
        new KeyValuePair<string, string>("apiToken", apiToken),
        new KeyValuePair<string, string>("ImageType", "EggColor"),
        new KeyValuePair<string, string>("ImageSlot", imageSlot.ToString()),
        new KeyValuePair<string, string>("contentXML", "<ImageData xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"> <ImageURL /> <TemplateName>T</TemplateName> </ImageData>"),
        new KeyValuePair<string, string>("imageFile", image.ToString()),
    });

    var response = await client.PostAsync(Config.URL_CONT_API + "/ContentWebService.asmx/SetImage", formContent);
    if (response.StatusCode != HttpStatusCode.OK) {
        response.Dispose();
        response = await client.PostAsync(Config.URL_CONT_API + "/v3/ContentWebService.asmx/SetImage", formContent);
    }
    var bodyRaw = await response.Content.ReadAsStringAsync();
    response.Dispose();
    return bodyRaw;
}


static async Task<string> CreateDragonFromXML(HttpClient client, string childApiToken, string profileID, XmlNode raisedPetData, string? imageData) {
    // create "template" pet - chimeragon ;-)
    
    Thread.Sleep(Config.NICE);
    
    var createPetRequest = "<?xml version=\"1.0\" encoding=\"utf-8\"?> <RPR xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"> <ptid>111</ptid> <SASP>false</SASP> <USOP>false</USOP> <pgid>9</pgid> <cir> <iid>18589</iid> <cid>11</cid> <q>-1</q> </cir> <rpd> <id>0</id> <uid>" + profileID + "</uid> <ptid>111</ptid> <gs> <id>593</id> <n>Hatching</n> <ptid>111</ptid> <o>1</o> </gs> <g>RS_DATA/DragonEgg.unity3d/PfEggChimeragon</g> <t /> <gd>1</gd> <at> <k>HatchTime</k> <v>7/15/2023 7:52:56 AM</v> <dt>11</dt> </at> <at> <k>Priority</k> <v>0</v> <dt>7</dt> </at> <at> <k>FoodEffect</k> <v /> <dt>11</dt> </at> <at> <k>IncubatorID</k> <v>1</v> <dt>7</dt> </at> <at> <k>NameCustomized</k> <v>False</v> <dt>1</dt> </at> <is>false</is> <ir>false</ir> <cdt>0001-01-01T00:00:00</cdt> <updt>0001-01-01T00:00:00</updt> <ispetcreated>false</ispetcreated> </rpd> </RPR>";
    var newPet = await CreatePet(client, childApiToken, createPetRequest);
    CreatePetResponse newPetXml = XmlUtil.DeserializeXml<CreatePetResponse>(newPet);
    
    // update original dragon
    
    // avoid locked dragons in Edge
    for (int i = 0; i < raisedPetData.ChildNodes.Count; i++) {
        if (raisedPetData.ChildNodes[i].Name == "at" && raisedPetData.ChildNodes[i]["k"].InnerText == "TicketID") {
            raisedPetData.RemoveChild(raisedPetData.ChildNodes[i]);
        }
    }
    
    // use new (emu) ids
    raisedPetData["uid"].InnerText = profileID;
    raisedPetData["eid"].InnerText = newPetXml.RaisedPetData.EntityID.ToString();
    raisedPetData["id"].InnerText  = newPetXml.RaisedPetData.RaisedPetID.ToString();
    raisedPetData["ip"].InnerText  = newPetXml.RaisedPetData.ImagePosition.ToString();
    
    // replace pet by original dragon
    
    Thread.Sleep(Config.NICE);
    
    var setRaisedPetRequest = "<?xml version=\"1.0\" encoding=\"utf-8\"?> <RPR xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"> <ptid>0</ptid> <rpd> " + raisedPetData.InnerXml + " </rpd> </RPR>";
    
    var res = await SetRaisedPet(client, childApiToken, setRaisedPetRequest);
    
    // set image
    
    Thread.Sleep(Config.NICE);
    
    if (imageData == null) {
        imageData = "/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAKBueIx4ZKCMgoy0qqC+8P//8Nzc8P//////////////////////////////////////////////////////////2wBDAaq0tPDS8P//////////////////////////////////////////////////////////////////////////////wAARCAEAAQADASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwBlFFFSUFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAFFFFABRRRQAUUUUAf/Z";
    }
    await SetImage(client, childApiToken, newPetXml.RaisedPetData.ImagePosition.Value, imageData);
    
    return res;
}
