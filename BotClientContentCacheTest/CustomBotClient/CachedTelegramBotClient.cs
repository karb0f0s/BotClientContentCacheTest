using System.Net;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Xunit.Abstractions;

namespace BotClientContentCacheTest.CustomBotClient;

internal class CachedTelegramBotClient(TelegramBotClientOptions options, int retryCount, ITestOutputHelper output)
    : TelegramBotClient(options)
{
    readonly TelegramBotClientOptions _options = options;
    readonly HttpClient _httpClient = new();
    private readonly int retryCount = retryCount;
    private readonly ITestOutputHelper output = output;

    public override async Task<TResponse> MakeRequestAsync<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ApiRequestException apiRequestException = default!;

        for (var i = 0; i < retryCount; i++)
        {
            output.WriteLine($"Sending request {i}.");

            try
            {
                TResponse response = await MakeRequestAsync2(request, cancellationToken);

                if (i == retryCount - 1) return response;
            }
            catch (ApiRequestException e) when (e.ErrorCode == 429)
            {
                apiRequestException = e;

                var timeout = e.Parameters?.RetryAfter is null
                    ? TimeSpan.FromSeconds(10)
                    : TimeSpan.FromSeconds(e.Parameters.RetryAfter.Value);

                output.WriteLine($"Retry attempt {i + 1}. Waiting for {timeout} seconds before retrying.");
                await Task.Delay(timeout, cancellationToken);
            }
        }
        throw apiRequestException;
    }

    private async Task<TResponse> MakeRequestAsync2<TResponse>(
        IRequest<TResponse> request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var url = $"{_options.BaseRequestUrl}/{request.MethodName}";

        var content = await request.CachedContent().ConfigureAwait(false);
        var httpRequest = new HttpRequestMessage(method: request.Method, requestUri: url)
        {
            Content = content,
        };

        using var httpResponse = await GetHttpResponseMessageAsync(
            httpClient: _httpClient,
            httpRequest: httpRequest,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);

        if (httpResponse.StatusCode != HttpStatusCode.OK)
        {
            var failedApiResponse = await httpResponse
                .DeserializeContentAsync<ApiResponse>(
                    guard: response =>
                        response.ErrorCode == default ||
                        // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                        response.Description is null
                )
                .ConfigureAwait(false);

            throw ExceptionsParser.Parse(failedApiResponse);
        }

        var apiResponse = await httpResponse
            .DeserializeContentAsync<ApiResponse<TResponse>>(
                guard: response => !response.Ok || response.Result is null)
            .ConfigureAwait(false);

        return apiResponse.Result!;
    }

    private static async Task<HttpResponseMessage> GetHttpResponseMessageAsync(HttpClient httpClient, HttpRequestMessage httpRequest, CancellationToken cancellationToken)
    {
        HttpResponseMessage? httpResponse;
        try
        {
            httpResponse = await httpClient
                .SendAsync(request: httpRequest, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new RequestException(message: "Request timed out", innerException: exception);
        }
        catch (Exception exception)
        {
            throw new RequestException(message: "Exception during making request", innerException: exception);
        }

        return httpResponse;
    }
}
