using System;
using System.Linq;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using compilerDEMO;

internal static class CompilerTests
{
    private static int count;

    private static void Check(bool condition, string message)
    {
        count++;
        if (!condition) throw new Exception(message);
    }

    private static string[] Run(Form1 form, int button, string source)
    {
        var input = (TextBox)form.Controls.Find("textBox1", false)[0];
        var output = (ListBox)form.Controls.Find("listBox1", false)[0];
        input.Text = source;
        typeof(Form1).GetMethod("button" + button + "_Click", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(form, new object[] { null, EventArgs.Empty });
        return output.Items.Cast<object>().Select(x => x.ToString()).ToArray();
    }

    [STAThread]
    private static void Main()
    {
        using (var form = new Form1())
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            Check(typeof(Form1).GetMethod("parseExpression", flags) != null &&
                  typeof(Form1).GetMethod("translateTACExpression", flags) != null &&
                  typeof(Form1).GetMethod("translateASTExpression", flags) != null, "原有三套分析方法应保留");

            string[] lex = Run(form, 1, "string $s$;\n$s$:=\"abc\"; while(trueandnot false):{$s$:=$s$+\"x\"}");
            Check(lex.Contains("(变量说明,string)") && lex.Contains("(字符串,\"abc\")") &&
                  lex.Contains("(while,while)") && lex.Contains("(and,and)") && lex.Contains("(冒号,:)")
                  && !lex.Any(x => x.StartsWith("(error,")), "完整版词法分析错误");

            const string program = "int $x$; $x$:=2; while($x$>0):{$x$:=$x$-1}; if($x$==0)$x$:=1; else $x$:=2;";
            string[] syntax = Run(form, 2, program);
            Check(syntax.Contains("语法分析结束") && !syntax.Any(x => x.Contains("错误")), "完整版语法分析错误");
            string[] tac = Run(form, 3, program);
            string[] expected = {
                "(0) (declare, int, null, $x$.type)", "(1) (assign, 2, null, $x$)",
                "(2) (gt, $x$, 0, T0)", "(3) (jnz, T0, null, 5)", "(4) (jump, null, null, 8)",
                "(5) (sub, $x$, 1, T1)", "(6) (assign, T1, null, $x$)", "(7) (jump, null, null, 2)",
                "(8) (eq, $x$, 0, T2)", "(9) (jnz, T2, null, 11)", "(10) (jump, null, null, 13)",
                "(11) (assign, 1, null, $x$)", "(12) (jump, null, null, 14)", "(13) (assign, 2, null, $x$)"
            };
            Check(tac.SequenceEqual(expected), "TAC 条件或循环回填错误：" + String.Join(" | ", tac));
            string[] ast = Run(form, 4, "int $x$; $x$:=9-3-2*4");
            Check(ast[0].Contains("assign  $x$  sub  sub  9  3  mul  2  4"), "AST 优先级或结合性错误");
            Check(ast[1].StartsWith("括号树：(topLevel)(") &&
                  ast[1].Contains("(sub)((sub)((9)(3))(mul)((2)(4)))"), "括号树未体现表达式父子关系");
            Check(File.ReadAllText(Path.Combine(Application.StartupPath, "outputAST.txt")).TrimEnd() == ast[0],
                  "AST 文件应保留附录要求的前序格式");

            tac = Run(form, 3, "bool $b$; $b$:=trueorfalseandnot false");
            Check(tac[1].Contains("(not,") && tac[2].Contains("(and,") && tac[3].Contains("(or,"), "逻辑优先级错误");
            tac = Run(form, 3, "string $s$; bool $b$; $s$:=\"ab\"*2+\"\"; $b$:=$s$<=\"abc\"");
            Check(tac.Any(x => x.Contains("(mul, \"ab\", 2,")) &&
                  tac.Any(x => x.Contains("(lte, $s$, \"abc\",")), "字符串或关系运算错误");
            tac = Run(form, 3, "int $x$; $x$:=9/2%3");
            Check(tac[1].Contains("(div,") && tac[2].Contains("(mod,"), "除法或取余错误");

            foreach (string invalid in new[] {
                "int $x$; int $x$; $x$:=1", "int $x$; $y$:=1", "int $x$; $x$:=true",
                "bool $b$; $b$:=1 and 2", "int $x$; $x$:=1/0", "int $x$; $x$:=1 2",
                "string $s$; $s$:=\"bad", "int $x$; while(1):{$x$:=1}"
            })
            {
                Check(Run(form, 3, invalid).Any(x => x.Contains("错误")), "应报告错误：" + invalid);
                Check(Run(form, 4, invalid).Any(x => x.Contains("错误")), "AST 应报告错误：" + invalid);
            }
            ast = Run(form, 4, program);
            Check(ast[0].Contains("while  gt  $x$  0") && ast[0].Contains("ifThenElse  eq  $x$  0"),
                  "AST 条件或循环错误");
            string nested = "int $x$; $x$:=2; while($x$>0):$x$:=$x$-1;";
            Check(Run(form, 2, nested).Contains("语法分析结束"), "单条循环体语法错误");
            Check(!Run(form, 3, nested).Any(x => x.Contains("错误")) &&
                  !Run(form, 4, nested).Any(x => x.Contains("错误")), "单条循环体翻译错误");
            Check(Run(form, 2, "int $x$; $x$:=1;").Any(x => x.Contains("错误")), "尾部分号应报错");
            Check(Run(form, 1, "int $x$; $x$:=123").Contains("(整数,123)"), "末尾整数丢失");
            Check(Run(form, 1, "int $x$; $x$:=1#2").Any(x => x.StartsWith("(error,")), "非法 # 被当作结束符");
            Check(Run(form, 3, "int $y$; $y$:=4*2")[1].Contains("T0"), "重复分析未重置临时变量");

            string partial = "int $x$; bool $flag$; $x$:=1; $x$:=$flag$/11";
            tac = Run(form, 3, partial);
            Check(tac.Length == 4 && tac[0] == "(0) (declare, int, null, $x$.type)" &&
                  tac[1] == "(1) (declare, bool, null, $flag$.type)" &&
                  tac[2] == "(2) (assign, 1, null, $x$)" && tac[3].StartsWith("分析错误："),
                  "TAC 出错时应先输出已生成的四元式，再输出错误");
            ast = Run(form, 4, partial);
            Check(ast.Length == 5 && ast[0] == "已完成的 AST 片段：" &&
                  ast[1].StartsWith("(declare)((int)") && ast[2].StartsWith("(declare)((bool)") &&
                  ast[3] == "(assign)(($x$)(1))" && ast[4].StartsWith("分析错误："),
                  "AST 出错时应保留已完成的声明和语句");

            string partialLoop = "int $x$; $x$:=1; while(true):{$x$:=2; $x$:=false}";
            tac = Run(form, 3, partialLoop);
            Check(tac.Any(x => x.Contains("(jump, null, null, 待回填)")) &&
                  tac.Any(x => x.Contains("(assign, 2, null, $x$)")) && tac.Last().StartsWith("分析错误："),
                  "循环出错时应保留已生成代码并标记未完成跳转");
            ast = Run(form, 4, partialLoop);
            Check(ast.Contains("(assign)(($x$)(2))") && ast.Last().StartsWith("分析错误："),
                  "嵌套语句中已完成的 AST 片段应保留");
            ast = Run(form, 4, "int $z$; $z$:=3");
            Check(ast[0].StartsWith("topLevel") && !ast.Any(x => x.Contains("已完成的 AST 片段")),
                  "成功分析不应显示前一次的部分结果");
            syntax = Run(form, 2, "int $q$; $q$:=");
            Check(!syntax.Any(x => x.Contains("(declare,")), "语法分析不应输出上一次的 TAC");

            string[] run = Run(form, 5, "int $a$; $a$:=3*4");
            Check(run.Contains("执行完成，共执行 3 条四元式") &&
                  run.Contains("$a$ | int | 12") && run.Contains("T0 | int | 12"),
                  "执行乘法时应计算临时变量和赋值结果");
            run = Run(form, 5, "int $a$,$b$,$c$,$d$; $a$:=123*456; $b$:=5-9; $c$:=7/2; $d$:=7%2");
            Check(run.Contains("$a$ | int | 5608") && run.Contains("$b$ | int | 0") &&
                  run.Contains("$c$ | int | 3") && run.Contains("$d$ | int | 1"),
                  "乘法前四位、减法下限、整除或取余执行错误");
            run = Run(form, 5, "int $a$,$b$; $a$:=12345678901234567890+1; $b$:=12345678901234567890*9");
            Check(run.Contains("$a$ | int | 12345678901234567891") && run.Contains("$b$ | int | 1111"),
                  "大整数不能在乘法截断之前溢出");
            run = Run(form, 5, "int $n$; bool $b$; $n$:=true+true; $b$:=not false and true or false");
            Check(run.Contains("$n$ | int | 2") && run.Contains("$b$ | bool | true"),
                  "布尔值数值运算或逻辑运算执行错误");
            run = Run(form, 5, "string $s$,$empty$; bool $eq$,$lt$; " +
                "$s$:=\"abc\"*9; $empty$:=\"\"; $eq$:=(\"ab\"==\"xy\"); $lt$:=(\"\"<\"z\")");
            Check(run.Contains("$s$ | string | \"abcabc\"") && run.Contains("$empty$ | string | \"\"") &&
                  run.Contains("$eq$ | bool | true") && run.Contains("$lt$ | bool | true"),
                  "字符串重复上限、空字符串或按长度比较错误");
            run = Run(form, 5, "int $s$,$z$; $z$:=0; $s$:=8/$z$");
            Check(run.Contains("$z$ | int | 0") && run.Contains("$s$ | int | 未赋值") &&
                  run.Last().Contains("运行错误") && run.Last().Contains("除数不能为 0"),
                  "运行时应检测通过变量产生的除零，并保留此前状态");
            run = Run(form, 5, "int $s$,$z$; $z$:=0; $s$:=0; if(false){$s$:=7/$z$}else{$s$:=5}");
            Check(run.Contains("$s$ | int | 5") && !run.Any(x => x.Contains("错误")),
                  "未执行的分支不能产生运行时除零错误");
            run = Run(form, 5, "int $x$; $x$:=3; while($x$>0):{$x$:=$x$-1}");
            Check(run.Contains("$x$ | int | 0") && run.Contains("T0 | bool | false"),
                  "循环应重复计算条件并更新变量");
            run = Run(form, 5, "int $a$,$b$; $a$:=$b$");
            Check(run.Last().Contains("变量尚未赋值：$b$") && run.Contains("$a$ | int | 未赋值"),
                  "读取未赋值变量应报告运行错误");
            run = Run(form, 5, "int $a$; $a$:=1; while(true):{$a$:=$a$}");
            Check(run.Last().Contains("执行超过 100000 步"), "无限循环应在步数上限停止");
            run = Run(form, 5, "int $a$; $a$:=true");
            Check(run.Last().StartsWith("分析错误：") && !run.Any(x => x.Contains("执行完成")),
                  "翻译失败时不能执行部分 TAC");
            var executeButton = (Button)form.Controls.Find("button5", false)[0];
            var astButton = (Button)form.Controls.Find("button4", false)[0];
            Check(executeButton.Text == "执行TAC" && executeButton.Left >= astButton.Right &&
                  executeButton.Right <= form.ClientSize.Width, "执行按钮的布局不正确");
            ((TextBox)form.Controls.Find("textBox1", false)[0]).Text = "int $x$; $x$:=2*3";
            typeof(Button).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(executeButton, new object[] { EventArgs.Empty });
            Check(((ListBox)form.Controls.Find("listBox1", false)[0]).Items.Cast<object>()
                  .Any(x => x.ToString() == "$x$ | int | 6"), "执行按钮没有连接到执行功能");
        }
        Console.WriteLine("通过 " + count + " 项检查");
    }
}
