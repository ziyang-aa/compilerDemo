using System;
using System.Collections.Generic;
using System.Linq;

namespace compilerDEMO
{
    // 完整版：词法分析和递归下降语法分析共用，避免 TAC 与 AST 的文法不一致。
    internal sealed class FullCompiler
    {
        internal sealed class CompileError : Exception
        {
            public CompileError(string message) : base(message) { }
        }

        internal sealed class Token
        {
            public string Kind, Text;
            public int Line, Column;
            public Token(string kind, string text, int line, int column)
            { Kind = kind; Text = text; Line = line; Column = column; }
            public override string ToString() { return "(" + Kind + "," + Text + ")"; }
        }

        internal sealed class Node
        {
            public string Name, LiteralType;
            public Token Token;
            public readonly List<Node> Children = new List<Node>();
            public Node(string name, Token token, params Node[] children)
            { Name = name; Token = token; Children.AddRange(children); }

            public string ToPrefix()
            {
                var parts = new List<string>();
                AppendPrefix(parts);
                return String.Join("  ", parts);
            }
            private void AppendPrefix(List<string> parts)
            {
                parts.Add(Name);
                foreach (Node child in Children) child.AppendPrefix(parts);
            }
        }

        internal sealed class Quadruple
        {
            public string Op, Left, Right, Result;
            public Quadruple(string op, string left, string right, string result)
            { Op = op; Left = left; Right = right; Result = result; }
            public override string ToString()
            { return "(" + Op + ", " + Left + ", " + Right + ", " + Result + ")"; }
        }

        private readonly string source;
        private int offset, line = 1, column = 1, position;
        private readonly List<Token> tokens = new List<Token>();
        internal readonly List<string> Trace = new List<string>();
        internal readonly List<Quadruple> Code = new List<Quadruple>();
        private readonly Dictionary<string, string> symbols = new Dictionary<string, string>();
        private int temporary;

        internal FullCompiler(string source) { this.source = source ?? ""; }
        private bool AtEnd { get { return offset >= source.Length; } }
        private char Current { get { return AtEnd ? '\0' : source[offset]; } }
        private static bool Letter(char c) { return c >= 'a' && c <= 'z'; }
        private static bool Digit(char c) { return c >= '0' && c <= '9'; }
        private void Advance()
        {
            if (Current == '\n') { line++; column = 1; }
            else column++;
            offset++;
        }
        private CompileError Error(string message, int atLine, int atColumn)
        { return new CompileError("第 " + atLine + " 行，第 " + atColumn + " 列：" + message); }
        private CompileError Error(string message, Token token)
        { return Error(message, token.Line, token.Column); }

