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
	public partial class EditorControl : UserControl
	{
		public EditorControl()
		{
			InitializeComponent();
		}

		public string GetText()
		{
			return richTextBox1.Text;
		}

		public void SetText(string text)
		{
			richTextBox1.Text = text;
		}
	}
}
