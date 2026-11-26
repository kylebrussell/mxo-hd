using System;
using System.Net;
using System.Text.Json;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace hds.accounts
{
    /// <summary>Environment settings for the sign-up API. Everything is off or loopback-only by default.</summary>
    public sealed class SignupSettings
    {
        public bool Enabled { get; set; }
        public string Url { get; set; } = DefaultUrl;
        public bool TrustCloudflareIp { get; set; }
        public string TurnstileSecret { get; set; }
        public string CorsOrigin { get; set; }
        public int PerIpLimit { get; set; } = 10;
        public int GlobalLimit { get; set; } = 100;
        public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(10);

        public const string DefaultUrl = "http://127.0.0.1:11080";

        public static SignupSettings FromEnvironment()
        {
            string Env(string name) => Environment.GetEnvironmentVariable(name);
            int Int(string name, int fallback) => int.TryParse(Env(name), out int value) && value > 0 ? value : fallback;
            return new SignupSettings
            {
                Enabled = Env("MXO_SIGNUP_HTTP") == "1",
                Url = string.IsNullOrWhiteSpace(Env("MXO_SIGNUP_URL")) ? DefaultUrl : Env("MXO_SIGNUP_URL").Trim(),
                TrustCloudflareIp = Env("MXO_SIGNUP_TRUST_CF") == "1",
                TurnstileSecret = string.IsNullOrWhiteSpace(Env("MXO_TURNSTILE_SECRET")) ? null : Env("MXO_TURNSTILE_SECRET").Trim(),
                CorsOrigin = string.IsNullOrWhiteSpace(Env("MXO_SIGNUP_CORS_ORIGIN")) ? null : Env("MXO_SIGNUP_CORS_ORIGIN").Trim(),
                PerIpLimit = Int("MXO_SIGNUP_PER_IP_LIMIT", 10),
                GlobalLimit = Int("MXO_SIGNUP_GLOBAL_LIMIT", 100),
                Window = TimeSpan.FromSeconds(Int("MXO_SIGNUP_WINDOW_SECONDS", 600)),
            };
        }
    }

    /// <summary>
    /// The sign-up HTTP API, hosted inside hds when MXO_SIGNUP_HTTP=1. It listens on MXO_SIGNUP_URL (default
    /// http://127.0.0.1:11080, loopback only) so it can sit behind a Cloudflare Tunnel.
    ///   POST /api/signup  {"username","password","email"?,"turnstileToken"?}
    ///   GET  /api/health
    /// Errors are generic: the only thing a caller can learn about existing accounts is "username_taken".
    /// </summary>
    public static class SignupHost
    {
        public const int MaxBodyBytes = 4096;
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        public static async Task<WebApplication> StartAsync(SignupSettings settings, SignupService service)
        {
            WebApplicationBuilder builder = WebApplication.CreateSlimBuilder(Array.Empty<string>());
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls(settings.Url);
            builder.WebHost.ConfigureKestrel(kestrel =>
            {
                kestrel.AddServerHeader = false;
                kestrel.Limits.MaxRequestBodySize = MaxBodyBytes;
                kestrel.Limits.MaxRequestHeadersTotalSize = 16 * 1024;
                kestrel.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
            });
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                    PartitionedRateLimiter.Create<HttpContext, string>(context => IsSignup(context)
                        ? RateLimitPartition.GetFixedWindowLimiter("ip:" + ClientIp(context, settings), _ => Window(settings.PerIpLimit, settings.Window))
                        : RateLimitPartition.GetNoLimiter("other")),
                    PartitionedRateLimiter.Create<HttpContext, string>(context => IsSignup(context)
                        ? RateLimitPartition.GetFixedWindowLimiter("global", _ => Window(settings.GlobalLimit, settings.Window))
                        : RateLimitPartition.GetNoLimiter("other")));
                options.OnRejected = (rejected, cancel) =>
                    new ValueTask(WriteAsync(rejected.HttpContext, StatusCodes.Status429TooManyRequests, "rate_limited",
                        "Too many sign-up attempts. Try again later."));
            });

            WebApplication app = builder.Build();
            app.Use((RequestDelegate next) => (RequestDelegate)(async context =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["Cache-Control"] = "no-store";
                if (settings.CorsOrigin != null)
                {
                    context.Response.Headers["Access-Control-Allow-Origin"] = settings.CorsOrigin;
                    context.Response.Headers["Vary"] = "Origin";
                    if (HttpMethods.IsOptions(context.Request.Method))
                    {
                        context.Response.Headers["Access-Control-Allow-Methods"] = "POST, GET, OPTIONS";
                        context.Response.Headers["Access-Control-Allow-Headers"] = "Content-Type";
                        context.Response.Headers["Access-Control-Max-Age"] = "600";
                        context.Response.StatusCode = StatusCodes.Status204NoContent;
                        return;
                    }
                }
                await next(context);
            }));
            app.UseRateLimiter();
            app.MapGet("/api/health", (RequestDelegate)(context =>
                WriteAsync(context, StatusCodes.Status200OK, null, null)));
            app.MapPost("/api/signup", (RequestDelegate)(context => HandleSignupAsync(context, settings, service)));

            await app.StartAsync();
            Output.WriteLine("Sign-up API listening on " + string.Join(", ", app.Urls) +
                             (settings.TurnstileSecret != null ? " (Turnstile on)" : " (no bot check)"));
            if (!IsLoopbackUrl(settings.Url))
                Output.WriteLine("WARNING: the sign-up API isn't bound to loopback. Put it behind a tunnel or proxy that sets the client address.");
            return app;
        }

        private static FixedWindowRateLimiterOptions Window(int permits, TimeSpan window) => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = window,
            QueueLimit = 0,
            AutoReplenishment = true,
        };

        private static bool IsSignup(HttpContext context) =>
            context.Request.Path.StartsWithSegments("/api/signup") && HttpMethods.IsPost(context.Request.Method);

        /// <summary>
        /// The caller's address. CF-Connecting-IP is believed only with MXO_SIGNUP_TRUST_CF=1 and only from a loopback
        /// peer (cloudflared on this machine); otherwise anyone could pick their own rate-limit bucket.
        /// </summary>
        public static string ClientIp(HttpContext context, SignupSettings settings)
        {
            IPAddress peer = context.Connection.RemoteIpAddress;
            if (settings.TrustCloudflareIp && peer != null && IPAddress.IsLoopback(peer) &&
                IPAddress.TryParse(context.Request.Headers["CF-Connecting-IP"].ToString(), out IPAddress forwarded))
                return forwarded.ToString();
            return peer?.ToString() ?? "unknown";
        }

        private static async Task HandleSignupAsync(HttpContext context, SignupSettings settings, SignupService service)
        {
            SignupRequest request;
            if (!context.Request.HasJsonContentType())
            {
                await WriteAsync(context, StatusCodes.Status415UnsupportedMediaType, "bad_request", "Send JSON.");
                return;
            }
            try
            {
                request = await JsonSerializer.DeserializeAsync<SignupRequest>(context.Request.Body, Json, context.RequestAborted);
            }
            catch (Exception)
            {
                await WriteAsync(context, StatusCodes.Status400BadRequest, "bad_request", "Malformed request.");
                return;
            }
            if (request == null)
            {
                await WriteAsync(context, StatusCodes.Status400BadRequest, "bad_request", "Malformed request.");
                return;
            }
            if (string.IsNullOrEmpty(request.TurnstileToken))
                request.TurnstileToken = context.Request.Headers["CF-Turnstile-Response"].ToString();

            SignupResult result = await service.SignupAsync(request, ClientIp(context, settings), context.RequestAborted);
            request.Password = null;
            switch (result.Outcome)
            {
                case SignupOutcome.Created:
                    Output.WriteLine("Signup: created account " + result.UserId);
                    await WriteAsync(context, StatusCodes.Status201Created, null, null, result.Username);
                    break;
                case SignupOutcome.InvalidUsername:
                    await WriteAsync(context, StatusCodes.Status400BadRequest, "invalid_username",
                        "Usernames are " + AccountRules.UsernameMin + "-" + AccountRules.UsernameMax +
                        " letters, digits or underscores, starting with a letter.");
                    break;
                case SignupOutcome.InvalidPassword:
                    await WriteAsync(context, StatusCodes.Status400BadRequest, "invalid_password",
                        "Passwords are " + AccountRules.PasswordMin + "-" + AccountRules.PasswordMax +
                        " printable ASCII characters and must not match the username.");
                    break;
                case SignupOutcome.InvalidEmail:
                    await WriteAsync(context, StatusCodes.Status400BadRequest, "invalid_email", "That email address isn't valid.");
                    break;
                case SignupOutcome.UsernameTaken:
                    await WriteAsync(context, StatusCodes.Status409Conflict, "username_taken", "That username is taken.");
                    break;
                case SignupOutcome.BotCheckFailed:
                    await WriteAsync(context, StatusCodes.Status403Forbidden, "verification_failed", "Verification failed. Please try again.");
                    break;
                default:
                    await WriteAsync(context, StatusCodes.Status503ServiceUnavailable, "unavailable", "Sign-up is unavailable right now.");
                    break;
            }
        }

        private static Task WriteAsync(HttpContext context, int status, string error, string message, string username = null)
        {
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/json; charset=utf-8";
            object body = error == null
                ? (username == null ? (object)new { ok = true } : new { ok = true, username })
                : new { ok = false, error, message };
            return context.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
        }

        private static bool IsLoopbackUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri uri)) return false;
            if (string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)) return true;
            return IPAddress.TryParse(uri.Host.Trim('[', ']'), out IPAddress address) && IPAddress.IsLoopback(address);
        }

        /// <summary>The bot check the settings ask for: Turnstile when a secret is configured, else none.</summary>
        public static IBotCheck BotCheckFor(SignupSettings settings) =>
            settings.TurnstileSecret != null ? (IBotCheck)new TurnstileBotCheck(settings.TurnstileSecret) : new NoBotCheck();
    }
}
