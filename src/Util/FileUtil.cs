using System.Net;
using System.Xml;
namespace dragonrescue.Util;

public static class FileUtil {
    public static void WriteToFile(string path, string name, string contents) {
        string fullPath = Path.Join(path, name);
        using (StreamWriter writer = new StreamWriter(fullPath)) {
            writer.WriteLine(contents);
        }
    }

    public static void WriteToChildFile(string path, string childId, string name, string contents) {
        string fullName = string.Format("{0}-{1}", childId, name);
        string fullPath = Path.Join(path, fullName);
        using (StreamWriter writer = new StreamWriter(fullPath)) {
            writer.WriteLine(contents);
        }
    }

    public static void WriteToChildFile(string path, string childId, string name, XmlDocument contents) {
        string fullName = string.Format("{0}-{1}", childId, name);
        string fullPath = Path.Join(path, fullName);
        using (XmlTextWriter writer = new XmlTextWriter(fullPath, null)) {
            writer.Formatting = Formatting.Indented;
            contents.Save(writer);
        }
    }

    public static void DownloadFile(string path, string name, string downloadUrl) {
        string fullPath = Path.Join(path, name);
        Console.WriteLine(fullPath);
        var webClient = new WebClient();
        webClient.DownloadFile(downloadUrl, fullPath);
    }
}
