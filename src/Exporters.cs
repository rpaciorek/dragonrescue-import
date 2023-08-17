using System.Net;
using dragonrescue.Api;
using dragonrescue.Util;
using dragonrescue.Schema;

namespace dragonrescue;
class Exporters {
    public static async System.Threading.Tasks.Task Export(string username, string password, string viking, string path) {
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
