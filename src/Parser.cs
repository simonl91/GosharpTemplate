using System.Diagnostics;
using System.Reflection;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

namespace GosharpTemplate
{
    using Nodes = List<Node>;
    using Ident = List<string>;
    using System.Text;

    internal class Parser
    {
        // Data for each node type, use Node.DataIdx to access from node;
        internal List<RootData> allRoots;
        internal List<IfData> allIfs;
        internal List<Ident> allIdents;
        internal List<DefineData> allDefines;
        internal List<Nodes> allChildren;
        internal List<TemplateData> allTemplateCalls;
        internal List<RangeData> allRanges;
        internal List<BlockData> allBlocks;
        internal Ident allHtml;

        //private List<string> errors;
        private List<Token> tokens;
        private int pos;
        private Lexer lexer;

#pragma warning disable 8618
        public Parser()
        {
            initializeState();
        }

        internal void Parse(Lexer lex, string rootName)
        {
            pos = tokens.Count;
            lexer = lex;
            tokens.AddRange(lex.Lex());

            var tree = CreateRootNode(rootName);
            tree.Kind = NodeKind.Root;
            parseTemplate(tree);
            //foreach (var tok in tokens)
            //{
            //    lexer.PrintToken(tok);
            //}
            //Console.WriteLine(printNode(tree, 0));
        }

        private void initializeState()
        {
            pos = 0;
            tokens = new List<Token>();
            allIfs = new List<IfData>();
            allIdents = new List<Ident>();
            allDefines = new List<DefineData>();
            allChildren = new List<Nodes>();
            allTemplateCalls = new List<TemplateData>();
            allRanges = new List<RangeData>();
            allBlocks = new List<BlockData>();
            allHtml = new Ident();
            allRoots = new List<RootData>();

            allIdents.Add(new Ident() { "." });
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
            sb.Append($"Error at {line} {col}-{col2} '{lexer.GetText(token)}': ");
            sb.AppendLine(message);
            return sb.ToString();
        }

        private void parseTemplate(Node rootNode)
        {
            var childrenIdx = rootNode.Kind switch
            {
                NodeKind.Root => allRoots[rootNode.DataIdx].ChildrenIdx,
                NodeKind.Define => allDefines[rootNode.DataIdx].ChildrenIdx,
                NodeKind.Block => allBlocks[rootNode.DataIdx].ChildrenIdx,
                NodeKind.Range => allRanges[rootNode.DataIdx].ChildrenIdx,
                NodeKind.If => allIfs[rootNode.DataIdx].ChildrenIdxTrue,
                _ => -1
            };
            Debug.Assert(childrenIdx >= 0, ErrorMessage($"{rootNode.Kind}, cant have children"));

            for (int i = 0; i < tokens.Count; i++)
            {
                switch (nth(0))
                {
                    case TokenKind.Html:
                        allChildren[childrenIdx]
                            .Add(CreateHtmlNode());
                        advance();
                        break;
                    case TokenKind.OpenBraceDouble:
                        var node = parseExpression();
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
                    case TokenKind.Error:
                        Debug.Assert(false,
                            ErrorMessage($"{lexer.GetErrorData(tokens[pos].DataIdx)}"));
                        return;
                    default:
                        Debug.Assert(false,
                            ErrorMessage($"Unexpedted token {nth(0)}, expected Html or '{{'"));
                        return;
                }
            }
        }

        private Node parseExpression()
        {
            expect(TokenKind.OpenBraceDouble);
            switch (nth(0))
            {
                case TokenKind.KeywordEnd:
                    return parseEnd();
                case TokenKind.KeywordElse:
                    return parseElse();
                case TokenKind.KeywordRange:
                    return parseRange();
                case TokenKind.KeywordIf:
                    return parseIf();
                case TokenKind.KeywordBlock:
                    return parseBlock();
                case TokenKind.KeywordDefine:
                    return parseDefine();
                case TokenKind.KeywordTemplate:
                    return parseTemplateCall();
                case TokenKind.Dot:
                    var identNode = parseIdent();
                    identNode.Kind = NodeKind.Expression;
                    expect(TokenKind.ClosingBraceDouble);
                    return identNode;
                default:
                    Debug.Assert(false,
                        ErrorMessage($"not a valid expression"));
                    return new Node();
            }
        }

