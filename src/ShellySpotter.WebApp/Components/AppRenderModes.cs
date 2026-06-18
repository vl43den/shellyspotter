using Microsoft.AspNetCore.Components.Web;

namespace ShellySpotter.WebApp.Components;

public static class AppRenderModes
{
    // Interactive Server WITHOUT prerendering. With prerender on, components render
    // server-side before the circuit connects, where ProtectedSessionStorage (JS
    // interop) is unavailable — so the auth session can't be restored and an
    // authenticated user is redirected to /login on every page reload. Disabling
    // prerender lets OnInitializedAsync restore the session before the auth check.
    public static readonly InteractiveServerRenderMode InteractiveServerNoPrerender = new(prerender: false);
}
