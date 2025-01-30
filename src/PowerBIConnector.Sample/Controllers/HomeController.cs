using Microsoft.AspNetCore.Mvc;
using PowerBIConnector.Sample.Models;
using PowerBIConnector.Services;
using System.Diagnostics;

namespace PowerBIConnector.Sample.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly PowerBIService _powerBIService;

        public HomeController(ILogger<HomeController> logger, PowerBIService powerBIService)
        {
            _logger = logger;
            _powerBIService = powerBIService;
        }

        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// Controller action to render an embedded interactive Power BI report
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Interactive()
        {
            // Group ID for the workspace where the report is located
            var groupId = "<Group ID>";

            // Report ID for the report to embed
            var reportId = "<Report ID>";

            // Get the embedded report model
            var model = await _powerBIService.GetEmbeddedReportAsync(
                groupId: groupId,
                reportId: reportId);

            return View(model);
        }

        /// <summary>
        /// Controller action to render an embedded paginated Power BI report
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Paginated()
        {
            // Group ID for the workspace where the report is located
            var groupId = "<Group ID>";

            // Report ID for the report to embed
            var reportId = "<Report ID>";

            // Dataset IDs for the datasets used in the report
            var datasetIds = new List<string>() { "<Dataset ID" };            

            // Get the embedded paginated report model
            var model = await _powerBIService.GetEmbeddedPaginatedReportAsync(
                groupId: groupId,
                reportId: reportId,
                datasetIds: datasetIds);

            return View(model);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
