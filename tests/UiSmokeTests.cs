using System;
using System.Reflection;
using System.Windows.Forms;
using compilerDEMO;

internal static class UiSmokeTests
{
    [STAThread]
    private static void Main()
    {
        using (var form = new Form1())
        {
            var input = (TextBox)form.Controls.Find("textBox1", false)[0];
            var output = (ListBox)form.Controls.Find("listBox1", false)[0];
            input.Text = "int $x$; $x$:=1+2";
            string[] expected = { "(变量说明,int)", "语法分析成功", "(0) (declare, int, null, $x$.type)", "topLevel" };
            for (int i = 1; i <= 4; i++)
            {
                typeof(Form1).GetMethod("button" + i + "_Click", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(form, new object[] { null, EventArgs.Empty });
                if (output.Items.Count == 0 || !output.Items[0].ToString().StartsWith(expected[i - 1]))
                    throw new Exception("按钮 " + i + " 输出错误");
            }
        }
        Console.WriteLine("4 个窗口入口检查通过");
    }
}
