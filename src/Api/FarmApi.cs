using System.Net;
using dragonrescue.Util;
using dragonrescue.Schema;
 
namespace dragonrescue.Api;
public static class FarmApi {
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
}
