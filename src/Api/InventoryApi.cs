using System.Net;
using dragonrescue.Util;

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

        var response = await client.PostAsync(Config.URL_CONT_API + "/ContentWebService.asmx/SetCommonInventory", formContent);
        var bodyRaw = await response.Content.ReadAsStringAsync();
        
        return bodyRaw;
    }
}
