using System;
using System.Net.Http;
using System.Net.Http.Headers;
using FlarePurge.App.Demo;
using FlarePurge.App.ViewModels;
using FlarePurge.Core.Api;
using FlarePurge.Core.Auth;
using FlarePurge.Core.Purge;
using FlarePurge.Core.Services;
using FlarePurge.Core.Status;
using Microsoft.Extensions.DependencyInjection;

namespace FlarePurge.App;

public static class DependencyInjection
{
    public static ServiceProvider Build(bool demoMode = false) => new ServiceCollection()
        .AddCoreServices(demoMode)
        .AddViewModels()
        .BuildServiceProvider();

    private static IServiceCollection AddCoreServices(this IServiceCollection services, bool demoMode)
    {
        services.AddSingleton<IPurgeHistoryStore, InMemoryPurgeHistoryStore>();

        if (demoMode)
        {
            services.AddSingleton<IKeychainProvider, DemoKeychain>();
            services.AddSingleton<IAccountStore, InMemoryAccountStore>();
            services.AddSingleton<IZoneCacheStore, InMemoryZoneCacheStore>();
            services.AddSingleton<IAuthService, DemoAuthService>();
            services.AddSingleton<IZoneService, DemoZoneService>();
            services.AddSingleton<ICacheService, DemoCacheService>();
            services.AddSingleton<IRemoteStatusService, DemoRemoteStatusService>();
            return services;
        }

        services.AddSingleton<IKeychainProvider, WindowsCredentialKeychain>();
        services.AddSingleton<IAccountStore>(_ => new JsonAccountStore());
        services.AddSingleton<IZoneCacheStore>(_ => new JsonZoneCacheStore());
        services.AddSingleton<RateLimiter>();

        services.AddSingleton(_ =>
        {
            var handler = new CertificatePinningHandler();
            var http = new HttpClient(handler)
            {
                BaseAddress = Endpoints.Base,
                Timeout = TimeSpan.FromSeconds(30),
            };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("FlarePurge/1.0 (Windows)");
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            return http;
        });

        services.AddSingleton<IApiClient>(sp => new ApiClient(
            sp.GetRequiredService<HttpClient>(),
            sp.GetRequiredService<RateLimiter>(),
            TokenProviderFactory.FromActiveAccount(
                sp.GetRequiredService<IAccountStore>(),
                sp.GetRequiredService<IKeychainProvider>())));

        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IZoneService, ZoneService>();
        services.AddSingleton<ICacheService, CacheService>();
        services.AddSingleton<IRemoteStatusService, RemoteStatusService>();

        return services;
    }

    private static IServiceCollection AddViewModels(this IServiceCollection services)
    {
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<TokenWizardViewModel>();
        services.AddTransient<ZoneListViewModel>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<SettingsViewModel>();
        return services;
    }
}
