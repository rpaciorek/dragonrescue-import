using System.Net;
using dragonrescue.Util;
using dragonrescue.Schema;

namespace dragonrescue.Api;
public static class ImageApi {
    public static async Task<string> SetImage(HttpClient client, string apiToken, int imageSlot, string image) {
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("ImageType", "EggColor"),
            new KeyValuePair<string, string>("ImageSlot", imageSlot.ToString()),
            new KeyValuePair<string, string>("contentXML", XmlUtil.SerializeXml(new ImageData(){TemplateName="T",ImageURL=""})),
            new KeyValuePair<string, string>("imageFile", image.ToString()),
        });

        string bodyRaw = null;
        try {
            bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/ContentWebService.asmx/SetImage", formContent);
        } catch {
            bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/v3/ContentWebService.asmx/SetImage", formContent);
        }
       
        return bodyRaw;
    }
    
    public static async Task<string> GetImageData(HttpClient client, string apiToken, int imageSlot) {
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("ImageType", "EggColor"),
            new KeyValuePair<string, string>("ImageSlot", imageSlot.ToString()),
        });

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/ContentWebService.asmx/GetImage", formContent);
        return bodyRaw;
        //return XmlUtil.DeserializeXml<ImageData>(bodyRaw);
    }
}
