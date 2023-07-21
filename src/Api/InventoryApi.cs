using System.Net;
using dragonrescue.Util;

namespace dragonrescue.Api;
public static class InventoryApi {
    public static async Task<string> AddItem(HttpClient client, string apiToken, int itemID, int count) {
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("commonInventoryRequestXml", "<?xml version=\"1.0\" encoding=\"utf-8\"?><ArrayOfCommonInventoryRequest xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"><CommonInventoryRequest><iid>" + itemID.ToString() + "</iid><q>" + count.ToString() + "</q></CommonInventoryRequest></ArrayOfCommonInventoryRequest>"),
            new KeyValuePair<string, string>("ContainerId", "1"),
        });

        var response = await client.PostAsync(Config.URL_CONT_API + "/ContentWebService.asmx/SetCommonInventory", formContent);
        var bodyRaw = await response.Content.ReadAsStringAsync();
        
        return bodyRaw;
    }
}
