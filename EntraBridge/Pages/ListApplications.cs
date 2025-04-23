using EntraBridge.Helpers;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace EntraBridge.Pages;

[Authorize]
[AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
public class ListApplicationsModel : PageModel
{
    private readonly GraphServiceClient _graphServiceClient;
    private readonly IConfiguration _configuration;
    private TelemetryClient _telemetry;

    // List to store all applications
    private List<ApplicationViewModel> _allApplications = new List<ApplicationViewModel>();

    // Paginated applications to display
    public List<ApplicationViewModel> Applications { get; set; } = new List<ApplicationViewModel>();

    // Pagination properties
    public int PageSize { get; private set; } = 30;
    public int CurrentPage { get; private set; } = 1;
    public bool HasMoreRecords { get; private set; } = false;
    public string NextPageToken { get; private set; } = string.Empty;

    public ListApplicationsModel(IConfiguration configuration, TelemetryClient telemetry, GraphServiceClient graphServiceClient)
    {
        _configuration = configuration;
        _telemetry = telemetry;
        _graphServiceClient = graphServiceClient;
    }

    public async Task OnGetAsync(int page = 1, string nextPage = null)
    {
        try
        {
            // Initialize the Applications collection
            Applications = new List<ApplicationViewModel>();
            _telemetry.TrackPageView("ListApplications");
            CurrentPage = page;
            NextPageToken = nextPage;
            await LoadApplicationsAsync(false);
        }
        catch (Exception ex)
        {
            AppInsights.TrackException(_telemetry, ex, "ListApplications:OnGetAsync");
            // Set an error message that can be displayed in the UI
            TempData["ErrorMessage"] = "Failed to load applications. Please try again later.";
        }
    }

    // Handle AJAX request for loading more items
    public async Task<IActionResult> OnGetLoadMoreAsync(int page, string nextPage = null)
    {
        try
        {
            // Create a new list to store just the new applications
            var newApplications = new List<ApplicationViewModel>();

            CurrentPage = page;
            NextPageToken = nextPage;

            // Get the new page of applications
            var result = await GetNextPageOfApplicationsAsync();
            newApplications = result.Item1;
            HasMoreRecords = result.Item2;
            NextPageToken = result.Item3;

            // Add the new applications to the master list
            _allApplications.AddRange(newApplications);

            return new JsonResult(new
            {
                applications = newApplications,
                hasMoreRecords = HasMoreRecords,
                nextPageToken = NextPageToken,
                nextPage = CurrentPage + 1
            });
        }
        catch (Exception ex)
        {
            AppInsights.TrackException(_telemetry, ex, "ListApplications:OnGetLoadMoreAsync");
            return new JsonResult(new { error = "Failed to load more applications" }) { StatusCode = 500 };
        }
    }

    private async Task LoadApplicationsAsync(bool isLoadMore = false)
    {
        if (!isLoadMore)
        {
            // Clear the master list if this is the initial load
            _allApplications.Clear();
            Applications.Clear();
        }

        var result = await GetNextPageOfApplicationsAsync();
        var pageApplications = result.Item1;
        HasMoreRecords = result.Item2;
        NextPageToken = result.Item3;

        // Add to both collections
        Applications.AddRange(pageApplications);
        _allApplications.AddRange(pageApplications);

        // Pass the data to the view
        ViewData["Applications"] = Applications;
        ViewData["ApplicationCount"] = Applications.Count;
        ViewData["HasMoreRecords"] = HasMoreRecords;
        ViewData["NextPageToken"] = NextPageToken;
        ViewData["NextPage"] = CurrentPage + 1;
    }

    // Extract the common application loading logic to a separate method
    private async Task<Tuple<List<ApplicationViewModel>, bool, string>> GetNextPageOfApplicationsAsync()
    {
        var applicationsRequest = _graphServiceClient.Applications.Request()
            .Select("id,appId,displayName,createdDateTime,passwordCredentials")
            .Top(PageSize);

        // If there's a next page token, use it
        if (!string.IsNullOrEmpty(NextPageToken))
        {
            try
            {
                var options = new List<QueryOption>
                    {
                        new QueryOption("$skiptoken", NextPageToken)
                    };
                applicationsRequest = _graphServiceClient.Applications.Request(options)
                    .Select("id,appId,displayName,createdDateTime,passwordCredentials")
                    .Top(PageSize);
            }
            catch (Exception ex)
            {
                _telemetry.TrackException(ex, new Dictionary<string, string> {
                        { "Context", "Skip token application" },
                        { "Token", NextPageToken?.Substring(0, Math.Min(50, NextPageToken?.Length ?? 0)) ?? "null" }
                    });
                // If skip token fails, start fresh
                applicationsRequest = _graphServiceClient.Applications.Request()
                    .Select("id,appId,displayName,createdDateTime,passwordCredentials")
                    .Top(PageSize);
            }
        }

        // Get applications from Microsoft Graph API
        var applicationsResult = await applicationsRequest.GetAsync();

        // Store next page token if exists
        bool hasMoreRecords = applicationsResult.NextPageRequest != null;
        string nextPageToken = null;

        if (hasMoreRecords)
        {
            try
            {
                var skipToken = applicationsResult.NextPageRequest.QueryOptions
                    .FirstOrDefault(o => o.Name == "$skiptoken");
                nextPageToken = skipToken?.Value;
            }
            catch (Exception ex)
            {
                _telemetry.TrackException(ex, new Dictionary<string, string> {
                        { "Error", "Failed to extract skip token" }
                    });
                hasMoreRecords = false;
            }
        }

        // Transform the applications into our view model
        var pageApplications = new List<ApplicationViewModel>();
        foreach (var app in applicationsResult.CurrentPage)
        {
            var appType = DetermineAppType(app);

            pageApplications.Add(new ApplicationViewModel
            {
                DisplayName = app.DisplayName,
                ApplicationId = app.AppId,
                CreatedOn = app.CreatedDateTime?.DateTime ?? DateTime.MinValue,
                HasSecrets = app.PasswordCredentials?.Count() > 0,
                ApplicationType = appType
            });
        }

        // Sort by display name
        pageApplications = pageApplications.OrderBy(a => a.DisplayName).ToList();

        return new Tuple<List<ApplicationViewModel>, bool, string>(pageApplications, hasMoreRecords, nextPageToken);
    }

    private string DetermineAppType(Microsoft.Graph.Application app)
    {
        if (string.IsNullOrEmpty(app.DisplayName))
            return "A";

        // Extract only letters from the display name
        var letters = new List<char>();
        foreach (char c in app.DisplayName)
        {
            if (char.IsLetter(c))
                letters.Add(char.ToUpper(c));
        }

        if (letters.Count == 0)
            return "A";

        // Try to get initials from word boundaries (after spaces, dashes, etc.)
        var initials = new List<char>();
        bool newWord = true;

        for (int i = 0; i < app.DisplayName.Length; i++)
        {
            char c = app.DisplayName[i];

            // Check for word boundaries
            if (c == ' ' || c == '-' || c == '_' || c == '.')
            {
                newWord = true;
                continue;
            }

            // If this is the first letter of a new word, add it to initials
            if (newWord && char.IsLetter(c))
            {
                initials.Add(char.ToUpper(c));
                newWord = false;

                // Stop after collecting 2 initials
                if (initials.Count == 2)
                    break;
            }
        }

        // If we got at least one initial, return them (up to 2)
        if (initials.Count > 0)
        {
            // If only one initial, duplicate it
            if (initials.Count == 1)
                initials.Add(initials[0]);

            return new string(initials.Take(2).ToArray());
        }

        // Fallback: take first 2 letters from the name
        return new string(letters.Take(2).ToArray());
    }
}

public class ApplicationViewModel
{
    public string DisplayName { get; set; } = string.Empty;
    public string ApplicationId { get; set; } = string.Empty;
    public DateTime CreatedOn { get; set; }
    public bool HasSecrets { get; set; }
    public string ApplicationType { get; set; } = "A"; // Default application type
}
