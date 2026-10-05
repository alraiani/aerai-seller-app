// Composition root for the AERai web app (seller.aeraigroup.com).
// This is the only file in the UI project allowed to reference AERai.Web.Infrastructure.

using System.Threading.RateLimiting;
using AERai.Web.Application;
using AERai.Web.Application.Security;
using AERai.Web.Infrastructure;
using AERai.Web.Infrastructure.Persistence;
using AERai.Web.Infrastructure.Storage;
using AERai.Web.UI.Middleware;
using AERai.Web.UI.Models;
using Azure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// In Azure, secrets (connection string overrides, seed values) come from Key Vault via the app's
// managed identity. Locally, KeyVault:Uri is unset and user-secrets / environment variables are used.
if (builder.Configuration["KeyVault:Uri"] is { Length: > 0 } keyVaultUri)
{
    builder.Configuration.AddAzureKeyVault(new Uri(keyVaultUri), new DefaultAzureCredential());
}

if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "AERai.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;

    // Development may run over plain HTTP (the "http" launch profile, the compose container);
    // everywhere else the cookie is HTTPS-only.
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorizationBuilder()
    // Secure by default: any endpoint without explicit metadata requires a signed-in user.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(AppPolicies.RequireAdmin, policy => policy.RequireRole(AppRoles.Admin))
    .AddPolicy(AppPolicies.RequireOperator, policy => policy.RequireRole(AppRoles.Admin, AppRoles.Operator));

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AuthorizeFolder("/Tools", AppPolicies.RequireOperator);
    options.Conventions.AuthorizeFolder("/Admin", AppPolicies.RequireAdmin);
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
    options.Conventions.AllowAnonymousToPage("/Account/ForgotPassword");
    options.Conventions.AllowAnonymousToPage("/Account/ForgotPasswordConfirmation");
    options.Conventions.AllowAnonymousToPage("/Account/ResetPassword");
    options.Conventions.AllowAnonymousToPage("/Error");
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Too many password reset requests from this network. Please wait 15 minutes and try again.", cancellationToken);
    };
    options.AddPolicy(RateLimits.PasswordReset, context =>
        HttpMethods.IsPost(context.Request.Method)
            ? RateLimitPartition.GetFixedWindowLimiter(
                // Behind App Service the client IP comes from X-Forwarded-For (ASPNETCORE_FORWARDEDHEADERS_ENABLED).
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(15), QueueLimit = 0 })
            : RateLimitPartition.GetNoLimiter("get"));
});

builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseRequestLocalization("en-US");

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Static assets must be reachable before sign-in (the login page needs its CSS).
app.MapStaticAssets().AllowAnonymous();
app.MapRazorPages().WithStaticAssets();
app.MapHealthChecks("/healthz").AllowAnonymous();

// Development applies migrations automatically; other environments are migrated by the deployment
// pipeline before the new build receives traffic. Roles (and the optional seed admin) are ensured everywhere.
await app.Services.InitializeDatabaseAsync(applyMigrations: app.Environment.IsDevelopment());
await app.Services.InitializeRawStorageAsync();

await app.RunAsync();
