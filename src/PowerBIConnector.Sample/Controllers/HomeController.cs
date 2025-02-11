using Microsoft.AspNetCore.Mvc;
using Microsoft.PowerBI.Api.Models;
using PowerBIConnector.Services;

namespace PowerBIConnector.Sample.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly PowerBIService _powerBIService;

        // These are the IDs for the reports and workspaces used in this sample.  You'll need to replace these with your own.
        private readonly string _interactiveGroupId = "";
        private readonly string _interactiveReportId = "";
        private readonly string _paginatedGroupId = "";
        private readonly string _paginatedReportId = "";
        private readonly List<string> _paginatedDatasetIds = [ "" ];

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
            var groupId = _interactiveGroupId;

            // Report ID for the report to embed
            var reportId = _interactiveReportId;

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
            var groupId = _paginatedGroupId;

            // Report ID for the report to embed
            var reportId = _paginatedReportId;

            // Dataset IDs for the datasets used in the report
            var datasetIds = _paginatedDatasetIds;            

            // Get the embedded paginated report model
            var model = await _powerBIService.GetEmbeddedPaginatedReportAsync(
                groupId: groupId,
                reportId: reportId,
                datasetIds: _paginatedDatasetIds);

            return View(model);
        }

        /// <summary>
        /// Controller action to render a list of workspaces that the user has access to
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> Workspaces()
        {
            // Get all workspaces that the user has access to
            var workspaces = _powerBIService.GetWorkspaces();

            return View(workspaces);
        }

        /// <summary>
        /// Controller action to render a list of reports in a workspace
        /// </summary>
        /// <param name="groupId"></param>
        /// <returns></returns>
        public async Task<IActionResult> Reports()
        {
            // Group ID for the workspace where the report is located
            var groupId = _paginatedGroupId;

            // Get all reports in the workspace
            var reports = await _powerBIService.GetReportListAsync(groupId);

            return View(reports);
        }

        public async Task<IActionResult> Export()
        {
            // Group ID for the workspace where the report is located
            var groupId = _paginatedGroupId;

            // Report ID for the report to embed
            var reportId = _paginatedReportId;

            // Dataset IDs for the datasets used in the report
            var datasetIds = _paginatedDatasetIds;

            // Initialize a cancellation token
            var cancellToken = new CancellationTokenSource();

            var fileExport = await _powerBIService.ExportPaginatedReportAsync(
                groupId: new Guid(groupId),
                reportId: new Guid(reportId),
                fileFormat: FileFormat.XLSX,
                pollingtimeOutInMinutes: 5,
                cancellToken.Token,

                // These are the parameters that are passed to the report.  You'll need the actual parameter names from the report definition as opposed to the prompts.
                parameters: new List<ParameterValue>()
                {
                    { new ParameterValue() 
                        { Name = "FromDate", Value = "2/1/2025"} 
                    },
                    { new ParameterValue()
                        { Name = "ToDate", Value = "2/10/2025"}
                    },
                    { new ParameterValue()
                        { Name = "Country", Value = "USA"}
                    },
                });

            var contentDispositionHeader = new System.Net.Mime.ContentDisposition
            {
                Inline = true,
                FileName = "Export"
            };

            Response.Headers.Append("Content-Disposition", contentDispositionHeader.ToString());

            return File(fileExport.FileStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }
    }
}