        internal List<Token> Lex()
        {
            if (tokens.Count != 0) return tokens;
            while (!AtEnd)
            {
                if (Char.IsWhiteSpace(Current)) { Advance(); continue; }
                int start = offset, startLine = line, startColumn = column;
                string kind;
                if (Current == '$')
                {
                    Advance();
                    if (!Letter(Current)) throw Error("标识符的 $ 后必须是小写字母", startLine, startColumn);
                    while (Letter(Current) || Digit(Current)) Advance();
                    if (Current != '$') throw Error("标识符缺少结束符 $", startLine, startColumn);
                    Advance();
                    kind = "标识符";
                }
                else if (Digit(Current))
                {
                    while (Digit(Current)) Advance();
                    kind = "整数";
                }
                else if (Current == '"')
                {
                    Advance();
                    while (Letter(Current) || Digit(Current)) Advance();
                    if (Current != '"') throw Error("字符串只能包含小写字母和数字，并须以双引号结束", startLine, startColumn);
                    Advance();
                    kind = "字符串";
                }
                else if (Letter(Current))
                {
                    // 关键字之间允许不写空格，例如 trueandfalse。
                    string word = null;
                    foreach (string keyword in new[] { "string", "while", "false", "true", "else",
                                                       "bool", "and", "not", "int", "or", "if" })
                    {
                        if (offset + keyword.Length <= source.Length &&
                            String.CompareOrdinal(source, offset, keyword, 0, keyword.Length) == 0)
                        { word = keyword; break; }
                    }
                    if (word == null) throw Error("无法识别的关键字", startLine, startColumn);
                    for (int i = 0; i < word.Length; i++) Advance();
                    switch (word)
                    {
                        case "int": case "bool": case "string": kind = "变量说明"; break;
                        case "if": case "else": case "while": case "or": case "and":
                        case "not": case "true": case "false": kind = word; break;
                        default: throw new InvalidOperationException("未知关键字 " + word);
                    }
                }
                else
                {
                    char c = Current;
                    Advance();
                    switch (c)
                    {
                        case ';': kind = "分号"; break;
                        case ',': kind = "逗号"; break;
                        case '(': kind = "左括号"; break;
                        case ')': kind = "右括号"; break;
                        case '{': kind = "begin"; break;
                        case '}': kind = "end"; break;
                        case '+': case '-': kind = "加法"; break;
                        case '*': case '/': case '%': kind = "乘法"; break;
                        case ':':
                            if (Current == '=') { Advance(); kind = "赋值号"; }
                            else kind = "冒号";
                            break;
                        case '<':
                            if (Current == '=' || Current == '>') Advance();
                            kind = "关系"; break;
                        case '>':
                            if (Current == '=') Advance();
                            kind = "关系"; break;
                        case '=':
                            if (Current != '=') throw Error("单独的 = 不是有效运算符", startLine, startColumn);
                            Advance(); kind = "关系"; break;
                        default: throw Error("无法识别的字符 " + c, startLine, startColumn);
                    }
                }
                tokens.Add(new Token(kind, source.Substring(start, offset - start), startLine, startColumn));
            }
            tokens.Add(new Token("#", "#", line, column));
            return tokens;
        }

        private Token Look { get { return tokens[position]; } }
        private bool Is(string kind) { return Look.Kind == kind; }
        private Token Take(string kind)
        {
            Token token = Look;
            if (token.Kind != kind)
                throw Error("期望 " + kind + "，实际遇到 " + token.Kind + " " + token.Text, token);
            position++;
            Trace.Add("匹配 " + token);
            return token;
        }
        private Node Make(string name, Token token, params Node[] children) { return new Node(name, token, children); }
        private Node Leaf(Token t, string type = null) { return new Node(t.Text, t) { LiteralType = type }; }

