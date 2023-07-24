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

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_USER_API + "/v3/AuthenticationWebService.asmx/LoginParent", formContent);
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

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_USER_API + "/ProfileWebService.asmx/GetDetailedChildList", formContent);
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

        var bodyRaw = await client.PostAndGetReplayOrThrow(Config.URL_USER_API + "/AuthenticationWebService.asmx/LoginChild", formContent);
        var bodyEncrypted = XmlUtil.DeserializeXml<string>(bodyRaw);
        return TripleDES.DecryptUnicode(bodyEncrypted, Config.KEY);
    }
    
    
    public static async Task<(HttpClient, string, UserProfileData)> DoVikingLogin(string username, string password, string viking) {
        Console.WriteLine(string.Format("Logging into School of Dragons (userApiUrl={2}, contentApiUrl={3}) as '{0}' with password '{1}'...", username, password, Config.URL_USER_API, Config.URL_CONT_API));
        
        HttpClient client = new HttpClient();
        string loginInfo = await LoginApi.LoginParent(client, username, password);

        ParentLoginInfo loginInfoObject = XmlUtil.DeserializeXml<ParentLoginInfo>(loginInfo);
        if (loginInfoObject.Status != MembershipUserStatus.Success) {
            Console.WriteLine("Login error. Please check username and password.");
        } else {
            Console.WriteLine("Fetching child profiles...");
            string children = await LoginApi.GetDetailedChildList(client, loginInfoObject.ApiToken);
            UserProfileDataList childrenObject = XmlUtil.DeserializeXml<UserProfileDataList>(children);
            Console.WriteLine(string.Format("Found {0} child profiles.", childrenObject.UserProfiles.Length));

            foreach (UserProfileData profile in childrenObject.UserProfiles) {
                if (viking != profile.AvatarInfo.AvatarData.DisplayName) { // always is the same as profile.AvatarInfo.UserInfo.Username and (for SoDOff only) profile.AvatarInfo.UserInfo.FirstName ???
                    Console.WriteLine(string.Format("Skip child profile: {0}.", profile.AvatarInfo.AvatarData.DisplayName));
                    continue;
                }
                
                Console.WriteLine(string.Format("Selecting profile {0} ({1})...", profile.AvatarInfo.AvatarData.DisplayName, profile.ID));
                var childApiToken = await LoginApi.LoginChild(client, loginInfoObject.ApiToken, profile.ID);
                
                return (client, childApiToken, profile);
            }
        }

        Environment.Exit(1);
        throw new Exception(); 
    }
}
