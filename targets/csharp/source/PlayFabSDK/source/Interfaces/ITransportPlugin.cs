using PlayFab.Internal;

namespace PlayFab
{
    /// <summary>
    /// Interface of any transport SDK plugin.
    /// </summary>
    public interface ITransportPlugin : IPlayFabPlugin
    {
        // Task<T> DoPost<T>(string fullPath, object? request, Dictionary<string, string> headers);
        Task<PlayFabResult<T>> DoPost<T>(string fullUrl, object? request, Dictionary<string, string> extraHeaders)
	        where T : PlayFabResultCommon;
    }
}