        internal Node Parse()
        {
            Lex(); position = 0; Trace.Clear();
            Node declarations = ParseDeclareList();
            Node statements = ParseSeqList();
            Take("#");
            return Make("topLevel", declarations.Token, declarations, statements);
        }
        // A、B、C、D、E、F 沿用原项目，G、H 分别处理 and 和加减。
        private Node ParseDeclareList()
        {
            Token t = Look;
            Node first = ParseDeclare();
            Take("分号");
            return Make("declareList", t, first, ParseA());
        }
        private Node ParseA()
        {
            Token t = Look;
            if (!Is("变量说明")) return Make("emptyDeclareList", t);
            Node declaration = ParseDeclare(); Take("分号");
            return Make("linkDeclareList", t, declaration, ParseA());
        }
        private Node ParseDeclare()
        {
            Token type = Take("变量说明");
            return Make("declare", type, Leaf(type), ParseVarList());
        }
        private Node ParseVarList()
        {
            Token id = Take("标识符");
            return Make("varList", id, Leaf(id), ParseB());
        }
        private Node ParseB()
        {
            Token t = Look;
            if (!Is("逗号")) return Make("emptyVarList", t);
            Take("逗号"); Token id = Take("标识符");
            return Make("linkVarList", t, Leaf(id), ParseB());
        }
        private Node ParseSeqList()
        {
            Token t = Look;
            Node first = ParseStatement();
            return Make("seqList", t, first, ParseC());
        }
        private Node ParseC()
        {
            Token t = Look;
            if (!Is("分号")) return Make("emptySeqList", t);
            Take("分号");
            return Make("linkSeqList", t, ParseStatement(), ParseC());
        }
        private Node ParseStatement()
        {
            Token t = Look;
            if (Is("标识符"))
            {
                Token id = Take("标识符"); Take("赋值号");
                return Make("assign", t, Leaf(id), ParseExpression());
            }
            if (Is("if"))
            {
                Take("if"); Take("左括号"); Node condition = ParseExpression(); Take("右括号");
                Node yes = ParseNested(); Take("else"); Node no = ParseNested();
                return Make("ifThenElse", t, condition, yes, no);
            }
            if (Is("while"))
            {
                Take("while"); Take("左括号"); Node condition = ParseExpression(); Take("右括号");
                Take("冒号");
                return Make("while", t, condition, ParseNested());
            }
            throw Error("语句不能以 " + Look.Text + " 开头", Look);
        }
        private Node ParseNested()
        {
            if (Is("begin"))
            {
                Take("begin"); Node body = ParseSeqList(); Take("end"); return body;
            }
            Node statement = ParseStatement(); Take("分号"); return statement;
        }
        private Node ParseExpression() { return ParseD(ParseConjunction()); }
        private Node ParseD(Node left)
        {
            while (Is("or")) { Token op = Take("or"); left = Make("or", op, left, ParseConjunction()); }
            return left;
        }
        private Node ParseConjunction() { return ParseG(ParseInversion()); }
        private Node ParseG(Node left)
        {
            while (Is("and")) { Token op = Take("and"); left = Make("and", op, left, ParseInversion()); }
            return left;
        }
        private Node ParseInversion()
        {
            if (Is("not")) { Token t = Take("not"); return Make("not", t, ParseInversion()); }
            return ParseRelation();
        }
        private Node ParseRelation() { return ParseE(ParseMath()); }
        private Node ParseE(Node left)
        {
            if (!Is("关系")) return left;
            Token op = Take("关系");
            return Make(OpName(op.Text), op, left, ParseMath());
        }
        private Node ParseMath() { return ParseH(ParseTerm()); }
        private Node ParseH(Node left)
        {
            while (Is("加法")) { Token op = Take("加法"); left = Make(OpName(op.Text), op, left, ParseTerm()); }
            return left;
        }
        private Node ParseTerm() { return ParseF(ParseFactor()); }
        private Node ParseF(Node left)
        {
            while (Is("乘法")) { Token op = Take("乘法"); left = Make(OpName(op.Text), op, left, ParseFactor()); }
            return left;
        }
        private Node ParseFactor()
        {
            Token t = Look;
            if (Is("标识符")) return Leaf(Take("标识符"));
            if (Is("整数")) return Leaf(Take("整数"), "int");
            if (Is("字符串")) return Leaf(Take("字符串"), "string");
            if (Is("true") || Is("false")) return Leaf(Take(t.Kind), "bool");
            if (Is("左括号"))
            { Take("左括号"); Node e = ParseExpression(); Take("右括号"); return e; }
            throw Error("期望表达式，实际遇到 " + t.Text, t);
        }
        private static string OpName(string op)
        {
            switch (op)
            {
                case "<": return "lt"; case ">": return "gt"; case "<>": return "noteq";
                case "<=": return "lte"; case ">=": return "gte"; case "==": return "eq";
                case "+": return "add"; case "-": return "sub"; case "*": return "mul";
                case "/": return "div"; case "%": return "mod";
                default: throw new InvalidOperationException("未知运算符 " + op);
            }
        }

