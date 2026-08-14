using Habit_Tracker.Application.Commands;
using Habit_Tracker.Application.Queries;
using Habit_Tracker.Client.Pages;
using Habit_Tracker.Components;
using Habit_Tracker.Components.Account;
using Habit_Tracker.Habits;
using Habit_Tracker.Persistence;
using Habit_Tracker.Persistence.Commands;
using Habit_Tracker.Persistence.Identity;
using Habit_Tracker.Persistence.Queries;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddNpgsqlDataSource(connectionString);
builder.Services.AddScoped<IHabitQueries, HabitQueries>();
builder.Services.AddScoped<IHabitCommands, HabitCommands>();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, PersistingRevalidatingAuthenticationStateProvider>();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
        options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
    })
    .AddIdentityCookies();

builder.Services.AddAuthorization();

builder.Services.AddIdentityCore<ApplicationUser>(options => options.SignIn.RequireConfirmedAccount = false)
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

var app = builder.Build();

// Azure App Service (and most PaaS hosts) terminate TLS at their own front-end and forward
// plain HTTP to the app with X-Forwarded-* headers - without this, UseHttpsRedirection and
// Identity's Secure cookies can't tell the original request was HTTPS. The front-end's IP
// range isn't practical to pin down, and App Service already restricts inbound traffic to
// only arrive via its own proxy, so clearing Known*/trusting the immediate proxy is safe here.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
// The default options only trust loopback proxies; Azure's front-end isn't loopback, so the
// known-networks/proxies allowlists must be cleared for the headers to actually be honored.
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Habit_Tracker.Client._Imports).Assembly);

app.MapAdditionalIdentityEndpoints();
app.MapHabitEndpoints();

app.Run();
