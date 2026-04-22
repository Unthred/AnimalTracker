using System.Security.Claims;
using AnimalTracker.Data;
using AnimalTracker.Services;
using Bunit;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace AnimalTracker.Tests.Ui;

public abstract class BunitTestBase : TestContext
{
    protected const string DefaultUserId = "test-ui-user";

    protected BunitTestBase()
    {
        Services.AddSingleton<IJSRuntime>(new NoopJsRuntime());
        Services.AddSingleton<AuthenticationStateProvider>(new TestAuthStateProvider(DefaultUserId));
        Services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor());

        // A few pages use HttpContext as a CascadingParameter (auth/account pages).
        Services.AddScoped(_ => CreateHttpContext());
    }

    protected static HttpContext CreateHttpContext(string? userId = null)
    {
        var ctx = new DefaultHttpContext();
        var id = userId ?? DefaultUserId;
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, id)],
            authenticationType: "Test"));
        return ctx;
    }

    protected sealed class NoopJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        {
            // Reasonable defaults for app JS helpers used in UI.
            object? value = identifier switch
            {
                "animalTrackerResolveDark" => false,
                "animalTrackerIsDarkActive" => false,
                _ => default(TValue)
            };

            return new ValueTask<TValue>((TValue?)value!);
        }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }

    protected sealed class TestAuthStateProvider(string userId) : AuthenticationStateProvider
    {
        private readonly ClaimsPrincipal _principal = new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)],
            authenticationType: "Test"));

        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(_principal));
    }
}