        private Node parseIf()
        {
            var start = pos;
            expect(TokenKind.KeywordIf);
            var identIdx = parseIdent().DataIdx;
            var node = CreateIfNode(start, identIdx);
            expect(TokenKind.ClosingBraceDouble);
            parseTemplate(node);
            return node;
        }

        private Node parseRange()
        {
            var start = pos;
            expect(TokenKind.KeywordRange);
            var identIdx = parseIdent().DataIdx;
            var node = CreateRangeNode(start, identIdx);
            expect(TokenKind.ClosingBraceDouble);
            parseTemplate(node);
            return node;
        }

        private Node parseTemplateCall()
        {
            var start = pos;
            expect(TokenKind.KeywordTemplate);
            var stringToken = expectToken(TokenKind.String);
            var identIdx = 0;
            if (nth(0) == TokenKind.Dot)
            {
                identIdx = parseIdent().DataIdx;
            }
            var node = CreateTemplateNode(lexer.GetTextFromStringToken(stringToken), start, identIdx);
            expect(TokenKind.ClosingBraceDouble);
            return node;
        }

        private Node parseBlock()
        {
            var start = pos;
            expect(TokenKind.KeywordBlock);
            var stringToken = expectToken(TokenKind.String);
            var identIdx = 0;
            if (nth(0) == TokenKind.Dot)
            {
                identIdx = parseIdent().DataIdx;
            }
            var node = CreateBlockNode(lexer.GetTextFromStringToken(stringToken), identIdx, start);
            expect(TokenKind.ClosingBraceDouble);
            parseTemplate(node);
            return node;
        }

        private Node parseDefine()
        {
            var start = pos;
            expect(TokenKind.KeywordDefine);
            var stringToken = expectToken(TokenKind.String);
            expect(TokenKind.ClosingBraceDouble);
            var node = CreateDefineNode(lexer.GetTextFromStringToken(stringToken), start);
            parseTemplate(node);
            return node;
        }

        private Node parseIdent()
        {
            expect(TokenKind.Dot);
            var node = CreateIdentNode();
            allIdents[node.DataIdx].Add(".");
            while (!at(TokenKind.ClosingBraceDouble))
            {
                switch (nth(0))
                {
                    case TokenKind.Dot:
                        if (allIdents[node.DataIdx].Last() == ".")
                            Debug.Assert(false,
                                ErrorMessage("Two '.' in a row, expected identifier or '}}'"));
                        else
                            allIdents[node.DataIdx].Add(".");
                        break;
                    case TokenKind.Ident:
                        allIdents[node.DataIdx].Add(lexer.GetText(tokens[pos]));
                        break;
                    default:
                        Debug.Assert(false,
                            ErrorMessage($"Expected '.', identifier or '}}'"));
                        break;
                }
                advance();
            }
            return node;
        }

        private Node parseEnd()
        {
            var curPos = pos;
            expect(TokenKind.KeywordEnd);
            expect(TokenKind.ClosingBraceDouble);
            return CreateNodeAt(curPos, NodeKind.End);
        }

        private Node parseElse()
        {
            var curPos = pos;
            expect(TokenKind.KeywordElse);
            expect(TokenKind.ClosingBraceDouble);
            return CreateNodeAt(curPos, NodeKind.Else);
        }

        private void advance()
        {
            Debug.Assert(!eof());
            pos += 1;
        }

        private bool eof()
        {
            return pos == tokens.Count;
        }

        private TokenKind nth(int lookahead)
        {
            if (pos + lookahead >= tokens.Count) return TokenKind.Eof;
            return tokens[pos + lookahead].Kind;
        }

        private bool at(TokenKind kind)
        {
            return nth(0) == kind;
        }

        private bool eat(TokenKind kind)
        {
            if (at(kind))
            {
                advance();
                return true;
            }
            else
            {
                return false;
            }
        }

        private void expect(TokenKind kind)
        {
            if (eat(kind))
            {
                return;
            }
            Debug.Assert(false, ErrorMessage($"expected '{kind}'"));
        }

        private Token expectToken(TokenKind kind)
        {
            if (eat(kind))
            {
                return tokens[pos - 1];
            }
            Debug.Assert(false, ErrorMessage($"expected '{kind}'"));
            return new Token { };
        }


