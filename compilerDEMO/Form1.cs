using System;
using System.IO;
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

        private void RunFullCompiler(int mode)
        {
            listBox1.Items.Clear();
            listBox2.Items.Clear();
            FullCompiler compiler = new FullCompiler(textBox1.Text);
            try
            {
                if (mode == 1)
                {
                    foreach (FullCompiler.Token token in compiler.Lex()) print(token.ToString());
                    print("词法分析结束");
                    return;
                }
                FullCompiler.Node program = compiler.Parse();
                if (mode == 2)
                {
                    print("语法分析成功");
                    return;
                }
                compiler.Generate(program);
                if (mode == 3)
                {
                    for (int i = 0; i < compiler.Code.Count; i++)
                        print("(" + i + ") " + compiler.Code[i]);
                }
                else
                {
                    string output = program.ToPrefix();
                    print(output);
                    string path = Path.Combine(Application.StartupPath, "outputAST.txt");
                    File.WriteAllText(path, output, Encoding.UTF8);
                    print("Save to " + path);
                }
            }
            catch (FullCompiler.CompileError ex)
            {
                print("分析错误：" + ex.Message);
            }
            catch (IOException ex)
            {
                print("输出文件错误：" + ex.Message);
            }
            finally
            {
                foreach (string entry in compiler.Trace) debug(entry);
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            RunFullCompiler(1);
        }

        private void button2_Click(object sender, EventArgs e)
        {
            RunFullCompiler(2);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            RunFullCompiler(3);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            RunFullCompiler(4);
        }
    }
}
