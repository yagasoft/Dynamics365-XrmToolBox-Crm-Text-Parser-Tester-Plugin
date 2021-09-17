using System.Windows.Forms;
using Yagasoft.CrmTextParserTesterPlugin.Control.Interfaces;
using Yagasoft.Libraries.Common;

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	public partial class EditorControl : UserControl, IEditor
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
			richTextBox1.Text = text.IsEmpty() ? "Start typing ..." : text;
		}
	}
}
