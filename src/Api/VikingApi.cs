using System.Net;
using dragonrescue.Util;
using dragonrescue.Schema;

namespace dragonrescue.Api;
public static class VikingApi {
    public static async Task<string> SetAvatar(HttpClient client, string apiToken, AvatarData avatarData) {
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("contentXML", XmlUtil.SerializeXml(avatarData)),
        });

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/V2/ContentWebService.asmx/SetAvatar", formContent);
        return bodyRaw;
        //return XmlUtil.DeserializeXml<ImageData>(bodyRaw);
    }
}
