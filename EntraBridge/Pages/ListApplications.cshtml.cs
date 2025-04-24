using EntraBridge.Helpers;
using EntraBridge.Models;
using EntraBridge.Services;
using Microsoft.ApplicationInsights;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace EntraBridge.Pages
{
    [Authorize]
    [AuthorizeForScopes(ScopeKeySection = "MicrosoftGraph:Scopes")]
    public class ListApplicationsModel : PageModel
    {
        private readonly GraphApplicationService _appService;
        private readonly TelemetryClient _telemetry;
        
        // Applications to display
        public List<ApplicationViewModel> Applications { get; private set; } = new();
        
        // Pagination properties
        public int PageSize { get; private set; } = 30;
        public int CurrentPage { get; private set; } = 1;
        public bool HasMoreRecords { get; private set; } = false;
        public string NextPageToken { get; private set; } = string.Empty;

        public ListApplicationsModel(GraphApplicationService appService,
            TelemetryClient telemetry)
        {
            _appService = appService;
            _telemetry = telemetry;
        }

        public async Task OnGetAsync(int currentPage = 1, string nextToken = null)
        {
            try
            {
                _telemetry.TrackPageView("ListApplications");
                CurrentPage = currentPage;
                NextPageToken = nextToken;
                
                var result = await _appService.GetApplicationsPageAsync(PageSize, NextPageToken);
                
                Applications = result.Applications;
                HasMoreRecords = result.HasMore;
                NextPageToken = result.NextToken;
                
                // Set view data for UI
                ViewData["HasMoreRecords"] = HasMoreRecords;
                ViewData["NextPageToken"] = NextPageToken;
                ViewData["NextPage"] = CurrentPage + 1;
                ViewData["ApplicationCount"] = Applications.Count;
            }
            catch (Exception ex)
            {
                AppInsights.TrackException(_telemetry, ex, "ListApplications:OnGetAsync");
                TempData["ErrorMessage"] = "Failed to load applications. Please try again later.";
            }
        }
        
        public async Task<IActionResult> OnGetSearchApplicationsAsync(string searchTerm)
        {
            try
            {
                var result = await _appService.GetApplicationsPageAsync(0, null, searchTerm);
                
                return new JsonResult(new
                {
                    applications = result.Applications,
                    hasMoreRecords = result.HasMore,
                    nextPageToken = result.NextToken,
                    nextPage = CurrentPage + 1
                });
            }
            catch (Exception ex)
            {
                AppInsights.TrackException(_telemetry, ex, "ListApplications:OnGetSearchApplicationsAsync");
                return new JsonResult(new { error = "Failed to search application" }) { StatusCode = 500 };
            }
        }

        public async Task<IActionResult> OnGetLoadMoreAsync(int currentPage, string nextToken = null)
        {
            try
            {
                CurrentPage = currentPage;
                var result = await _appService.GetApplicationsPageAsync(PageSize, nextToken);
                
                return new JsonResult(new
                {
                    applications = result.Applications,
                    hasMoreRecords = result.HasMore,
                    nextPageToken = result.NextToken,
                    nextPage = CurrentPage + 1
                });
            }
            catch (Exception ex)
            {
                AppInsights.TrackException(_telemetry, ex, "ListApplications:OnGetLoadMoreAsync");
                return new JsonResult(new { error = "Failed to load more applications" }) { StatusCode = 500 };
            }
        }
    }
}
