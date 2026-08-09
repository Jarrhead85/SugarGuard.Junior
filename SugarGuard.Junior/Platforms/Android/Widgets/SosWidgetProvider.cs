#if ANDROID
using Android.App;
using Android.Appwidget;
using Android.Content;
using Android.OS;
using Android.Widget;
using SugarGuard.Junior.Platforms.Android.Glucose;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.Platforms.Android.Widgets;

[BroadcastReceiver(Enabled = true, Exported = false, Label = "SugarGuard SOS")]
[IntentFilter(new[] { AppWidgetManager.ActionAppwidgetUpdate, SosAction })]
[MetaData("android.appwidget.provider", Resource = "@xml/sugarguard_sos_widget_info")]
public sealed class SosWidgetProvider : AppWidgetProvider
{
    public const string SosAction = "com.sugarguard.junior.widget.SOS";
    private const int SosRequestCode = 7701;

    public override void OnUpdate(Context context, AppWidgetManager manager, int[] appWidgetIds)
    {
        foreach (var id in appWidgetIds) UpdateWidget(context, manager, id);
    }

    public override void OnAppWidgetOptionsChanged(
        Context context,
        AppWidgetManager appWidgetManager,
        int appWidgetId,
        Bundle newOptions)
    {
        UpdateWidget(context, appWidgetManager, appWidgetId);
    }

    public override void OnReceive(Context context, Intent intent)
    {
        base.OnReceive(context, intent);
        if (intent.Action != SosAction) return;

        var result = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                var sent = await JugglucoBroadcastRuntime.GetRequiredService<IWidgetEmergencyService>().SendSosAsync();
                Toast.MakeText(context, sent ? "SOS и координаты отправлены родителю" : "Не удалось получить координаты или отправить SOS. Откройте SugarGuard.", ToastLength.Long)?.Show();
            }
            finally { result.Finish(); }
        });
    }

    private static void UpdateWidget(Context context, AppWidgetManager manager, int appWidgetId)
    {
        var options = manager.GetAppWidgetOptions(appWidgetId);
        var minWidth = options?.GetInt(AppWidgetManager.OptionAppwidgetMinWidth, 0) ?? 0;
        manager.UpdateAppWidget(appWidgetId, CreateViews(context, minWidth < 170));
    }

    private static RemoteViews CreateViews(Context context, bool compact)
    {
        var layout = compact
            ? Resource.Layout.sugarguard_sos_widget_compact
            : Resource.Layout.sugarguard_sos_widget;
        var views = new RemoteViews(context.PackageName, layout);
        var intent = new Intent(context, typeof(SosWidgetProvider)).SetAction(SosAction).SetPackage(context.PackageName);
        var pending = PendingIntent.GetBroadcast(context, SosRequestCode, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        views.SetOnClickPendingIntent(Resource.Id.sos_button, pending);
        return views;
    }
}
#endif
