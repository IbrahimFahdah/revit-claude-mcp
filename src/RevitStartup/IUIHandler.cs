namespace RevitStartup.Base
{
    public interface IUIHandler
    {
        void Set(string path, string method, string body, TaskCompletionSource<string> tcs);
    }
}
