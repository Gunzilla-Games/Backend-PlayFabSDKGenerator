using System.Net.Http.Headers;
using System.Text;

namespace PlayFab.Internal
{
	public class PlayFabSysHttp : ITransportPlugin
	{
		private readonly HttpClient _client = new();

		public async Task<PlayFabResult<T>> DoPost<T>(string fullUrl, object? request,
			Dictionary<string, string>? extraHeaders) where T : PlayFabResultCommon
		{
			await new PlayFabUtil.SynchronizationContextRemover();

			var serializer = PluginManager.GetPlugin<ISerializerPlugin>(PluginContract.PlayFab_Serializer);
			var bodyString = request is null ? "{}" : serializer.Serialize(request);

			using var postBody = new StringContent(bodyString, Encoding.UTF8, "application/json");

			postBody.Headers.Add("X-PlayFabSDK", PlayFabSettings.SdkVersionString);
			if (extraHeaders is not null)
			{
				foreach (var headerPair in extraHeaders)
				{
					// Special case for Authorization header
					if (headerPair.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase))
					{
						_client.DefaultRequestHeaders.Authorization =
							new AuthenticationHeaderValue("Bearer", headerPair.Value);
					}
					else
					{
						postBody.Headers.Add(headerPair.Key, headerPair.Value);
					}
				}
			}

			try
			{
				var httpResponse = await _client.PostAsync(fullUrl, postBody);
				return await ProcessResponse<T>(httpResponse, serializer);
			}
			catch (HttpRequestException e)
			{
				return CreateError<T>(new PlayFabError
				{
					Error = PlayFabErrorCode.ConnectionError,
					ErrorMessage = e.InnerException?.Message ?? e.Message
				});
			}
			catch (Exception e)
			{
				return CreateError<T>(new PlayFabError
				{
					Error = PlayFabErrorCode.ConnectionError,
					ErrorMessage = e.Message
				});
			}
		}

		private static async Task<PlayFabResult<T>> ProcessResponse<T>(HttpResponseMessage httpResponse,
			ISerializerPlugin serializer) where T : PlayFabResultCommon
		{
			await using var stream = await httpResponse.Content.ReadAsStreamAsync();

			var requestId = GetRequestId(httpResponse);

			if (httpResponse.IsSuccessStatusCode)
			{
				if (stream.Length == 0)
				{
					return CreateError<T>(new PlayFabError
					{
						Error = PlayFabErrorCode.Unknown,
						ErrorMessage = "Internal server error",
						RequestId = requestId
					});
				}

				var playFabJsonSuccess = await serializer.DeserializeAsync<PlayFabJsonSuccess<T>>(stream);
				if (playFabJsonSuccess is null)
				{
					return CreateError<T>(new PlayFabError
					{
						Error = PlayFabErrorCode.Unknown,
						ErrorMessage = "Serialization issue",
						RequestId = requestId
					});
				}
				return new PlayFabResult<T> { Result = playFabJsonSuccess.data };
			}

			// In case of errors
			if (stream.Length == 0)
			{
				return CreateError<T>(new PlayFabError
				{
					HttpCode = (int)httpResponse.StatusCode,
					HttpStatus = httpResponse.StatusCode.ToString(),
					RequestId = requestId
				});
			}

			try
			{
				var errorResult = await serializer.DeserializeAsync<PlayFabJsonError>(stream);
				var error = new PlayFabError
				{
					HttpCode = errorResult.code,
					HttpStatus = errorResult.status,
					Error = (PlayFabErrorCode)errorResult.errorCode,
					ErrorMessage = errorResult.errorMessage,
					RetryAfterSeconds = errorResult.retryAfterSeconds,
					RequestId = requestId
				};

				if (errorResult.errorDetails is not null)
				{
					error.ErrorDetails = new Dictionary<string, string[]>();
					foreach (var detail in errorResult.errorDetails)
					{
						error.ErrorDetails.Add(detail.Key, detail.Value);
					}
				}

				return CreateError<T>(error);
			}
			catch (Exception e)
			{
				return CreateError<T>(new PlayFabError
				{
					HttpCode = (int)httpResponse.StatusCode,
					HttpStatus = httpResponse.StatusCode.ToString(),
					Error = PlayFabErrorCode.JsonParseError,
					ErrorMessage = e.Message,
					RequestId = requestId
				});
			}
		}

		private static string GetRequestId(HttpResponseMessage httpResponse)
		{
			const string defaultReqId = "NoRequestIdFound";

			try
			{
				if (!httpResponse.Headers.TryGetValues("X-RequestId", out var requestId))
				{
					return defaultReqId;
				}

				var reqId = requestId.FirstOrDefault();
				if (string.IsNullOrEmpty(reqId))
				{
					reqId = defaultReqId;
				}

				return reqId;
			}
			catch (Exception e)
			{
				return "Failed to Enumerate RequestId. Exception message: " + e.Message;
			}
		}

		private static PlayFabResult<T> CreateError<T>(PlayFabError error) where T : PlayFabResultCommon
		{
			return new PlayFabResult<T> { Error = error };
		}
	}
}