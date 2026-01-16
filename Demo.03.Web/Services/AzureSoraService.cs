using Azure.Core;
using Demo.Embedding.Web.Configurations.ConfigsModel;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text;

namespace Demo.Embedding.Web.Services;

public interface IAzureSoraService
{
    Task<dynamic> VideoGenerationRequest(string deploymentName, object body, HttpClient client);
    Task SaveVideoContent(string endpoint, string deploymentName, string generationId, HttpClient client, string outputFilename);
}

public class AzureSoraService : IAzureSoraService
{
    private readonly AzureAIOptions _azureAIOptions;

    public AzureSoraService(IOptions<AzureAIOptions> options)
    {
        _azureAIOptions = options.Value;
    }

    public async Task<dynamic> VideoGenerationRequest(string deploymentName, object body, HttpClient client)
    {
        try
        {
            var endPoint = _azureAIOptions.VideoModelEndpoint;
            var apiVersion = "preview";
            var path = $"openai/v1/video/generations/jobs";
            var paramsQuery = $"?api-version={apiVersion}";
            var constructedUrl = $"{endPoint}{path}{paramsQuery}";

            var requestBody = System.Text.Json.JsonSerializer.Serialize(body);
            HttpResponseMessage response = await client.PostAsync(constructedUrl, new StringContent(requestBody, Encoding.UTF8, "application/json")).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            Console.WriteLine(content);
            dynamic result = JsonConvert.DeserializeObject(content) ?? throw new Exception("Failed to deserialize response");

            var jobId = result.id;
            Console.WriteLine($"Polling job status for ID: {jobId}");
            while (result.status != "succeeded" && result.status != "failed")
            {
                await Task.Delay(5000);
                response = await client.GetAsync($"{endPoint}openai/v1/video/generations/jobs/{jobId}{paramsQuery}").ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                content = await response.Content.ReadAsStringAsync();
                result = JsonConvert.DeserializeObject(content) ?? throw new Exception("Failed to deserialize response");
                Console.WriteLine($"Status: {result.status}");
            }
            if (result.status == "succeeded")
            {
                Console.WriteLine(result.generations.Count > 0 ? "Video generation succeeded." : " Status is succeeded, but no generations were returned.");
            }
            else if (result.status == "failed")
            {
                Console.WriteLine("Video generation failed.");
                Console.WriteLine(content);
            }
            return result;
        }
        catch (Exception ex)
        {

            throw;
        }
       
    }

    public async Task SaveVideoContent(string endpoint, string deploymentName, string generationId, HttpClient client, string outputFilename)
    {
        var apiVersion = "preview";
        HttpResponseMessage response = await client.GetAsync($"{endpoint}openai/v1/video/generations/{generationId}/content/video?api-version={apiVersion}").ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var videoStream = await response.Content.ReadAsStreamAsync();

        using (var fileStream = new FileStream(outputFilename, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await videoStream.CopyToAsync(fileStream).ConfigureAwait(false);
        }
        Console.WriteLine($"Generated video saved as \"{outputFilename}\"");
    }
}
