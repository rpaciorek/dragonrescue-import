using System.Net;
using System.Xml;
using dragonrescue.Api;
using dragonrescue.Util;
using dragonrescue.Schema;

namespace dragonrescue;
class Importers {
    public static async System.Threading.Tasks.Task ImportDragons(string username, string password, string viking, string path, bool replaceStables = false) {
        // read dragon XML
        XmlDocument dragonsXml = new XmlDocument();
        dragonsXml.PreserveWhitespace = true;
        dragonsXml.Load(path);
        
        string basePath = Path.GetDirectoryName(path) + "/" + dragonsXml["ArrayOfRaisedPetData"]["RaisedPetData"]["uid"].InnerText;
        
        // read stables XML
        XmlDocument stablesXml = new XmlDocument();
        try {
            stablesXml.Load(File.OpenText(basePath + "-Stables.xml"));
        } catch (FileNotFoundException) {
            stablesXml = null;
            Console.WriteLine("Can't open stables file (this is normal for original SoD data) ... ignoring");
        }
        
        // read dragon XP
        XmlDocument achievementsXml = new XmlDocument();
        try {
            achievementsXml.Load(File.OpenText(basePath + "-GetPetAchievementsByUserID.xml"));
        } catch (FileNotFoundException) {
            Console.WriteLine("Can't open dragons achievements (xp) file (this is normal for original SoD data) ...");
            return;
        }
        var dragonsXP = new Dictionary<string, int>();
        foreach (XmlNode achievementInfo in achievementsXml["ArrayOfUserAchievementInfo"].ChildNodes) {
            if (achievementInfo["p"].InnerText == "8") {
                dragonsXP.Add(achievementInfo["u"].InnerText, Convert.ToInt32(achievementInfo["a"].InnerText));
            }
        }
        
        // read dir (for images)
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
                    
                    (var res1, var res2, var res3) = await DragonApi.CreateDragonFromXML(client, apiToken, profile.ID, raisedPetData, dragonsXP[dragonEID], imgData);
                    
                    // add to IDs map for update stables XML
                    
                    dragonsIDMap.Add(dragonID, raisedPetData["id"].InnerText);
                    
                    // check results
                    
                    XmlDocument resXml = new XmlDocument();
                    resXml.LoadXml(res1);
                    if (resXml["SetRaisedPetResponse"]["RaisedPetSetResult"].InnerText == "1") {
                        Console.WriteLine(string.Format("{0} moved to new server successfully (new id is {1} / {2})", raisedPetData["n"].InnerText, raisedPetData["id"].InnerText, raisedPetData["eid"].InnerText));
                    } else {
                        Console.WriteLine(string.Format("Error while moving {0} to new server: {1}", raisedPetData["n"].InnerText, res1));
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
    
    public static async System.Threading.Tasks.Task ImportOnlyStables(string username, string password, string viking, string path, bool replaceStables = true) {
        XmlDocument stablesXml = new XmlDocument();
        stablesXml.Load(path);
        
        // connect to server and login as viking
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(username, password, viking);
        
        // send stables to server
        Console.WriteLine("Importing stables ...");
        var res = await StablesApi.SetStables(client, apiToken, stablesXml, new Dictionary<string, string>(), replaceStables);
        Console.WriteLine(res);
    }
    
    public static async System.Threading.Tasks.Task ImportInventory(string username, string password, string viking, string path, bool skipStables = true) {
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
    
    public static async System.Threading.Tasks.Task ImportHideout(string username, string password, string viking, string path, bool addToInventory = true) {
        string roomXml = System.IO.File.ReadAllText(path);
        
        // connect to server and login as viking
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(username, password, viking);
        
        // send hideout to server
        Console.WriteLine("Importing hideout ...");
        var res = await RoomApi.SetUserItemPositions(client, apiToken, profile.ID, "MyRoomINT", roomXml, addToInventory);
        Console.WriteLine(res);
    }
    
    public static async System.Threading.Tasks.Task ImportFarm(string username, string password, string viking, string path, bool replaceRooms = true, bool addToInventory = true) {
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
                Console.WriteLine(res2);
            }
            
            Console.WriteLine(string.Format("Setting item positions for room \"{0}\" (old \"{1}\") using file {2} ...", newRoomID, room.RoomID, roomFile));
            var res = await RoomApi.SetUserItemPositions(client, apiToken, profile.ID, newRoomID, System.IO.File.ReadAllText(roomFile), addToInventory);
            Console.WriteLine(res);
        }
    }
    
    public static async System.Threading.Tasks.Task ImportAvatar(string username, string password, string viking, string path, string importName) {
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
}
