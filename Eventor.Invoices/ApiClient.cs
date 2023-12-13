using Eventor.Api.Model;
using System.Security.Cryptography;
using System.Text;

namespace Eventor.Api;

public record ApiConfig(string OrgId, string CacheRoot, int Year, string ReportFileName);

public class ApiClient(ApiConfig config, HttpClient httpClient)
{
    public async Task<EntryList> GetOrgEntries(int year, int month)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        return await GetFromStream<EntryList>($"entries?fromEventDate={from:yyyy-MM-dd} 00:00:00&toEventDate={to:yyyy-MM-dd} 23:59:59&organisationIds={config.OrgId}&includeEntryFees=true", $"{year}-{month:00}");
    }

    public async Task<EntryFeeList> GetEntryFees(string eventId)
        => await GetFromStream<EntryFeeList>($"entryfees/events/{eventId}", eventId.ToString());

    public async Task<ResultList> GetOrgEventResults(string eventId)
        => await GetFromStream<ResultList>($"results/organisation?eventId={eventId}&organisationIds={config.OrgId}", eventId.ToString());

    public async Task<PersonList> GetOrgPersons()
        => await GetFromStream<PersonList>($"persons/organisations/{config.OrgId}", config.OrgId.ToString());

    private async Task<T> GetFromStream<T>(string url, string cachePrefix)
    {
        var folderName = url.Split(['/', '?'])[0];
        var folder = Path.Combine(config.CacheRoot, folderName);

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        var urlHash = GetStringHash(url);
        var filename = Path.Combine(folder,  $"{cachePrefix}_{urlHash}.xml");
        
        if(!File.Exists(filename)) 
        {
            var response = await httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            using (var writeStream = File.Create(filename))
            {
                using var responseStream = await response.Content.ReadAsStreamAsync();
                await responseStream.CopyToAsync(writeStream);
            }
        }

        using var readStream = File.OpenRead(filename);
        return XmlParser.Parse<T>(readStream);
    }

    private static string GetStringHash(string text)
    {
        var messageBytes = Encoding.UTF8.GetBytes(text);
        var hashValue = SHA256.HashData(messageBytes);
        return Convert.ToHexString(hashValue);
        
    }
}