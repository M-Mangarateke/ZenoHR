// REQ-OPS-008: Dark/light mode toggle — ThemeService persists user preference via localStorage JS interop.
// REQ-SEC-002: Theme preference is per-user, stored client-side only (no tenant data implications).

using Microsoft.JSInterop;

namespace ZenoHR.Web.Services;

/// <summary>
/// Manages dark/light theme preference via JavaScript interop with localStorage.
/// Registered as Scoped so each Blazor circuit gets its own instance.
/// </summary>
public sealed class ThemeService
{
    private readonly IJSRuntime _js;
    private string _currentTheme = "light";
    private bool _initialized;

    public ThemeService(IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(js);
        _js = js;
    }

    /// <summary>
    /// Gets the current theme ("light" or "dark").
    /// </summary>
    public string CurrentTheme => _currentTheme;

    /// <summary>
    /// Whether the current theme is dark mode.
    /// </summary>
    public bool IsDarkMode => string.Equals(_currentTheme, "dark", StringComparison.Ordinal);

    /// <summary>
    /// Raised when the theme changes. Components should subscribe to re-render.
    /// </summary>
    public event Action? OnThemeChanged;

    /// <summary>
    /// Initializes the theme from localStorage and applies it to the document.
    /// Call once during app initialization (e.g., MainLayout OnAfterRenderAsync).
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized) return;

        try
        {
            var theme = await _js.InvokeAsync<string>("zenohr.initTheme");
            _currentTheme = IsValidTheme(theme) ? theme : "light";
            _initialized = true;
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected — safe to ignore during prerender.
        }
        catch (InvalidOperationException)
        {
            // JS interop not available during prerender — use default.
        }
    }

    /// <summary>
    /// Gets the persisted theme from localStorage.
    /// </summary>
    /// <returns>"light" or "dark"</returns>
    public async Task<string> GetThemeAsync()
    {
        try
        {
            var theme = await _js.InvokeAsync<string>("zenohr.getTheme");
            _currentTheme = IsValidTheme(theme) ? theme : "light";
            return _currentTheme;
        }
        catch (JSDisconnectedException)
        {
            return _currentTheme;
        }
        catch (InvalidOperationException)
        {
            return _currentTheme;
        }
    }

    /// <summary>
    /// Sets the theme and persists to localStorage.
    /// </summary>
    /// <param name="theme">"light" or "dark"</param>
    public async Task SetThemeAsync(string theme)
    {
        ArgumentNullException.ThrowIfNull(theme);

        if (!IsValidTheme(theme))
            throw new ArgumentException("Theme must be 'light' or 'dark'.", nameof(theme));

        _currentTheme = theme;

        try
        {
            await _js.InvokeVoidAsync("zenohr.setTheme", theme);
        }
        catch (JSDisconnectedException)
        {
            // Circuit disconnected — preference was already set in memory.
        }

        OnThemeChanged?.Invoke();
    }

    /// <summary>
    /// Toggles between light and dark mode.
    /// </summary>
    /// <returns>The new theme after toggling.</returns>
    public async Task<string> ToggleThemeAsync()
    {
        var newTheme = IsDarkMode ? "light" : "dark";
        await SetThemeAsync(newTheme);
        return newTheme;
    }

    private static bool IsValidTheme(string? theme) =>
        string.Equals(theme, "light", StringComparison.Ordinal) ||
        string.Equals(theme, "dark", StringComparison.Ordinal);
}
