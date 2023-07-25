#region Imports

using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Yagasoft.CrmTextParserTesterPlugin.Control.Interfaces;

#endregion

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	public partial class BrowserEditorControl : UserControl, IEditor, IEditorAync
	{
		public bool IsInitialised { get; set; }

		public BrowserEditorControl()
		{
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
				return Regex.Unescape(await webView21.CoreWebView2.ExecuteScriptAsync("getEditorData()")).Trim('"');
			}
			catch
			{
				return string.Empty;
			}
		}

		public async Task SetText(string text)
		{
			if (!IsInitialised)
			{
				try
				{
					await webView21.EnsureCoreWebView2Async();
					IsInitialised = true;
				}
				catch (DllNotFoundException)
				{
					MessageBox.Show(
						$"Failed to automatically add 'WebView2Loader.dll' to the runtime, which is required for HTML editing.\r\n\r\n"
							+ $"Please download it and add it to the following path under the XrmToolBox folder: Plugins/runtimes/win-x64/native/WebView2Loader.dll",
						$"WebView2Loader Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
					return;
				}
				catch (WebView2RuntimeNotFoundException)
				{
					var result = MessageBox.Show($"Microsoft WebView2 Runtime is required for HTML editing.\r\n\r\n"
						+ $"Would like to go to the official page to download it?",
						$"WebView2 Runtime Not Found", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

					if (result == DialogResult.Yes)
					{
						Process.Start(new ProcessStartInfo("https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section"));
					}

					return;
				}

				var content =
					@"
<!DOCTYPE html>
<html>
  <head>
    <meta charset='utf-8'>
    <title>CKEditor</title>
    <script src='https://cdn.ckeditor.com/4.16.2/full-all/ckeditor.js'></script>
  </head>
  <body>
    <textarea name='editor1' id='editor1'>
    </textarea>
    <script>
      const config =
      {
        editorplaceholder: 'Start typing …',
        allowedContent: true,
        fullPage: true
      };

      CKEDITOR.replace('editor1', config);

      var editor = CKEDITOR.dom.element.get('editor1').getEditor();

      if (editor)
      {
          editor.on('instanceReady', function(event)
          {
              if(event.editor.getCommand('maximize').state == CKEDITOR.TRISTATE_OFF);  //ckeck if maximize is off
              {
                  event.editor.execCommand('maximize');
              }
          });
      }

      var getEditorData = () => editor?.getData();
      var setEditorData = (data) => editor?.setData(data);
    </script>
  </body>
</html>";

				webView21.NavigateToString(content);
			}

			text ??= string.Empty;

			bool isRetry;

			do
			{
				await Task.Delay(500);

				await webView21.CoreWebView2.ExecuteScriptAsync($"setEditorData({ToLiteral(text)})");

				var currentText = await GetTextAsync();
				isRetry = currentText == "null" || string.IsNullOrWhiteSpace(currentText)
					|| (
						((text.Contains("<html>") && !text.Contains("<body></body>"))
							|| (text.Any() && !text.Contains("<html>")))
							&& currentText.Contains("<body></body>"));
			}
			while (isRetry);
		}

		private static string ToLiteral(string input)
		{
			using var writer = new StringWriter();
			using var provider = CodeDomProvider.CreateProvider("CSharp");
			provider.GenerateCodeFromExpression(new CodePrimitiveExpression(input), writer, null);
			return writer.ToString();
		}
	}
}
