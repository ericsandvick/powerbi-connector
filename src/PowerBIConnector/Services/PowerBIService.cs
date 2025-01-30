
using Microsoft.Identity.Client;
using Microsoft.PowerBI.Api;
using Microsoft.PowerBI.Api.Models;
using Microsoft.Rest;
using PowerBIConnector.Models;
using PowerBIConnector.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PowerBIConnector.Services
{
    public class PowerBIService
    {
        // Authentication variables
        internal string _clientId;
        internal string _tenantId;
        internal string _clientSecret;

        // Power BI API paths
        internal string _resource;
        internal string _apiUrl;
        internal string _authority;

        private PowerBIClient _pbiClient;


        public PowerBIService(PowerBIServiceConfig config)
        {
            _clientId = config.ClientId;
            _tenantId = config.TenantId;
            _clientSecret = config.ClientSecret;
            _authority = $"{config.AuthorityBaseUrl}/{config.TenantId}";
            _resource = config.ResourceUrl;
            _apiUrl = config.ApiUrl;
        }

        /// <summary>
        /// Gets a Power BI client.
        /// </summary>
        /// <returns></returns>
        internal PowerBIClient GetPowerBIClient()
        {
            var tokenCredentials = new TokenCredentials(GetAccessToken(), "Bearer");

            return new PowerBIClient(new Uri(_apiUrl), tokenCredentials);
        }

        /// <summary>
        /// Gets an auth token.
        /// </summary>
        /// <returns></returns>
        private string GetAccessToken()
        {
            IConfidentialClientApplication app = null;

            // Create authorization app
            try
            {
                app = ConfidentialClientApplicationBuilder
                    .Create(_clientId)
                    .WithClientSecret(_clientSecret)
                    .WithAuthority(new Uri(_authority))
                    .Build();
            }
            catch (Exception ex)
            {
                throw ex;
            }

            string[] scopes = new string[] { $"{_resource}/.default" };
            AuthenticationResult result = null;

            // Authenticate and obtain persmissions scope
            try
            {
                result = app.AcquireTokenForClient(scopes).ExecuteAsync().Result;
            }
            catch (MsalUiRequiredException ex)
            {
                throw ex;
            }
            catch (MsalServiceException ex) when (ex.Message.Contains("AADSTS70011"))
            {
                throw ex;
            }

            return result.AccessToken;
        }

        /// <summary>
        /// Gets a list of all workspaces TESTING ONLY
        /// </summary>
        /// <returns></returns>
        public async Task<List<AdminGroup>> GetWorkspaces()
        {
            var groups = GetPowerBIClient().Groups.GetGroupsAsAdmin(100, filter: "type eq 'Workspace'").Value;

            return groups.ToList();
        }

        /// <summary>
        /// Gets a report from a group
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="reportId"></param>
        /// <returns></returns>
        public async Task<Report> GetReportAsync(string groupId, string reportId)
        {
            return await GetPowerBIClient().Reports.GetReportInGroupAsync(new Guid(groupId), new Guid(reportId));
        }

        /// <summary>
        /// Gets a list of reports from a group
        /// </summary>
        /// <returns></returns>
        public async Task<List<Report>> GetReportListAsync(string groupId)
        {
            // Get a client
            PowerBIClient pbiClient = GetPowerBIClient();

            // Call the Power BI Service API to get embedding data
            var reports = await pbiClient.Reports.GetReportsInGroupAsync(new Guid(groupId));

            return reports.Value.ToList();
        }

        /// <summary>
        /// Gets the embed url and related data for an interactive report
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="reportId"></param>
        /// <returns></returns>
        public async Task<PowerBIReportViewModel> GetEmbeddedReportAsync(string groupId, string reportId)
        {
            // Call the Power BI Service API to get embedding data
            var report = await GetPowerBIClient().Reports.GetReportInGroupAsync(new Guid(groupId), new Guid(reportId));

            // Get a report token
            var reportToken = await GetPowerBIClient().Reports.GenerateTokenAsync(
                new Guid(groupId),
                new Guid(reportId),
                new GenerateTokenRequest(accessLevel: "view"));

            // Return report embedding data to caller
            return new PowerBIReportViewModel
            {
                Id = report.Id.ToString(),
                EmbedUrl = report.EmbedUrl,
                Name = report.Name,
                Token = reportToken.Token
            };
        }

        /// <summary>
        /// Gets the embed url and related data for a paginated report
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="reportId"></param>
        /// <param name="datasetIds"></param>
        /// <returns></returns>
        public async Task<PowerBIReportViewModel> GetEmbeddedPaginatedReportAsync(string groupId, string reportId, List<string> datasetIds)
        {
            // Get the PBI client
            var client = GetPowerBIClient();

            // Get the requested report
            var report = await client.Reports.GetReportInGroupAsync(new Guid(groupId), new Guid(reportId));

            // Create token requests
            var workspaces = new List<GenerateTokenRequestV2TargetWorkspace>() { new GenerateTokenRequestV2TargetWorkspace(new Guid(groupId)) };
            var reports = new List<GenerateTokenRequestV2Report>() { new GenerateTokenRequestV2Report(new Guid(reportId)) };
            var datasets = datasetIds.Select(_ => new GenerateTokenRequestV2Dataset(_, XmlaPermissions.ReadOnly));

            // Generate the full token request
            var tokenRequest = new GenerateTokenRequestV2(datasets.ToList(), reports, workspaces, null);

            // Get the embed token
            var embedToken = client.EmbedToken.GenerateToken(tokenRequest);

            // Return the embed report model
            return new PowerBIReportViewModel
            {
                Id = report.Id.ToString(),
                EmbedUrl = report.EmbedUrl,
                Name = report.Name,
                Token = embedToken.Token
            };
        }


        /// <summary>
        /// Exports a report in the desired file format
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="reportId"></param>
        /// <param name="fileFormat"></param>
        /// <param name="token"></param>
        /// <param name="pollingtimeOutInMinutes"></param>
        /// <param name="parameters"></param>
        /// <param name="formatSettings"></param>
        /// <returns></returns>
        public async Task<ExportedFile> ExportReportAsync(
            string groupId,
            string reportId,
            FileFormat fileFormat,
            CancellationToken token,
            int pollingtimeOutInMinutes = 5,
            List<ParameterValue> parameters = null,
            Dictionary<string, string> formatSettings = null)
        {
            try
            {
                // Get the report 
                var report = await GetPowerBIClient().Reports.GetReportInGroupAsync(new Guid(groupId), new Guid(reportId));

                if (report == null)
                {
                    throw new Exception($"Report '{reportId}' in Group '{groupId}' not found.");
                }

                if (report.ReportType.Equals("PaginatedReport"))
                {
                    return await ExportPaginatedReportAsync(
                        new Guid(groupId),
                        new Guid(reportId),
                        fileFormat,
                        pollingtimeOutInMinutes,
                        token,
                        parameters: parameters,
                        formatSettings: formatSettings);
                }
                else if (report.ReportType.Equals("Interactive"))
                {
                    throw new Exception($"Unsupported report type of '{report.ReportType}' found.");
                }
                else
                {
                    throw new Exception($"Unsupported report type of '{report.ReportType}' found.");
                }
            }
            catch (Exception ex)
            {
                // Error handling
                throw;
            }
        }

        #region Paginated

        /// <summary>
        /// Exports a paginated report in the desired file format
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="reportId"></param>
        /// <param name="fileFormat"></param>
        /// <param name="pollingtimeOutInMinutes"></param>
        /// <param name="token"></param>
        /// <param name="parameters"></param>
        /// <param name="formatSettings"></param>
        /// <returns></returns>
        public async Task<ExportedFile> ExportPaginatedReportAsync(
            Guid groupId,
            Guid reportId,
            FileFormat fileFormat,
            int pollingtimeOutInMinutes,
            CancellationToken token,
            List<ParameterValue> parameters = null,
            Dictionary<string, string> formatSettings = null)
        {
            try
            {

                // Build the export config
                var exportConfig = new PaginatedReportExportConfiguration()
                {
                    FormatSettings = formatSettings ?? new Dictionary<string, string>(),
                    ParameterValues = parameters ?? new List<ParameterValue>()
                };

                // Get the export Id to retrieve the report file
                var exportId = await PostExportRequestPaginatedAsync(
                    groupId,
                    reportId,
                    fileFormat,
                    parameters,
                    exportConfig);

                var export = await PollExportRequestPaginatedAsync(groupId, reportId, exportId, pollingtimeOutInMinutes, token);
                if (export == null || export.Status != ExportState.Succeeded)
                {
                    // Error, failure in exporting the report
                    return null;
                }

                return await GetExportedFilePaginatedAsync(groupId, reportId, export);
            }
            catch (Exception ex)
            {
                // Error handling
                throw;
            }
        }

        /// <summary>
        /// Post an export request to the Power BI service for a paginated report
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="reportId"></param>
        /// <param name="fileFormat"></param>
        /// <param name="parameters"></param>
        /// <param name="exportConfig"></param>
        /// <returns></returns>
        private async Task<string> PostExportRequestPaginatedAsync(
            Guid groupId,
            Guid reportId,
            FileFormat fileFormat,
            List<ParameterValue> parameters,
            PaginatedReportExportConfiguration exportConfig)
        {
            // Build the export request
            var exportRequest = new ExportReportRequest
            {
                Format = fileFormat,
                PaginatedReportConfiguration = exportConfig,
            };

            var export = await GetPowerBIClient().Reports.ExportToFileInGroupAsync(groupId, reportId, exportRequest);

            // Save the export ID, you'll need it for polling and getting the exported file
            return export.Id;
        }

        /// <summary>
        /// Polls the Power BI service for the exported report until the timeout is exceeded
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="reportId"></param>
        /// <param name="exportId"></param>
        /// <param name="timeOutInMinutes"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        private async Task<Export> PollExportRequestPaginatedAsync(
            Guid groupId,
            Guid reportId,
            string exportId /* Get from the ExportToAsync response */,
            int timeOutInMinutes,
            CancellationToken token)
        {
            Export exportStatus = null;
            DateTime startTime = DateTime.UtcNow;
            const int secToMillisec = 1000;
            do
            {
                if (DateTime.UtcNow.Subtract(startTime).TotalMinutes > timeOutInMinutes || token.IsCancellationRequested)
                {
                    // Error handling for timeout and cancellations
                    return null;
                }

                var httpMessage =
                    await GetPowerBIClient().Reports.GetExportToFileStatusInGroupWithHttpMessagesAsync(groupId, reportId, exportId);

                exportStatus = httpMessage.Body;
                if (exportStatus.Status == ExportState.Running || exportStatus.Status == ExportState.NotStarted)
                {
                    // The recommended waiting time between polling requests can be found in the RetryAfter header
                    // Note that this header is only populated when the status is either Running or NotStarted
                    var retryAfter = httpMessage.Response.Headers.RetryAfter;
                    var retryAfterInSec = retryAfter.Delta.Value.Seconds;

                    await Task.Delay(retryAfterInSec * secToMillisec);
                }
            }
            // While not in a terminal state, keep polling
            while (exportStatus.Status != ExportState.Succeeded && exportStatus.Status != ExportState.Failed);

            return exportStatus;
        }

        /// <summary>
        /// Gets the file export http response for a paginated report
        /// </summary>
        /// <param name="groupId"></param>
        /// <param name="reportId"></param>
        /// <param name="export"></param>
        /// <returns></returns>
        private async Task<ExportedFile> GetExportedFilePaginatedAsync(
            Guid groupId,
            Guid reportId,
            Export export /* Get from the GetExportStatusAsync response */)
        {
            if (export.Status == ExportState.Succeeded)
            {
                var httpMessage =
                    await GetPowerBIClient().Reports.GetFileOfExportToFileInGroupWithHttpMessagesAsync(groupId, reportId, export.Id);

                return new ExportedFile
                {
                    FileStream = httpMessage.Body,
                    ReportName = export.ReportName,
                    FileExtension = export.ResourceFileExtension,
                };
            }

            return null;
        }

        #endregion

        //public async Task<PowerBIReportViewModel> GetReportFileAsync(string workspaceId, string reportId)
        //{
        //    PowerBIClient pbiClient = GetPowerBIClient();

        //    // Call the Power BI Service API to get embedding data
        //    var report = await pbiClient.Reports.GetReportInGroupAsync(new Guid(workspaceId), new Guid(reportId));

        //    // Get a report token
        //    var reportToken = await pbiClient.Reports.GenerateTokenAsync(
        //        new Guid(workspaceId),
        //        new Guid(reportId),
        //        new GenerateTokenRequest(accessLevel: "view"));

        //    var reportFile = await pbiClient.Reports.ExportToFileAsync(
        //        new Guid(workspaceId),
        //        new Guid(reportId),
        //        new ExportReportRequest
        //        {
        //            Format = FileFormat.PDF
        //        });

        //    reportFile.

        //    // Return report embedding data to caller
        //    //return new PowerBIReportViewModel
        //    //{
        //    //    Id = report.Id.ToString(),
        //    //    EmbedUrl = report.EmbedUrl,
        //    //    Name = report.Name,
        //    //    Token = reportToken.Token
        //    //};
        //}


        //    private async Task<string> PostExportRequest(
        //Guid reportId,
        //Guid groupId,
        //FileFormat format,
        //IList<string> pageNames = null, /* Get the page names from the GetPages REST API */
        //string urlFilter = null)
        //    {
        //        //var powerBIReportExportConfiguration = new PowerBIReportExportConfiguration
        //        //{
        //        //    Settings = new ExportReportSettings
        //        //    {
        //        //        Locale = "en-us",
        //        //    },
        //        //    // Note that page names differ from the page display names
        //        //    // To get the page names use the GetPages REST API
        //        //    Pages = pageNames?.Select(pn => new ExportReportPage(pn)).ToList(),
        //        //    // ReportLevelFilters collection needs to be instantiated explicitly
        //        //    ReportLevelFilters = !string.IsNullOrEmpty(urlFilter) ? new List<ExportFilter>() { new ExportFilter(urlFilter) } : null,

        //        //};

        //        //var exportRequest = new ExportReportRequest
        //        //{
        //        //    Format = format,
        //        //    PowerBIReportConfiguration = powerBIReportExportConfiguration,
        //        //};

        //        // For documentation purposes the export configuration is created in this method
        //        // Ordinarily, it would be created outside and passed in
        //        var paginatedReportExportConfiguration = new PaginatedReportExportConfiguration()
        //        {
        //            FormatSettings = new Dictionary<string, string>()
        //            {
        //                    {"PageHeight", "14in"},
        //                    {"PageWidth", "8.5in" },
        //                    {"StartPage", "1"},
        //                    {"EndPage", "4"},
        //                },
        //                        ParameterValues = new List<ParameterValue>()
        //                {
        //                    { new ParameterValue() {Name = "invoicenumber", Value = "109363a7-4dde-499e-b048-14f506fee2c3"} }
        //                },
        //            };

        //        var exportRequest = new ExportReportRequest
        //        {
        //            Format = FileFormat.PDF,
        //            PaginatedReportConfiguration = paginatedReportExportConfiguration,
        //        };


        //        // The 'Client' object is an instance of the Power BI .NET SDK
        //        var pbiClient = GetPowerBIClient();
        //        var export = await pbiClient.Reports.ExportToFileInGroupAsync(groupId, reportId, exportRequest);

        //        // Save the export ID, you'll need it for polling and getting the exported file
        //        return export.Id;
        //    }

        //    private async Task<HttpOperationResponse<Export>> PollExportRequest(
        //        Guid reportId,
        //        Guid groupId,
        //        string exportId /* Get from the PostExportRequest response */,
        //        int timeOutInMinutes,
        //        CancellationToken token)
        //    {
        //        HttpOperationResponse<Export> httpMessage = null;
        //        Export exportStatus = null;
        //        DateTime startTime = DateTime.UtcNow;
        //        const int c_secToMillisec = 1000;
        //        do
        //        {
        //            if (DateTime.UtcNow.Subtract(startTime).TotalMinutes > timeOutInMinutes || token.IsCancellationRequested)
        //            {
        //                // Error handling for timeout and cancellations 
        //                return null;
        //            }

        //            // The 'Client' object is an instance of the Power BI .NET SDK
        //            var pbiClient = GetPowerBIClient();
        //            httpMessage = await pbiClient.Reports.GetExportToFileStatusInGroupWithHttpMessagesAsync(groupId, reportId, exportId);
        //            exportStatus = httpMessage.Body;

        //            // You can track the export progress using the PercentComplete that's part of the response
        //            //SomeTextBox.Text = string.Format("{0} (Percent Complete : {1}%)", exportStatus.Status.ToString(), exportStatus.PercentComplete);
        //            if (exportStatus.Status == ExportState.Running || exportStatus.Status == ExportState.NotStarted)
        //            {
        //                // The recommended waiting time between polling requests can be found in the RetryAfter header
        //                // Note that this header is not always populated
        //                var retryAfter = httpMessage.Response.Headers.RetryAfter;
        //                var retryAfterInSec = retryAfter.Delta.Value.Seconds;
        //                await Task.Delay(retryAfterInSec * c_secToMillisec);
        //            }
        //        }
        //        // While not in a terminal state, keep polling
        //        while (exportStatus.Status != ExportState.Succeeded && exportStatus.Status != ExportState.Failed);

        //        return httpMessage;
        //    }

        //    private async Task<ExportedFile> GetExportedFile(
        //        Guid reportId,
        //        Guid groupId,
        //        Export export /* Get from the PollExportRequest response */)
        //    {
        //        if (export.Status == ExportState.Succeeded)
        //        {
        //            // The 'Client' object is an instance of the Power BI .NET SDK
        //            var pbiClient = GetPowerBIClient();
        //            //var fileStream = await pbiClient.Reports.GetFileOfExportToFileAsync(groupId, reportId, export.Id);

        //            var httpMessage =
        //                await pbiClient.Reports.GetFileOfExportToFileInGroupWithHttpMessagesAsync(groupId, reportId, export.Id);


        //            return new ExportedFile
        //            {
        //                FileStream = httpMessage.Body,
        //                ReportName = export.ReportName,
        //                FileSuffix = export.ResourceFileExtension,
        //            };
        //        }
        //        return null;
        //    }

        //    public class ExportedFile
        //    {
        //        public Stream FileStream;
        //        public string ReportName;
        //        public string FileSuffix;
        //    }

        //    public async Task<ExportedFile> ExportPowerBIReport(
        //        Guid reportId,
        //        Guid groupId,
        //        FileFormat format,
        //        int pollingtimeOutInMinutes,
        //        CancellationToken token,
        //        IList<string> pageNames = null,  /* Get the page names from the GetPages REST API */
        //        string urlFilter = null)
        //    {
        //        const int c_maxNumberOfRetries = 3; /* Can be set to any desired number */
        //        const int c_secToMillisec = 1000;
        //        try
        //        {
        //            Export export = null;
        //            int retryAttempt = 1;
        //            do
        //            {
        //                var exportId = await PostExportRequest(reportId, groupId, format, pageNames, urlFilter);
        //                var httpMessage = await PollExportRequest(reportId, groupId, exportId, pollingtimeOutInMinutes, token);
        //                export = httpMessage.Body;
        //                if (export == null)
        //                {
        //                    // Error, failure in exporting the report
        //                    return null;
        //                }
        //                if (export.Status == ExportState.Failed)
        //                {
        //                    // Some failure cases indicate that the system is currently busy. The entire export operation can be retried after a certain delay
        //                    // In such cases the recommended waiting time before retrying the entire export operation can be found in the RetryAfter header
        //                    var retryAfter = httpMessage.Response.Headers.RetryAfter;
        //                    if (retryAfter == null)
        //                    {
        //                        // Failed state with no RetryAfter header indicates that the export failed permanently
        //                        return null;
        //                    }

        //                    var retryAfterInSec = retryAfter.Delta.Value.Seconds;
        //                    await Task.Delay(retryAfterInSec * c_secToMillisec);
        //                }
        //            }
        //            while (export.Status != ExportState.Succeeded && retryAttempt++ < c_maxNumberOfRetries);

        //            if (export.Status != ExportState.Succeeded)
        //            {
        //                // Error, failure in exporting the report
        //                return null;
        //            }

        //            var exportedFile = await GetExportedFile(reportId, groupId, export);

        //            // Now you have the exported file stream ready to be used according to your specific needs
        //            // For example, saving the file can be done as follows:
        //            /*
        //                var pathOnDisk = @"C:\temp\" + export.ReportName + exportedFile.FileSuffix;

        //                using (var fileStream = File.Create(pathOnDisk))
        //                {
        //                    exportedFile.FileStream.CopyTo(fileStream);
        //                }
        //            */

        //            return exportedFile;
        //        }
        //        catch
        //        {
        //            // Error handling
        //            throw;
        //        }
        //    }


    }
}
