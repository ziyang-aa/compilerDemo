using System;
using System.Collections.Generic;
using System.Linq;
using compilerDEMO;

internal static class CompilerTests
{
    private static int checks;
    private static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    private static Form1 Compile(string source)
    {
        var compiler = new Form1();
        compiler.resetCompiler(source);
        var tree = compiler.parseProgram();
        compiler.translateTACProgram(tree);
        return compiler;
    }
    private static void Fails(string source, string expected)
    {
        try { Compile(source); }
        catch (Form1.compilerException ex) { Check(ex.Message.Contains(expected), ex.Message); return; }
        throw new Exception("Expected error: " + expected + " in " + source);
    }
    [STAThread]
    private static void Main()
    {
        var compiler = Compile("int $x$; $x$ := 9 - 3 - 2 * 4");
        Check(compiler.tacTable.Select(x => x.ToString()).SequenceEqual(new[] {
            "(declare, int, null, $x$.type)",
            "(sub, 9, 3, T0)",
            "(mul, 2, 4, T1)",
            "(sub, T0, T1, T2)",
            "(assign, T2, null, $x$)"
        }), "算术优先级或左结合错误");

        compiler = Compile("bool $b$; $b$ := true or false and not false");
        Check(compiler.tacTable[1].op == "not" && compiler.tacTable[2].op == "and" &&
              compiler.tacTable[3].op == "or", "逻辑运算优先级错误");
        compiler = Compile("bool $b$;$b$:=trueorfalseandnotfalse");
        Check(compiler.tacTable[3].op == "or", "无空格的关键字没有正确拆分");

        compiler = Compile("int $x$; $x$:=2; while($x$>0):{$x$:=$x$-1}; if($x$==0)$x$:=1; else $x$:=2;");
        Check(compiler.tacTable.Select(x => x.ToString()).SequenceEqual(new[] {
            "(declare, int, null, $x$.type)", "(assign, 2, null, $x$)",
            "(gt, $x$, 0, T0)", "(jnz, T0, null, 5)", "(jump, null, null, 8)",
            "(sub, $x$, 1, T1)", "(assign, T1, null, $x$)", "(jump, null, null, 2)",
            "(eq, $x$, 0, T2)", "(jnz, T2, null, 11)", "(jump, null, null, 13)",
            "(assign, 1, null, $x$)", "(jump, null, null, 14)", "(assign, 2, null, $x$)"
        }), "条件或循环的跳转目标错误");

        compiler = Compile("string $s$; bool $b$; $s$ := \"ab\" * 2 + \"\"; $b$ := $s$ <= \"abc\"");
        Check(compiler.tacTable.Any(x => x.ToString().StartsWith("(mul, \"ab\", 2,")) &&
              compiler.tacTable.Any(x => x.ToString().StartsWith("(lte, $s$, \"abc\",")), "字符串或关系运算错误");

        compiler = Compile("int $x$; bool $b$; $x$:=9/2%3; $b$:=1>=0");
        Check(compiler.tacTable.Select(x => x.op).SequenceEqual(new[] {
            "declare", "declare", "div", "mod", "assign", "gte", "assign"
        }), "除法、取余或 >= 识别错误");

        compiler = Compile("int $x$; bool $done$; string $text$;\n" +
            "$x$:=2+3*4; $text$:=\"ab\"*2+\"c\"; while($x$>0):{$x$:=$x$-1};" +
            "if($x$==0 and not false){$done$:=true}else{$done$:=false}");
        Check(compiler.tacTable.Count > 0, "窗口默认示例无法编译");

        var parser = new Form1();
        parser.resetCompiler("int $x$; $x$:=1+2*3");
        string ast = parser.parseProgram().toPrefix();
        Check(ast.Contains("assign  $x$  add  1  mul  2  3"), "AST 优先级或序列化错误");
        parser.resetCompiler("int\r\n$x$;\t$x$:=1");
        Check(parser.tokenize().Last().type == "#", "空白字符处理错误");

        Fails("int $x$; int $x$; $x$:=1", "重复声明");
        Fails("int $x$; $y$:=1", "未声明");
        Fails("int $x$; $x$:=true", "赋值类型");
        Fails("bool $b$; $b$:=1 and 2", "不支持");
        Fails("int $x$; $x$:=1/0", "除数不能为 0");
        Fails("int $x$; $x$:=1%00", "除数不能为 0");
        Fails("string $s$; int $n$; $s$:=\"a\"*$n$", "不支持");
        Fails("int $x$; $x$:=1 junk", "关键字");
        Fails("int $x$; $x$:=1 2", "期望 #");
        Fails("string $s$; $s$:=\"bad", "字符串");
        Fails("int $x$; while(1):{$x$:=1}", "条件必须是 bool");
        Fails("int $x$; $x$:=1;", "语句不能");
        Console.WriteLine("通过 " + checks + " 项检查");
    }
}
