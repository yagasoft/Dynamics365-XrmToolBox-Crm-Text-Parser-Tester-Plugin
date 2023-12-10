#region Imports

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using NuGet.Packaging.Signing;

using Yagasoft.CrmTextParserTesterPlugin.Control.Interfaces;
using Yagasoft.CrmTextParserTesterPlugin.Helpers;
using Yagasoft.CrmTextParserTesterPlugin.Model;
using Yagasoft.CrmTextParserTesterPlugin.Parsers;
using Yagasoft.Libraries.Common;
using EventHandler = System.EventHandler;

#endregion

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
    public partial class TemplateEditor : UserControl
    {
        private readonly ToolParameters toolParameters;
		
        private readonly WorkerHelper workerHelper;

        private bool isOutputShown;

        private UserControl currentControl;
        private UserControl browserEditorControl;
        private UserControl textEditorControl;
        private UserControl editorControl;
        private UserControl browserOutputControl;
        private UserControl textOutputControl;
        private UserControl outputControl;

        private string editorText;
        private string outputText;

        public TemplateEditor(WorkerHelper workerHelper, ToolParameters toolParameters)
        {
            this.workerHelper = workerHelper;
	        this.toolParameters = toolParameters;
	        InitializeComponent();
        }

        private async void TemplateEditor_Load(object sender, EventArgs e)
        {
	        var labels = new List<System.Windows.Forms.Control>();
			
	        foreach (var category in Constants.Constructs)
	        {
		        labels
						.Add(new Label
							 {
								 Text = category.Key,
								 Font = new Font(DefaultFont, FontStyle.Bold | FontStyle.Underline),
							AutoSize = true,
							Padding = new Padding(0, 1, 1, 1)
							 });

		        foreach (var construct in category.Key.StartsWith("Operators")
			        ? [..category.Value]
							 : category.Value.OrderBy(c => c.Key).ToArray())
		        {
			        var label =
				        new Label
						{
							Text = $"{construct.Key} \"{construct.Value}\"",
							AutoSize = true,
							Padding = new Padding(0, 1, 1, 1)
						};
					
			        label.Click +=
						async (_, _) =>
										 {
											 switch (currentControl)
											 {
												 case EditorControl control:
													 control.InsertText(construct.Value);
													 break;
												 
												 case BrowserEditorControl browserControl:
													 await browserControl.InsertText(construct.Value);
													 break;
											 }
										 };
					
			        labels.Add(label);
					
			        labels.Add(
							new Label
							{
								BorderStyle = BorderStyle.Fixed3D,
							Height = 2,
							MaximumSize = new Size(20, 2),
							Padding = new Padding(5, 1, 1, 1)
							});
		        }
	        }

			var flowLayoutPanel =
				new FlowLayoutPanel
				{
					FlowDirection = FlowDirection.TopDown,
					WrapContents = false,
					AutoScroll = true,
					AutoSize = true,
					Dock = DockStyle.Fill
				};

	        flowLayoutPanel.Controls.AddRange([..labels]);
			listBoxConstructs.Controls.Add(flowLayoutPanel);
			
            await ShowEditor();
        }

        public async Task ShowEditor(bool isOutput = false)
        {
            isOutputShown = isOutput;

            editorText = currentControl == editorControl ? await GetEditorText(currentControl) : editorText;
            outputText = currentControl == outputControl ? await GetEditorText(currentControl) : outputText;

            editorControl = checkBoxHtmlEditor.Checked
                ? browserEditorControl ??= new BrowserEditorControl()
                : textEditorControl ??= new EditorControl();

            outputControl = checkBoxHtmlOutput.Checked
                ? browserOutputControl ??= new BrowserOutputControl(this)
                : textOutputControl ??= new OutputControl(this);

            currentControl = isOutput ? outputControl : editorControl;

            panelCodeEditor.Controls.Clear();
            panelCodeEditor.Controls.Add(currentControl);

            currentControl.Dock = DockStyle.Fill;

            var latestCode = string.Empty;
            
            await webView21.EnsureCoreWebView2Async();

            var timestamp = DateTime.UtcNow.Ticks;

            async Task HighlightCode()
            {
                var snapTimestamp = timestamp = DateTime.UtcNow.Ticks; 
                await Task.Delay(1000);

                    if (snapTimestamp != timestamp)
                    {
                        return;
                    }
                
                var code = string.Empty;

                try
                {
                    code = await GetEditorText() ?? string.Empty;

                    if (code == latestCode)
                    {
                        return;
                    }

                    latestCode = code;

                    try
                    {
	                    code = await Task.FromResult(
		                    toolParameters.IsOldParser
			                    ? CrmParserOld.HighlightCode(code).Replace("```ELEMENT~~~", "div")
			                    : new CrmParser.Interpreter().Interpret(code)
				                    .Print(expressionWrapper: @"<div class=""code ```type"">```exp</div>"));
                    }
                    catch (Exception e)
                    {
                        code = $"<div style=\"color: red; font-weight: bold;\" class=\"code\">{e.Message}</div>";
                    }
                    finally
                    {
                        webView21.NavigateToString(
							toolParameters.IsOldParser
							?
$$"""
<html>
<head>
  <script src="https://code.jquery.com/jquery-3.7.1.slim.min.js"></script>
  <script>
	function bindHover()
	{
		$('.code')
			.on("mouseenter",
				(e) => {
					e.stopPropagation();
					$(e.target).addClass('code-hover')
						.parents().removeClass('code-hover');
				})
			.on("mouseleave", 
				(e) => {
					$(e.target).removeClass('code-hover');

					setTimeout(() =>
					{
						$('.code:hover').trigger('mouseover');
					}, 100);
				})
	}

	$(() => bindHover());
  </script>
  <style>
	.code {
		display:contents;
		font-family: Consolas;
	}

	.code-hover {
		color: #FF00FF !important;
		text-shadow: 1px 1px #EDEDED;
		font-weight: bold !important;
	}
  </style>
</head>
<body>
  {{code}}
</body>
</html>
"""
								:
$$"""
<html>
<head>
  <script src="https://code.jquery.com/jquery-3.7.1.slim.min.js"></script>
  <script>
    function bindHover()
    {
      $('.code')
        .on('mouseenter',
          (e) => {
            e.stopPropagation();
            $(e.target).addClass('code-hover')
              .parents().removeClass('code-hover');
          })
        .on('mouseleave', 
          (e) => {
            $(e.target).removeClass('code-hover');

            setTimeout(() =>
            {
              $('.code:hover').trigger('mouseover');
            }, 100);
          })
    }

    $(() => bindHover());
  </script>
  <style>
    .operator { color: #000000 }
    .scope { color: #0055e8 }
    .function { color: #a600ed }
    .object { color: #6e3c00 }
    .memory { color: #008f0c }
    .text { color: #9c9c9c }
    .literal { color: #d17a00 }
    .other { color: #000000 }

    .code {
      display:contents;
      font-family: Consolas;
    }

    .code-hover {
      color: #FF00FF !important;
      text-shadow: 2px 2px #EDEDED;
      font-weight: bold !important;
    }
  </style>
</head>
<body>
  {{code}}
</body>
</html>
""");
                    }
                }
                catch (Exception e)
                {
                    try
                    {
                        webView21.NavigateToString(e.Message);
                    }
                    catch
                    {
                        // ignored
                    }
                }
            }
            
            (editorControl as IContentChanged<EventHandler>)?
                .RegisterContentChange(async (_, _) => await HighlightCode());
            
            (editorControl as IContentChanged<EventHandler<CoreWebView2WebMessageReceivedEventArgs>>)?
                .RegisterContentChange(async (_, _) => await HighlightCode());
            
            await SetEditorText(currentControl == editorControl ? editorText : outputText, isOutput);
        }

        public async Task<string> GetEditorText(UserControl control = null)
        {
            control ??= editorControl;

            if (control != currentControl)
            {
                return currentControl == editorControl ? editorText : outputText;
            }

            return await ((control as IEditorAsync)?.GetTextAsync()
                ?? Task.FromResult(((control ?? editorControl) as IEditor)?.GetText()));
        }

        public async Task SetEditorText(string text, bool isOutput = false)
        {
            var control = (isOutput ? outputControl : editorControl) as IEditor;

            if (control == null)
            {
                return;
            }

            await control.SetText(text);
        }

        private async void checkBoxHtmlEditor_CheckedChanged(object sender, EventArgs e)
        {
            if (!isOutputShown)
            {
                checkBoxHtmlEditor.Enabled = false;

                try
                {
                    await ShowEditor();
                    await Task.Delay(300);
                }
                finally
                {
                    checkBoxHtmlEditor.Enabled = true;
                }
            }
        }

        private async void checkBoxHtmlOutput_CheckedChanged(object sender, EventArgs e)
        {
            if (isOutputShown)
            {
                checkBoxHtmlOutput.Enabled = false;

                try
                {
                    await ShowEditor(true);
                    await Task.Delay(300);
                }
                finally
                {
                    checkBoxHtmlOutput.Enabled = true;
                }
            }
        }

		private void checkBoxV5Parser_CheckedChanged(object sender, EventArgs e)
		{
			toolParameters.IsOldParser = checkBoxV5Parser.Enabled;
		}
	}
}
