using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using PdfTextReader.Azure;

namespace ParserFunctions
{
    public static class DurableFunctions
    {
        const string INPUT_PATH = "wasb://input/pdf/";
        const string OUTPUT_PATH = "wasb://output/output/";

        static AzureFS g_fileSystem;

        static DurableFunctions()
        {
            string inputStorage = Environment.GetEnvironmentVariable("PDFTEXTREADER_PDF");
            string outputStorage = Environment.GetEnvironmentVariable("PDFTEXTREADER_OUTPUT");

            g_fileSystem = new AzureFS(inputStorage, outputStorage);
        }

        [FunctionName("DurableFunctions")]
        public static async Task<Model.PdfStats> RunOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext context)
        {
            string year = context.GetInput<string>();

            var files = await context.CallActivityAsync<Model.Pdf[]>("DurableFunctions_ListFiles", year);

            var stats = await context.CallActivityAsync<Model.PdfStats>("DurableFunctions_CreateStats", year);

            var tasks = files.Select(async pdf =>
            {
                var file = stats.Create(pdf);

                file = await context.CallActivityAsync<Model.PdfStats.File>("DurableFunctions_ProcessPdf", file);

                return file;
            }).ToArray();

            var results = await Task.WhenAll(tasks);

            stats.Done(results);

            return stats;
        }

        [FunctionName("DurableFunctions_ListFiles")]
        public static Model.Pdf[] ListFiles([ActivityTrigger]string year)
        {
            if (year == null)
                throw new ArgumentNullException(nameof(year));

            string folderName = year.Replace('|', '/');

            var files = EnumerateFiles(folderName).ToArray();

            return files;
        }

        [FunctionName("DurableFunctions_CreateStats")]
        public static Model.PdfStats CreateStats([ActivityTrigger]string year)
        {
            // PdfStats constructor is NOT deterministic
            // Thus, it needs to be called from a function
            return new Model.PdfStats(year);
        }

        [FunctionName("DurableFunctions_ProcessPdf")]
        public static Model.PdfStats.File ProcessPdf([ActivityTrigger]Model.PdfStats.File pdf, 
            [Queue("done")] out string result,
            TraceWriter log)
        {
            string document = pdf.Name;
            string inputfolder = $"{INPUT_PATH}{pdf.Path}";
            string outputfolder = $"{OUTPUT_PATH}artigos/{pdf.Path}";
            string error = null;

            try
            {
                pdf.Start();

                log.Info($"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: Processing file: {document}, inputfolder={inputfolder}, outputfolder={outputfolder}");

                //PdfTextReader.ExamplesAzure.RunParserPDF(g_fileSystem, document, inputfolder, outputfolder);
                PdfTextReader.ExamplesAzure.RunCreateArtigos(g_fileSystem, document, inputfolder, "nul://", outputfolder);
            }
            catch(Exception ex)
            {
                error = ex.ToString();
            }

            pdf.Done(error);

            result = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}: Processing file: {document}, time={pdf.ElapsedTime}, hostname={pdf.Hostname}";

            return pdf;
        }

        [FunctionName("DurableFunctions_HttpStart")]
        public static async Task<HttpResponseMessage> HttpStart(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            TraceWriter log)
        {
            // Function input comes from the request content.
            string instanceId = await starter.StartNewAsync("DurableFunctions", null);

            log.Info($"Started orchestration with ID = '{instanceId}'.");

            return starter.CreateCheckStatusResponse(req, instanceId);
        }

        [FunctionName("HttpStartProcess")]
        public static async Task<HttpResponseMessage> HttpStartProcess(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")]HttpRequestMessage req,
            [OrchestrationClient]DurableOrchestrationClient starter,
            TraceWriter log)
        {
            string qs = req.RequestUri.Query;

            if (qs == null )
                throw new ArgumentNullException("Null query string");

            string year = qs.TrimStart('?');

            if (year == "" )
                throw new ArgumentNullException("Empty query string");
            
            string instanceId = await starter.StartNewAsync("DurableFunctions", year);

            log.Info($"Started orchestration with ID = '{instanceId}'.");
            
            var response = starter.CreateCheckStatusResponse(req, instanceId);

            var content = await response.Content.ReadAsStringAsync();

            return response;
        }

        static IEnumerable<Model.Pdf> EnumerateFiles(string folderName)
        {
            var folder = g_fileSystem.GetFolder(INPUT_PATH).GetFolder(folderName);

            foreach (var file in GetFilesRecursive(folder))
            {
                if (file.Name.ToLower().EndsWith(".pdf"))
                {
                    string basepath = file.Path.Substring(INPUT_PATH.Length);
                    string folderPath = basepath.Substring(0, basepath.Length - file.Name.Length).Trim('/');
                    string filenameWithoutExt = file.Name.Substring(0, file.Name.Length - ".pdf".Length);

                    yield return new Model.Pdf { Name = filenameWithoutExt, Path = folderPath };
                }
            }
        }
        static IEnumerable<IAzureBlobFile> GetFilesRecursive(IAzureBlobFolder folder)
        {
            var items = folder.EnumItems();

            foreach (var it in items)
            {
                if (it is IAzureBlobFile)
                    yield return (IAzureBlobFile)it;

                if (it is IAzureBlobFolder)
                {
                    var childFolder = (IAzureBlobFolder)it;
                    var recursive_items = GetFilesRecursive(childFolder);

                    foreach (var rec_it in recursive_items)
                    {
                        yield return rec_it;
                    }
                }
            }
        }
    }
}