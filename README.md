# Power BI Connector
Power BI Connector is a .Net Standard 2.0 libarary that allows provides the following services:
- Embed interactive and paginated reports in your website.
- Export paginated reports to a file.
- Get lists of workspaces and reports.

<br/>

## :star: Good to Know

<ul>
  <li>
    This connector is built with Asp.net websites in mind but can easily be adapted to other frameworks
  </li>
  <li>
    Authentication is based on using a service principal.  However, you can easily modify to support other methods.
  </li>
</ul>

<br/>

## :rocket: Getting Started

- Add a reference to PowerBIConnector.dll to your project.
- Add embed.js to your website if embedding reports in the UI.

<br/>

## :hammer: Usage

These snippets assume usage in an Asp.net MVC web app.

### Initializing the Power BI Service

Power BI Service supports authentication using a service principal and client secret.  However, you can easily modify to use other authentication methods.

```c#
PowerBIService pbiService = new PowerBIService(
  new PowerBIServiceConfig
  {
    ApiUrl = "https://api.powerbi.com",                        // Typically static but refer to Microsoft's documentation
    AuthorityBaseUrl = "https://login.microsoftonline.com",    // Typically static but refer to Microsoft's documentation
    ClientId = "B9C5C22B-23E2-4142-8EEF-C3E07250C8FD",         // Azure Portal > Microsoft Entra ID > App Registrations > Application (client) ID
    ClientSecret = "ZTI8L~s.ax64tvWNEghGMAPnRYw3IaIe9bmj2zJk", // Azure Portal > Microsoft Entra ID > App Registrations > Certificates & secrets
    ResourceUrl = "https://analysis.windows.net/powerbi/api",  // Typically static but refer to Microsoft's documentation
    TenantId = "08CBF6D0-23BC-4016-A0F9-8C51E35AE24C"          // Azure Portal > Microsoft Entra ID > Overview > Tenant ID
  });
```

### Embedding an Interactive Report

```c#
// Get the embedded report model.  This model has of the information need to render the report in the browser.
PowerBIReportViewModel model = await pbiService.GetEmbeddedReportAsync(
  groupId: "2E99EA32-CBB3-4DC4-ABD0-362C86D70F3F",  // ID of the workspace containing the report.  It's the guid after "groups/" in the report url.
  reportId: "D2BF030D-6860-4F1B-9129-8B137CBE94B4"  // ID of the report.  It's the guid after "reports/" in the report url.
);

// Return the model for renedering
return View(model);
```

### Embedding a Paginated Report

```c#
// Get the embedded paginated report model.  This model has of the information need to render the report in the browser.
PowerBIReportViewModel model = await pbiService.GetEmbeddedPaginatedReportAsync(
  groupId: "2E99EA32-CBB3-4DC4-ABD0-362C86D70F3F",      // ID of the workspace containing the report.  It's the guid after "groups/" in the report url.
  reportId: "2760A075-349C-4A32-989E-9B3847855F16",     // ID of the report.  It's the guid after "rdlreports/" in the report url.
  datasetIds: [                                         // ID's of all datasets (semantic models) that the report connects to.  It's the guid after "datasets/" in the dataset url.
                "7935596E-1451-402E-8EDB-7643D379E02D"
                ,"8BC50752-B75B-497C-892C-13C5CB325526"
              ]
);

// Return the model for renedering
return View(model);
```

### Exporting a Paginated Report

This implemention assumes returning the exported file to the browser but you can just as easily store the file on the server or in a storage service.

```c#
// Initialize a cancellation token
var cancellToken = new CancellationTokenSource();

// Get the file
ExportedFile fileExport = await pbiService.ExportPaginatedReportAsync(
  groupId: new Guid("2E99EA32-CBB3-4DC4-ABD0-362C86D70F3F"),    // ID of the workspace containing the report.  It's the guid after "groups/" in the report url.
  reportId: new Guid("2760A075-349C-4A32-989E-9B3847855F16"),   // ID of the report.  It's the guid after "rdlreports/" in the report url.
  fileFormat: FileFormat.XLSX,                                  // File format for the exported file.  This can be any format that the report supports.
  pollingtimeOutInMinutes: 5,                                   // Basically, the max time the export can run.
  cancellToken.Token,                                           // A cancellation token that can be used to stop the export.

  parameters: new List<ParameterValue>()                        // These are the parameters that are passed to the report.  You'll need the actual parameter names from the report definition as opposed to the prompts.
  {
    {
      new ParameterValue() { Name = "FromDate", Value = "2/1/2025" } 
    },
    {
      new ParameterValue() { Name = "To", Value = "2/10/2025" }
    },
    {
      new ParameterValue() Name = "Country", Value = "USA"}
    }
});

// Add a content header to the response to inform the browser
var contentDispositionHeader = new System.Net.Mime.ContentDisposition
{
  Inline = true,
  FileName = "Export"
};

Response.Headers.Append("Content-Disposition", contentDispositionHeader.ToString());

// Return the to the browser for render/download
return File(fileExport.FileStream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
```

### Getting a list of workspaces

```c#
// Get all workspaces that the user has access to
List<AdminGroup> workspaces = pbiService.GetWorkspaces();
```

### Getting a list of reports in a workspace

```c#
// Get all reports in the workspace
var reports = await pbiService.GetReportListAsync(
  groupId: "2E99EA32-CBB3-4DC4-ABD0-362C86D70F3F",      // ID of the workspace containing the reports.  It's the guid after "groups/" in the report url.
);
```
