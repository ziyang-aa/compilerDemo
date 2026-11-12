using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.Diagnostics.SymbolStore;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace compilerDEMO
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private string sourceProgram;
        private int pointer;
        private token curentToken;
        private identifierTable idenTable;
        private tempVariableTable tempVarTable;
        private midCodeTable tacTable;
        private node ast;
        private string astDeclareType;
        private List<node> completedASTNodes = new List<node>();
        private int completedTACIndex;
        private int tacNestedDepth;
        private string currentTACAssignment;

        private bool isDebug = false;
        private void print(string str) { listBox1.Items.Add(str); }

        private  void  debug(string str) { listBox2.Items.Add(str); }

        private class token
        {
            public string type { get; set; }
            public string value { get; set; }

            public token(string type,string value)
            {
                this.type = type;
                this.value = value;
            }

            public override string ToString()
            {
               
                return $"({this.type},{this.value})";
            }
        }

        private class triple
        {
            //int $abc$ =123;
            public string name {  get; set; }
            public string type { get; set; }
            public string value { get; set; }
            public bool isIntegerLiteral { get; set; }
            public bool isInitialized { get; set; }

            public triple(string name)
            {
                this .name = name; 
                this.type = "";
                this.value = "";
            }
            public int getValueAsINT32() {
                if (this.type != "int") return -1;

                if (this.value == "") return -1;
                return Convert.ToInt32(this.value);
            }

            public bool getValueAsBOOL()
            {
                if (this.type != "bool") return false;

                if (this.value == "") return false;

               if(this.value =="true") return true;
               return false;

            }
            public override string ToString()
            {
                return $"{name}|{type}|{value}";
            }
        }

        private class identifierTable
        {
            public List<triple> table=new List<triple>();
            public identifierTable() { 
                this.table.Clear();
            }

            public triple getIdentifierByName(string name) {
                foreach (triple t in table) { 
                    if(t.name == name) return t;
                }
                return null;
            }

            public bool Add(string name)
            {
                if(getIdentifierByName(name) != null)
                {
                    return false;
                }

                this.table.Add(new triple(name));
                return true;
            }

            public bool UpdateTypeByName(string name, string type) {
                triple temp=getIdentifierByName(name);
                if(temp==null) return false;    
                if (temp.type != "") { return false; }
                temp.type = type;

                return true;
            }
            public bool UpdateValueByName(string name, string value)
            {
                triple temp = getIdentifierByName(name);
            
                temp.value = value;

                return true;
            }

           /* public void dump(ListBox lb) {
                lb.Items.Add($"|\t name |\t type | \t value|");
                foreach (triple t in table) { 
                    lb.Items.Add($"|{t.ToString()}|");
                }
                    }*/
            public void dump(ListBox lb)
            {
                lb.Font = new Font("Consolas", 10f);      // ① 关键：等宽字体
                lb.HorizontalScrollbar = true;

                const int W_NAME = 14;                    // ② 固定列宽（字符数）
                const int W_TYPE = 10;

                lb.Items.Add("name".PadRight(W_NAME) + "type".PadRight(W_TYPE) + "value");
                foreach (triple t in table)
                    lb.Items.Add(t.name.PadRight(W_NAME) + t.type.PadRight(W_TYPE) + t.value);
            }
        }

        private class tempVariableTable
        {
            public List<triple> table = new List<triple>();
            public tempVariableTable()
            {
                this.table.Clear();
            }

            public triple createNewVariable()
            {
                //T0, T1,T2
                int count =table.Count;

                triple temp = new triple ( $"T{table.Count}" );
                table.Add(temp);
                return temp;
            }

        }
        private class TAC
        {
            public string op { get; set; }
            public string oprand1 { get; set; }
            public string oprand2 { get; set; }
            public string result { get; set; }

            public TAC(string op, string oprand1,string oprand2,string result)
            {
               this.op = op;
                this.oprand1 = oprand1;
                this.oprand2 = oprand2;
                this.result = result;
            }

            public override string ToString()
            {

                return $"({this.op}, {this.oprand1}, {this.oprand2}, {this.result})";
            }
        }

        private class node
        {
            public string root;
            public string type;
            public bool isIntegerLiteral;
            public List<node> children = new List<node>();

            public node(string root)
            {
                this.children.Clear();
                this.root = root;
                
            }

            public void addChild(node child)
            {
                this.children.Add(child);
            }
            public void addNodeAsFirstChild(node child) {
                children.Insert(0, child);
            }
            public node getFirstLeftNodeLeafchild()
            {
                if (children.Count == 0) return null;

                node pointer = children[0];
                node lastPointer = null;
                while (pointer.children.Count != 0)
                {
                    lastPointer = pointer;
                    pointer = pointer.children[0];

                }
                return lastPointer;
            }
            public override string ToString()
            {
                string str = this.root;
                foreach (node child in children)
                {
                    if (child != null)
                    {
                        str += "  " + child.ToString();
                    }
                }
                return str;
            }

            // 使用原版的括号形式展示根节点和孩子节点的关系。
            public string toBracketString()
            {
                string str = $"({this.root})";
                if (children.Count == 0) return str;

                str += "(";
                foreach (node child in children)
                {
                    if (child != null) str += child.toBracketString();
                }
                str += ")";
                return str;
            }

            public void saveToTXT(string path)

            {
                StreamWriter sw = new StreamWriter(path);

               sw.WriteLine(this.ToString());
                sw.Close();
            }


        }

        private class midCodeTable
        {
            public List<TAC> table = new List<TAC>();
            public int NXQ
            {
                get
                {
                    return table.Count;
                }
            }
            public midCodeTable()
            {
                this.table.Clear();
            }
            public void generate(string op, string oprand1, string oprand2, string result)
            {
                this.table.Add(new TAC(op, oprand1, oprand2, result));
            }
            public void backpatch(int index, string result)
            {
                //table[0][1][2]
                //count=3
                //index:0,1,2
                if (index < 0 || index >= table.Count) return;
                table[index].result = result;
            }

            public void dump(ListBox lb)
            {
                for (int i = 0; i < table.Count; i++)
                {
                    lb.Items.Add($"({i}) {table[i].ToString()}");
                }

            }
            public void saveToTXT(string path)
               
            {
                StreamWriter sw = new StreamWriter(path);

                    for
                        (int i = 0; i < table.Count; i++)
                    {
                        sw.WriteLine($"({i}) {table[i].ToString()}");
                    }
                sw.Close();
            }
        }

        #region 词法分析
        private token nextInput()
        {
            string tmpWord = "";
            int state = 0;

            while (pointer < sourceProgram.Length - 1)
            {
                char symbol = sourceProgram[pointer];

                if (char.IsWhiteSpace(symbol))
                {
                    if (state == 0) { pointer++; continue; }
                    if (state == 200) return new token("整数", tmpWord);
                    if (state == 1400 || state == 1410) return new token("关系", tmpWord);
                    if (state == 700) return new token("冒号", tmpWord);
                    pointer++;
                    return new token("error", "单词不完整：" + tmpWord);
                }

                if (state == 0) {

                    if (symbol == '$')
                    {
                        state = 100;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol >= '0' && symbol <= '9') {
                        state = 200;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol ==';')
                    {
                        
                        tmpWord += symbol;
                        pointer++;
                        return new token("分号",tmpWord);
                    }
                    else if (symbol =='i')
                    {
                        state = 400;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == 'b')
                    {
                        state = 500;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == ',')
                    {
                        
                        tmpWord += symbol;
                        pointer++;
                       return new token("逗号",tmpWord) ;
                    }
                    else if (symbol == ':')
                    {
                        state = 700;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == 'o')
                    {
                        state = 800;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == 'n')
                    {
                        state = 900;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == 'a' || symbol == 's' || symbol == 'w')
                    {
                        state = symbol == 'a' ? 1800 : symbol == 's' ? 1900 : 2000;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == '"')
                    {
                        state = 2100;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == 't')
                    {
                        state = 1000;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == 'f')
                    {
                        state = 1100;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == '(')
                    {
                      
                        tmpWord += symbol;
                        pointer++;
                       return new token("左括号",tmpWord);
                    }
                    else if (symbol == ')')
                    {
                       
                        tmpWord += symbol;
                        pointer++;
                        return new token("右括号", tmpWord);
                    }
                    else if (symbol == '<')
                    {
                        state = 1400;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if(symbol=='>')
                    {
                        state = 1410;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == '=')
                    {
                        state = 1420;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == '*'||symbol=='/'||symbol=='%')
                    {
                        
                        tmpWord += symbol;
                        pointer++;
                        return new token("乘法", tmpWord);
                    }
                    else if (symbol == '+' || symbol == '-')
                    {
                        pointer++;
                        return new token("加法", symbol.ToString());
                    }
                    else if (symbol == 'e')
                    {
                        state = 1700;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == '{')
                    {
                      tmpWord += symbol;
                        pointer++;
                        return new token("begin", tmpWord);
                    }
                    else if (symbol == '}')
                    {

                        tmpWord += symbol;
                        pointer++;
                        return new token("end", tmpWord);
                    }
                    else {
                        pointer++;
                        return new token("error", $"状态{state}无法识别的符号");
                    }
                }
                if (state == 100)
                {
                    if (symbol >= 'a' && symbol <= 'z') {
                        state = 101;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state ==101) {
                    if ((symbol >= 'a' && symbol <= 'z') || (symbol >= '0' && symbol <= '9'))
                    {
                        state = 101;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == '$') {
                        tmpWord += symbol;
                        pointer++;
                        return new token("标识符", tmpWord);
                    }
                    
                }
                if (state == 200)
                {
                    if (symbol >= '0'&&symbol<='9')
                    {
                        state = 200;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }else
                    {
                       
                        return new token("整数", tmpWord);
                    }
                }
                if (state == 400)
                {
                    if (symbol == 'n')
                    {
                        state = 401;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                    else if (symbol == 'f') {
                        tmpWord += symbol;
                        pointer++;
                        return new token("if", tmpWord);
                    }
                }
                if (state == 401)
                {
                    if (symbol == 't')
                    {
                       
                        tmpWord += symbol;
                        pointer++;
                        return new token("变量说明", tmpWord);
                    }
                }
                if (state == 500) {
                    if (symbol == 'o')
                    {
                        state = 501;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state == 501)
                {
                    if (symbol == 'o')
                    {
                        state = 502;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state == 502)
                {
                    if (symbol == 'l')
                    {
                        tmpWord += symbol;
                        pointer++;
                        return new token("变量说明", tmpWord);
                    }
                }
                if (state == 700)
                {
                    if (symbol == '=')
                    {
                        
                        tmpWord += symbol;
                        pointer++;
                        return new token("赋值号", tmpWord);
                    }
                    return new token("冒号", tmpWord);
                }
                if (state == 800)
                {
                    if (symbol == 'r')
                    {

                        tmpWord += symbol;
                        pointer++;
                        return new token("or", tmpWord);
                    }
                }
                if (state == 900)
                {
                    if (symbol == 'o')
                    {
                        state = 901;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state == 901)
                {
                    if (symbol == 't')
                    {
                        tmpWord += symbol;
                        pointer++;
                        return new token("not", tmpWord);
                    }
                }
                if (state == 1000)
                {
                    if (symbol == 'r')
                    {
                        state = 1001;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state == 1001)
                {
                    if (symbol == 'u')
                    {
                        state = 1002;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state == 1002)
                {
                    if (symbol == 'e')
                    {

                        tmpWord += symbol;
                        pointer++;
                        return new token("true", tmpWord);
                    }
                }
                if (state == 1100)
                {
                    if (symbol == 'a')
                    {
                        state = 1101;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state == 1101)
                {
                    if (symbol == 'l')
                    {
                        state = 1102;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state == 1102)
                {
                    if (symbol == 's')
                    {
                        state = 1103;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state == 1103)
                {
                    if (symbol == 'e')
                    {

                        tmpWord += symbol;
                        pointer++;
                        return new token("false", tmpWord);
                    }
                }
                if (state == 1400)
                {
                    if (symbol == '>' || symbol == '=')
                    {

                        tmpWord += symbol;
                        pointer++;
                        return new token("关系", tmpWord);
                    }
                    else
                    {
                        return new token("关系", tmpWord);
                    }
                }
                if (state == 1410)
                {
                    if (symbol == '=')
                    {
                        tmpWord += symbol;
                        pointer++;
                    }
                    return new token("关系", tmpWord);
                }
                if (state == 1420)
                {
                    if (symbol == '=')
                    {

                        tmpWord += symbol;
                        pointer++;
                        return new token("关系", tmpWord);
                    }
                }
                if (state == 1700)
                {
                    if (symbol == 'l')
                    {
                        state = 1701;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state == 1701)
                {
                    if (symbol == 's')
                    {
                        state = 1702;
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                if (state == 1702)
                {
                    if (symbol == 'e')
                    {
                        
                        tmpWord += symbol;
                        pointer++;
                        return new token("else", tmpWord);
                    }
                }
                // 新关键字沿用逐字符状态转换，状态号与原有自动机区分。
                if (state >= 1800 && state < 2100)
                {
                    string keyword = state < 1900 ? "and" : state < 2000 ? "string" : "while";
                    int baseState = state < 1900 ? 1800 : state < 2000 ? 1900 : 2000;
                    int index = state - baseState + 1;
                    if (index < keyword.Length && symbol == keyword[index])
                    {
                        tmpWord += symbol;
                        pointer++;
                        if (index == keyword.Length - 1)
                            return new token(keyword == "string" ? "变量说明" : keyword, tmpWord);
                        state++;
                        continue;
                    }
                }
                if (state == 2100)
                {
                    if (symbol == '"')
                    {
                        tmpWord += symbol;
                        pointer++;
                        return new token("字符串", tmpWord);
                    }
                    if ((symbol >= 'a' && symbol <= 'z') || (symbol >= '0' && symbol <= '9'))
                    {
                        tmpWord += symbol;
                        pointer++;
                        continue;
                    }
                }
                pointer++;
                return new token("error", "无法识别的单词：" + tmpWord + symbol);
            }

            if (state == 200) return new token("整数", tmpWord);
            if (state == 1400 || state == 1410) return new token("关系", tmpWord);
            if (state == 700) return new token("冒号", tmpWord);
            if (state != 0) return new token("error", "单词不完整：" + tmpWord);
            return new token("#", $"#");
        }

        private void button1_Click(object sender, EventArgs e)
        {

            listBox1.Items.Clear();
            listBox2.Items.Clear();

            sourceProgram=textBox1.Text+"#";
            pointer = 0;

            curentToken=nextInput();
            print(curentToken.ToString());

            while (curentToken.value != "#"&&curentToken.type!="error") {
                curentToken = nextInput();
                print(curentToken.ToString());
            }

            print("词法分析结束");
           
        }
        #endregion
        #region 语法分析
        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                isDebug=false;
                listBox1.Items.Clear();
                listBox2.Items.Clear();

                sourceProgram = textBox1.Text + "#";
                pointer = 0;

                curentToken = nextInput();
               debug(curentToken.ToString());

                parseProgram();
            }
            catch (InvalidOperationException ex)
            {
                print("分析错误：" + ex.Message);
            }
        }

        private bool match(string expectedTokenType)
        {
            if (curentToken.type != expectedTokenType) {

                if(isDebug)
                    debug($"匹配失败:expect{expectedTokenType},got{curentToken.type}");
                else
                print($"匹配失败:expect{expectedTokenType},got{curentToken.type}");
                throw new InvalidOperationException($"期望 {expectedTokenType}，实际遇到 {curentToken.type} {curentToken.value}");
            }
            if (isDebug)
               debug($"匹配成功:expect{expectedTokenType}");
            else
            print($"匹配成功:expect{expectedTokenType}");
            curentToken = nextInput();
            debug(curentToken.ToString());
            return true;
        }

        private void parseProgram()
        {
            print("推导:<程序> → <变量说明部分> <语句部分>");
            parseDeclareSection();
            parseStatementSection();
            if (curentToken.type != "#")
                throw new InvalidOperationException("程序末尾存在多余内容：" + curentToken.value);

            print("语法分析结束");

        }
        private void parseDeclareSection()
        {
            print("推导:<变量说明部分>→<变量说明语句> 分号  A ");
            parseDeclareStatement();
            match("分号");
           // if (!match("分号")) { return; };
            parseA();

        }
        //todo
        private void parseA()
        {

            print("选择产生式:A →<变量说明语句> 分号 A | ε");

            if (curentToken.type == "变量说明") {


                print("推导：A →<变量说明语句> 分号 A");
                parseDeclareStatement();
                match("分号");
                // if (!match("分号")) { return; };
                parseA();
            }

            else if (curentToken.type == "标识符" || curentToken.type == "if" || curentToken.type == "while")
            {
                print("推导：A→ε");
            }
        }
        private void parseDeclareStatement()
        {
            print("推导:<变量说明语句> → <变量说明> <标识符列表>");
            match("变量说明");
            parseVarList();
        }
        private void parseVarList()
        {
            print("推导:<标识符列表> →标识符 B");
            match("标识符");
            parseB();
        }

        private void parseB()
        {
            print("选择产生式:B →逗号 标识符 B | ε");
            if (curentToken.type == "逗号")
            {


                print("推导：B →逗号 标识符 B ");
                match("逗号");
                match("标识符");                // if (!match("逗号")) { return; };
                parseB();
            }

            else if (curentToken.type == "分号" )
            {
                print("推导：B →ε");
            }
            else
            {
                print($"语法错误：期望 逗号 或 分号，实际遇到 {curentToken.type}");
            }

        }

        private void parseStatementSection()
        {
            print("推导:<语句部分> →<语句> C");
            parseStatement();
            parseC();
        }
        private void parseC()
        {
        
            print("选择产生式:C →分号 <语句> C | ε");
            if (curentToken.type == "分号")
            {


                print("推导：C →分号 <语句> C ");
                match("分号");
                parseStatement();
                parseC();
            }

            else if (curentToken.type == "#" ||curentToken.type=="end")
            {
                print("推导：C →ε");
            }
            else
            {
                print($"语法错误：期望 逗号 或 分号，实际遇到 {curentToken.type}");
            }
        }
        private void parseStatement()
        {
            print("选择产生式:<语句> → <赋值语句>|<条件语句>");
          
            if (curentToken.type == "标识符")
            {


                print("推导：<语句> → <赋值语句>");
                parseAssignmentStatement();
            }

            else if (curentToken.type == "if" )
            {
                print("推导：<语句> → <条件语句>");
                parseConditionalStatement();
            }
            else if (curentToken.type == "while")
            {
                print("推导：<语句> → <循环语句>");
                parseWhileStatement();
            }
            else
            {
                throw new InvalidOperationException($"语法错误：语句不能以 {curentToken.type} 开头");
            }
        }

        private void parseAssignmentStatement() {
            print("推导:<赋值语句> → 标识符 赋值号 <表达式>");
            match("标识符");
            match("赋值号");
            parseExpression();
        }
        private void parseExpression() {
            print("推导:<表达式> → <conjunction> D");
            parseConjunction();
                parseD();
        }
        private void parseD()
        {
            print("选择产生式:D →or <inversion> D|ε");

            if (curentToken.type == "or")
            {


                print("推导：D →or <inversion> D");
                match("or");
                parseConjunction();
                parseD();
            }

            else if (curentToken.type == "分号" ||curentToken.type=="#" || curentToken.type == "end" || curentToken.type == "右括号")
            {
                print("推导：D → ε");
                
            }
        }

        private void parseConjunction()
        {
            print("推导:<conjunction> → <inversion> G");
            parseInversion();
            parseG();
        }
        private void parseG()
        {
            print("选择产生式:G → and <inversion> G | ε");
            if (curentToken.type == "and")
            {
                match("and");
                parseInversion();
                parseG();
            }
        }

        private void parseInversion() {
            print("选择产生式:<inversion > → not < inversion > | < 关系表达式 >");

            if (curentToken.type == "not")
            {


                print("推导：<inversion > → not < inversion > ");
                match("not");
                parseInversion();
             
            }

            else if (curentToken.type == "标识符" || curentToken.type == "true"
                || curentToken.type == "false"|| curentToken.type == "整数" || curentToken.type == "字符串"
                || curentToken.type == "左括号" )
            {
                print("推导：<inversion > →< 关系表达式 > ");
                parseRelationExpression();

            }
            else throw new InvalidOperationException("期望表达式，实际遇到 " + curentToken.value);
        }

        private void parseRelationExpression() {
            print("推导:<关系表达式 > → <算术表达式> E");
            parseMathExpression();
            parseE();
        }
        private void parseE()
        {
            print("选择产生式:E →关系 <算术表达式> | ε");

            if (curentToken.type == "关系")
            {

                
                print("推导：E →关系 <算术表达式> ");
                match("关系");
                parseMathExpression();

            }

            else if (curentToken.type == "分号" || curentToken.type == "#"
                || curentToken.type == "end" || curentToken.type == "右括号"
                 || curentToken.type == "or" || curentToken.type == "and")
            {
                print("推导：E → ε");

            }
        }
        private void parseMathExpression() {
            print("推导:<算术表达式> → <term> H");
            parseTerm();
            parseH();
        }

        private void parseH()
        {
            print("选择产生式:H → 加法 <term> H | ε");
            if (curentToken.type == "加法")
            {
                match("加法");
                parseTerm();
                parseH();
            }
        }
        private void parseTerm()
        {
            print("推导:<term> → <factor> F");
            parseFactor();
            parseF();
        }

        private void parseF()
        {
            print("选择产生式:F →乘法 <factor> F | ε");

            if (curentToken.type == "乘法")
            {


                print("推导：F →乘法 <factor> F ");
                match("乘法");
                parseFactor();
                parseF();

            }

            else if (curentToken.type == "分号" || curentToken.type == "#"
                || curentToken.type == "end" || curentToken.type == "右括号"
                 || curentToken.type == "关系" || curentToken.type == "or" || curentToken.type == "and"
                 || curentToken.type == "加法")
            {
                print("推导：F → ε");

            }
        }
        private void parseFactor()
        {
            print("选择产生式:<factor> → 标识符 | true | false | 整数 | 左括号 <表达式> 右括号");

            if (curentToken.type == "标识符")
            {
                print("推导：<factor>→ 标识符 ");
                match("标识符");
            }
            else if (curentToken.type == "true")
            {
                print("推导：<factor>→ true ");
                match("true");
            }
            else if (curentToken.type == "false")
            {
                print("推导：<factor>→ false ");
                match("false");
            }
            else if (curentToken.type == "整数")
            {
                print("推导：<factor>→ 整数 ");
                match("整数");
            }
            else if (curentToken.type == "字符串")
            {
                print("推导：<factor>→ 字符串 ");
                match("字符串");
            }
            else if (curentToken.type == "左括号")
            {
                print("推导：左括号 <表达式> 右括号 ");
                match("左括号");
                parseExpression();
                match("右括号");

            }
           
        }
      

        private void parseConditionalStatement() {
            print("推导：<条件语句> → if 左括号 <表达式> 右括号  <复合语句>  else  <复合语句>");
            match("if");          
            match("左括号");
            parseExpression();
            match("右括号");
            parseNestedStatement();
            match("else");
            parseNestedStatement();
        }
        private void parseWhileStatement()
        {
            print("推导：<循环语句> → while ( <表达式> ) : <嵌套语句>");
            match("while");
            match("左括号");
            parseExpression();
            match("右括号");
            match("冒号");
            parseNestedStatement();
        }
        private void parseNestedStatement()
        {
            if (curentToken.type == "begin") parseCompoundStatement();
            else { parseStatement(); match("分号"); }
        }
        private void parseCompoundStatement()
        {
            print("推导：<复合语句> → begin <语句部分> end");
            match("begin");
            parseStatementSection();
            match("end");
        }

        #endregion
        #region 语义分析TAC
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                isDebug = true;
                listBox1.Items.Clear();
                listBox2.Items.Clear();

                sourceProgram = textBox1.Text + "#";
                pointer = 0;

           
                idenTable=new identifierTable();
                tempVarTable=new tempVariableTable();
                    tacTable = new midCodeTable();         

                curentToken = nextInput();
                debug(curentToken.ToString());

                translateTACProgram();

                tacTable.dump(listBox1);

                /*    string path = Application.StartupPath+@"\outputTAC.txt";
        tacTable.saveToTXT(path);
        print("Save to "+path);*/
            }
            catch (InvalidOperationException ex)
            {
                if (tacTable != null) tacTable.dump(listBox1);
                print("分析错误：" + ex.Message);
            }
        }
     
        private void translateTACProgram()
        {
            completedTACIndex = 0;
            tacNestedDepth = 0;
            currentTACAssignment = null;
            debug("推导:<程序> → <变量说明部分> <语句部分>");
            translateTACDeclareSection();
            translateTACStatementSection();
            if (curentToken.type != "#")
                throw new InvalidOperationException("程序末尾存在多余内容：" + curentToken.value);

            debug("语法分析结束");

        }
        private void translateTACDeclareSection()
        {
            debug("推导:<变量说明部分>→<变量说明语句> 分号  A ");
            translateTACDeclareStatement();
            match("分号");
            // if (!match("分号")) { return; };
            translateTACA();

        }
        //todo
        private void translateTACA()
        {

            debug("选择产生式:A →<变量说明语句> 分号 A | ε");

            if (curentToken.type == "变量说明")
            {


                debug("推导：A →<变量说明语句> 分号 A");
                translateTACDeclareStatement();
                match("分号");
                // if (!match("分号")) { return; };
                translateTACA();
            }

            else if (curentToken.type == "标识符" || curentToken.type == "if" || curentToken.type == "while")
            {
                debug("推导：A→ε");
            }
        }
        private void translateTACDeclareStatement()
        {
            debug("推导:<变量说明语句> → <变量说明> <标识符列表>");
            string type = curentToken.value;
            match("变量说明");
            translateTACVarList(type);
        }
        private void translateTACVarList(string type)
        {
            debug("推导:<标识符列表> →标识符 B");
            string name = curentToken.value;
            match("标识符");

            //语义检查,完善
            if (!idenTable.Add(name)) throw new InvalidOperationException("重复声明标识符 " + name);
            idenTable.UpdateTypeByName(name, type);
            tacTable.generate("declare", type, "null", $"{name}.type");

            translateTACB(type);
        }

        private void translateTACB(string type)
        {
            debug("选择产生式:B →逗号 标识符 B | ε");
            if (curentToken.type == "逗号")
            {


                debug("推导：B →逗号 标识符 B ");
                match("逗号");

                string name = curentToken.value;
                match("标识符");
                //语义检查,完善
                if (!idenTable.Add(name)) throw new InvalidOperationException("重复声明标识符 " + name);
                idenTable.UpdateTypeByName(name, type);
                tacTable.generate("declare", type, "null", $"{name}.type");

                // if (!match("逗号")) { return; };
                translateTACB(type);
            }

            else if (curentToken.type == "分号")
            {
                debug("推导：B →ε");
            }
            else
            {
                debug($"语法错误：期望 逗号 或 分号，实际遇到 {curentToken.type}");
            }

        }

        private void translateTACStatementSection()
        {
            debug("推导:<语句部分> →<语句> C");
            translateTACStatement();
            translateTACC();
        }
        private void translateTACC()
        {

            debug("选择产生式:C →分号 <语句> C | ε");
            if (curentToken.type == "分号")
            {


                debug("推导：C →分号 <语句> C ");
                match("分号");
                translateTACStatement();
                translateTACC();
            }

            else if (curentToken.type == "#" || curentToken.type == "end")
            {
                debug("推导：C →ε");
            }
            else
            {
                debug($"语法错误：期望 逗号 或 分号，实际遇到 {curentToken.type}");
            }
        }
        private void translateTACStatement()
        {
            debug("选择产生式:<语句> → <赋值语句>|<条件语句>");

            if (curentToken.type == "标识符")
            {


                debug("推导：<语句> → <赋值语句>");
                translateTACAssignmentStatement();
            }

            else if (curentToken.type == "if")
            {
                debug("推导：<语句> → <条件语句>");
                translateTACConditionalStatement();
            }
            else if (curentToken.type == "while")
            {
                debug("推导：<语句> → <循环语句>");
                translateTACWhileStatement();
            }
            else
            {
                throw new InvalidOperationException($"语法错误：语句不能以 {curentToken.type} 开头");
            }
            // 只有完整的顶层语句可以在后续分析失败时安全执行。
            if (tacNestedDepth == 0) completedTACIndex = tacTable.NXQ;
        }

        private void translateTACAssignmentStatement()
        {
            debug("推导:<赋值语句> → 标识符 赋值号 <表达式>");

            string name = curentToken.value;
            currentTACAssignment = name;
            match("标识符");
            match("赋值号");
          triple Exp=  translateTACExpression();

            //更新标识符列表，只有动态编译才能获得正确结果

           
            triple target = idenTable.getIdentifierByName(name);
            if (target == null) throw new InvalidOperationException("未声明的标识符 " + name);
            if (Exp == null || target.type != Exp.type)
                throw new InvalidOperationException("赋值类型不匹配：" + name);

            tacTable.generate("assign", Exp.name, "null", name);
            currentTACAssignment = null;

        }
        private triple translateTACExpression()
        {
            debug("推导:<表达式> →< inversion > D");
            triple E1=translateTACConjunction();
           triple E= translateTACD(E1);

            return E;
        }
        private triple translateTACD(triple E1)
        {
            debug("选择产生式:D →or <inversion> D|ε");

            if (curentToken.type == "or")
            {


                debug("推导：D →or <inversion> D");
                match("or");
               triple E2= translateTACConjunction();
                if (E1.type != "bool" || E2.type != "bool")
                    throw new InvalidOperationException("or 的操作数必须是 bool");

                //创建临时变量
                triple T = tempVarTable.createNewVariable();
                T.type = "bool";

                //根据E1和E2进行or运算,获得T的正确值
                T.value = "";

                tacTable.generate("or", E1.name, E2.name, T.name);

                triple E = translateTACD(T);
                return E;
            }

            else if (curentToken.type == "分号" || curentToken.type == "#" || curentToken.type == "end" || curentToken.type == "右括号")
            {
                debug("推导：D → ε");
                return E1;
            }
            throw new InvalidOperationException("表达式中遇到意外的单词：" + curentToken.value);
        }

        private triple translateTACConjunction()
        {
            debug("推导:<conjunction> → <inversion> G");
            return translateTACG(translateTACInversion());
        }
        private triple translateTACG(triple E1)
        {
            debug("选择产生式:G → and <inversion> G | ε");
            if (curentToken.type != "and") return E1;
            match("and");
            triple E2 = translateTACInversion();
            if (E1.type != "bool" || E2.type != "bool")
                throw new InvalidOperationException("and 的操作数必须是 bool");
            triple T = tempVarTable.createNewVariable();
            T.type = "bool";
            tacTable.generate("and", E1.name, E2.name, T.name);
            return translateTACG(T);
        }

        private triple translateTACInversion()
        {
            debug("选择产生式:<inversion > → not < inversion > | < 关系表达式 >");

            if (curentToken.type == "not")
            {


                debug("推导：<inversion > → not < inversion > ");
                match("not");
                triple E1=translateTACInversion();
                if (E1 == null || E1.type != "bool")
                    throw new InvalidOperationException("not 的操作数必须是 bool");
                //创建临时变量
                triple T = tempVarTable.createNewVariable();
                T.type = "bool";

                //根据E1进行Not运算运算,获得T的正确值
                T.value = "";
               
            

                tacTable.generate("not", E1.name, "null", T.name);
                return T;
            }

            else if (curentToken.type == "标识符" || curentToken.type == "true"
                || curentToken.type == "false" || curentToken.type == "整数" || curentToken.type == "字符串"
                || curentToken.type == "左括号")
            {
                debug("推导：<inversion > →< 关系表达式 > ");
               return  translateTACRelationExpression();

            }
            throw new InvalidOperationException("期望表达式，实际遇到 " + curentToken.value);
        }

        private triple translateTACRelationExpression()
        {
            debug("推导:<关系表达式 > → <算术表达式> E");
            triple E1=translateTACMathExpression();

            //可能
           triple E= translateTACE(E1);
            return E;
        }
        private triple  translateTACE(triple E1)
        {
            debug("选择产生式:E →关系 <算术表达式> | ε");

            if (curentToken.type == "关系")
            {


                debug("推导：E →关系 <算术表达式> ");

                string op = curentToken.value;
                match("关系");
                triple E2=translateTACMathExpression();
                if (E1.type != E2.type)
                    throw new InvalidOperationException("关系运算的类型不匹配");

                //创建临时变量
                triple T = tempVarTable.createNewVariable();
                T.type = "bool";

                //根据E1和E2进行关系运算,获得T的正确值
                T.value = "";

                if (op == "<") { op = "lt"; }
              else  if (op == ">") { op = "gt"; }
              else  if (op == "<>") { op = "noteq"; }
              else  if (op == "==") { op = "eq"; }
              else  if (op == "<=") { op = "lte"; }
              else  if (op == ">=") { op = "gte"; }
               
                if (op == "<") { op = "lt"; }

                tacTable.generate(op, E1.name, E2.name, T.name);


                return T;
            }

            else if (curentToken.type == "分号" || curentToken.type == "#"
                || curentToken.type == "end" || curentToken.type == "右括号"
                 || curentToken.type == "or" || curentToken.type == "and")
            {
                debug("推导：E → ε");
                return E1;

            }
            throw new InvalidOperationException("表达式中遇到意外的单词：" + curentToken.value);
        }
        private triple  translateTACMathExpression()
        {
            debug("推导:<算术表达式> → <factor> F");
          triple E1=  translateTACTerm();
           triple E= translateTACH(E1);
            return E;

        }

        private triple translateTACH(triple E1)
        {
            debug("选择产生式:H → 加法 <term> H | ε");
            if (curentToken.type != "加法") return E1;
            string op = curentToken.value;
            match("加法");
            triple E2 = translateTACTerm();
            triple T = translateTACArithmetic(op, E1, E2);
            return translateTACH(T);
        }
        private triple translateTACTerm()
        {
            debug("推导:<term> → <factor> F");
            return translateTACF(translateTACFactor());
        }
        private triple translateTACArithmetic(string op, triple E1, triple E2)
        {
            string type = null;
            if (op == "+" && E1.type == "string" && E2.type == "string") type = "string";
            else if (op == "*" && E1.type == "string" && E2.type == "int" && E2.isIntegerLiteral) type = "string";
            else if (E1.type == E2.type && (E1.type == "int" || E1.type == "bool")) type = "int";
            if (type == null) throw new InvalidOperationException("运算类型不匹配：" + op);
            if ((op == "/" || op == "%") &&
                ((E2.isIntegerLiteral && E2.value.All(c => c == '0')) ||
                 (E2.type == "bool" && E2.name == "false")))
                throw new InvalidOperationException("除数不能为 0");
            string name = op == "+" ? "add" : op == "-" ? "sub" :
                          op == "*" ? "mul" : op == "/" ? "div" : "mod";
            triple T = tempVarTable.createNewVariable();
            T.type = type;
            tacTable.generate(name, E1.name, E2.name, T.name);
            return T;
        }

        private triple translateTACF(triple E1)
        {
            debug("选择产生式:F →乘法 <factor> F | ε");

            if (curentToken.type == "乘法")
            {


                debug("推导：F →乘法 <factor> F ");
                string op = curentToken.value;
                match("乘法");
               triple E2= translateTACFactor();

                triple T = translateTACArithmetic(op, E1, E2);

               return  translateTACF(T);

            }

            else if (curentToken.type == "分号" || curentToken.type == "#"
                || curentToken.type == "end" || curentToken.type == "右括号"
                 || curentToken.type == "关系" || curentToken.type == "or" || curentToken.type == "and"
                 || curentToken.type == "加法")
            {
                debug("推导：F → ε");
                return E1;

            }
            throw new InvalidOperationException("表达式中遇到意外的单词：" + curentToken.value);
        }
        private triple translateTACFactor()
        {
            debug("选择产生式:<factor> → 标识符 | true | false | 整数 | 左括号 <表达式> 右括号");

            if (curentToken.type == "标识符")
            {
                debug("推导：<factor>→ 标识符 ");
                string name = curentToken.value;
                match("标识符");

                triple identifier = idenTable.getIdentifierByName(name);
                if (identifier == null) throw new InvalidOperationException("未声明的标识符 " + name);
                return identifier;
            }
            else if (curentToken.type == "true")
            {
                debug("推导：<factor>→ true ");
                match("true");

                triple t = new triple("true");
                t.type = "bool";
                t.value = "true";

                return t;
            }
            else if (curentToken.type == "false")
            {
                debug("推导：<factor>→ false ");
                match("false");


                triple t = new triple("false");
                t.type = "bool";
                t.value = "false";

                return t;
            }
            else if (curentToken.type == "整数")
            {
                debug("推导：<factor>→ 整数 ");

                string value = curentToken.value;
                match("整数");


                triple t = new triple(value);
                t.type = "int";
                t.value = value;
                t.isIntegerLiteral = true;

                return t;

            }
            else if (curentToken.type == "字符串")
            {
                string value = curentToken.value;
                match("字符串");
                triple t = new triple(value);
                t.type = "string";
                t.value = value;
                return t;
            }
            else if (curentToken.type == "左括号")
            {
                debug("推导：左括号 <表达式> 右括号 ");
                match("左括号");
               triple Exp= translateTACExpression();
                match("右括号");

                return Exp;
            }
            throw new InvalidOperationException("表达式中遇到意外的单词：" + curentToken.value);
        }


        private void translateTACConditionalStatement()
        {
            debug("推导：<条件语句> → if 左括号 <表达式> 右括号  <复合语句>  else  <复合语句>");
            match("if");
            match("左括号");
           triple T= translateTACExpression();
            if (T == null || T.type != "bool") throw new InvalidOperationException("if 条件必须是 bool");

            tacTable.generate("jnz",T.name , "null",(tacTable.NXQ+2).ToString());
            int falseIndex=tacTable.NXQ;
            tacTable.generate("jump", "null", "null", "待回填");


            match("右括号");
            translateTACNestedStatement();

            int exitIndex=tacTable.NXQ;
            tacTable.generate("jump", "null", "null", "待回填");
            tacTable.backpatch(falseIndex, tacTable.NXQ.ToString());

            match("else");
            translateTACNestedStatement();

            tacTable.backpatch(exitIndex, tacTable.NXQ.ToString());

        }
        private void translateTACWhileStatement()
        {
            debug("推导：<循环语句> → while ( <表达式> ) : <嵌套语句>");
            match("while");
            match("左括号");
            int start = tacTable.NXQ;
            triple condition = translateTACExpression();
            if (condition == null || condition.type != "bool") throw new InvalidOperationException("while 条件必须是 bool");
            match("右括号");
            match("冒号");
            tacTable.generate("jnz", condition.name, "null", (tacTable.NXQ + 2).ToString());
            int exit = tacTable.NXQ;
            tacTable.generate("jump", "null", "null", "待回填");
            translateTACNestedStatement();
            tacTable.generate("jump", "null", "null", start.ToString());
            tacTable.backpatch(exit, tacTable.NXQ.ToString());
        }
        private void translateTACNestedStatement()
        {
            tacNestedDepth++;
            try
            {
                if (curentToken.type == "begin") translateTACCompoundStatement();
                else { translateTACStatement(); match("分号"); }
            }
            finally
            {
                tacNestedDepth--;
            }
        }
        private void translateTACCompoundStatement()
        {
            debug("推导：<复合语句> → begin <语句部分> end");
            match("begin");
            translateTACStatementSection();
            match("end");
        }
        #endregion

        #region TAC执行
        private int executionPointer;
        private int executionSteps;
        private int executionLimit;
        private const int maxExecutionSteps = 100000;

        private void button5_Click(object sender, EventArgs e)
        {
            isDebug = true;
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            sourceProgram = textBox1.Text + "#";
            pointer = 0;
            idenTable = new identifierTable();
            tempVarTable = new tempVariableTable();
            tacTable = new midCodeTable();
            bool translated = false;

            try
            {
                // 仍使用原来的 TAC 翻译流程。
                curentToken = nextInput();
                debug(curentToken.ToString());
                translateTACProgram();
                translated = true;
                executeTAC(tacTable.NXQ);
                print("执行完成，共执行 " + executionSteps + " 条四元式");
                printExecutionTables();
            }
            catch (InvalidOperationException ex)
            {
                if (translated)
                {
                    print("执行中止，已完成 " + executionSteps + " 条四元式");
                    printExecutionTables();
                    print("运行错误：" + ex.Message);
                }
                else
                {
                    // 只执行最后一个完整顶层语句之前的四元式，
                    // 避免进入未完成回填的 if 或 while。
                    try
                    {
                        executeTAC(completedTACIndex);
                        print("已执行错误前完整的顶层语句，共执行 " + executionSteps + " 条四元式");
                        printExecutionTables();
                    }
                    catch (InvalidOperationException runEx)
                    {
                        print("执行中止，已完成 " + executionSteps + " 条四元式");
                        printExecutionTables();
                        print("运行错误：" + runEx.Message);
                    }
                    string context = currentTACAssignment == null ? "" : "给 " + currentTACAssignment + " 赋值时，";
                    print("分析错误：" + context + ex.Message);
                }
            }
        }

        private void executeTAC(int limit)
        {
            executionPointer = 0;
            executionSteps = 0;
            executionLimit = limit;
            foreach (triple t in idenTable.table) { t.value = ""; t.isInitialized = false; }
            foreach (triple t in tempVarTable.table) { t.value = ""; t.isInitialized = false; }

            while (executionPointer < executionLimit)
            {
                if (executionSteps >= maxExecutionSteps)
                    throw new InvalidOperationException("执行超过 " + maxExecutionSteps + " 步，可能存在无限循环");

                TAC instruction = tacTable.table[executionPointer];
                int nextPointer = executionPointer + 1;
                try
                {
                    if (instruction.op == "declare")
                    {
                        // 声明已在生成四元式时加入标识符表。
                    }
                    else if (instruction.op == "assign")
                    {
                        triple source = getExecutionOperand(instruction.oprand1);
                        setExecutionValue(instruction.result, source.type, source.value);
                    }
                    else if (instruction.op == "jump")
                    {
                        nextPointer = getExecutionTarget(instruction.result);
                    }
                    else if (instruction.op == "jnz")
                    {
                        triple condition = getExecutionOperand(instruction.oprand1);
                        if (condition.type != "bool") throw new InvalidOperationException("跳转条件必须是 bool");
                        if (condition.value == "true") nextPointer = getExecutionTarget(instruction.result);
                    }
                    else if (instruction.op == "not")
                    {
                        triple value = getExecutionOperand(instruction.oprand1);
                        if (value.type != "bool") throw new InvalidOperationException("not 的操作数必须是 bool");
                        setExecutionValue(instruction.result, "bool", value.value == "true" ? "false" : "true");
                    }
                    else
                    {
                        triple left = getExecutionOperand(instruction.oprand1);
                        triple right = getExecutionOperand(instruction.oprand2);
                        executeBinaryTAC(instruction, left, right);
                    }
                }
                catch (InvalidOperationException ex)
                {
                    throw new InvalidOperationException("第 " + executionPointer + " 条四元式：" + ex.Message);
                }
                executionPointer = nextPointer;
                executionSteps++;
            }
        }

        private int getExecutionTarget(string target)
        {
            int result;
            if (!int.TryParse(target, out result) || result < 0 || result > executionLimit)
                throw new InvalidOperationException("无效的跳转目标 " + target);
            return result;
        }

        private triple getExecutionVariable(string name)
        {
            triple result = idenTable.getIdentifierByName(name);
            if (result != null) return result;
            foreach (triple t in tempVarTable.table)
                if (t.name == name) return t;
            return null;
        }

        private triple getExecutionOperand(string operand)
        {
            if (operand == "true" || operand == "false")
                return new triple(operand) { type = "bool", value = operand, isInitialized = true };
            if (operand.Length >= 2 && operand[0] == '"' && operand[operand.Length - 1] == '"')
                return new triple(operand) { type = "string", value = operand.Substring(1, operand.Length - 2), isInitialized = true };
            if (operand.Length > 0 && operand.All(c => c >= '0' && c <= '9'))
                return new triple(operand) { type = "int", value = operand, isInitialized = true };

            triple result = getExecutionVariable(operand);
            if (result == null) throw new InvalidOperationException("未知操作数 " + operand);
            if (!result.isInitialized) throw new InvalidOperationException("变量尚未赋值：" + operand);
            return result;
        }

        private void setExecutionValue(string name, string type, string value)
        {
            triple target = getExecutionVariable(name);
            if (target == null) throw new InvalidOperationException("未知结果变量 " + name);
            if (target.type != type) throw new InvalidOperationException("结果类型不匹配：" + name);
            target.value = value;
            target.isInitialized = true;
        }

        private BigInteger getExecutionNumber(triple value)
        {
            if (value.type == "bool") return value.value == "true" ? BigInteger.One : BigInteger.Zero;
            if (value.type == "int") return BigInteger.Parse(value.value);
            throw new InvalidOperationException("操作数不是整数或布尔值");
        }

        private void executeBinaryTAC(TAC instruction, triple left, triple right)
        {
            string op = instruction.op;
            if (op == "and" || op == "or")
            {
                if (left.type != "bool" || right.type != "bool")
                    throw new InvalidOperationException("逻辑运算的操作数必须是 bool");
                bool a = left.value == "true";
                bool b = right.value == "true";
                bool result = op == "and" ? a && b : a || b;
                setExecutionValue(instruction.result, "bool", result ? "true" : "false");
                return;
            }

            if (op == "lt" || op == "gt" || op == "lte" || op == "gte" || op == "eq" || op == "noteq")
            {
                if (left.type != right.type) throw new InvalidOperationException("关系运算类型不匹配");
                int comparison = left.type == "string"
                    ? left.value.Length.CompareTo(right.value.Length)
                    : getExecutionNumber(left).CompareTo(getExecutionNumber(right));
                bool result = op == "lt" ? comparison < 0 : op == "gt" ? comparison > 0 :
                              op == "lte" ? comparison <= 0 : op == "gte" ? comparison >= 0 :
                              op == "eq" ? comparison == 0 : comparison != 0;
                setExecutionValue(instruction.result, "bool", result ? "true" : "false");
                return;
            }

            if (left.type == "string")
            {
                if (op == "add" && right.type == "string")
                    setExecutionValue(instruction.result, "string", left.value + right.value);
                else if (op == "mul" && right.type == "int")
                {
                    BigInteger count = getExecutionNumber(right);
                    int repeats = count > 2 ? 2 : (int)count;
                    string value = "";
                    for (int i = 0; i < repeats; i++) value += left.value;
                    setExecutionValue(instruction.result, "string", value);
                }
                else throw new InvalidOperationException("字符串运算类型不匹配");
                return;
            }

            if (left.type != right.type) throw new InvalidOperationException("算术运算类型不匹配");
            BigInteger number1 = getExecutionNumber(left);
            BigInteger number2 = getExecutionNumber(right);
            BigInteger number;
            if (op == "add") number = number1 + number2;
            else if (op == "sub") number = BigInteger.Max(BigInteger.Zero, number1 - number2);
            else if (op == "mul") number = number1 * number2;
            else if (op == "div" || op == "mod")
            {
                if (number2.IsZero) throw new InvalidOperationException("除数不能为 0");
                number = op == "div" ? number1 / number2 : number1 % number2;
            }
            else throw new InvalidOperationException("未知四元式操作符 " + op);

            string text = number.ToString();
            if (op == "mul" && text.Length > 4) text = text.Substring(0, 4);
            setExecutionValue(instruction.result, "int", text);
        }

        private void printExecutionTables()
        {
            print("标识符表：");
            printExecutionTable(idenTable.table);
            print("临时变量表：");
            printExecutionTable(tempVarTable.table);
        }

        private void printExecutionTable(List<triple> table)
        {
            print("name | type | value");
            foreach (triple t in table)
            {
                string value = !t.isInitialized ? "未赋值" : t.type == "string" ? "\"" + t.value + "\"" : t.value;
                print(t.name + " | " + t.type + " | " + value);
            }
        }
        #endregion

        private void button4_Click(object sender, EventArgs e)
        {
            try
            {
                isDebug = true;
                listBox1.Items.Clear();
                listBox2.Items.Clear();
                completedASTNodes.Clear();

                sourceProgram = textBox1.Text + "#";
                pointer = 0;


                idenTable = new identifierTable();
                tempVarTable = new tempVariableTable();
                // ast = new node("topLevek");

                curentToken = nextInput();
                debug(curentToken.ToString());

                ast=translateASTProgram();
            

                print(ast.ToString());
                print("括号树：" + ast.toBracketString());

                string path = Application.StartupPath + @"\outputAST.txt";
                ast.saveToTXT(path);
                print("Save to " + path);
            }
            catch (InvalidOperationException ex)
            {
                if (completedASTNodes.Count > 0)
                {
                    print("已完成的 AST 片段：");
                    foreach (node completedNode in completedASTNodes)
                        print(completedNode.toBracketString());
                }
                print("分析错误：" + ex.Message);
            }
        }
        #region 语义分析AST
        private node translateASTProgram()
        {
            debug("推导:<程序> → <变量说明部分> <语句部分>");

            node root = new node("topLevel");
           node c1 =translateASTDeclareSection();
          node c2  =translateASTStatementSection();

            root.addChild(c1);
            root.addChild(c2);

            if (curentToken.type != "#")
                throw new InvalidOperationException("程序末尾存在多余内容：" + curentToken.value);
            debug("语法分析结束");
            return root;
        }
        private node translateASTDeclareSection()
        {
            debug("推导:<变量说明部分>→<变量说明语句> 分号  A ");

            node root = new node("declareList");

           node c1= translateASTDeclareStatement();
            match("分号");
            completedASTNodes.Add(c1);
            // if (!match("分号")) { return; };

         node c2=   translateASTA();
            root.addChild(c1);
            root.addChild(c2);
            return root;
        }
        //todo
        private node translateASTA()
        {

            debug("选择产生式:A →<变量说明语句> 分号 A | ε");

            if (curentToken.type == "变量说明")
            {


                debug("推导：A →<变量说明语句> 分号 A");

                node root = new node("linkDeclareList");

                node c1=translateASTDeclareStatement();
                match("分号");
                completedASTNodes.Add(c1);
                // if (!match("分号")) { return; };
                node c2=translateASTA();

                root.addChild(c1);
                root.addChild(c2);
                return root;
            }

            else if (curentToken.type == "标识符" || curentToken.type == "if" || curentToken.type == "while")
            {
                
                debug("推导：A→ε");
                return new node("emptyDeclareList");
            }

            return null;
        }
        private node translateASTDeclareStatement()
        {
            debug("推导:<变量说明语句> → <变量说明> <标识符列表>");


            node root = new node("declare");

            node c1 = new node(curentToken.value);
            astDeclareType = curentToken.value;
            match("变量说明");

            node c2=translateASTVarList();
            root.addChild(c1);
            root.addChild(c2);
            return root;
        }
        private node translateASTVarList()
        {
            debug("推导:<标识符列表> →标识符 B");

            node root = new node("varList");

            node c1 = new node(curentToken.value);
            match("标识符");
            if (!idenTable.Add(c1.root)) throw new InvalidOperationException("重复声明标识符 " + c1.root);
            idenTable.UpdateTypeByName(c1.root, astDeclareType);
          node c2=  translateASTB();

            root.addChild(c1);
            root.addChild(c2);
            return root;
        }

        private node translateASTB()
        {
            debug("选择产生式:B →逗号 标识符 B | ε");
            if (curentToken.type == "逗号")
            {


                debug("推导：B →逗号 标识符 B ");

                node root = new node("linkVarList");

               
                match("逗号");
                node c1 = new node(curentToken.value);
                match("标识符");                // if (!match("逗号")) { return; };
                if (!idenTable.Add(c1.root)) throw new InvalidOperationException("重复声明标识符 " + c1.root);
                idenTable.UpdateTypeByName(c1.root, astDeclareType);
                node c2=translateASTB();

                root.addChild(c1);
                root.addChild(c2);
                return root;
            }

            else if (curentToken.type == "分号")
            {

                debug("推导：B →ε");

                return new node("emptyVarList");
            }
            else
            {
                debug($"语法错误：期望 逗号 或 分号，实际遇到 {curentToken.type}");
            }
            return null;
        }

        private node translateASTStatementSection()
        {
            debug("推导:<语句部分> →<语句> C");

            node root = new node("seqList");
            node c1=translateASTStatement();
            node c2=translateASTC();

            root.addChild(c1);
            root.addChild(c2);
            return root;
        }
        private node translateASTC()
        {

            debug("选择产生式:C →分号 <语句> C | ε");
            if (curentToken.type == "分号")
            {


                debug("推导：C →分号 <语句> C ");
                node root = new node("linkSeqList");
                match("分号");
                node c1=translateASTStatement();
                node c2=translateASTC();

                root.addChild(c1);
                root.addChild(c2);
                return root;
            }

            else if (curentToken.type == "#" || curentToken.type == "end")
            {
                debug("推导：C →ε");
                return new node("emptySeqList");
            }
            else
            {
                debug($"语法错误：期望 逗号 或 分号，实际遇到 {curentToken.type}");
            }
            return null;
        }
        private node translateASTStatement()
        {
            debug("选择产生式:<语句> → <赋值语句>|<条件语句>");
            int completedBefore = completedASTNodes.Count;
            node statement;

            if (curentToken.type == "标识符")
            {


                debug("推导：<语句> → <赋值语句>");
                statement = translateASTAssignmentStatement();
            }

            else if (curentToken.type == "if")
            {
                debug("推导：<语句> → <条件语句>");
                statement = translateASTConditionalStatement();
            }
            else if (curentToken.type == "while")
            {
                debug("推导：<语句> → <循环语句>");
                statement = translateASTWhileStatement();
            }
            else
            {
                throw new InvalidOperationException($"语法错误：语句不能以 {curentToken.type} 开头");
            }
            // 外层语句完成后，用完整节点替换其内部已经记录的片段。
            completedASTNodes.RemoveRange(completedBefore, completedASTNodes.Count - completedBefore);
            completedASTNodes.Add(statement);
            return statement;
        }

        private node translateASTAssignmentStatement()
        {
            debug("推导:<赋值语句> → 标识符 赋值号 <表达式>");
            node root = new node("assign");

            node c1=new node(curentToken.value);
            match("标识符");
            match("赋值号");
           node c2= translateASTExpression();
            triple target = idenTable.getIdentifierByName(c1.root);
            if (target == null) throw new InvalidOperationException("未声明的标识符 " + c1.root);
            if (target.type != c2.type) throw new InvalidOperationException("赋值类型不匹配：" + c1.root);
            root.addChild(c1);
            root.addChild(c2);
            return root;
        }
        private node translateASTExpression()
        {
            debug("推导:<表达式> → <conjunction> D");
            return translateASTD(translateASTConjunction());
        }
        private node translateASTD(node left)
        {
            debug("选择产生式:D → or <conjunction> D | ε");
            if (curentToken.type != "or") return left;
            match("or");
            node root = new node("or");
            root.addChild(left);
            node right = translateASTConjunction();
            if (left.type != "bool" || right.type != "bool") throw new InvalidOperationException("or 的操作数必须是 bool");
            root.addChild(right);
            root.type = "bool";
            return translateASTD(root);
        }

        private node translateASTConjunction()
        {
            debug("推导:<conjunction> → <inversion> G");
            return translateASTG(translateASTInversion());
        }

        private node translateASTG(node left)
        {
            debug("选择产生式:G → and <inversion> G | ε");
            if (curentToken.type != "and") return left;
            match("and");
            node root = new node("and");
            root.addChild(left);
            node right = translateASTInversion();
            if (left.type != "bool" || right.type != "bool") throw new InvalidOperationException("and 的操作数必须是 bool");
            root.addChild(right);
            root.type = "bool";
            return translateASTG(root);
        }

        private node translateASTInversion()
        {
            debug("选择产生式:<inversion > → not < inversion > | < 关系表达式 >");

            if (curentToken.type == "not")
            {


                debug("推导：<inversion > → not < inversion > ");

                node root = new node("not");
                match("not");
              node c1=  translateASTInversion();
                if (c1.type != "bool") throw new InvalidOperationException("not 的操作数必须是 bool");

                root.addChild(c1);
                root.type = "bool";
                return root;

            }

            else if (curentToken.type == "标识符" || curentToken.type == "true"
                || curentToken.type == "false" || curentToken.type == "整数" || curentToken.type == "字符串"
                || curentToken.type == "左括号")
            {
                debug("推导：<inversion > →< 关系表达式 > ");
               return  translateASTRelationExpression();

            }

            throw new InvalidOperationException("期望表达式，实际遇到 " + curentToken.value);
        }

        private node translateASTRelationExpression()
        {
            debug("推导:<关系表达式 > → <算术表达式> E");
            
          node c1=  translateASTMathExpression();
           node c2= translateASTE();

            if (c2.root == "empty")
            {
                return c1;
            }
            else
            {
                if (c1.type != c2.children[0].type) throw new InvalidOperationException("关系运算的类型不匹配");
                c2.type = "bool";
                c2.addNodeAsFirstChild(c1);
                return c2;
            }
        }
        private node translateASTE()
        {
            debug("选择产生式:E →关系 <算术表达式> | ε");

            if (curentToken.type == "关系")
            {


                debug("推导：E →关系 <算术表达式> ");
                string op = curentToken.value;

                if (op == "<") op = "lt";
                else if (op == ">") op = "gt";
                else if (op == "<>") op = "noteq";
                else if (op == "==") op = "eq";
                else if (op == "<=") op = "lte";
                else if (op == ">=") op = "gte";

                match("关系");
               node c1= translateASTMathExpression();
                 node root= new node(op);
                root.addChild(c1);

                return root;

            }

            else if (curentToken.type == "分号" || curentToken.type == "#"
                || curentToken.type == "end" || curentToken.type == "右括号"
                 || curentToken.type == "or" || curentToken.type == "and")
            {
                debug("推导：E → ε");
                return new node("empty");
            }

            throw new InvalidOperationException("表达式中遇到意外的单词：" + curentToken.value);
        }
        private node translateASTMathExpression()
        {
            debug("推导:<算术表达式> → <term> H");
            return translateASTH(translateASTTerm());
        }

        private node translateASTH(node left)
        {
            debug("选择产生式:H → 加法 <term> H | ε");
            if (curentToken.type != "加法") return left;
            string op = curentToken.value == "+" ? "add" : "sub";
            match("加法");
            node root = new node(op);
            root.addChild(left);
            node right = translateASTTerm();
            root.type = checkASTArithmetic(op, left, right);
            root.addChild(right);
            return translateASTH(root);
        }

        private node translateASTTerm()
        {
            debug("推导:<term> → <factor> F");
            return translateASTF(translateASTFactor());
        }

        private node translateASTF(node left)
        {
            debug("选择产生式:F → 乘法 <factor> F | ε");
            if (curentToken.type != "乘法") return left;
            string op = curentToken.value == "*" ? "mul" : curentToken.value == "/" ? "div" : "mod";
            match("乘法");
            node root = new node(op);
            root.addChild(left);
            node right = translateASTFactor();
            root.type = checkASTArithmetic(op, left, right);
            root.addChild(right);
            return translateASTF(root);
        }
        private string checkASTArithmetic(string op, node left, node right)
        {
            if (op == "add" && left.type == "string" && right.type == "string") return "string";
            if (op == "mul" && left.type == "string" && right.type == "int" && right.isIntegerLiteral) return "string";
            if (left.type != right.type || (left.type != "int" && left.type != "bool"))
                throw new InvalidOperationException("运算类型不匹配：" + op);
            if ((op == "div" || op == "mod") &&
                ((right.isIntegerLiteral && right.root.All(c => c == '0')) ||
                 (right.type == "bool" && right.root == "false")))
                throw new InvalidOperationException("除数不能为 0");
            return "int";
        }
        private node translateASTFactor()
        {
            debug("选择产生式:<factor> → 标识符 | true | false | 整数 | 左括号 <表达式> 右括号");

            if (curentToken.type == "标识符")
            {
                debug("推导：<factor>→ 标识符 ");
                node leave = new node(curentToken.value);
                match("标识符");
                triple identifier = idenTable.getIdentifierByName(leave.root);
                if (identifier == null) throw new InvalidOperationException("未声明的标识符 " + leave.root);
                leave.type = identifier.type;
                return leave;
            }
            else if (curentToken.type == "true")
            {
                debug("推导：<factor>→ true ");
                match("true");

                return new node("true") { type = "bool" };
            }
            else if (curentToken.type == "false")
            {
                debug("推导：<factor>→ false ");
                match("false");

                return new node("false") { type = "bool" };
            }
            else if (curentToken.type == "整数")
            {
                debug("推导：<factor>→ 整数 ");

                node leave = new node(curentToken.value);
                match("整数");
                leave.type = "int";
                leave.isIntegerLiteral = true;
                return leave;
            }
            else if (curentToken.type == "字符串")
            {
                node leave = new node(curentToken.value);
                match("字符串");
                leave.type = "string";
                return leave;
            }
            else if (curentToken.type == "左括号")
            {
                debug("推导：左括号 <表达式> 右括号 ");
                match("左括号");
               node c1= translateASTExpression();
                match("右括号");
                return c1;
            }
            throw new InvalidOperationException("表达式中遇到意外的单词：" + curentToken.value);

        }


        private node  translateASTConditionalStatement()
        {
            debug("推导：<条件语句> → if 左括号 <表达式> 右括号  <复合语句>  else  <复合语句>");

            node root=new node("ifThenElse");
            match("if");
            match("左括号");
           node c1= translateASTExpression();
            if (c1.type != "bool") throw new InvalidOperationException("if 条件必须是 bool");
            match("右括号");
         node c2=   translateASTNestedStatement();
            match("else");
         node c3=   translateASTNestedStatement();

            root.addChild(c1);
            root.addChild(c2);
            root.addChild(c3);
            return root;
        }
        private node translateASTWhileStatement()
        {
            debug("推导：<循环语句> → while ( <表达式> ) : <嵌套语句>");
            match("while");
            match("左括号");
            node condition = translateASTExpression();
            if (condition.type != "bool") throw new InvalidOperationException("while 条件必须是 bool");
            match("右括号");
            match("冒号");
            node body = translateASTNestedStatement();
            node root = new node("while");
            root.addChild(condition);
            root.addChild(body);
            return root;
        }
        private node translateASTNestedStatement()
        {
            if (curentToken.type == "begin") return translateASTCompoundStatement();
            node statement = translateASTStatement();
            match("分号");
            return statement;
        }
        private node translateASTCompoundStatement()
        {
            debug("推导：<复合语句> → begin <语句部分> end");
            match("begin");
           node c1= translateASTStatementSection();
            match("end");

            return c1;
        }
        #endregion
    }
}
