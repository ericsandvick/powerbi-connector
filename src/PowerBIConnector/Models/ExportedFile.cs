using System.IO;

namespace PowerBIConnector.Models
{
    public class ExportedFile
    {
        public Stream FileStream { get; set; }
        public string ReportName { get; set; }
        public string FileExtension { get; set; }
    }
}
