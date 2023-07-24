using System.Net;
using dragonrescue.Util;
using dragonrescue.Schema;

namespace dragonrescue.Api;
public static class InventoryApi {
    public static async Task<string> AddItems(HttpClient client, string apiToken, int itemID, int count) {
        return await AddItems(client, apiToken, new Dictionary<int, int>(){{itemID, count}});
    }
    
    public static async Task<string> AddItems(HttpClient client, string apiToken, Dictionary<int, int> itemsCount) {
        string xml = "<?xml version=\"1.0\" encoding=\"utf-8\"?><ArrayOfCommonInventoryRequest xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">";
        foreach (var x in itemsCount) {
            xml += "<CommonInventoryRequest><iid>" + x.Key.ToString() + "</iid><q>" + x.Value.ToString() + "</q></CommonInventoryRequest>";
        }
        xml += "</ArrayOfCommonInventoryRequest>";
        
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("commonInventoryRequestXml", xml),
            new KeyValuePair<string, string>("ContainerId", "1"),
        });

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/ContentWebService.asmx/SetCommonInventory", formContent);
        
        return bodyRaw;
    }
    
    public static async Task<string> GetCommonInventory(HttpClient client, string apiToken) {
        GetCommonInventoryRequest request = new GetCommonInventoryRequest {
            ContainerId = 1,
            LoadItemStats = true,
            Locale = "en-US"
        };
        var requestString = XmlUtil.SerializeXml(request);

        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("getCommonInventoryRequestXml", requestString)
        });

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/V2/ContentWebService.asmx/GetCommonInventory", formContent);
        
        return bodyRaw;
        //return XmlUtil.DeserializeXml<CommonInventoryData>(bodyRaw);
    }
}
