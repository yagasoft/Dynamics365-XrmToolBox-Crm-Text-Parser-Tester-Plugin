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

		public async void SetText(string content)
		{
			var filePath = Path.GetDirectoryName(new Uri(typeof(CoreWebView2Environment).Assembly.CodeBase).LocalPath) ?? ".";
			filePath = Path.Combine(filePath, "runtimes/win-x64/native/WebView2Loader.dll");

			if (!File.Exists(filePath))
			{
				var folder = Path.GetDirectoryName(filePath);

				if (!string.IsNullOrWhiteSpace(folder))
				{
					Directory.CreateDirectory(folder);
				}

				File.WriteAllBytes(filePath, Properties.Resources.WebView2Loader);
			}

			await webView21.EnsureCoreWebView2Async();

			webView21.NavigateToString(content);
		}

		private void button1_Click(object sender, EventArgs e)
		{
			templateEditor.ShowEditor();
		}
	}
}
