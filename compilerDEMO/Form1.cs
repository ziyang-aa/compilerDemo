using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace compilerDEMO
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void print(string str) { listBox1.Items.Add(str); }
        private void debug(string str) { listBox2.Items.Add(str); }

        private void runCompiler(int mode)
        {
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            resetCompiler(textBox1.Text);
            try
            {
                if (mode == 1)
                {
                    foreach (token current in tokenize()) print(current.ToString());
                    print("词法分析结束");
                    return;
                }
                node program = parseProgram();
                if (mode == 2)
                {
                    print("语法分析成功");
                    return;
                }
                translateTACProgram(program);
                if (mode == 3)
                {
                    for (int i = 0; i < tacTable.Count; i++)
                        print("(" + i + ") " + tacTable[i]);
                }
                else
                {
                    string output = program.toPrefix();
                    print(output);
                    string path = Path.Combine(Application.StartupPath, "outputAST.txt");
                    File.WriteAllText(path, output, Encoding.UTF8);
                    print("Save to " + path);
                }
            }
            catch (compilerException ex)
            {
                print("分析错误：" + ex.Message);
            }
            catch (IOException ex)
            {
                print("输出文件错误：" + ex.Message);
            }
            finally
            {
                foreach (string entry in trace) debug(entry);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            runCompiler(1);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            runCompiler(2);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            runCompiler(3);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            runCompiler(4);
        }

        internal sealed class compilerException : Exception
        {
            public compilerException(string message) : base(message) { }
        }

        internal sealed class token
        {
            public string type, value;
            public int line, column;
            public token(string kind, string text, int line, int column)
            { type = kind; value = text; this.line = line; this.column = column; }
            public override string ToString() { return "(" + type + "," + value + ")"; }
        }

        internal sealed class node
        {
            public string name, literalType;
            public token token;
            public readonly List<node> children = new List<node>();
            public node(string name, token token, params node[] children)
            { this.name = name; this.token = token; this.children.AddRange(children); }

            public string toPrefix()
            {
                var parts = new List<string>();
                appendPrefix(parts);
                return String.Join("  ", parts);
            }
            private void appendPrefix(List<string> parts)
            {
                parts.Add(name);
                foreach (node child in children) child.appendPrefix(parts);
            }
        }

        internal sealed class TAC
        {
            public string op, oprand1, oprand2, result;
            public TAC(string op, string left, string right, string result)
            { this.op = op; oprand1 = left; oprand2 = right; this.result = result; }
            public override string ToString()
            { return "(" + op + ", " + oprand1 + ", " + oprand2 + ", " + result + ")"; }
        }

        private string sourceProgram;
        private int pointer, line = 1, column = 1, position;
        private readonly List<token> tokens = new List<token>();
        internal readonly List<string> trace = new List<string>();
        internal readonly List<TAC> tacTable = new List<TAC>();
        private readonly Dictionary<string, string> symbols = new Dictionary<string, string>();
        private int temporary;

        internal void resetCompiler(string source)
        {
            sourceProgram = source ?? "";
            pointer = 0;
            line = 1;
            column = 1;
            position = 0;
            tokens.Clear();
            trace.Clear();
            tacTable.Clear();
            symbols.Clear();
            temporary = 0;
        }
        private bool atEnd { get { return pointer >= sourceProgram.Length; } }
        private char current { get { return atEnd ? '\0' : sourceProgram[pointer]; } }
        private static bool isLetter(char c) { return c >= 'a' && c <= 'z'; }
        private static bool isDigit(char c) { return c >= '0' && c <= '9'; }
        private void advance()
        {
            if (current == '\n') { line++; column = 1; }
            else column++;
            pointer++;
        }
        private compilerException error(string message, int atLine, int atColumn)
        { return new compilerException("第 " + atLine + " 行，第 " + atColumn + " 列：" + message); }
        private compilerException error(string message, token token)
        { return error(message, token.line, token.column); }

        #region 词法分析
        internal List<token> tokenize()
        {
            if (tokens.Count != 0) return tokens;
            while (!atEnd)
            {
                if (Char.IsWhiteSpace(current)) { advance(); continue; }
                int start = pointer, startLine = line, startColumn = column;
                string kind;
                if (current == '$')
                {
                    advance();
                    if (!isLetter(current)) throw error("标识符的 $ 后必须是小写字母", startLine, startColumn);
                    while (isLetter(current) || isDigit(current)) advance();
                    if (current != '$') throw error("标识符缺少结束符 $", startLine, startColumn);
                    advance();
                    kind = "标识符";
                }
                else if (isDigit(current))
                {
                    while (isDigit(current)) advance();
                    kind = "整数";
                }
                else if (current == '"')
                {
                    advance();
                    while (isLetter(current) || isDigit(current)) advance();
                    if (current != '"') throw error("字符串只能包含小写字母和数字，并须以双引号结束", startLine, startColumn);
                    advance();
                    kind = "字符串";
                }
                else if (isLetter(current))
                {
                    // 关键字之间允许不写空格，例如 trueandfalse。
                    string word = null;
                    foreach (string keyword in new[] { "string", "while", "false", "true", "else",
                                                       "bool", "and", "not", "int", "or", "if" })
                    {
                        if (pointer + keyword.Length <= sourceProgram.Length &&
                            String.CompareOrdinal(sourceProgram, pointer, keyword, 0, keyword.Length) == 0)
                        { word = keyword; break; }
                    }
                    if (word == null) throw error("无法识别的关键字", startLine, startColumn);
                    for (int i = 0; i < word.Length; i++) advance();
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
                    char c = current;
                    advance();
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
                            if (current == '=') { advance(); kind = "赋值号"; }
                            else kind = "冒号";
                            break;
                        case '<':
                            if (current == '=' || current == '>') advance();
                            kind = "关系"; break;
                        case '>':
                            if (current == '=') advance();
                            kind = "关系"; break;
                        case '=':
                            if (current != '=') throw error("单独的 = 不是有效运算符", startLine, startColumn);
                            advance(); kind = "关系"; break;
                        default: throw error("无法识别的字符 " + c, startLine, startColumn);
                    }
                }
                tokens.Add(new token(kind, sourceProgram.Substring(start, pointer - start), startLine, startColumn));
            }
            tokens.Add(new token("#", "#", line, column));
            return tokens;
        }

        #endregion

        #region 语法分析与AST
        private token currentToken { get { return tokens[position]; } }
        private bool isToken(string kind) { return currentToken.type == kind; }
        private token matchToken(string kind)
        {
            token token = currentToken;
            if (token.type != kind)
                throw error("期望 " + kind + "，实际遇到 " + token.type + " " + token.value, token);
            position++;
            trace.Add("匹配 " + token);
            return token;
        }
        private node makeNode(string name, token token, params node[] children) { return new node(name, token, children); }
        private node makeLeaf(token t, string type = null) { return new node(t.value, t) { literalType = type }; }

        internal node parseProgram()
        {
            tokenize(); position = 0; trace.Clear();
            node declarations = parseDeclareSection();
            node statements = parseStatementSection();
            matchToken("#");
            return makeNode("topLevel", declarations.token, declarations, statements);
        }
        // A、B、C、D、E、F 沿用原项目，G、H 分别处理 and 和加减。
        private node parseDeclareSection()
        {
            token t = currentToken;
            node first = parseDeclareStatement();
            matchToken("分号");
            return makeNode("declareList", t, first, parseA());
        }
        private node parseA()
        {
            token t = currentToken;
            if (!isToken("变量说明")) return makeNode("emptyDeclareList", t);
            node declaration = parseDeclareStatement(); matchToken("分号");
            return makeNode("linkDeclareList", t, declaration, parseA());
        }
        private node parseDeclareStatement()
        {
            token type = matchToken("变量说明");
            return makeNode("declare", type, makeLeaf(type), parseVarList());
        }
        private node parseVarList()
        {
            token id = matchToken("标识符");
            return makeNode("varList", id, makeLeaf(id), parseB());
        }
        private node parseB()
        {
            token t = currentToken;
            if (!isToken("逗号")) return makeNode("emptyVarList", t);
            matchToken("逗号"); token id = matchToken("标识符");
            return makeNode("linkVarList", t, makeLeaf(id), parseB());
        }
        private node parseStatementSection()
        {
            token t = currentToken;
            node first = parseStatement();
            return makeNode("seqList", t, first, parseC());
        }
        private node parseC()
        {
            token t = currentToken;
            if (!isToken("分号")) return makeNode("emptySeqList", t);
            matchToken("分号");
            return makeNode("linkSeqList", t, parseStatement(), parseC());
        }
        private node parseStatement()
        {
            token t = currentToken;
            if (isToken("标识符"))
            {
                token id = matchToken("标识符"); matchToken("赋值号");
                return makeNode("assign", t, makeLeaf(id), parseExpression());
            }
            if (isToken("if"))
            {
                matchToken("if"); matchToken("左括号"); node condition = parseExpression(); matchToken("右括号");
                node yes = parseNestedStatement(); matchToken("else"); node no = parseNestedStatement();
                return makeNode("ifThenElse", t, condition, yes, no);
            }
            if (isToken("while"))
            {
                matchToken("while"); matchToken("左括号"); node condition = parseExpression(); matchToken("右括号");
                matchToken("冒号");
                return makeNode("while", t, condition, parseNestedStatement());
            }
            throw error("语句不能以 " + currentToken.value + " 开头", currentToken);
        }
        private node parseNestedStatement()
        {
            if (isToken("begin"))
            {
                matchToken("begin"); node body = parseStatementSection(); matchToken("end"); return body;
            }
            node statement = parseStatement(); matchToken("分号"); return statement;
        }
        private node parseExpression() { return parseD(parseConjunction()); }
        private node parseD(node left)
        {
            while (isToken("or")) { token op = matchToken("or"); left = makeNode("or", op, left, parseConjunction()); }
            return left;
        }
        private node parseConjunction() { return parseG(parseInversion()); }
        private node parseG(node left)
        {
            while (isToken("and")) { token op = matchToken("and"); left = makeNode("and", op, left, parseInversion()); }
            return left;
        }
        private node parseInversion()
        {
            if (isToken("not")) { token t = matchToken("not"); return makeNode("not", t, parseInversion()); }
            return parseRelationExpression();
        }
        private node parseRelationExpression() { return parseE(parseMathExpression()); }
        private node parseE(node left)
        {
            if (!isToken("关系")) return left;
            token op = matchToken("关系");
            return makeNode(getOperatorName(op.value), op, left, parseMathExpression());
        }
        private node parseMathExpression() { return parseH(parseTerm()); }
        private node parseH(node left)
        {
            while (isToken("加法")) { token op = matchToken("加法"); left = makeNode(getOperatorName(op.value), op, left, parseTerm()); }
            return left;
        }
        private node parseTerm() { return parseF(parseFactor()); }
        private node parseF(node left)
        {
            while (isToken("乘法")) { token op = matchToken("乘法"); left = makeNode(getOperatorName(op.value), op, left, parseFactor()); }
            return left;
        }
        private node parseFactor()
        {
            token t = currentToken;
            if (isToken("标识符")) return makeLeaf(matchToken("标识符"));
            if (isToken("整数")) return makeLeaf(matchToken("整数"), "int");
            if (isToken("字符串")) return makeLeaf(matchToken("字符串"), "string");
            if (isToken("true") || isToken("false")) return makeLeaf(matchToken(t.type), "bool");
            if (isToken("左括号"))
            { matchToken("左括号"); node e = parseExpression(); matchToken("右括号"); return e; }
            throw error("期望表达式，实际遇到 " + t.value, t);
        }
        private static string getOperatorName(string op)
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

        #endregion

        #region 语义分析TAC
        private sealed class expressionValue
        {
            public string name, type;
            public bool isIntegerLiteral;
            public expressionValue(string name, string type, bool literal = false)
            { this.name = name; this.type = type; isIntegerLiteral = literal; }
        }
        private int generateTAC(string op, string a, string b, string result)
        {
            int index = tacTable.Count;
            tacTable.Add(new TAC(op, a, b, result));
            return index;
        }
        private void backpatch(int index, int target)
        {
            tacTable[index].result = target.ToString();
        }
        internal void translateTACProgram(node program)
        {
            tacTable.Clear(); symbols.Clear(); temporary = 0;
            translateTACDeclareSection(program.children[0]);
            translateTACStatement(program.children[1]);
        }
        private void translateTACDeclareSection(node list)
        {
            if (list.name == "emptyDeclareList") return;
            node declaration = list.children[0];
            translateTACVarList(declaration.children[1], declaration.children[0].name);
            translateTACDeclareSection(list.children[1]);
        }
        private void translateTACVarList(node list, string type)
        {
            if (list.name == "emptyVarList") return;
            node id = list.children[0];
            if (symbols.ContainsKey(id.name)) throw error("重复声明标识符 " + id.name, id.token);
            symbols.Add(id.name, type);
            generateTAC("declare", type, "null", id.name + ".type");
            translateTACVarList(list.children[1], type);
        }
        private string getSymbolType(node id)
        {
            string type;
            if (!symbols.TryGetValue(id.name, out type)) throw error("未声明的标识符 " + id.name, id.token);
            return type;
        }
        private void translateTACStatement(node n)
        {
            switch (n.name)
            {
                case "seqList": case "linkSeqList":
                    translateTACStatement(n.children[0]); translateTACStatement(n.children[1]); return;
                case "emptySeqList": return;
                case "assign":
                    string type = getSymbolType(n.children[0]);
                    expressionValue value = translateTACExpression(n.children[1]);
                    if (type != value.type) throw error("赋值类型不匹配：" + type + " 与 " + value.type, n.token);
                    generateTAC("assign", value.name, "null", n.children[0].name); return;
                case "ifThenElse":
                    expressionValue condition = translateTACExpression(n.children[0]);
                    if (condition.type != "bool") throw error("if 条件必须是 bool", n.token);
                    generateTAC("jnz", condition.name, "null", (tacTable.Count + 2).ToString());
                    int otherwise = generateTAC("jump", "null", "null", "0");
                    translateTACStatement(n.children[1]);
                    int exit = generateTAC("jump", "null", "null", "0");
                    backpatch(otherwise, tacTable.Count);
                    translateTACStatement(n.children[2]);
                    backpatch(exit, tacTable.Count); return;
                case "while":
                    int start = tacTable.Count;
                    expressionValue loopCondition = translateTACExpression(n.children[0]);
                    if (loopCondition.type != "bool") throw error("while 条件必须是 bool", n.token);
                    generateTAC("jnz", loopCondition.name, "null", (tacTable.Count + 2).ToString());
                    int end = generateTAC("jump", "null", "null", "0");
                    translateTACStatement(n.children[1]);
                    generateTAC("jump", "null", "null", start.ToString());
                    backpatch(end, tacTable.Count); return;
                default: throw new InvalidOperationException("未知语句 " + n.name);
            }
        }
        private expressionValue translateTACExpression(node n)
        {
            if (n.children.Count == 0)
                return new expressionValue(n.name, n.literalType ?? getSymbolType(n), n.literalType == "int");
            expressionValue a = translateTACExpression(n.children[0]);
            if (n.name == "not")
            {
                if (a.type != "bool") throw error("not 的操作数必须是 bool", n.token);
                string name = "T" + temporary++;
                generateTAC("not", a.name, "null", name);
                return new expressionValue(name, "bool");
            }
            expressionValue b = translateTACExpression(n.children[1]);
            string resultType = null;
            if (n.name == "or" || n.name == "and")
            {
                if (a.type == "bool" && b.type == "bool") resultType = "bool";
            }
            else if (n.name == "lt" || n.name == "gt" || n.name == "noteq" ||
                     n.name == "lte" || n.name == "gte" || n.name == "eq")
            {
                if (a.type == b.type) resultType = "bool";
            }
            else if (n.name == "add" && a.type == "string" && b.type == "string")
                resultType = "string";
            else if (n.name == "mul" && a.type == "string" && b.type == "int" && b.isIntegerLiteral)
                resultType = "string";
            else if (a.type == b.type && (a.type == "int" || a.type == "bool"))
                resultType = "int";
            if (resultType == null)
                throw error("运算 " + n.name + " 不支持 " + a.type + " 和 " + b.type, n.token);
            if ((n.name == "div" || n.name == "mod") &&
                ((b.isIntegerLiteral && b.name.All(c => c == '0')) ||
                 (b.type == "bool" && b.name == "false")))
                throw error("除数不能为 0", n.token);
            string temp = "T" + temporary++;
            generateTAC(n.name, a.name, b.name, temp);
            return new expressionValue(temp, resultType);
        }
        #endregion
    }
}
