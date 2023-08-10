using System.Net;
using System.Xml;
using dragonrescue.Util;
using dragonrescue.Schema;

namespace dragonrescue.Api;
public static class InventoryApi {
    public static async Task<int> AddItemAndGetInventoryId(HttpClient client, string apiToken, int itemID, int count) {
        string res = await InventoryApi.AddItem(client, apiToken, itemID, 1);
        XmlDocument resXML = new XmlDocument();
        resXML.LoadXml(res);
        
        // return CommonInventoryID (aka UserInventoryID)
        return Convert.ToInt32(resXML["CIRS"]["cids"]["cid"].InnerText);
    }
    
    public static async Task<string> AddItem(HttpClient client, string apiToken, int itemID, int count) {
        return await AddItems(client, apiToken, new Dictionary<int, int>(){{itemID, count}});
    }
    
    public static async Task<string> AddItems(HttpClient client, string apiToken, Dictionary<int, int> itemsCount) {
        var request = new List<CommonInventoryRequest>();
        
        foreach (var x in itemsCount) {
            request.Add(new CommonInventoryRequest(){ItemID=x.Key, Quantity=x.Value});
        }
        
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("commonInventoryRequestXml", XmlUtil.SerializeXml(request.ToArray())),
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
