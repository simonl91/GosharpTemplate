using System.IO;
using System.Diagnostics;
using System.Text;
using System;

namespace GosharpTemplate
{
    /// <summary>
    /// Represents the parsed representation of a template
    /// <example>
    /// <code>
    /// var template = new Template();
    /// template.ParseFiles("file1.html", "file2.html");
    /// template.ExecuteTemplate("foo", data);
    /// </code>
    /// </example>
    /// </summary>
    public class Template
    {
        private string[] fileNames;
        private string[] files;
        private Parser parser;

        public Template()
        {
            parser = new Parser();
            fileNames = new string[0];
            files = new string[0];
        }

        /// <summary>
        /// Parse the content of one or multiple template files
        /// <example>
        /// <code>
        /// var template = new Template();
        /// template.ParseFiles("file1.html", "file2.html");
        /// </code>
        /// </example>
        /// </summary>
        public void ParseFiles(params string[] filesPaths)
        {
            fileNames = new string[filesPaths.Length];
            files = new string[filesPaths.Length];
            for (var i = 0; i < filesPaths.Length; i++)
            {
                var fileName = Path.GetFileName(filesPaths[i]);
                if (!File.Exists(filesPaths[i]))
                    throw new ArgumentException($"File '{fileName}' does not exist.");
                fileNames[i] = fileName;
                files[i] = File.ReadAllText(filesPaths[i]);
            }
            for (var i = 0; i < files.Length; i++)
            {
                var lexer = new Lexer(ref files[i]);
                parser.Parse(lexer, fileNames[i]);
            }
        }

        /// <summary>
        /// Parse the content of the <paramref name="text"/> parameter.
        /// <example>
        /// <code>
        /// var template = new Template();
        /// template.Parse("foo",@"{{define ""T""}}Hello, {{.}}!{{end}}");
        /// var result = template.ExecuteTemplate("T", "John");
        /// </code>
        /// </example>
        /// </summary>
        /// <param name="name">Name the template</param>
        /// <param name="text">Text representation of the template</param>
        public void Parse(string name, string text)
        {
            var lexer = new Lexer(ref text);
            parser.Parse(lexer, name);
        }

        /// <summary>
        /// Search for a template/define/block with the name: <paramref name="name"/>.<br/>
        /// Evaluate the parsed template with the parameter <paramref name="data"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        /// <returns>string representation of the evaluated template</returns>
        /// <exception cref="Exception" />
        public string ExecuteTemplate(string name, object data)
        {
            var childrenIdx = parser.FindTemplateChildrenIdx(name);
            var children = parser.allChildren[childrenIdx];
            var sb = new StringBuilder(4000);
            foreach (var child in children)
            {
                sb.Append(parser.Eval(child, data));
            }
            return sb.ToString();
        }


        /// <summary>
        /// Search for a template/define/block with the name: <paramref name="name"/>.<br/>
        /// Evaluate the parsed template with the parameter <paramref name="data"/>. <br/>
        /// This method uses go-style error handling, use ExecuteTemplate if you prefer exceptions.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="data"></param>
        /// <returns>
        /// 1: string of the evaluated template<br/>
        /// 2: string with error message.
        /// <code>
        /// var (result, err) = template.ExecuteTemplateErr("foo", data);
        /// if (err != null) return; //handle error
        /// await context.WriteAsync(result);
        /// </code>
        /// </returns>
        public (string, string) ExecuteTemplateErr(string name, object data)
        {
            try
            {
                var childrenIdx = parser.FindTemplateChildrenIdx(name);
                var children = parser.allChildren[childrenIdx];
                var sb = new StringBuilder(4000);
                foreach (var child in children)
                {
                    sb.Append(parser.Eval(child, data));
                }
                return (sb.ToString(), null);
            }
            catch (Exception e)
            {
                return (null, e.Message);
            }
        }

    }

}
