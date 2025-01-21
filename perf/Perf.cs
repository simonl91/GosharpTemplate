using GosharpTemplate;
using System.Diagnostics;

public class Perf
{
    const bool RUN_GO = true; 
    const int RUNS = 1000;

    public static void Main(params string[] args)
    {
        Console.WriteLine("Run Csharp:");
        Table1000Items();

#if true
        Console.WriteLine();
        Console.WriteLine();
        Console.WriteLine("Run Go:");
        Process cmd = new Process();
        cmd.StartInfo.FileName = "cmd.exe";
        cmd.StartInfo.RedirectStandardInput = true;
        cmd.StartInfo.RedirectStandardOutput = true;
        cmd.StartInfo.CreateNoWindow = true;
        cmd.StartInfo.UseShellExecute = false;
        cmd.Start();

        cmd.StandardInput.WriteLine("go run ../../../perf.go");
        cmd.StandardInput.Flush();
        cmd.StandardInput.Close();
        cmd.WaitForExit();
        Console.WriteLine(cmd.StandardOutput.ReadToEnd());
#endif
    }

    static void Table1000Items()
    {
        //Setup
        var template = new Template();
        //template.ParseFiles("List.html");
        template.ParseFiles("../../../List.html");
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

    record Customer(
        string Name,
        string Address,
        string City,
        string Zip,
        string EMail,
        string Phone
    );

    record TableItem(
        int Id,
        string Name,
        bool Valid,
        string Description,
        int Count,
        double Price,
        Customer Customer
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
                    Random.Shared.NextDouble() * 1000.0,
                    new Customer(
                        $"Customer {i}",
                        $"Customer Street {i}",
                        $"City{i}",
                        $"{i}",
                        $"Cust.omer{i}@email.com",
                        $"{i}"
                    )
                )
            );
        }
        return items;
    }
}
