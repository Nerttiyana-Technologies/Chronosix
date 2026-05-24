using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Chronosix.Internal;

/// <summary>
/// Decides whether a caller may reach the Chronosix dashboard, API or hub. A custom
/// <see cref="ChronosixOptions.Authorize"/> delegate, when present, fully governs access;
/// otherwise access is gated on the hosting environment.
/// </summary>
public sealed class ChronosixAccessGuard
{
    private readonly ChronosixOptions _options;
    private readonly IHostEnvironment _environment;

    public ChronosixAccessGuard(IOptions<ChronosixOptions> options, IHostEnvironment environment)
    {
        _options = options.Value;
        _environment = environment;
    }

    /// <summary>Returns true when the supplied request is permitted to use Chronosix.</summary>
    public bool IsAllowed(HttpContext? context)
    {
        if (!_options.Enabled)
        {
            return false;
        }

        if (_options.Authorize is not null)
        {
            return context is not null && _options.Authorize(context);
        }

        if (_options.RestrictToDevelopment && !_environment.IsDevelopment())
        {
            return false;
        }

        return true;
    }
}
