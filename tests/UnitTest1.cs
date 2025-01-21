using GosharpTemplate;

namespace GosharpTemplate.Tests;

public class Tests
{
    private const string easyBlockTemplate =
        @"{{block ""title"" . }}<h1>{{if .IsTrue }}{{ .PageTitle }}{{else}}its false{{end}}</h1>{{end}}";

    private const string easyDefineTemplate =
        @"{{define ""title"" }}<h1>{{if .IsTrue }}{{ .PageTitle }}{{else}}its false{{end}}</h1>{{end}}";


    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void ExampleTest()
    {
        var template = new Template();
        template.Parse("foo", @"{{define ""T""}}Hello, {{.FirstName}} {{.LastName}}!{{end}}");
        var data = new { FirstName = "John", LastName = "Johnson" };
        var result = template.ExecuteTemplate("T", data);
        Assert.That(result, 
            Is.EqualTo("Hello, John Johnson!"));
    }

    struct WithPageData
    {
        public string Title;
        public string OtherTitle;
    }
    [Test]
    public void Example2Test()
    {
        var template = new Template();
        template.ParseFiles("../../../TestFiles/TextFile1.txt");
        var result = template.ExecuteTemplate("TextFile1.txt",
            new 
            {
                Name = "Aunt Mildred",
                Gift = "bone china tea set",
                Attended = true 
            }
        );
        Assert.That(result,
            Is.EqualTo(@"Dear Aunt Mildred,

It was a pleasure to see you at the wedding.
Thank you for the lovely bone china tea set.

Best wishes,
Josie"
        ));
    }

    [Test]
    public void WithTest()
    {
        var template = new Template();
        template.ParseFiles("../../../TestFiles/with.html");
        var withTitle = template.ExecuteTemplate("with.html",
            new WithPageData
            {
                Title = "Title",
                OtherTitle = "OtherTitle"
            }
        );
        var withOtherTitle = template.ExecuteTemplate("with.html",
            new WithPageData
            {
                Title = null,
                OtherTitle = "OtherTitle"
            }
        );
        var withoutTitle = template.ExecuteTemplate("with.html",
            new WithPageData
            {
                Title = null,
                OtherTitle = null
            }
        );

        var template2 = new Template();
        template2.Parse("test",
            @"{{with .}}
                  {{range .}}
                      {{.}}
                      <p>Unreachable</p>
                  {{end}}
              {{end}}");
        var passNullToWithDirectly =
            template2.ExecuteTemplate("test", null);


        Assert.That(withTitle,
            Is.EqualTo("<h1>Title</h1>"));
        Assert.That(withOtherTitle,
            Is.EqualTo("<h1>OtherTitle</h1>"));
        Assert.That(withoutTitle,
            Is.EqualTo("<h1>Nothing</h1>"));
        Assert.That(passNullToWithDirectly,
            Is.EqualTo(""));
    }

    [Test]
    public void TestArrayAccess()
    {
        var data = new {
            Customers = new[] {
                new { Name = "Customer0" },
                new { Name = "Customer1" },
                new { Name = "Customer2" }
            }
        };
        var templString = @"{{.Customers[2].Name}}";
        var template = new Template();
        template.Parse("root", templString);
        var result = template.ExecuteTemplate("root", data);
        Assert.That(result,
            Is.EqualTo("Customer2"));
    }

    [Test]
    public void TestSmallBlockTemplate()
    {
        var data = new
        {
            IsTrue = true,
            PageTitle = "This is a title"
        };
        var template = new Template();
        template.Parse("root", easyBlockTemplate);
        var result = template.ExecuteTemplate("title", data);
        var result2 = template.ExecuteTemplate("root", data);
        Assert.That(result,
            Is.EqualTo("<h1>This is a title</h1>"));
        Assert.That(result,
            Is.EqualTo(result2));
    }

    [Test]
    public void TestSmallDefineTemplate()
    {
        var data = new
        {
            IsTrue = true,
            PageTitle = "This is a title"
        };
        var template = new Template();
        template.Parse("root", easyDefineTemplate);
        var result = template.ExecuteTemplate("title", data);
        var result2 = template.ExecuteTemplate("root", data);
        Assert.That(result,
            Is.EqualTo("<h1>This is a title</h1>"));
        Assert.That(result2,
            Is.EqualTo(""));
    }

    [Test]
    public void TestTodoAppHtml()
    {
        var template = new Template();
        template.ParseFiles("../../../TestTodoApp/index.html", "../../../TestTodoApp/todo.html");

        var data = new
        {
            PageTitle = "This is the title",
            Items = new[] {
                new { Done = true, Text = "Item1"},
                new { Done = false, Text = "Item2"},
                new { Done = false, Text = "Item3"},
                new { Done = true, Text = "Item4"},
                new { Done = true, Text = "Item1"},
                new { Done = false, Text = "Item2"},
                new { Done = false, Text = "Item3"},
                new { Done = true, Text = "Item4"},
                new { Done = true, Text = "Item1"},
                new { Done = false, Text = "Item2"},
                new { Done = false, Text = "Item3"},
                new { Done = true, Text = "Item4"},
                new { Done = true, Text = "Item1"},
                new { Done = false, Text = "Item2"},
            }
        };

        var result = template.ExecuteTemplate("layout", data);
        Assert.Pass();
        Assert.That(result,
            Is.EqualTo(@"<!DOCTYPE html>
<html>

<head>
    <meta charset=""utf8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title> This is the title </title>
    <link rel=""stylesheet"" href=""assets/site.css"">
</head>

<body>
    <section class=""app"">

<section class=""app"">
    <header class=""header"">
        <h1>This is the title</h1>
    </header>
    <section class=""create"" data-todo=""create"">
        <input placeholder=""Write something!"" autofocus class=""new-item"" data-todo=""new"" />    
        <hr />
    </section>
    <section class=""main"" data-todo=""main"">
        <ul class=""list"" data-todo=""list"">

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox""  checked >
    <label data-todo=""label"">Item1</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item1"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox"" >
    <label data-todo=""label"">Item2</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item2"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox"" >
    <label data-todo=""label"">Item3</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item3"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox""  checked >
    <label data-todo=""label"">Item4</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item4"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox""  checked >
    <label data-todo=""label"">Item1</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item1"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox"" >
    <label data-todo=""label"">Item2</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item2"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox"" >
    <label data-todo=""label"">Item3</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item3"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox""  checked >
    <label data-todo=""label"">Item4</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item4"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox""  checked >
    <label data-todo=""label"">Item1</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item1"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox"" >
    <label data-todo=""label"">Item2</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item2"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox"" >
    <label data-todo=""label"">Item3</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item3"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox""  checked >
    <label data-todo=""label"">Item4</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item4"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox""  checked >
    <label data-todo=""label"">Item1</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item1"">
</div>
</li>

            <li class=""item"">
<div class=""view"">
    <input data-todo=""toggle"" class=""toggle"" type=""checkbox"" >
    <label data-todo=""label"">Item2</label>
    <button class=""destroy"" data-todo=""destroy""></button>
    <input class=""edit"" data-todo=""edit"" value=""Item2"">
</div>
</li>

        </ul>
    </section>
</section>

    </section>
    <script type=""module"" src=""assets/app.js""></script>
</body>

</html>
"));
    }

}


