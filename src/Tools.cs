using System.Net;
using dragonrescue.Api;
using dragonrescue.Util;
using dragonrescue.Schema;

namespace dragonrescue;
class Tools {
    public static async System.Threading.Tasks.Task SelectDragon(LoginApi.Data loginData, int raisedPetID, int petTypeID) {
        (var client, var apiToken, var profile) = await LoginApi.DoVikingLogin(loginData);
        
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("raisedPetID", raisedPetID.ToString()),
        });
        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/ContentWebService.asmx/SetSelectedPet", formContent);
        Config.LogWriter(string.Format("SetSelectedPet for {0}: {1}", raisedPetID, bodyRaw));
        
        string keyName;
        if (Config.APIKEY == "1552008f-4a95-46f5-80e2-58574da65875"){
           keyName = "CurrentRaisedPetType";
        } else if (Config.APIKEY == "6738196d-2a2c-4ef8-9b6e-1252c6ec7325"){
            keyName = "MBCurrentRaisedPetType";
        } else {
            return;
        }
        formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("apiToken", apiToken),
            new KeyValuePair<string, string>("pairId", "1967"),
            new KeyValuePair<string, string>("contentXML", 
                "<?xml version=\"1.0\"?><Pairs xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\" xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"><Pair><PairKey>" + keyName +
                "</PairKey><PairValue>" + petTypeID.ToString() + "</PairValue><UpdateDate>0001-01-01T00:00:00</UpdateDate></Pair></Pairs>"),
        });
        bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_CONT_API + "/ContentWebService.asmx/SetKeyValuePair", formContent);
        Config.LogWriter(string.Format("Set {0} to {1}: {2}", keyName, petTypeID, bodyRaw));
    }
}
