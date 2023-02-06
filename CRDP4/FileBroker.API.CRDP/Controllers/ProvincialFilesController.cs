using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using FileBroker.API.CRDP.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace FileBroker.API.CRDP.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class ProvincialFilesController : ControllerBase
    {
        [HttpGet("Version")]
        public ActionResult GetVersion()
        {
            return Ok("Version 1.1");
        }

        [HttpGet("Audit/{cycle}")]
        public async Task<ActionResult> GetAudit([FromServices] IConfiguration config, [FromRoute] string cycle)
        {
            string apiKey = HttpContext.Request.Headers["API_KEY"];

            var apiKeys = config.GetSection("API_KEY");
            string province = GetProvinceFromApiKey(apiKey, apiKeys);

            if (string.IsNullOrEmpty(apiKey)) return BadRequest("Missing api key");
            if (string.IsNullOrEmpty(province)) return BadRequest("Invalid api key");

            string fileName = $"{province}DCO_AUDIT.{cycle}.XML";

            var fileContent = await GetFileAsStream(province, fileName, config);

            if (fileContent is not null)
                return File(fileContent, "application/xml", fileName);
            else
                return NotFound(fileName + " not found");
        }

        [HttpGet("Status/{cycle}")]
        public async Task<ActionResult> GetStatus([FromServices] IConfiguration config, [FromRoute] string cycle)
        {
            string apiKey = HttpContext.Request.Headers["API_KEY"];

            var apiKeys = config.GetSection("API_KEY");
            string province = GetProvinceFromApiKey(apiKey, apiKeys);

            if (string.IsNullOrEmpty(apiKey)) return BadRequest("Missing api key");
            if (string.IsNullOrEmpty(province)) return BadRequest("Invalid api key");

            string fileName = $"{province}DCO_STATUS.{cycle}.XML";

            var fileContent = await GetFileAsStream(province, fileName, config);

            if (fileContent is not null)
                return File(fileContent, "application/xml", fileName);
            else
                return NotFound(fileName + " not found");
        }

        [HttpGet("Processed/{year}/{month}/{day}")]
        public async Task<ActionResult> GetAudit([FromServices] IConfiguration config, 
                                                 [FromRoute] string year, [FromRoute] string month, [FromRoute] string day)
        {
            string apiKey = HttpContext.Request.Headers["API_KEY"];

            var apiKeys = config.GetSection("API_KEY");
            string province = GetProvinceFromApiKey(apiKey, apiKeys);

            if (string.IsNullOrEmpty(apiKey)) return BadRequest("Missing api key");
            if (string.IsNullOrEmpty(province)) return BadRequest("Invalid api key");

            string fileName = $"Processed_{year}-{month}-{day}.zip";

            var fileContent = await GetFileAsStream(province, fileName, config);
            
            if (fileContent is not null)
                return File(fileContent, "application/zip", fileName);
            else
                return NotFound(fileName + " not found");
        }

        [HttpPost("")]
        public async Task<ActionResult> UploadXML([FromServices] IConfiguration config)
        {
            var request = HttpContext.Request;

            string fileName = request.Headers["FileName"];
            string apiKey = HttpContext.Request.Headers["API_KEY"];

            var apiKeys = config.GetSection("API_KEY");
            string province = GetProvinceFromApiKey(apiKey, apiKeys);

            if (string.IsNullOrEmpty(fileName)) return BadRequest("Missing filename");
            if (string.IsNullOrEmpty(apiKey)) return BadRequest("Missing api key");
            if (string.IsNullOrEmpty(province)) return BadRequest("Invalid api key");

            string xmlData;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8))
                xmlData = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(province)) return BadRequest("Missing xml data");

            await SaveFile(xmlData, province, fileName, config);

            return Created("", "Saved " + fileName);
        }

        private static string GetProvinceFromApiKey(string apiKey, IConfigurationSection apiKeys)
        {
            string province = string.Empty;
            foreach (var key in apiKeys.GetChildren())
            {
                if (key.Value.ReplaceVariablesWithEnvironmentValues() == apiKey)
                {
                    province = key.Key;
                    break;
                }
            }

            return province;
        }

        private static async Task SaveFile(string content, string province, string fileName, IConfiguration config)
        {
            string connectionString = config["Storage:ConnectionString"].ReplaceVariablesWithEnvironmentValues();

            string shareName = $"{province}-files".ToLower();

            var share = new ShareClient(connectionString, shareName);
            var directory = share.GetDirectoryClient("");
            var file = directory.GetFileClient(fileName);

            using var stream = await file.OpenWriteAsync(true, 0, new ShareFileOpenWriteOptions { MaxSize = content.Length });

            await stream.WriteAsync(Encoding.UTF8.GetBytes(content));

            await stream.FlushAsync();
        }

        private static async Task<Stream> GetFileAsStream(string province, string fileName, IConfiguration config)
        {
            string connectionString = config["Storage:ConnectionString"].ReplaceVariablesWithEnvironmentValues();

            string shareName = $"{province}-files".ToLower();

            var share = new ShareClient(connectionString, shareName);
            var directory = share.GetDirectoryClient("");

            var file = directory.GetFileClient(fileName);

            if (!await file.ExistsAsync())
                return null;

            try
            {
                ShareFileDownloadInfo download = await file.DownloadAsync();
                return download.Content;
            }
            catch
            {
                return null;
            }
        }

    }
}
