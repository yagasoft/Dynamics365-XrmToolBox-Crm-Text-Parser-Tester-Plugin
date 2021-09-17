using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	public partial class OutputControl : UserControl
	{
		private readonly TemplateEditor templateEditor;

		public OutputControl(TemplateEditor templateEditor)
		{
			this.templateEditor = templateEditor;
			InitializeComponent();
		}

		public void SetText(string text)
		{
			richTextBox1.Text = text;
		}

		private void button1_Click(object sender, EventArgs e)
		{
			templateEditor.ShowEditor();
		}
	}
}
