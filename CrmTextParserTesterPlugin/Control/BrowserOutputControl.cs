using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Core.Raw;
using Yagasoft.CrmTextParserTesterPlugin.Control.Interfaces;
using Yagasoft.Libraries.Common;

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	public partial class BrowserOutputControl : UserControl, IEditor, IEditorAync
	{
		private readonly TemplateEditor templateEditor;

		public BrowserOutputControl(TemplateEditor templateEditor)
		{
			this.templateEditor = templateEditor;
			InitializeComponent();
		}

		public string GetText()
		{
			return webView21.Text;
		}

		public async Task<string> GetTextAsync()
		{
			try
			{
				return Regex.Unescape(await webView21.CoreWebView2.ExecuteScriptAsync("document.getElementsByTagName('html')[0].innerHTML")).Trim('"');
			}
			catch
			{
				return string.Empty;
			}
		}

		public async Task SetText(string content)
		{
			await webView21.EnsureCoreWebView2Async();
			webView21.NavigateToString(content ?? string.Empty);
		}

		private async void button1_Click(object sender, EventArgs e)
		{
			await templateEditor.ShowEditor();
		}
	}
}
