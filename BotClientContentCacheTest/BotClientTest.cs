using BotClientContentCacheTest.CustomBotClient;
using Telegram.Bot;
using Telegram.Bot.Types;
using Xunit.Abstractions;

namespace BotClientContentCacheTest;

public class BotClientTest(ITestOutputHelper output)
{
    private const string botToken = @"{BOT:TOKEN}";
    private const int retryCount = 5;
    private const long privateChatId = -10011999999;
    private const string filePath = @"Files/moon-landing.mp4";

    private readonly ITelegramBotClient cachedTelegramBotClient = new CachedTelegramBotClient(new(botToken), retryCount, output);
    private readonly ITelegramBotClient telegramBotClient = new RegularTelegramBotClient(new(botToken), retryCount, output);

    [Fact]
    public async Task Should_Use_CacheClient()
    {
        await using Stream file = System.IO.File.OpenRead(filePath);

        var message = await cachedTelegramBotClient.SendVideoAsync(
            chatId: privateChatId,
            video: new InputFileStream(file, Guid.NewGuid().ToString()));

        output.WriteLine($"Message {message?.MessageId}");
    }

    [Fact]
    public async Task Should_Use_RegularClient()
    {
        await using Stream file = System.IO.File.OpenRead(filePath);

        var message = await telegramBotClient.SendVideoAsync(
            chatId: privateChatId,
            video: new InputFileStream(file, Guid.NewGuid().ToString()));

        output.WriteLine($"Message {message?.MessageId}");
    }
}
