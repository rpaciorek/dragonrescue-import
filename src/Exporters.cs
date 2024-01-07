using System.Net;
using dragonrescue.Api;
using dragonrescue.Util;
using dragonrescue.Schema;

namespace dragonrescue;
public class Exporters {
    public static async System.Threading.Tasks.Task Export(LoginApi.Data loginData, string path) {
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(loginData);
        
        Config.LogWriter("Fetching dragons ...");
        var pets = await DragonApi.GetAllActivePetsByuserId(client, apiToken, profile.ID);
        FileUtil.WriteToChildFile(path, profile.ID, "GetAllActivePetsByuserId.xml", pets);
        Config.ProgressInfo(15);

        Config.LogWriter("Fetching dragons achievements ...");
        var petAchievements = await DragonApi.GetPetAchievementsByUserID(client, apiToken, profile.ID);
        FileUtil.WriteToChildFile(path, profile.ID, "GetPetAchievementsByUserID.xml", petAchievements);
        Config.ProgressInfo(20);
        
        Config.LogWriter("Fetching dragons stables ...");
        var dragonsStables = await StablesApi.GetStables(client, apiToken);
        FileUtil.WriteToChildFile(path, profile.ID, "Stables.xml", dragonsStables);
        Config.ProgressInfo(25);
        
        Config.LogWriter("Write viking avatar ...");
        FileUtil.WriteToChildFile(path, profile.ID, "VikingProfileData.xml", XmlUtil.SerializeXml(profile)); // viking XP is saved here
        Config.ProgressInfo(40);
       
        Config.LogWriter("Fetching inventory ...");
        string childInventory = await InventoryApi.GetCommonInventory(client, apiToken);
        FileUtil.WriteToChildFile(path, profile.ID, "GetCommonInventory.xml", childInventory);
        Config.ProgressInfo(60);
        
        try {
            Config.LogWriter("Fetching item positions for hideout ...");
            string itemPositions = await RoomApi.GetUserItemPositions(client, apiToken, profile.ID, "MyRoomINT");
            FileUtil.WriteToChildFile(path, profile.ID, "GetUserItemPositions_MyRoomINT.xml", itemPositions);
            Config.ProgressInfo(70);
            
            Config.LogWriter("Fetching rooms (farms) ...");
            string rooms = await RoomApi.GetUserRoomList(client, apiToken, profile.ID);
            FileUtil.WriteToChildFile(path, profile.ID, "GetUserRoomList.xml", rooms);
            Config.ProgressInfo(80);

            UserRoomResponse roomsObject = XmlUtil.DeserializeXml<UserRoomResponse>(rooms);
            foreach (UserRoom room in roomsObject.UserRoomList) {
                if (room.RoomID is null) continue;
                Config.LogWriter("Fetching item positions for room {0} ...", room.RoomID);
                itemPositions = await RoomApi.GetUserItemPositions(client, apiToken, profile.ID, room.RoomID);
                FileUtil.WriteToChildFile(path, profile.ID, String.Format("GetUserItemPositions_{0}.xml", room.RoomID), itemPositions);
            }
            Config.ProgressInfo(90);
        } catch {
            Config.LogWriter("Error while exporting hideout / farms ... do your emu have hideout / farms support?");
        }
        
        string[] imgTypes;
        if (Config.APIKEY == "1552008f-4a95-46f5-80e2-58574da65875"){
            imgTypes = new string[]{"EggColor", "Mythie"};
        } else {
            imgTypes = new string[]{"EggColor"};
        }
        var petsObj = XmlUtil.DeserializeXml<RaisedPetData[]>(pets);
        foreach (var pet in petsObj) {
            Config.LogWriter(string.Format("Fetching images for {0} ...", pet.Name));
            foreach (var type in imgTypes) {
                try {
                    Config.LogWriter(string.Format("Get image {0}/{1} ...", type, pet.ImagePosition));
                    ImageData imageDataObject = XmlUtil.DeserializeXml<ImageData>( await ImageApi.GetImageData(client, apiToken, (int)(pet.ImagePosition), type) );
                    string imageUrl = imageDataObject.ImageURL;
                    string filename = $"{profile.ID}_{type}_{pet.ImagePosition}.jpg";
                    Config.LogWriter(string.Format("Downloading image {0} ...", imageUrl));
                    FileUtil.DownloadFile(path, filename, imageUrl);
                } catch {
                    Config.LogWriter("Error ...");
                }
            }
        }
        Config.ProgressInfo(100);
    }
}