        private Node CreateHtmlNode()
        {
            var dataIdx = allHtml.Count;
            allHtml.Add(lexer.GetText(tokens[pos]));
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
            var ident = new Ident();
            var dataIdx = allIdents.Count;
            allIdents.Add(ident);
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
            var sb = new StringBuilder(4096);
            var stack = new Stack<(Node, object)>(512);
            stack.Push((rootNode, rootData));
            while (stack.Count > 0)
            {
                var (node, data) = stack.Pop();
                switch (node.Kind)
                {
                    case NodeKind.Expression:
                        {
                            var ident = allIdents[node.DataIdx];
                            sb.Append(resolveObjectMembers(data, ident).ToString() ?? "");
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
                            var ifVariable = resolveObjectMembers(data, ident);
                            Debug.Assert(ifVariable.GetType() == typeof(bool),
                                $"expected boolean, got {ifVariable.GetType()}");
                            var children = (bool)ifVariable ?
                                allChildren[ifData.ChildrenIdxTrue]
                                : allChildren[ifData.ChildrenIdxFalse];
                            //var sb = new StringBuilder(100);
                            foreach (var child in children)
                            {
                                stack.Push((child, data));
                            }
                            //return sb.ToString();
                        }
                        break;
                    case NodeKind.Range:
                        {
                            var rangeData = allRanges[node.DataIdx];
                            var ident = allIdents[rangeData.IdentIdx];
                            var rangeVariable = resolveObjectMembers(data, ident);
                            var rangeObjType = rangeVariable.GetType().GetGenericArguments()[0];
                            Debug.Assert(isCollection(rangeVariable),
                                $"{ident} needs to be IEnumerable to be used as range");
                            var children = allChildren[rangeData.ChildrenIdx];
                            foreach (var rangeObj in (ICollection)rangeVariable)
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
                            var dataVariable = resolveObjectMembers(data, ident);
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
                            var dataVariable = resolveObjectMembers(data, ident);
                            var childrenIdx = findTemplateChildrenIdx(templateData.Name);
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

        internal int findTemplateChildrenIdx(string name)
        {
            var idx = allRoots.FindIndex(x => x.Name == name);
            if (idx > -1) return allRoots[idx].ChildrenIdx;
            idx = allDefines.FindIndex(x => x.Name == name);
            if (idx > -1) return allDefines[idx].ChildrenIdx;
            idx = allBlocks.FindIndex(x => x.Name == name);
            if (idx > -1) return allBlocks[idx].ChildrenIdx;
            Debug.Assert(false, $"Template '{name}' was not found");
            return -1;
        }

        static bool isCollection(object o) =>
            o.GetType().GetInterfaces().Any(i => i.Name == "ICollection");

        internal object resolveObjectMembers(object data, Ident path)
        {
            var idents = new Queue<string>(path);
            object newData = data;
            while (idents.Count > 0)
            {
                var ident = idents.Dequeue();
                if (ident == ".") continue;
                var type = newData.GetType();
                var member = type.GetMembers()
                    .Where(x =>
                        x.MemberType == MemberTypes.Field
                        || x.MemberType == MemberTypes.Property)
                    .FirstOrDefault(x => x.Name == ident);
                Debug.Assert(member != null, $"Object member '{ident}' does not exist");
                newData = member.MemberType switch
                {
                    MemberTypes.Property => type.GetProperty(ident)?.GetValue(newData),
                    MemberTypes.Field => type.GetField(ident)?.GetValue(newData),
                    _ => null
                };
                Debug.Assert(newData != null, "Object member '{ident}' is null");
            }
            return newData;
        }

        // Big ugly print method for debugging
        private string printNode(Node node, int level)
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
                            sb.AppendLine(printNode(child, level + 2));
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
                            sb.AppendLine(printNode(child, level + 2));
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
                            sb.AppendLine(printNode(child, level + 2));
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
                            sb.AppendLine(printNode(child, level + 2));
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
                            sb.AppendLine(printNode(child, level + 2));
                        }
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\t]");
                        sb.Append(new string('\t', level));
                        sb.AppendLine("\tChildren if false [");
                        foreach (var child in allChildren[ifData.ChildrenIdxFalse])
                        {
                            sb.AppendLine(printNode(child, level + 2));
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
        Template,
        Range,
        If,
        Expression,
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

}
