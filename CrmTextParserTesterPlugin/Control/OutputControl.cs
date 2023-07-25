using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yagasoft.CrmTextParserTesterPlugin.Control.Interfaces;

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	public partial class OutputControl : UserControl, IEditor
	{
		private readonly TemplateEditor templateEditor;

		public OutputControl(TemplateEditor templateEditor)
		{
			this.templateEditor = templateEditor;
			InitializeComponent();
		}

		public string GetText()
		{
			return richTextBox1.Text;
		}

		public async Task SetText(string text)
		{
			richTextBox1.Text = text;
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			await templateEditor.ShowEditor();
		}
	}
}
