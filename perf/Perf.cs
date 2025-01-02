using GosharpTemplate;
using System.Diagnostics;

public class Perf
{
    const int RUNS = 10_000;

    public static void Main(params string[] args)
    {
        Table1000Items();

    }

    static void Table1000Items()
    {
        //Setup
        var template = new Template();
        template.ParseFiles("List.html");
        var items = GenerateRange(1000);
        var sw = new Stopwatch();

        var times = new List<double>(RUNS);
        for (int i = 0; i < RUNS; i++)
        {
            sw.Start();
            template.ExecuteTemplate("table", items);
            sw.Stop();
            times.Add(sw.Elapsed.TotalMicroseconds);
            sw.Reset();
        }
        Console.WriteLine($"Table with 1000 items, {RUNS} runs:");
        Console.WriteLine($"Min: {times.Skip(1).Min()} (micros)");
        Console.WriteLine($"Max: {times.Skip(1).Max()} (micros)");
        Console.WriteLine($"Average: {times.Skip(1).Average()} (micros)");
    }

    record TableItem(
        int Id,
        string Name,
        bool Valid,
        string Description,
        int Count,
        double Price
    );

    static List<TableItem> GenerateRange(int n)
    {
        var items = new List<TableItem>(n);
        for (int i = 1; i <= n; i++)
        {
            items.Add(
                new TableItem(
                    i,
                    $"Item{i}",
                    true,
                    "Description {i}",
                    i % 100,
                    Random.Shared.NextDouble() * 1000.0
                )
            );
        }
        return items;
    }
}
