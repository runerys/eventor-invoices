using Eventor.Api;
using Eventor.Api.Model;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

var services = new ServiceCollection();

var apiConfig = new ApiConfig("245", @"c:\temp\eventor\cache", 2023, @"c:\temp\eventor\report.csv");

services.AddSingleton(apiConfig);

services.AddHttpClient<ApiClient>(
                              client => {
                                  client.BaseAddress = new Uri("https://eventor.orientering.no/api/");
                                  client.DefaultRequestHeaders.Add("ApiKey", Environment.GetEnvironmentVariable("EVENTOR_API_KEY"));
                                  });

var serviceProvider = services.BuildServiceProvider();

var apiClient = serviceProvider.GetRequiredService<ApiClient>();

var entries = await apiClient.GetOrgEntries(2023, 6);
//var entryfees = await apiClient.GetEntryFees("17233");
var results = await apiClient.GetOrgEventResults("17233");

var persons = await apiClient.GetOrgPersons();

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs e) 
{
    cts.Cancel();
    e.Cancel = true;
};

var allEntries = new ConcurrentBag<Entry>();
var allFees = new ConcurrentBag<EntryFee>();
var allResults = new ConcurrentBag<ResultList>();

var options = new ParallelOptions { MaxDegreeOfParallelism = 2, CancellationToken = cts.Token };

await Parallel.ForEachAsync(Enumerable.Range(1, 12), options, async (month, token) => {
    var forMonth = await apiClient.GetOrgEntries(apiConfig.Year, month);
    foreach (var e in forMonth.Entry)
    {
        allEntries.Add(e);
    }
});

var distinctEvents = allEntries.Select(x => x.Items1.OfType<EventId>().Single().Text.Single()).Distinct().ToList();
await Parallel.ForEachAsync(distinctEvents, options, async (race, token) =>
{
    var feeList = await apiClient.GetEntryFees(race);
    foreach (var fee in feeList.EntryFee)
    {
        allFees.Add(fee);
    }
    
    allResults.Add(await apiClient.GetOrgEventResults(race));
});

//var competitorNullEvents = new List<string>();

//var distinctPersonIds = allEntries.Select(x =>
//{
//    var competitor = x.Items.OfType<Competitor>().SingleOrDefault();
//    if (competitor == null)
//    {
//        var eventId = x.Items1.OfType<EventId>().Single().Text.Single();
//        var results = allResults.Single(x => ((Event)x.Item).EventId.Text.Single() == eventId);
//        var race = (Event)results.Item;
//        competitorNullEvents.Add(race.Name.Text.Single());
//        // relay
//        return null;
//    }
//    var personId = new[] { competitor.Item, competitor.Item1, competitor.Item2}.OfType<PersonId>().Single().Text; 
//    return personId;
//}).Where(x => x != null).Distinct().ToList();


Console.WriteLine($"Found {distinctEvents.Count} events and {allEntries.Count} entries .");

using var report = new StreamWriter(apiConfig.ReportFileName, true);

foreach (var entry in allEntries)
{
    var competitor = entry.Items.OfType<Competitor>().SingleOrDefault();

    if (competitor == null)
        continue;

    var personId = new[] { competitor.Item, competitor.Item1, competitor.Item2 }.OfType<PersonId>().Single().Text.Single();
    var person = persons.Person.SingleOrDefault(x => x.PersonId.Text.Single() == personId);

    if(person == null)
    {
        Console.WriteLine($"Ukjent personid {personId}");
        continue;
    }

    var personGivenName = person.PersonName.Given.Single().Text.Single();
    var personFamilyName = person.PersonName.Family.Text.Single();
    var personName = $"{personGivenName} {personFamilyName}";
    var eventId = entry.Items1.OfType<EventId>().Single().Text.Single();

    var race = allResults.Where(x => ((Event)x.Item).EventId.Text.Single() == eventId).Select(x => x.Item as Event).Single();
    var raceName = race.Name.Text.Single();

    var fees = from f in allFees
               join e in entry.EntryEntryFee on f.EntryFeeId.Text.Single() equals ((EntryFeeId)e.Item).Text.Single()
               select new { Amount = f.Amount.Text.Single(), ValueOperator = f.valueOperator.ToString(), Name = f.Name.Text.Single() };

    var rows = from f in fees
               select new EntryItem(personId, personName, eventId, raceName, f.Amount, f.Name, f.ValueOperator, "IKKE SATT", DateOnly.Parse(race.StartDate.Date.Text.Single()));

    foreach(var row in rows)
    {
        await report.WriteLineAsync(row.ToString());
    }
}

record EntryItem(string PersonId, string PersonName, string EventId, string EventName, string Fee, string FeeName, string ValueOperator, string ResultStatus, DateOnly EventDate)
{
    public override string ToString()
    {
        return string.Join(";",
        [
            EventId, EventName, EventDate.ToString("yyyy-MM-dd"), PersonId, PersonName, Fee, FeeName, ValueOperator, ResultStatus
        ]);
    }
}

//Console.WriteLint($"Eve)
