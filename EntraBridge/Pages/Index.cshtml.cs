using EntraBridge.Helpers;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;
using Microsoft.Identity.Web;

namespace EntraBridge.Pages;

[Authorize]
[AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
public class IndexModel : PageModel
{
    private readonly GraphServiceClient _graphServiceClient;
    private readonly IConfiguration _configuration;
    private TelemetryClient _telemetry;

    public IndexModel(IConfiguration configuration, TelemetryClient telemetry, GraphServiceClient graphServiceClient)
    {
        _configuration = configuration;
        _telemetry = telemetry;
        _graphServiceClient = graphServiceClient;
    }

    public async Task OnGet()
    {
        try
        {
            // Get the user unique identifier
            string? userObjectId = User.GetObjectId();

            _telemetry.TrackPageView("Profile:Disable");
            var graphClient = MsalAccessTokenHandler.GetGraphClient(_configuration);
            var result = graphClient.Users[userObjectId];
            var user = await _graphServiceClient.Me.Request().GetAsync();

            ViewData["GraphApiResult"] = user.DisplayName; ;
        }
        catch (Exception ex)
        {
            AppInsights.TrackException(_telemetry, ex, "OnPostProfileAsync");
        }
    }
}
