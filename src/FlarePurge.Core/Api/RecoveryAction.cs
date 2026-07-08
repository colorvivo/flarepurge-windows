using System;

namespace FlarePurge.Core.Api;

public abstract record RecoveryAction
{
    public static readonly RecoveryAction Reauthenticate = new ReauthenticateAction();
    public static readonly RecoveryAction RefreshList = new RefreshListAction();
    public static readonly RecoveryAction UpdateApp = new UpdateAppAction();
    public static readonly RecoveryAction CheckConnection = new CheckConnectionAction();
    public static readonly RecoveryAction OpenSettings = new OpenSettingsAction();
    public static readonly RecoveryAction None = new NoneAction();

    public sealed record Retry(TimeSpan? After) : RecoveryAction;

    private sealed record ReauthenticateAction : RecoveryAction;
    private sealed record RefreshListAction : RecoveryAction;
    private sealed record UpdateAppAction : RecoveryAction;
    private sealed record CheckConnectionAction : RecoveryAction;
    private sealed record OpenSettingsAction : RecoveryAction;
    private sealed record NoneAction : RecoveryAction;
}
