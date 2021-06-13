#r "Newtonsoft.Json"

using System.IO;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;

public static async Task<IActionResult> Run(HttpRequest req, ILogger log)
{
    log.LogInformation("C# HTTP trigger function processed a request.");

    string name = req.Query["link"];

    string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
    dynamic data = JsonConvert.DeserializeObject(requestBody);
    name = name ?? data?.link;

    if(string.IsNullOrEmpty(name))
    {
     return new BadRequestObjectResult("Link is null or empty");   
    }

    // string extension = Path.GetExtension(name);
    var strings = name.Split("/");
    var s = strings[strings.Length - 1];
    return new OkObjectResult(s);
}
