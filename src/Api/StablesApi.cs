using System.Net;
using System.Xml;
using dragonrescue.Schema;
using dragonrescue.Util;

namespace dragonrescue.Api;
public static class StablesApi {
    public static async Task<XmlDocument> GetStablesFull(HttpClient client, string apiToken) {
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("pairId", "2014"),
        });

        var response = await client.PostAsync(Config.URL_CONT_API + "/ContentWebService.asmx/GetKeyValuePair", formContent);
        var bodyRaw = await response.Content.ReadAsStringAsync();
        
        XmlDocument stablesXml = new XmlDocument();
        stablesXml.LoadXml(bodyRaw);
        
        return stablesXml;
    }
    
    public static async Task<string> SetStables(HttpClient client, string apiToken, XmlDocument newStablesXml, Dictionary<string, string> dragonsIDMap, bool replace) {
        var inventoryChanges = new Dictionary<int, int>();
        int oldStablesCount = 0;
        int newStablesCount = 0;
        XmlNode stablesCountNode = null;
        
        // read "current" stables info
        Thread.Sleep(Config.NICE);
        var stablesXml = await GetStablesFull(client, apiToken);
        
        // get stables count xml node and "old" stable count
        for (int i = stablesXml["Pairs"].ChildNodes.Count - 1; i >= 0; --i) {
            var key = stablesXml["Pairs"].ChildNodes[i]["PairKey"].InnerText;
            if (key == "NumStables") {
                stablesCountNode = stablesXml["Pairs"].ChildNodes[i]["PairValue"];
                oldStablesCount = Convert.ToInt32(stablesCountNode.InnerText);
            }
        }

        // remove old stables (if mode is replace)
        if (replace) {
            oldStablesCount = 0;
            for (int i = stablesXml["Pairs"].ChildNodes.Count - 1; i >= 0; --i) {
                var key = stablesXml["Pairs"].ChildNodes[i]["PairKey"].InnerText;
                if (key.Length > 6 && key.Substring(0,6) == "Stable") {
                    // count stables items to remove from inventory
                    XmlDocument stableData = new XmlDocument();
                    stableData.PreserveWhitespace = true;
                    stableData.LoadXml(stablesXml["Pairs"].ChildNodes[i]["PairValue"].InnerText);

                    // count stables items to add to inventory
                    int itemID = Convert.ToInt32(stableData["StableData"]["ItemID"].InnerText);
                    try {
                        inventoryChanges[itemID] -= 1;
                    } catch (KeyNotFoundException) {
                        inventoryChanges[itemID] = -1;
                    }
                    
                    // remove "old" stable from "current" stables xml
                    stablesXml["Pairs"].RemoveChild(stablesXml["Pairs"].ChildNodes[i]);
                }
            }
        }
        
        // add new stables
        for (int i = 0; i < newStablesXml["Pairs"].ChildNodes.Count; ++i) {
            var key = newStablesXml["Pairs"].ChildNodes[i]["PairKey"].InnerText;
            if (key.Length > 6 && (key.Substring(0,6) == "Stable")) {
                // parse stable data as XML
                XmlDocument stableData = new XmlDocument();
                stableData.PreserveWhitespace = true;
                stableData.LoadXml(newStablesXml["Pairs"].ChildNodes[i]["PairValue"].InnerText);

                // count stables items to add to inventory
                int itemID = Convert.ToInt32(stableData["StableData"]["ItemID"].InnerText);
                try {
                    inventoryChanges[itemID] += 1;
                } catch (KeyNotFoundException) {
                    inventoryChanges[itemID] = 1;
                }
                
                // update dragons IDs
                for (int j = 0; j < stableData["StableData"].ChildNodes.Count; ++j) {
                    if (stableData["StableData"].ChildNodes[j].Name == "Nests") {
                        string newID;
                        if (dragonsIDMap.TryGetValue(stableData["StableData"].ChildNodes[j]["PetID"].InnerText, out newID)) {
                            stableData["StableData"].ChildNodes[j]["PetID"].InnerText = newID;
                        }
                    }
                }
                    
                // add "new" stable data to "current" stables xml
                XmlElement newPair = stablesXml.CreateElement("Pair");
                
                XmlElement newPairKey = stablesXml.CreateElement("PairKey");
                newPairKey.InnerText = "Stable" + (oldStablesCount+newStablesCount).ToString();
                newPair.AppendChild(newPairKey);
                
                XmlElement newPairVal = stablesXml.CreateElement("PairValue");
                newPairVal.InnerText = stableData.OuterXml;
                newPair.AppendChild(newPairVal);
                
                XmlElement newPairUpd = stablesXml.CreateElement("UpdateDate");
                newPairUpd.InnerText = DateTime.Now.ToUniversalTime().ToString("u").Replace(" ", "T");
                newPair.AppendChild(newPairUpd);
                
                stablesXml["Pairs"].AppendChild(newPair);
                
                ++newStablesCount;
            }
        }
        
        // update stables count node
        stablesCountNode.InnerText = (oldStablesCount + newStablesCount).ToString();
        
        // call server API
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("pairId", "2014"),
            new KeyValuePair<string, string>("contentXML", stablesXml.OuterXml),
        });
        
        Thread.Sleep(Config.NICE);
        var response = await client.PostAsync(Config.URL_CONT_API + "/ContentWebService.asmx/SetKeyValuePair", formContent);
        var bodyRaw = await response.Content.ReadAsStringAsync();
        
        // add/remove stables itmes to/from inventory
        foreach (var x in inventoryChanges) {
            Console.WriteLine("InventoryChanges: " + x.Key.ToString() + "  " + x.Value.ToString());
            if (x.Value != 0) {
                Thread.Sleep(Config.NICE);
                string res = await InventoryApi.AddItem(client, apiToken, x.Key, x.Value);
                try {
                    XmlDocument resXML = new XmlDocument();
                    resXML.LoadXml(res);
                    if (resXML["CIRS"]["s"].InnerText == "true")
                        continue;
                } catch {}
                Console.WriteLine(string.Format("Error while adding {0} of {0} stable items to inventory", x.Value, x.Key));
            }
        }
        
        return bodyRaw;
    }
    
    public static async Task<XmlDocument> GetStables(HttpClient client, string apiToken) {
        var stablesXml = await GetStablesFull(client, apiToken);
        
        // we need only stables definitions, so we everything else
        for (int i = stablesXml["Pairs"].ChildNodes.Count - 1; i >= 0; --i) {
            var key = stablesXml["Pairs"].ChildNodes[i]["PairKey"].InnerText;
            if (key.Length < 6 || (key != "NumStables" && key.Substring(0,6) != "Stable")) {
                stablesXml["Pairs"].RemoveChild(stablesXml["Pairs"].ChildNodes[i]);
            }
        }
        
        return stablesXml;
    }
}
