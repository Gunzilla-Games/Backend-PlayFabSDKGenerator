namespace PlayFab.Internal
{
    /// <summary>
    /// This is a base-class for all Api-request objects.
    /// It is currently unfinished, but we will add result-specific properties,
    ///   and add template where-conditions to make some code easier to follow
    /// </summary>
    public class PlayFabRequestCommon
    {
        public PlayFabAuthenticationContext? AuthenticationContext;
    }

    /// <summary>
    /// This is a base-class for all Api-result objects.
    /// It is currently unfinished, but we will add result-specific properties,
    ///   and add template where-conditions to make some code easier to follow
    /// </summary>
    public class PlayFabResultCommon
    {
    }

    public class PlayFabLoginResultCommon : PlayFabResultCommon
    {
        public PlayFabAuthenticationContext AuthenticationContext;
    }

    public class PlayFabJsonError
    {
        public int code;
        public string status;
        public string error;
        public int errorCode;
        public string errorMessage;
        public Dictionary<string, string[]>? errorDetails = null;
        public uint? retryAfterSeconds = null;
    }

    public class PlayFabJsonSuccess<TResult> where TResult : PlayFabResultCommon
    {
        public int code;
        public string status;
        public TResult data;
    }

    public static class PlayFabHttp
    {
        public static async Task<PlayFabResult<T>> DoPost<T>(string urlPath, PlayFabRequestCommon request, string? authType, string? authKey, Dictionary<string, string>? extraHeaders, PlayFabApiSettings? instanceSettings = null) where T : PlayFabResultCommon
        {
            await new PlayFabUtil.SynchronizationContextRemover();

            var settings = instanceSettings ?? PlayFabSettings.staticSettings;
            var fullPath = settings.GetFullUrl(urlPath);
            return await _DoPost<T>(fullPath, request, authType, authKey, extraHeaders, instanceSettings);
        }

        public static async Task<PlayFabResult<T>> DoPostWithFullUri<T>(string fullUriPath, PlayFabRequestCommon request, string? authType, string? authKey, Dictionary<string, string>? extraHeaders, PlayFabApiSettings? instanceSettings = null) where T : PlayFabResultCommon
        {
            await new PlayFabUtil.SynchronizationContextRemover();

            return await _DoPost<T>(fullUriPath, request, authType, authKey, extraHeaders, instanceSettings);
        }

        private static async Task<PlayFabResult<T>> _DoPost<T>(string fullPath, PlayFabRequestCommon request, string? authType, string? authKey, Dictionary<string, string>? extraHeaders, PlayFabApiSettings? instanceSettings = null) where T : PlayFabResultCommon
        {
            var settings = instanceSettings ?? PlayFabSettings.staticSettings;
            var titleId = settings.TitleId;
            if (titleId == null)
                throw new PlayFabException(PlayFabExceptionCode.TitleNotSet, "You must set your titleId before making an api call");
            var transport = PluginManager.GetPlugin<ITransportPlugin>(PluginContract.PlayFab_Transport);

            var headers = new Dictionary<string, string>();
            
            if (authType is not null && authKey is not null)
            {
                headers[authType] = authKey;
            }
            
            if (extraHeaders is not null)
            {
                foreach (var extraHeader in extraHeaders)
                {
                    headers.Add(extraHeader.Key, extraHeader.Value);
                }
            }

            return await transport.DoPost<T>(fullPath, request, headers);
        }
    }
}