        private sealed class Value
        {
            public string Name, Type;
            public bool IsIntegerLiteral;
            public Value(string name, string type, bool literal = false)
            { Name = name; Type = type; IsIntegerLiteral = literal; }
        }
        private int Emit(string op, string a, string b, string result)
        {
            int index = Code.Count;
            Code.Add(new Quadruple(op, a, b, result));
            return index;
        }
        private void Patch(int index, int target)
        {
            Code[index].Result = target.ToString();
        }
        internal void Generate(Node program)
        {
            Code.Clear(); symbols.Clear(); temporary = 0;
            DeclareList(program.Children[0]);
            GenerateStatement(program.Children[1]);
        }
        private void DeclareList(Node list)
        {
            if (list.Name == "emptyDeclareList") return;
            Node declaration = list.Children[0];
            DeclareVariables(declaration.Children[1], declaration.Children[0].Name);
            DeclareList(list.Children[1]);
        }
        private void DeclareVariables(Node list, string type)
        {
            if (list.Name == "emptyVarList") return;
            Node id = list.Children[0];
            if (symbols.ContainsKey(id.Name)) throw Error("重复声明标识符 " + id.Name, id.Token);
            symbols.Add(id.Name, type);
            Emit("declare", type, "null", id.Name + ".type");
            DeclareVariables(list.Children[1], type);
        }
        private string SymbolType(Node id)
        {
            string type;
            if (!symbols.TryGetValue(id.Name, out type)) throw Error("未声明的标识符 " + id.Name, id.Token);
            return type;
        }
        private void GenerateStatement(Node n)
        {
            switch (n.Name)
            {
                case "seqList": case "linkSeqList":
                    GenerateStatement(n.Children[0]); GenerateStatement(n.Children[1]); return;
                case "emptySeqList": return;
                case "assign":
                    string type = SymbolType(n.Children[0]);
                    Value value = Expression(n.Children[1]);
                    if (type != value.Type) throw Error("赋值类型不匹配：" + type + " 与 " + value.Type, n.Token);
                    Emit("assign", value.Name, "null", n.Children[0].Name); return;
                case "ifThenElse":
                    Value condition = Expression(n.Children[0]);
                    if (condition.Type != "bool") throw Error("if 条件必须是 bool", n.Token);
                    Emit("jnz", condition.Name, "null", (Code.Count + 2).ToString());
                    int otherwise = Emit("jump", "null", "null", "0");
                    GenerateStatement(n.Children[1]);
                    int exit = Emit("jump", "null", "null", "0");
                    Patch(otherwise, Code.Count);
                    GenerateStatement(n.Children[2]);
                    Patch(exit, Code.Count); return;
                case "while":
                    int start = Code.Count;
                    Value loopCondition = Expression(n.Children[0]);
                    if (loopCondition.Type != "bool") throw Error("while 条件必须是 bool", n.Token);
                    Emit("jnz", loopCondition.Name, "null", (Code.Count + 2).ToString());
                    int end = Emit("jump", "null", "null", "0");
                    GenerateStatement(n.Children[1]);
                    Emit("jump", "null", "null", start.ToString());
                    Patch(end, Code.Count); return;
                default: throw new InvalidOperationException("未知语句 " + n.Name);
            }
        }
        private Value Expression(Node n)
        {
            if (n.Children.Count == 0)
                return new Value(n.Name, n.LiteralType ?? SymbolType(n), n.LiteralType == "int");
            Value a = Expression(n.Children[0]);
            if (n.Name == "not")
            {
                if (a.Type != "bool") throw Error("not 的操作数必须是 bool", n.Token);
                string name = "T" + temporary++;
                Emit("not", a.Name, "null", name);
                return new Value(name, "bool");
            }
            Value b = Expression(n.Children[1]);
            string resultType = null;
            if (n.Name == "or" || n.Name == "and")
            {
                if (a.Type == "bool" && b.Type == "bool") resultType = "bool";
            }
            else if (n.Name == "lt" || n.Name == "gt" || n.Name == "noteq" ||
                     n.Name == "lte" || n.Name == "gte" || n.Name == "eq")
            {
                if (a.Type == b.Type) resultType = "bool";
            }
            else if (n.Name == "add" && a.Type == "string" && b.Type == "string")
                resultType = "string";
            else if (n.Name == "mul" && a.Type == "string" && b.Type == "int" && b.IsIntegerLiteral)
                resultType = "string";
            else if (a.Type == b.Type && (a.Type == "int" || a.Type == "bool"))
                resultType = "int";
            if (resultType == null)
                throw Error("运算 " + n.Name + " 不支持 " + a.Type + " 和 " + b.Type, n.Token);
            if ((n.Name == "div" || n.Name == "mod") &&
                ((b.IsIntegerLiteral && b.Name.All(c => c == '0')) ||
                 (b.Type == "bool" && b.Name == "false")))
                throw Error("除数不能为 0", n.Token);
            string temp = "T" + temporary++;
            Emit(n.Name, a.Name, b.Name, temp);
            return new Value(temp, resultType);
        }
    }
}
