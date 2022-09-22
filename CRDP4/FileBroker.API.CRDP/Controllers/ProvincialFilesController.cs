using Azure.Core;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using FileBroker.API.CRDP.Helpers;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Xml.Linq;

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
            if (string.IsNullOrEmpty(fileName))
                return BadRequest("Missing filename");

            string apiKey = HttpContext.Request.Headers["API_KEY"];
            if (string.IsNullOrEmpty(apiKey))
                return BadRequest("Missing api key");

            var apiKeys = config.GetSection("API_KEY");
            string province = GetProvinceFromApiKey(apiKey, apiKeys);

            if (string.IsNullOrEmpty(province))
                return BadRequest("Invalid api key");

            string xmlData;
            using (var reader = new StreamReader(request.Body, Encoding.UTF8))
                xmlData = await reader.ReadToEndAsync();

            if (string.IsNullOrEmpty(province))
                return BadRequest("Missing xml data");

            SaveFile(xmlData.ToString(), province.ToLower(), fileName, config);

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

        private static void SaveFile(string content, string province, string fileName, IConfiguration config)
        {
            string connectionString = config["Storage:ConnectionString"].ReplaceVariablesWithEnvironmentValues();

            string shareName = $"{province}-files";

            var share = new ShareClient(connectionString, shareName);
            var directory = share.GetDirectoryClient("");
            var file = directory.GetFileClient(fileName);

            using (var stream = file.OpenWrite(true, 0, new ShareFileOpenWriteOptions { MaxSize = content.Length }))
            {
                stream.Write(Encoding.UTF8.GetBytes(content));

                stream.Flush();
            }
        }

    }
}
