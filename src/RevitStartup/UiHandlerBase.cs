namespace RevitStartup.Base
{
    public class UiHandlerBase : IUIHandler
    {
        protected string _path = "/";
        protected string _method = "GET";
        protected string _body = "{}";
        protected TaskCompletionSource<string> _tcs;

        public void Set(string path, string method, string body, TaskCompletionSource<string> tcs)
        {
            _path = path; _method = method; _body = body; _tcs = tcs;
        }
    }
}
