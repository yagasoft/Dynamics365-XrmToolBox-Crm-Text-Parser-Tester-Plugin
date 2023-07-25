#region Imports

using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yagasoft.CrmTextParserTesterPlugin.Control.Interfaces;
using Yagasoft.CrmTextParserTesterPlugin.Helpers;

#endregion

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	public partial class TemplateEditor : UserControl
	{
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

		public TemplateEditor(WorkerHelper workerHelper)
		{
			this.workerHelper = workerHelper;
			InitializeComponent();
		}

		private async void TemplateEditor_Load(object sender, EventArgs e)
		{
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

			await SetEditorText(currentControl == editorControl ? editorText : outputText, isOutput);
		}

		public async Task<string> GetEditorText(UserControl control = null)
		{
			control ??= editorControl;

			if (control != currentControl)
			{
				return currentControl == editorControl ? editorText : outputText;
			}

			return await ((control as IEditorAync)?.GetTextAsync()
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
	}
}
