using System.Runtime.CompilerServices;
using System.Text;

namespace BotClientContentCacheTest.CustomBotClient;

internal static class StringExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string EncodeUtf8(this string value) =>
        new(Encoding.UTF8.GetBytes(value).Select(c => Convert.ToChar(c)).ToArray());
}
