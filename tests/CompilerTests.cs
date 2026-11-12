using System;
using System.Linq;
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
        }
        Console.WriteLine("通过 " + count + " 项检查");
    }
}
