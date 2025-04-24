using Microsoft.ApplicationInsights;
using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EntraBridge.Pages;
using EntraBridge.Models;

namespace EntraBridge.Services;

public class GraphApplicationService
{
    private readonly GraphServiceClient _graphClient;
    private readonly TelemetryClient _telemetry;

    public GraphApplicationService(GraphServiceClient graphClient, TelemetryClient telemetry)
    {
        _graphClient = graphClient;
        _telemetry = telemetry;
    }

    public async Task<(List<ApplicationViewModel> Applications, bool HasMore, string NextToken)>
        GetApplicationsPageAsync(int pageSize, string skipToken = null, string appName = "")
    {
        try
        {
            var request = _graphClient.Applications.Request()
                .Select("id,appId,displayName,createdDateTime");
                
            // Only apply Top parameter if pageSize is greater than 0
            if (pageSize > 0)
            {
                request = request.Top(pageSize);
            }

            if (!string.IsNullOrEmpty(skipToken))
            {
                var options = new List<QueryOption>
                    {
                        new QueryOption("$skiptoken", skipToken)
                    };
                request = _graphClient.Applications.Request(options)
                    .Select("id,appId,displayName,createdDateTime");
                    
                // Only apply Top parameter if pageSize is greater than 0
                if (pageSize > 0)
                {
                    request = request.Top(pageSize);
                }
            }

            var result = await request.GetAsync();

            // Get next page token
            string nextToken = null;
            bool hasMore = result.NextPageRequest != null;

            if (hasMore)
            {
                var token = result.NextPageRequest.QueryOptions
                    .FirstOrDefault(o => o.Name == "$skiptoken");
                nextToken = token?.Value;
            }

            // Map to view models
            var applications = result.CurrentPage.Select(app => new ApplicationViewModel
            {
                DisplayName = app.DisplayName,
                ApplicationId = app.AppId,
                CreatedOn = app.CreatedDateTime?.DateTime ?? DateTime.MinValue,
                ApplicationType = GetApplicationTypeInitials(app)
            })
            .Where(app => string.IsNullOrEmpty(appName) || app.DisplayName.Contains(appName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.DisplayName)
            .ToList();

            return (applications, hasMore, nextToken);
        }
        catch (Exception ex)
        {
            _telemetry.TrackException(ex);
            throw;
        }
    }

    private string GetApplicationTypeInitials(Application app)
    {
        if (string.IsNullOrEmpty(app.DisplayName))
            return "A";

        // Extract initials from word boundaries
        var initials = new List<char>();
        bool newWord = true;

        foreach (char c in app.DisplayName)
        {
            // Check for word boundary
            if (!char.IsLetterOrDigit(c))
            {
                newWord = true;
                continue;
            }

            // Get first letter of each word
            if (newWord && char.IsLetter(c))
            {
                initials.Add(char.ToUpper(c));
                newWord = false;

                if (initials.Count == 2)
                    break;
            }
        }

        // If we got at least one initial
        if (initials.Count > 0)
        {
            // Duplicate if only one initial
            if (initials.Count == 1)
                initials.Add(initials[0]);

            return new string(initials.Take(2).ToArray());
        }

        // Fallback: get first 2 letters
        var letters = app.DisplayName.Where(char.IsLetter).Take(2).Select(char.ToUpper).ToArray();
        return letters.Length > 0 ? new string(letters) : "A";
    }
}
