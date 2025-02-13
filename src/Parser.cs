using System.Diagnostics;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace GosharpTemplate
{
    using Nodes = List<Node>;
    using System.Text;
    using System;
    using System.Linq.Expressions;


    internal class Parser
    {
        // Data for each node type, use Node.DataIdx to access from node;
        internal List<RootData> allRoots;
        internal List<IfData> allIfs;
        internal List<string> allIdents;
        internal List<DefineData> allDefines;
        internal List<Nodes> allChildren;
        internal List<TemplateData> allTemplateCalls;
        internal List<RangeData> allRanges;
        internal List<WithData> allWiths;
        internal List<BlockData> allBlocks;
        internal List<string> allHtml;

        internal DataAccessors accessors;

        private List<Token> tokens;
        private int pos;
        private Lexer lexer;

        private string rootName;

#pragma warning disable 8618
        public Parser()
        {
            InitializeState();
        }

        internal void Parse(Lexer lex, string rootName)
        {
            this.rootName = rootName;
            pos = tokens.Count;
            lexer = lex;
            tokens.AddRange(lex.Lex());

            var lexingErrors = tokens.Where(x => x.Kind == TokenKind.Error).ToList();
            if (lexingErrors.Count > 0)
            {
                var errorBuilder = new StringBuilder();
                errorBuilder.AppendLine($"Syntax errors in {rootName}:");
                foreach (var errorToken in lexingErrors)
                {
                    errorBuilder.AppendLine(lexer.GetErrorData(errorToken.DataIdx));
                }
                throw new ArgumentException(errorBuilder.ToString());
            }

            var tree = CreateRootNode(rootName);
            tree.Kind = NodeKind.Root;
            ParseTemplate(tree);
        }

        private void InitializeState()
        {
            pos = 0;
            tokens = new List<Token>();
            allIfs = new List<IfData>();
            allIdents = new List<string>();
            allDefines = new List<DefineData>();
            allChildren = new List<Nodes>();
            allTemplateCalls = new List<TemplateData>();
            allRanges = new List<RangeData>();
            allWiths = new List<WithData>();
            allBlocks = new List<BlockData>();
            allHtml = new List<string>();
            allRoots = new List<RootData>();

            accessors = new DataAccessors();

            allIdents.Add(".");
        }

        private string ErrorMessage(string message)
        {
            return ErrorMessageAt(pos, message);
        }

        private string ErrorMessageAt(int pos, string message)
        {
            var sb = new StringBuilder();
            var token = tokens[pos];
            var line = lexer.GetLine(token.Start);
            var col = lexer.GetColumn(token.Start);
            var col2 = col + System.Math.Abs(token.Length - 1);
            sb.AppendLine($"Error in '{rootName}' at {line} {col}-{col2} '{lexer.GetText(token)}': ");
            sb.AppendLine(message);
            return sb.ToString();
        }

        private void ParseTemplate(Node rootNode)
        {
            var childrenIdx = rootNode.Kind switch
            {
                NodeKind.Root => allRoots[rootNode.DataIdx].ChildrenIdx,
                NodeKind.Define => allDefines[rootNode.DataIdx].ChildrenIdx,
                NodeKind.Block => allBlocks[rootNode.DataIdx].ChildrenIdx,
                NodeKind.Range => allRanges[rootNode.DataIdx].ChildrenIdx,
                NodeKind.If => allIfs[rootNode.DataIdx].ChildrenIdxTrue,
                NodeKind.With => allWiths[rootNode.DataIdx].ChildrenIdxTrue,
                _ => -1
            };
            Debug.Assert(childrenIdx >= 0, ErrorMessage($"{rootNode.Kind}, cant have children"));

            for (int i = 0; i < tokens.Count; i++)
            {
                switch (Nth(0))
                {
                    case TokenKind.Html:
                        allChildren[childrenIdx]
                            .Add(CreateHtmlNode());
                        Advance();
                        break;
                    case TokenKind.OpenBraceDouble:
                        var node = ParseExpression();
                        if (node.Kind == NodeKind.End) return;
                        if (node.Kind == NodeKind.Else)
                        {
                            childrenIdx++;
                            break;
                        }
                        allChildren[childrenIdx].Add(node);
                        break;
                    case TokenKind.Eof:
                        return;
                    default:
                        throw new Exception(
                            ErrorMessage($"Unexpedted token {Nth(0)}, expected Html or '{{'"));
                }
            }
        }

        private Node ParseExpression()
        {
            Expect(TokenKind.OpenBraceDouble);
            Eat(TokenKind.Dash);
            switch (Nth(0))
            {
                case TokenKind.Comment:
                    return ParseComment();
                case TokenKind.KeywordEnd:
                    return ParseEnd();
                case TokenKind.KeywordElse:
                    return ParseElse();
                case TokenKind.KeywordRange:
                    return ParseRange();
                case TokenKind.KeywordWith:
                    return ParseWith();
                case TokenKind.KeywordIf:
                    return ParseIf();
                case TokenKind.KeywordBlock:
                    return ParseBlock();
                case TokenKind.KeywordDefine:
                    return ParseDefine();
                case TokenKind.KeywordTemplate:
                    return ParseTemplateCall();
                case TokenKind.Dot:
                    var identNode = ParseIdent();
                    identNode.Kind = NodeKind.Expression;
                    Eat(TokenKind.Dash);
                    Expect(TokenKind.ClosingBraceDouble);
                    return identNode;
                default:
                    throw new Exception(ErrorMessage($"not a valid expression"));
            }
        }

        private Node ParseIf()
        {
            var start = pos;
            Expect(TokenKind.KeywordIf);
            var identIdx = ParseIdent().DataIdx;
            var node = CreateIfNode(start, identIdx);
            Eat(TokenKind.Dash);
            Expect(TokenKind.ClosingBraceDouble);
            ParseTemplate(node);
            return node;
        }

        private Node ParseRange()
        {
            var start = pos;
            Expect(TokenKind.KeywordRange);
            var identIdx = ParseIdent().DataIdx;
            var node = CreateRangeNode(start, identIdx);
            Eat(TokenKind.Dash);
            Expect(TokenKind.ClosingBraceDouble);
            ParseTemplate(node);
            return node;
        }

        private Node ParseWith()
        {
            var start = pos;
            Expect(TokenKind.KeywordWith);
            var identIdx = ParseIdent().DataIdx;
            var node = CreateWithNode(start, identIdx);
            Eat(TokenKind.Dash);
            Expect(TokenKind.ClosingBraceDouble);
            ParseTemplate(node);
            return node;
        }
        private Node ParseTemplateCall()
        {
            var start = pos;
            Expect(TokenKind.KeywordTemplate);
            var stringToken = expectToken(TokenKind.String);
            var identIdx = 0;
            if (Nth(0) == TokenKind.Dot)
            {
                identIdx = ParseIdent().DataIdx;
            }
            var node = CreateTemplateNode(lexer.GetTextFromStringToken(stringToken), start, identIdx);
            Eat(TokenKind.Dash);
            Expect(TokenKind.ClosingBraceDouble);
            return node;
        }

        private Node ParseBlock()
        {
            var start = pos;
            Expect(TokenKind.KeywordBlock);
            var stringToken = expectToken(TokenKind.String);
            var identIdx = 0;
            if (Nth(0) == TokenKind.Dot)
            {
                identIdx = ParseIdent().DataIdx;
            }
            var node = CreateBlockNode(lexer.GetTextFromStringToken(stringToken), identIdx, start);
            Eat(TokenKind.Dash);
            Expect(TokenKind.ClosingBraceDouble);
            ParseTemplate(node);
            return node;
        }

        private Node ParseDefine()
        {
            var start = pos;
            Expect(TokenKind.KeywordDefine);
            var stringToken = expectToken(TokenKind.String);
            Eat(TokenKind.Dash);
            Expect(TokenKind.ClosingBraceDouble);
            var node = CreateDefineNode(lexer.GetTextFromStringToken(stringToken), start);
            ParseTemplate(node);
            return node;
        }

        private Node ParseIdent()
        {
            Expect(TokenKind.Dot);
            var node = CreateIdentNode();
            var sb = new StringBuilder();
            sb.Append(".");
            while (!At(TokenKind.ClosingBraceDouble))
            {
                switch (Nth(0))
                {
                    case TokenKind.Dash:
                        break;
                    case TokenKind.Dot:
                        sb.Append(".");
                        break;
                    case TokenKind.Ident:
                        sb.Append(lexer.GetText(tokens[pos]));
                        break;
                    default:
                        throw new Exception(ErrorMessage($"Expected '.', identifier or '}}'"));
                }
                Advance();
            }
            allIdents[node.DataIdx] = sb.ToString();
            return node;
        }

        private Node ParseComment()
        {
            var curPos = pos;
            Expect(TokenKind.Comment);
            Eat(TokenKind.Dash);
            Expect(TokenKind.ClosingBraceDouble);
            return CreateNodeAt(curPos, NodeKind.Comment);
        }

        private Node ParseEnd()
        {
            var curPos = pos;
            Expect(TokenKind.KeywordEnd);
            Eat(TokenKind.Dash);
            Expect(TokenKind.ClosingBraceDouble);
            return CreateNodeAt(curPos, NodeKind.End);
        }

        private Node ParseElse()
        {
            var curPos = pos;
            Expect(TokenKind.KeywordElse);
            Eat(TokenKind.Dash);
            Expect(TokenKind.ClosingBraceDouble);
            return CreateNodeAt(curPos, NodeKind.Else);
        }

        private void Advance()
        {
            //lexer.PrintToken(tokens[pos]);
            Debug.Assert(!Eof());
            pos += 1;
        }

        private bool Eof()
        {
            return pos == tokens.Count;
        }

        private TokenKind Nth(int lookahead)
        {
            if (pos + lookahead >= tokens.Count) return TokenKind.Eof;
            return tokens[pos + lookahead].Kind;
        }

        private bool At(TokenKind kind)
        {
            return Nth(0) == kind;
        }

        private bool Eat(TokenKind kind)
        {
            if (At(kind))
            {
                Advance();
                return true;
            }
            else
            {
                return false;
            }
        }

        private void Expect(TokenKind kind)
        {
            if (Eat(kind))
            {
                return;
            }
            throw new Exception(ErrorMessage($"expected '{kind}' got '{Nth(0)}'"));
        }

        private Token expectToken(TokenKind kind)
        {
            if (Eat(kind))
            {
                return tokens[pos - 1];
            }
            throw new Exception(ErrorMessage($"expected '{kind}' got '{Nth(0)}'"));
        }


        private Node CreateHtmlNode()
        {
            var dataIdx = allHtml.Count;
            var html = lexer.GetText(tokens[pos]);
            if (0 <= (pos - 2) && tokens[pos - 2].Kind == TokenKind.Dash)
                html = html.TrimStart();
            if (pos + 2 < tokens.Count && tokens[pos + 2].Kind == TokenKind.Dash)
                html = html.TrimEnd();
            allHtml.Add(html);
            return new Node
            {
                Kind = NodeKind.Html,
                TokenIdx = pos,
                DataIdx = dataIdx,
            };
        }


        private Node CreateRootNode(string name)
        {
            var childrenIdx = allChildren.Count;
            allChildren.Add(new Nodes());
            var rootData = new RootData
            {
                Name = name,
                ChildrenIdx = childrenIdx
            };
            var dataIdx = allRoots.Count;
            allRoots.Add(rootData);
            var node = new Node
            {
                Kind = NodeKind.Root,
                TokenIdx = -1,
                DataIdx = dataIdx,
            };
            return node;
        }

        private Node CreateBlockNode(string name, int dataIdentIdx, int pos)
        {
            var childrenIdx = allChildren.Count;
            allChildren.Add(new Nodes());

            var blockData = new BlockData(name, dataIdentIdx, childrenIdx);
            var dataIdx = allBlocks.Count;
            allBlocks.Add(blockData);
            var node = new Node
            {
                Kind = NodeKind.Block,
                TokenIdx = pos,
                DataIdx = dataIdx,
            };
            return node;
        }

        private Node CreateDefineNode(string name, int pos)
        {
            var childrenIdx = allChildren.Count;
            allChildren.Add(new Nodes());

            var defineData = new DefineData(name, childrenIdx);
            var dataIdx = allDefines.Count;
            allDefines.Add(defineData);
            var node = new Node
            {
                Kind = NodeKind.Define,
                TokenIdx = pos,
                DataIdx = dataIdx,
            };
            return node;
        }

        private Node CreateNodeAt(int pos, NodeKind kind)
        {
            return new Node
            {
                Kind = kind,
                TokenIdx = pos,
                DataIdx = -1,
            };
        }

        private Node CreateIdentNode()
        {
            var dataIdx = allIdents.Count;
            allIdents.Add("");
            var node = new Node
            {
                Kind = NodeKind.Ident,
                TokenIdx = pos,
                DataIdx = dataIdx
            };
            return node;
        }

        private Node CreateTemplateNode(string name, int pos, int identIdx)
        {
            var templateData = new TemplateData
            {
                Name = name,
                IdentIdx = identIdx
            };
            var dataIdx = allTemplateCalls.Count;
            allTemplateCalls.Add(templateData);
            var node = new Node
            {
                Kind = NodeKind.Template,
                TokenIdx = pos,
                DataIdx = dataIdx
            };
            return node;
        }

        private Node CreateWithNode(int pos, int identIdx)
        {
            var childrenIdx = allChildren.Count;
            allChildren.Add(new Nodes());
            allChildren.Add(new Nodes());
            var dataIdx = allWiths.Count;
            var withData = new WithData
            {
                IdentIdx = identIdx,
                ChildrenIdxTrue = childrenIdx,
                ChildrenIdxFalse = childrenIdx + 1
            };
            allWiths.Add(withData);
            var node = new Node
            {
                Kind = NodeKind.With,
                TokenIdx = pos,
                DataIdx = dataIdx
            };
            return node;
        }
        private Node CreateRangeNode(int pos, int identIdx)
        {
            var childrenIdx = allChildren.Count;
            allChildren.Add(new Nodes());

            var dataIdx = allRanges.Count;
            var rangeData = new RangeData
            {
                IdentIdx = identIdx,
                ChildrenIdx = childrenIdx
            };
            allRanges.Add(rangeData);

            var node = new Node
            {
                Kind = NodeKind.Range,
                TokenIdx = pos,
                DataIdx = dataIdx
            };
            return node;
        }

        private Node CreateIfNode(int pos, int identIdx)
        {
            var childrenTrue = new Nodes();
            var childrenFalse = new Nodes();
            var childrenIdx = allChildren.Count;
            allChildren.Add(childrenTrue);
            allChildren.Add(childrenFalse);

            var ifData = new IfData
            {
                IdentIdx = identIdx,
                ChildrenIdxTrue = childrenIdx,
                ChildrenIdxFalse = childrenIdx + 1
            };
            var dataIdx = allIfs.Count;
            allIfs.Add(ifData);

            var node = new Node
            {
                Kind = NodeKind.If,
                TokenIdx = pos,
                DataIdx = dataIdx
            };
            return node;
        }

        internal string Eval(Node rootNode, object rootData)
        {
            var sb = new StringBuilder(1024);
            var stack = new Stack<(Node, object)>(1024);
            stack.Push((rootNode, rootData));
            while (stack.Count > 0)
            {
                var (node, data) = stack.Pop();
                switch (node.Kind)
                {
                    case NodeKind.Comment:
                        continue;
                    case NodeKind.Expression:
                        {
                            var ident = allIdents[node.DataIdx];
                            var accessor = GetDataAccessorFunction(data, ident);
                            sb.Append(accessor.Invoke(data)?.ToString() ?? "");
                        }
                        break;
                    case NodeKind.Html:
                        {
                            var nodeData = allHtml[node.DataIdx];
                            sb.Append(nodeData);
                        }
                        break;
                    case NodeKind.If:
                        {
                            var ifData = allIfs[node.DataIdx];
                            var ident = allIdents[ifData.IdentIdx];
                            var ifAccessor = GetDataAccessorFunction(data, ident);
                            var ifVariable = ifAccessor.Invoke(data);
                            if (ifVariable.GetType() != typeof(bool))
                                throw new ArgumentException(
                                    $"expected boolean, got {ifVariable.GetType()}");
                            var children = (bool)ifVariable ?
                                allChildren[ifData.ChildrenIdxTrue]
                                : allChildren[ifData.ChildrenIdxFalse];
                            for (var i = children.Count - 1; i >= 0; i--)
                            {
                                stack.Push((children[i], data));
                            }
                        }
                        break;
                    case NodeKind.With:
                        {
                            var withData = allWiths[node.DataIdx];
                            var ident = allIdents[withData.IdentIdx];
                            var hasData = false;
                            object withVariable = null;
                            hasData = TryGetDataAccessorFunction(data, ident, out Func<object,object> withAccessor);
                            if (hasData)
                                withVariable = withAccessor.Invoke(data);
                            hasData = withVariable != null;
                            var children = hasData ?
                                allChildren[withData.ChildrenIdxTrue]
                                : allChildren[withData.ChildrenIdxFalse];
                            for (var i = children.Count - 1; i >= 0; i--)
                            {
                                stack.Push((children[i], hasData ? withVariable : data));
                            }
                        }
                        break;
                    case NodeKind.Range:
                        {
                            var rangeData = allRanges[node.DataIdx];
                            var ident = allIdents[rangeData.IdentIdx];
                            var rangeAccessor = GetDataAccessorFunction(data, ident);
                            var rangeVariable = rangeAccessor.Invoke(data);
                            if (!IsCollection(rangeVariable))
                                throw new ArgumentException(
                                    $"{ident} needs to implement ICollection to be used in range expression");
                            var children = allChildren[rangeData.ChildrenIdx];
                            foreach (var rangeObj in ((ICollection)rangeVariable).OfType<object>().Reverse())
                            {
                                for (var i = children.Count - 1; i >= 0; i--)
                                {
                                    stack.Push((children[i], rangeObj));
                                }
                            }
                        }
                        break;
                    case NodeKind.Block:
                        {
                            var blockData = allBlocks[node.DataIdx];
                            var ident = allIdents[blockData.DataIdentIdx];
                            var dataAccessor = GetDataAccessorFunction(data, ident);
                            var dataVariable = dataAccessor.Invoke(data);
                            var children = allChildren[blockData.ChildrenIdx];
                            for (var i = children.Count - 1; i >= 0; i--)
                            {
                                stack.Push((children[i], dataVariable));
                            }
                        }
                        break;
                    case NodeKind.Template:
                        {
                            var templateData = allTemplateCalls[node.DataIdx];
                            var ident = allIdents[templateData.IdentIdx];
                            var dataAccessor = GetDataAccessorFunction(data, ident);
                            var dataVariable = dataAccessor.Invoke(data);
                            var childrenIdx = FindTemplateChildrenIdx(templateData.Name);
                            var children = allChildren[childrenIdx];
                            for (var i = children.Count - 1; i >= 0; i--)
                            {
                                stack.Push((children[i], dataVariable));
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            return sb.ToString();
        }

        internal int FindTemplateChildrenIdx(string name)
        {
            var idx = allRoots.FindIndex(x => x.Name == name);
            if (idx > -1) return allRoots[idx].ChildrenIdx;
            idx = allDefines.FindIndex(x => x.Name == name);
            if (idx > -1) return allDefines[idx].ChildrenIdx;
            idx = allBlocks.FindIndex(x => x.Name == name);
            if (idx > -1) return allBlocks[idx].ChildrenIdx;
            throw new ArgumentException($"Template '{name}' was not found");
        }

        static bool IsCollection(object o) =>
            o.GetType().GetInterfaces().Any(i => i.Name == "ICollection");

        /// <summary>
        /// Compile a lambda function to access a member of a object.
        /// Ex: var customer = new { Name = "John", Address = new { Town = "Oslo" }}
        ///     var accessor = GenerateGetterLamda(customer, ".Address.Town")
        ///     Console.WriteLine(accessor.Invoke(customer))  => 'Oslo'
        /// </summary>
        private static Func<object, object> CreateAccessorFunc(object data, string path)
        {
            var objParameterExpr = Expression.Parameter(typeof(object), "instance");
            var instanceExpr = Expression.Convert(objParameterExpr, data.GetType());

            Expression current = instanceExpr;

            foreach (var propertyName in path.Split('.'))
            {
                if (string.IsNullOrEmpty(propertyName)) continue;
                var openBracket = propertyName.LastIndexOf('[');
                var closingBracket = propertyName.LastIndexOf(']');

                // Check and perform array access
                if (openBracket > 0 && closingBracket > 0)
                {
                    var propertyNameStr  = propertyName.Substring(0, openBracket);
                    var indexString = propertyName.Substring(openBracket + 1, closingBracket - openBracket - 1);
                    int.TryParse(indexString, out var index);
                    current = Expression.PropertyOrField(current, propertyNameStr);
                    current = Expression.ArrayIndex(current, Expression.Constant(index));
                }
                // Normal case
                else 
                { 
                    current = Expression.PropertyOrField(current, propertyName);
                }
            }

            current = Expression.Convert(current, typeof(object));

            return Expression.Lambda<Func<object, object>>(current, objParameterExpr).Compile();
        }

        internal bool TryGetDataAccessorFunction(object data, string path, out Func<object, object> accessor)
        {
            accessor = null;
            try {
                accessor = GetDataAccessorFunction(data,path);
                return true;
            } catch {
                return false;
            }
        }

        internal Func<object, object> GetDataAccessorFunction(object data, string path)
        {
            if (data is null && path == ".")
            { 
                return (x) => null;
            }

            // Check if accessor function exists
            var typ = data.GetType();
            if (accessors.TryFind(typ, path, out Func<object, object> x)) 
                return x;

            // else create accessor function
            var accessor = CreateAccessorFunc(data, path);
            // and store it
            accessors.Add(typ, path, accessor);
            return accessor;
        }

        // Big ugly print method for debugging
        private string PrintNode(Node node, int level)
        {
            var sb = new StringBuilder();
            sb.Append(new string('\t', level));
            sb.AppendLine("Node {");
            sb.Append(new string('\t', level));
            sb.Append("\tType");
            switch (node.Kind)
            {
                case NodeKind.Root:
                    {
                        sb.AppendLine("\tRoot");
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\tChildren [");
                        foreach (var child in allChildren[node.DataIdx])
                        {
                            sb.AppendLine(PrintNode(child, level + 2));
                        }
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\t]");
                    }
                    break;
                case NodeKind.End:
                    {
                        sb.AppendLine("\tEnd");
                    }
                    break;
                case NodeKind.Else:
                    {
                        sb.AppendLine("\tElse");
                    }
                    break;
                case NodeKind.Html:
                    {
                        sb.AppendLine("\tHtml");
                        sb.AppendLine("```");
                        sb.AppendLine(lexer.GetText(tokens[node.TokenIdx]));
                        sb.AppendLine("```");
                    }
                    break;
                case NodeKind.Ident:
                    {
                        sb.AppendLine("\tIdent");
                    }
                    break;
                case NodeKind.Define:
                    {
                        sb.AppendLine("\tDefine");
                        sb.Append(new string('\t', level));
                        sb.AppendLine($"\tName\t{allDefines[node.DataIdx].Name}");
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\tChildren [");
                        var childrenIdx = allDefines[node.DataIdx].ChildrenIdx;
                        foreach (var child in allChildren[childrenIdx])
                        {
                            sb.AppendLine(PrintNode(child, level + 2));
                        }
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\t]");
                    }
                    break;
                case NodeKind.Block:
                    {
                        var blockData = allBlocks[node.DataIdx];
                        sb.AppendLine("\tBlock");
                        sb.Append(new string('\t', level));
                        sb.AppendLine($"\tName\t{blockData.Name}");
                        sb.Append(new string('\t', level));
                        sb.Append($"\tDataIdent\t");
                        if (blockData.DataIdentIdx > -1)
                        {
                            sb.AppendJoin("", allIdents[blockData.DataIdentIdx]);
                        }
                        else
                        {
                            sb.Append("null");
                        }
                        sb.AppendLine();
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\tChildren [");
                        var childrenIdx = blockData.ChildrenIdx;
                        foreach (var child in allChildren[childrenIdx])
                        {
                            sb.AppendLine(PrintNode(child, level + 2));
                        }
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\t]");
                    }
                    break;
                case NodeKind.Template:
                    {
                        var templateData = allTemplateCalls[node.DataIdx];
                        sb.AppendLine("\tTemplate");
                        sb.Append(new string('\t', level));
                        sb.AppendLine($"\tName\t{templateData.Name}");
                        sb.Append(new string('\t', level));
                        sb.Append($"\tDataIdent\t");
                        sb.AppendJoin("", allIdents[templateData.IdentIdx]);
                        sb.AppendLine();
                    }
                    break;
                case NodeKind.Range:
                    {
                        var rangeData = allRanges[node.DataIdx];
                        sb.AppendLine("\t\tRange");
                        sb.Append(new string('\t', level));
                        sb.Append($"\tDataIdent\t");
                        sb.AppendJoin("", allIdents[rangeData.IdentIdx]);
                        sb.AppendLine();
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\tChildren [");
                        foreach (var child in allChildren[rangeData.ChildrenIdx])
                        {
                            sb.AppendLine(PrintNode(child, level + 2));
                        }
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\t]");
                    }
                    break;
                case NodeKind.If:
                    {
                        var ifData = allIfs[node.DataIdx];
                        sb.AppendLine("\tIf");
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\tChildren if true [");
                        foreach (var child in allChildren[ifData.ChildrenIdxTrue])
                        {
                            sb.AppendLine(PrintNode(child, level + 2));
                        }
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\t]");
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\tChildren if false [");
                        foreach (var child in allChildren[ifData.ChildrenIdxFalse])
                        {
                            sb.AppendLine(PrintNode(child, level + 2));
                        }
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\t]");
                    }
                    break;
                case NodeKind.Expression:
                    {
                        sb.AppendLine("\t\tExpression");
                        sb.Append(new string('\t', level));
                        sb.Append($"\tDataIdent\t");
                        sb.AppendJoin("", allIdents[node.DataIdx]);
                        sb.AppendLine();
                    }
                    break;
                case NodeKind.Error:
                    sb.AppendLine("\tError");
                    break;
            }
            sb.Append(new string('\t', level));
            sb.AppendLine("}");
            return sb.ToString();
        }
    }


    internal enum NodeKind
    {
        Root,
        End,
        Else,
        Html,
        Ident,
        Define,
        Block,
        With,
        Template,
        Range,
        If,
        Expression,
        Comment,
        Error
    }

    internal struct Node
    {
        internal int TokenIdx;
        internal int DataIdx;
        internal NodeKind Kind;

        public Node(int tokenIndex, int dataIndex, NodeKind kind)
        {
            TokenIdx = tokenIndex;
            DataIdx = dataIndex;
            Kind = kind;
        }
    }

    internal struct RootData
    {
        internal string Name;
        internal int ChildrenIdx;

        public RootData(string name, int childrenIdx)
        {
            Name = name;
            ChildrenIdx = childrenIdx;
        }
    }

    internal struct DefineData
    {
        internal string Name;
        internal int ChildrenIdx;

        public DefineData(string name, int childrenIdx)
        {
            Name = name;
            ChildrenIdx = childrenIdx;
        }
    }

    internal struct BlockData
    {
        internal string Name;
        internal int DataIdentIdx;
        internal int ChildrenIdx;

        public BlockData(string name, int dataIdentIdx, int childrenIdx)
        {
            Name = name;
            DataIdentIdx = dataIdentIdx;
            ChildrenIdx = childrenIdx;
        }
    }

    internal struct TemplateData
    {
        internal string Name;
        internal int IdentIdx;
    }

    internal struct WithData
    {
        internal int IdentIdx;
        internal int ChildrenIdxTrue;
        internal int ChildrenIdxFalse;
    }

    internal struct RangeData
    {
        internal int IdentIdx;
        internal int ChildrenIdx;
    }

    internal struct IfData
    {
        internal int IdentIdx;
        internal int ChildrenIdxTrue;
        internal int ChildrenIdxFalse;

        public IfData(int identIdx, int childrenIdxTrue, int childrenIdxFalse)
        {
            IdentIdx = identIdx;
            ChildrenIdxTrue = childrenIdxTrue;
            ChildrenIdxFalse = childrenIdxFalse;
        }

    }

    /// <summary>
    /// Store / Find Accessor functions used in the template
    /// </summary>
    internal class DataAccessors
    {
        private readonly List<Type> types;
        private readonly List<string> paths;
        private readonly List<Func<object, object>> accessors;

        public DataAccessors()
        {
            types = new List<Type>();
            paths = new List<string>();
            accessors = new List<Func<object, object>>();
        }

        public void Add(Type type, string path, Func<object, object> accessor)
        {
            types.Add(type);
            paths.Add(path);
            accessors.Add(accessor);
        }

        public bool TryFind(Type type, string path, out Func<object, object> accessor)
        {
            accessor = null;
            for (int i = 0; i < types.Count; i++)
            {
                if (types[i] == type)
                {
                    if (paths[i] == path)
                    {
                        accessor = accessors[i];
                        return true;
                    }
                }
            }
            return false;
        }
    }

}
