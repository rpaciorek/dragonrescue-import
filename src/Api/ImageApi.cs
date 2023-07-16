using System.Net;
using dragonrescue.Schema;
using dragonrescue.Util;

namespace dragonrescue.Api;
public static class ImageApi {
    public static async Task<string> SetImage(HttpClient client, string apiToken, int imageSlot, string image) {
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
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
    
    public static async Task<string> GetImageData(HttpClient client, string apiToken, int imageSlot) {
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("ImageType", "EggColor"),
            new KeyValuePair<string, string>("ImageSlot", imageSlot.ToString()),
        });

        var response = await client.PostAsync(Config.URL_CONT_API + "/ContentWebService.asmx/GetImage", formContent);
        var bodyRaw = await response.Content.ReadAsStringAsync();
        return bodyRaw;
        //return XmlUtil.DeserializeXml<ImageData>(bodyRaw);
    }
}
