using System.Net;
using dragonrescue.Util;
using dragonrescue.Schema;
 
namespace dragonrescue.Api;
public static class RoomApi {
    public static async Task<string> GetUserRoomList(HttpClient client, string apiToken, string userId) {
        UserRoomGetRequest request = new UserRoomGetRequest {
            UserID = Guid.Parse(userId),
            CategoryID = 541 // this seems hard coded
        };
        var requestString = XmlUtil.SerializeXml(request);

        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("request", requestString),
        });

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/ContentWebService.asmx/GetUserRoomList", formContent);
        return bodyRaw;
        //return XmlUtil.DeserializeXml<GetGroupsResult>(bodyRaw);
    }

    public static async Task<string> GetUserItemPositions(HttpClient client, string apiToken, string userId, string roomId) {
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("userId", userId),
            new KeyValuePair<string, string>("roomId", roomId),
        });

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/ContentWebService.asmx/GetUserRoomItemPositions", formContent);
        return bodyRaw;
        //return XmlUtil.DeserializeXml<UserItemPositionList>(bodyRaw);
    }
    
    public static async Task<string> SetUserRoom(HttpClient client, string apiToken, string roomId, string roomName) {
        UserRoom request = new UserRoom {
            RoomID = roomId,
            Name = roomName
        };
        var requestString = XmlUtil.SerializeXml(request);
        
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("request", requestString),
        });

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/ContentWebService.asmx/SetUserRoom", formContent);
        return bodyRaw;
        //return XmlUtil.DeserializeXml<UserItemPositionList>(bodyRaw);
    }
    
    public static async Task<string> SetUserItemPositions(HttpClient client, string apiToken, string userId, string roomId, string roomXml, bool addToInventory) {
        var inventoryChanges = new Dictionary<int, int>();
        
        var curItems = await GetUserItemPositions(client, apiToken, userId, roomId);
        ;
        var oldItems = XmlUtil.DeserializeXml<UserItemPositionList>(curItems).UserItemPosition;
        int[] remItems;
        if (oldItems != null) {
            remItems = new int[oldItems.Length];
            for (int i = 0; i < oldItems.Length; ++i) {
                if (oldItems[i].UserItemPositionID != null) {
                    remItems[i] = oldItems[i].UserItemPositionID.Value;
                    try {
                        inventoryChanges[oldItems[i].Item.ItemID] -= 1;
                    } catch (KeyNotFoundException) {
                        inventoryChanges[oldItems[i].Item.ItemID] = -1;
                    }
                }
            }
        } else {
            remItems = new int[0];
        }
        
        var newItems = XmlUtil.DeserializeXml<UserItemPositionList>(roomXml).UserItemPosition;
        var addItems = new UserItemPositionSetRequestList();
        addItems.UserItemPosition = new UserItemPosition[newItems.Length];
        for (int i = 0; i < newItems.Length; ++i) {
            addItems.UserItemPosition[i] = newItems[i];
            try {
                inventoryChanges[newItems[i].Item.ItemID] += 1;
            } catch (KeyNotFoundException) {
                inventoryChanges[newItems[i].Item.ItemID] = 1;
            }
        }
        
        if (addToInventory) {
            Console.WriteLine("Update inventory ...");
            string res = await InventoryApi.AddItems(client, apiToken, inventoryChanges);
            Thread.Sleep(Config.NICE);
        }
        
        Console.WriteLine(string.Format("Update room {0} ...", roomId));
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("roomId", roomId),
            new KeyValuePair<string, string>("createXml", XmlUtil.SerializeXml(addItems)),
            new KeyValuePair<string, string>("updateXml", XmlUtil.SerializeXml(new UserItemPositionSetRequestList())),
            new KeyValuePair<string, string>("removeXml", XmlUtil.SerializeXml(remItems)),
        });

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/ContentWebService.asmx/SetUserRoomItemPositions", formContent);
        return bodyRaw;
        //return XmlUtil.DeserializeXml<UserItemPositionList>(bodyRaw);
    }
}
