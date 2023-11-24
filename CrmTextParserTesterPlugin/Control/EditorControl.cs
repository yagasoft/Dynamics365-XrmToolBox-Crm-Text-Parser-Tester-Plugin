using System;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Web.WebView2.Core;

using Yagasoft.CrmTextParserTesterPlugin.Control.Interfaces;
using Yagasoft.Libraries.Common;

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	public partial class EditorControl : UserControl, IEditor, IContentChanged<EventHandler>
	{
		public EditorControl()
		{
			InitializeComponent();
		}

		public string GetText()
		{
			return richTextBox1.Text;
		}

		public async Task SetText(string text)
		{
			richTextBox1.Text = text.IsEmpty() ? "Start typing ..." : text;
		}
		
		
		public async Task RegisterContentChange(EventHandler handler)
		{
			richTextBox1.TextChanged -= handler;
			richTextBox1.TextChanged += handler;
		}

	}
}
