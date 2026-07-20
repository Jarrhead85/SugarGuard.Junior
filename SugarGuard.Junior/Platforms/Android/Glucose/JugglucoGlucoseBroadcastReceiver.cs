#if ANDROID
using Android.Content;
using Android.OS;
using JavaNumber = Java.Lang.Number;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Core.Sensors;

namespace SugarGuard.Junior.Platforms.Android.Glucose;

/// <summary>
/// Принимает документированные broadcast Juggluco и xDrip.
/// Для работы пользователь выбирает SugarGuard в настройке передачи данных Juggluco.
/// </summary>
[BroadcastReceiver(Enabled = true, Exported = true)]
[global::Android.App.IntentFilter(new[]
{
    JugglucoBroadcastContract.GlucodataMinuteAction,
    JugglucoBroadcastContract.XdripBgEstimateAction
})]
public sealed class JugglucoGlucoseBroadcastReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action is null)
        {
            return;
        }

        var pendingResult = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                var payload = CreatePayload(intent);
                if (!JugglucoBroadcastReadingParser.TryParse(payload, DateTime.UtcNow, out var reading, out var error) || reading is null)
                {
                    System.Diagnostics.Debug.WriteLine($"Некорректное сообщение Juggluco: {error}");
                    return;
                }

                await JugglucoBroadcastRuntime.ConsumeAsync(reading);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка обработки Juggluco broadcast: {ex}");
            }
            finally
            {
                pendingResult?.Finish();
            }
        });
    }

    private static JugglucoBroadcastPayload CreatePayload(Intent intent)
    {
        var isXdrip = intent.Action == JugglucoBroadcastContract.XdripBgEstimateAction;

        return new JugglucoBroadcastPayload(
            Action: intent.Action ?? string.Empty,
            Glucose: ReadDouble(intent, isXdrip
                ? JugglucoBroadcastContract.XdripGlucoseExtra
                : JugglucoBroadcastContract.GlucoseExtra),
            MgDl: ReadDouble(intent, JugglucoBroadcastContract.MgDlExtra),
            TimestampUnixMilliseconds: ReadLong(intent, isXdrip
                ? JugglucoBroadcastContract.XdripTimestampExtra
                : JugglucoBroadcastContract.TimeExtra),
            Rate: ReadDouble(intent, isXdrip
                ? JugglucoBroadcastContract.XdripDeltaExtra
                : JugglucoBroadcastContract.RateExtra),
            SensorSerialNumber: ReadString(intent, JugglucoBroadcastContract.SerialNumberExtra));
    }

    private static double? ReadDouble(Intent intent, string key)
    {
        var value = intent.Extras?.Get(key);
        if (value is JavaNumber number)
        {
            return number.DoubleValue();
        }

        return double.TryParse(value?.ToString(), System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static long? ReadLong(Intent intent, string key)
    {
        var value = intent.Extras?.Get(key);
        if (value is JavaNumber number)
        {
            return number.LongValue();
        }

        return long.TryParse(value?.ToString(), System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? ReadString(Intent intent, string key) =>
        intent.Extras?.Get(key)?.ToString();
}
#endif
