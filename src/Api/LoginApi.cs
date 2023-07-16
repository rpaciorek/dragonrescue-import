using System.Net;
using dragonrescue.Schema;
using dragonrescue.Util;

namespace dragonrescue.Api;
public static class LoginApi {
    public static async Task<string> LoginParent(HttpClient client, string UserName, string Password) {
        ParentLoginData loginData = new ParentLoginData {
            UserName = UserName,
            Password = Password,
            Locale = "en-US"
        };

        var loginDataString = XmlUtil.SerializeXml(loginData);
        var loginDataStringEncrypted = TripleDES.EncryptUnicode(loginDataString, Config.KEY);

        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("parentLoginData", loginDataStringEncrypted)
        });

        var response = await client.PostAsync(Config.URL_USER_API + "/v3/AuthenticationWebService.asmx/LoginParent", formContent);
        var bodyRaw = await response.Content.ReadAsStringAsync();
        var bodyEncrypted = XmlUtil.DeserializeXml<string>(bodyRaw);
        var bodyDecrypted = TripleDES.DecryptUnicode(bodyEncrypted, Config.KEY);
        return bodyDecrypted;
        //return XmlUtil.DeserializeXml<ParentLoginInfo>(bodyDecrypted);
    }

    public static async Task<string> GetDetailedChildList(HttpClient client, string apiToken) {
        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("parentApiToken", apiToken)
        });

        var response = await client.PostAsync(Config.URL_USER_API + "/ProfileWebService.asmx/GetDetailedChildList", formContent);
        var bodyRaw = await response.Content.ReadAsStringAsync();
        return bodyRaw;
        //return XmlUtil.DeserializeXml<UserProfileDataList>(bodyRaw);
    }

    public static async Task<string> LoginChild(HttpClient client, string apiToken, string childUserId) {
        var childUserIdEncrypted = TripleDES.EncryptUnicode(childUserId, Config.KEY);

        var ticks = DateTime.UtcNow.Ticks.ToString();
        var locale = "en-US";
        var signature = Md5.GetMd5Hash(string.Concat(new string[]
            {
                ticks,
                Config.KEY,
                apiToken,
                childUserIdEncrypted,
                locale
            }));

        var formContent = new FormUrlEncodedContent(new[] {
            new KeyValuePair<string, string>("apiKey", Config.APIKEY),
            new KeyValuePair<string, string>("parentApiToken", apiToken),
            new KeyValuePair<string, string>("ticks", ticks),
            new KeyValuePair<string, string>("signature", signature),
            new KeyValuePair<string, string>("childUserID", childUserIdEncrypted),
            new KeyValuePair<string, string>("locale", locale),
        });

        var response = await client.PostAsync(Config.URL_USER_API + "/AuthenticationWebService.asmx/LoginChild", formContent);
        var bodyRaw = await response.Content.ReadAsStringAsync();
        var bodyEncrypted = XmlUtil.DeserializeXml<string>(bodyRaw);
        return TripleDES.DecryptUnicode(bodyEncrypted, Config.KEY);
    }
}
