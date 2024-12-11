#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace VideoIndexerClient
{
    public class VideoIndexerClient
    {
        private readonly HttpClient _httpClient;
        private string _accountAccessToken;
        private readonly Account _account;
        private readonly ILogger<VideoIndexerClient> _logger;

        public VideoIndexerClient(ILogger<VideoIndexerClient> logger, Account account)
        {
            System.Net.ServicePointManager.SecurityProtocol |= System.Net.SecurityProtocolType.Tls12 | System.Net.SecurityProtocolType.Tls13;
            _account = account ?? throw new ArgumentNullException(nameof(account));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpClient = new HttpClient();
        }

        public async Task Authenticate(string? accessToken = null)
        {
            try
            {
                _accountAccessToken = string.IsNullOrEmpty(accessToken)
                    ? await AccountTokenProvider.GetAccountAccessTokenAsync(_logger).ConfigureAwait(false)
                    : accessToken;
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _accountAccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could Not Authenticate VI Client");
                throw;
            }
        }

        public string GetThumbnailRequestUri(string videoId, string thumbnailId)
        {
            try
            {
                _logger.LogInformation("Getting Thumbnail {0} for Video {1}", videoId, thumbnailId);
                var requestUrl = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Videos/{videoId}/Thumbnails/{thumbnailId}?{GetQueryParams()}";
                return requestUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not Get Thumbnail from Server. videoId: {0}, thumbnailId: {1}", videoId, thumbnailId);
                return string.Empty;
            }
        }

        public async Task<VideoIndexData?> GetVideoIndexInsights(string videoId)
        {
            try
            {
                var requestUrl = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Videos/{videoId}/Index?{GetQueryParams()}";
                var videoGetIndexRequestResult = await _httpClient.GetAsync(requestUrl).ConfigureAwait(false);
                videoGetIndexRequestResult.VerifyStatus(System.Net.HttpStatusCode.OK);
                var videoGetIndexResult = await videoGetIndexRequestResult.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<VideoIndexData>(videoGetIndexResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not Get Video Index Insights. videoId: {0}", videoId);
                return null;
            }
        }
        public async Task<byte[]?> GetThumbnailImageAsync(string filePath)
        {
            try
            {
                var videoGetIndexRequestResult = await _httpClient.GetAsync(filePath).ConfigureAwait(false);
                videoGetIndexRequestResult.VerifyStatus(System.Net.HttpStatusCode.OK);
                return await videoGetIndexRequestResult.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not Get Thumbnail Image. filePath: {0}", filePath);
                return null;
            }
        }

        public async Task<string> PatchIndex(string videoId, CustomInsights customInsights, bool customInsightsAlreadyExists = false, string embeddedPath = DEFAULT_EMBEDDED_PATH)
        {
            var requestUrl = $"{ApiEndpoint}/{_account.Location}/Accounts/{_account.Properties.Id}/Videos/{videoId}/Index?{GetQueryParams()}";
            var wrapper = new List<object>
            {
                new
                {
                    op = customInsightsAlreadyExists ? "replace" : "add",
                    value = new[] { customInsights },
                    path = embeddedPath,
                }
            };

            var jsonPayload = JsonConvert.SerializeObject(wrapper);
            var patchContent = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

            try
            {
                var pathResponse = await _httpClient.PatchAsync(requestUrl, patchContent).ConfigureAwait(false);
                pathResponse.VerifyStatus(System.Net.HttpStatusCode.OK);
                return pathResponse.StatusCode.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not Patch Index. videoId: {0}", videoId);
                return string.Empty;
            }
        }

        private string GetQueryParams()
        {
            return new Dictionary<string, string>
            {
                { "accessToken", _accountAccessToken },
                { "language", "English" },
                { "format", "Jpeg" }
            }.CreateQueryString();
        }
    }
}