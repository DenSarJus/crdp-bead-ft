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
            return Ok("Version 1.0");
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

            await SaveFile(xmlData.ToString(), province.ToLower(), fileName, config);

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

            string shareName = $"{province}-files";

            var share = new ShareClient(connectionString, shareName);
            var directory = share.GetDirectoryClient("");
            var file = directory.GetFileClient(fileName);

            using (var stream = await file.OpenWriteAsync(true, 0, new ShareFileOpenWriteOptions { MaxSize = content.Length }))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(content));

                await stream.FlushAsync();
            }
        }

    }
}
