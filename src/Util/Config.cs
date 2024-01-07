namespace dragonrescue.Util;

public static class Config {
    public static string URL_USER_API = "";
    public static string URL_CONT_API = "";
    public const string KEY = "56BB211B-CF06-48E1-9C1D-E40B5173D759";
    public static string APIKEY = "b99f695c-7c6e-4e9b-b0f7-22034d799013";
    public const int NICE = 50;
    
    public delegate void WriteDelegate(string msg, params object[] args);
    public static WriteDelegate LogWriter = Console.WriteLine;
    
    public delegate void ProgressDelegate(double value);
    public static ProgressDelegate ProgressInfo = (double value) => {};
}
