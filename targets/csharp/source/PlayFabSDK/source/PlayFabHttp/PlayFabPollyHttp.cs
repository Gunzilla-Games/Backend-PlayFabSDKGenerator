using Polly;
using Polly.Wrap;

namespace PlayFab.Internal
{
    /// <summary>
    /// A Polly wrapped version of PlayFab Transport plug in which used
    /// when making http requests to PlayFab Main Server.
    /// </summary>
    public class PlayFabPollyHttp : ITransportPlugin
    {
        /// <summary>
        /// Http requests worth retrying
        /// </summary>
        public readonly HashSet<int> HttpStatusCodesWorthRetrying = new()
        {
            408, //HttpStatusCode.RequestTimeout
            409, //HttpStatusCode.Conflict
            429, //HttpStatusCode.TooManyRequests
            500, //HttpStatusCode.InternalServerError
            502, //HttpStatusCode.BadGateway
            503, //HttpStatusCode.ServiceUnavailable
            504 //HttpStatusCode.GatewayTimeout
        };

        /// <summary>
        /// Gets or set the name of the plug in. Used by the PluginManager when looking up
        /// plugins upon request.
        /// </summary>
        public string Name;

        private readonly PlayFabSysHttp _playFabSysHttp;

        /// <summary>
        /// Gets the resilience policies defined.
        /// </summary>
        public AsyncPolicyWrap<PlayFabBaseResult> CommonResilience { get; private set; }

        /// <summary>
        /// Constructor for objects of type PollyTransportPlug.
        /// <remarks>
        /// Sets a default resilience policy with the fololwing common settings
        /// 1) Sets the retry to 3 times and has an embedded backoff.
        /// 2) > Sets a circuit breaker that will trigger is 25% of collapsed calls within 
        ///      a 5 second window are failing.
        ///    > Period for evaluation requires a burst of >=2RPS before evaluating breaker rule,
        ///      requires a minimum of 10 requests in 5 seconds, and the circuit breaker will
        ///      will be open for 20 second.
        /// More information on the client can be found here: https://github.com/App-vNext/Polly
        ///</remarks>
        /// </summary>
        public PlayFabPollyHttp()
        {
	        _playFabSysHttp = new PlayFabSysHttp();
	        var jitter = new Random();
            var retryPolicy = Policy
               .Handle<Exception>()
               .OrResult<PlayFabBaseResult>(r => r.Error != null && HttpStatusCodesWorthRetrying.Contains(r.Error.HttpCode))
                    .WaitAndRetryAsync(1,
                      retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))  // exponential back-off: 2, 4, 8 etc
                                + TimeSpan.FromMilliseconds(jitter.Next(0, 1000)));  // plus some jitter: up to 1 second

            var breakerPolicy = Policy.Handle<Exception>()
                .OrResult<PlayFabBaseResult>(r => r.Error != null && HttpStatusCodesWorthRetrying.Contains(r.Error.HttpCode))
                .AdvancedCircuitBreakerAsync(
                    failureThreshold: 0.25,
                    samplingDuration: TimeSpan.FromSeconds(5),
                    minimumThroughput: 2,
                    durationOfBreak: TimeSpan.FromSeconds(20));

            CommonResilience = breakerPolicy.WrapAsync(retryPolicy);
        }

        public async Task<PlayFabResult<T>> DoPost<T>(string fullPath, object? request, Dictionary<string, string> headers) where T : PlayFabResultCommon
        {
	        var executeAsync = await CommonResilience
		        .ExecuteAsync(async () => await _playFabSysHttp.DoPost<T>(fullPath, request, headers));

	        return (PlayFabResult<T>)executeAsync;
        }

        /// <summary>
        /// Overrides the Polly Policies to enforce.
        /// </summary>
        /// <param name="policy">The policy to use.</param>
        /// <exception cref="ArgumentNullException"> Thrown when retryPolicy and/or breakerPolicy is null.</exception>
        public void OverridePolicies(AsyncPolicyWrap<PlayFabBaseResult> policy)
        {
            CommonResilience = policy;
        }
    }
}
