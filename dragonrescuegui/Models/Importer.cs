using dragonrescue.Api;
using dragonrescue.Schema;
using dragonrescue.Util;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace dragonrescuegui.Models {
    internal class Importer {
        public static async System.Threading.Tasks.Task ImportDragons(LoginApi.Data loginData, string path, IProgress<double> progress, bool replaceStables = false) {
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
            progress.Report(10);
            // read dragon XP
            var dragonsXP = new Dictionary<string, int>();
            try {
                XmlDocument achievementsXml = new XmlDocument();
                achievementsXml.Load(File.OpenText(basePath + "-GetPetAchievementsByUserID.xml"));

                foreach (XmlNode achievementInfo in achievementsXml["ArrayOfUserAchievementInfo"].ChildNodes) {
                    if (achievementInfo["p"].InnerText == "8") {
                        dragonsXP.Add(achievementInfo["u"].InnerText, Convert.ToInt32(achievementInfo["a"].InnerText));
                    }
                }
                Console.WriteLine(string.Format("Read XP for {0} dragons", dragonsXP.Count));
            } catch (FileNotFoundException) {
                Console.WriteLine("Can't open dragons achievements (xp) file ...");
                return;
            }
            progress.Report(20);
            // read dir (for images)
            List<string> inputFiles = new List<string>();
            inputFiles.AddRange(Directory.GetFiles(Path.GetDirectoryName(path)).ToList());

            // connect to server and login as viking
            (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(loginData);
            progress.Report(30);

            // process dragons XML (do import)
            var dragonsIDMap = new Dictionary<string, string>();
            for (int j = 0; j < dragonsXml.ChildNodes.Count; j++) {
                for (int i = 0; i < dragonsXml.ChildNodes[j].ChildNodes.Count; i++) {
                    var raisedPetData = dragonsXml.ChildNodes[j].ChildNodes[i];
                    if (raisedPetData.HasChildNodes && raisedPetData.Name == "RaisedPetData") {
                        var vikingUID = raisedPetData["uid"].InnerText;
                        var dragonID = raisedPetData["id"].InnerText;
                        var dragonEID = raisedPetData["eid"].InnerText;
                        var dragonIP = raisedPetData["ip"].InnerText;

                        // read image data if available

                        string? imgData = null;
                        string? imgFile = inputFiles.Find(x => x.EndsWith($"{vikingUID}_EggColor_{dragonIP}.jpg"));
                        if (imgFile is not null) {
                            imgData = Convert.ToBase64String(System.IO.File.ReadAllBytes(imgFile));
                        }

                        // get XP

                        int? dragonXP = null;
                        try {
                            dragonXP = dragonsXP[dragonEID];
                        } catch { }

                        // create dragon on server

                        (var res1, var res2, var res3) = await DragonApi.CreateDragonFromXML(client, apiToken, profile.ID, raisedPetData, dragonXP, imgData);

                        // add to IDs map for update stables XML

                        dragonsIDMap.Add(dragonID, raisedPetData["id"].InnerText);

                        // check results

                        XmlDocument resXml = new XmlDocument();
                        resXml.LoadXml(res1);
                        if (resXml["SetRaisedPetResponse"]["RaisedPetSetResult"].InnerText == "1") {
                            Console.WriteLine(string.Format("{0} moved to new server successfully (new id is {1} / {2}, xp={3}, img={4})",
                                                            raisedPetData["n"].InnerText, raisedPetData["id"].InnerText, raisedPetData["eid"].InnerText, res2, XmlUtil.DeserializeXml<bool>(res3)));
                        } else {
                            Console.WriteLine(string.Format("Error while moving {0} to new server: {1}", raisedPetData["n"].InnerText, res1));
                        }
                    }
                }
            }
            progress.Report(90);
            if (stablesXml != null) {
                Console.WriteLine("Importing stables ...");
                var res = await StablesApi.SetStables(client, apiToken, stablesXml, dragonsIDMap, replaceStables);
                Console.WriteLine(res);
            }
            progress.Report(100);
        }

        public static async System.Threading.Tasks.Task ImportOnlyStables(LoginApi.Data loginData, string path, bool replaceStables = true) {
            XmlDocument stablesXml = new XmlDocument();
            stablesXml.Load(path);

            // connect to server and login as viking
            (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(loginData);

            // send stables to server
            Console.WriteLine("Importing stables ...");
            var res = await StablesApi.SetStables(client, apiToken, stablesXml, new Dictionary<string, string>(), replaceStables);
            Console.WriteLine(res);
        }

        public static async System.Threading.Tasks.Task ImportInventory(LoginApi.Data loginData, string path, IProgress<double> progress, bool skipStables = true) {
            CommonInventoryData inventory = XmlUtil.DeserializeXml<CommonInventoryData>(System.IO.File.ReadAllText(path));

            var inventoryChanges = new Dictionary<int, int>();
            var battleInventoryChanges = new List<BattleItemTierMap>();
            foreach (UserItemData userItem in inventory.Item) {
                if (skipStables && userItem.Item.AssetName.Length >= 12 && userItem.Item.AssetName.Substring(0, 12) == "DragonStable")
                    continue;

                if (userItem.Quantity < 1)
                    continue;

                if (userItem.ItemTier != null && userItem.ItemStats != null) {
                    battleInventoryChanges.Add(
                        new BattleItemTierMap {
                            ItemID = userItem.ItemID,
                            Quantity = userItem.Quantity,
                            Tier = userItem.ItemTier,
                            ItemStats = userItem.ItemStats
                        }
                    );
                } else {
                    inventoryChanges[userItem.ItemID] = userItem.Quantity;
                }
            }
            progress.Report(15);

            // connect to server and login as viking
            (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(loginData);
            progress.Report(30);

            // new http client due to timeout
            HttpClient client2 = new HttpClient();
            client2.Timeout = TimeSpan.FromMinutes(10);

            // send inventory to server
            Console.WriteLine("Importing inventory ... please be patient ... it may take a while ...");
            var res1 = await InventoryApi.AddItems(client2, apiToken, inventoryChanges);
            progress.Report(80);

            XmlDocument res1Xml = new XmlDocument();
            res1Xml.LoadXml(res1);
            Console.WriteLine(res1Xml["CIRS"]["s"].InnerText);

            Console.WriteLine("Importing battle inventory ... please be patient ... it may take a while ...");
            var res2 = await InventoryApi.AddBattleItems(client2, apiToken, battleInventoryChanges);
            progress.Report(95);

            XmlDocument res2Xml = new XmlDocument();
            res2Xml.LoadXml(res2);
            Console.WriteLine(res2Xml["ABIRES"]["ST"].InnerText);
            progress.Report(100);
        }

        public static async System.Threading.Tasks.Task ImportHideout(LoginApi.Data loginData, string path, IProgress<double> progress, bool addToInventory = true) {
            string roomXml = System.IO.File.ReadAllText(path);

            // connect to server and login as viking
            (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(loginData);
            progress.Report(30);
            // send hideout to server
            Console.WriteLine("Importing hideout ...");
            var res = await RoomApi.SetUserItemPositions(client, apiToken, profile.ID, "MyRoomINT", roomXml, addToInventory);
            Console.WriteLine(res);
            progress.Report(100);

        }

        public static async System.Threading.Tasks.Task ImportFarm(LoginApi.Data loginData, string path, IProgress<double> progress, bool replaceRooms = true, bool addToInventory = true) {
            // read room list to import
            var roomsList = XmlUtil.DeserializeXml<UserRoomResponse>(System.IO.File.ReadAllText(path)).UserRoomList;

            // connect to server and login as viking
            (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(loginData);
            progress.Report(20);
            Console.WriteLine("Importing farm ...");

            // get old rooms list
            List<UserRoom> rooms = null;
            if (replaceRooms) {
                var x = await RoomApi.GetUserRoomList(client, apiToken, profile.ID);
                Console.WriteLine(string.Format("bbbb {0}", x));
                rooms = XmlUtil.DeserializeXml<UserRoomResponse>(x).UserRoomList;
            }
            progress.Report(30);

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
            progress.Report(100);
        }

        public static async System.Threading.Tasks.Task ImportAvatar(LoginApi.Data loginData, string path, string importName, bool importXP, IProgress<double> progress) {
            XmlDocument avatarXmlDoc = new XmlDocument();
            avatarXmlDoc.Load(path);
            AvatarDisplayData avatar = null;

            progress.Report(10);
            if (avatarXmlDoc["ArrayOfUserProfileDisplayData"] != null) {
                foreach (XmlNode profileData in avatarXmlDoc["ArrayOfUserProfileDisplayData"].ChildNodes) {
                    var tmpAvatar = XmlUtil.DeserializeXml<AvatarDisplayData>(profileData["Avatar"].OuterXml);
                    if (tmpAvatar.AvatarData.DisplayName == importName) {
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
                    avatar = XmlUtil.DeserializeXml<AvatarDisplayData>(avatarXmlDoc["UserProfileDisplayData"]["Avatar"].OuterXml);
                } catch {
                    Console.WriteLine(string.Format("Can't find valid viking profile in input file ({0})", path));
                    return;
                }
            }

            // change imported name to current name
            avatar.AvatarData.DisplayName = loginData.viking;

            // connect to server and login as viking
            (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(loginData);
            progress.Report(30);

            // send avatar data to server
            Console.WriteLine("Importing viking avatar ...");
            var res = await VikingApi.SetAvatar(client, apiToken, avatar.AvatarData);
            Console.WriteLine(res);
            progress.Report(90);
            // send avatar xp to server
            if (importXP) {
                Console.WriteLine("Importing viking XP ...");
                foreach (var xpEntry in avatar.Achievements) {
                    if (xpEntry.PointTypeID != null && xpEntry.AchievementPointTotal != null) {
                        string res2 = "err";
                        try {
                            res2 = await VikingApi.SetPlayerXP(client, apiToken, (int)(xpEntry.PointTypeID), (int)(xpEntry.AchievementPointTotal));
                        } catch { }
                        Console.WriteLine(string.Format(" set xp type={0} to {1} res={2}", xpEntry.PointTypeID, xpEntry.AchievementPointTotal, res2));
                    }
                }
            }
            progress.Report(100);
        }
    }
}
