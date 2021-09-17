using System;
using System.Windows.Forms;
using Yagasoft.CrmTextParserTesterPlugin.Helpers;
using Yagasoft.CrmTextParserTesterPlugin.Model.Settings;
using Yagasoft.CrmTextParserTesterPlugin.Model.ViewModels;

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	public partial class TemplateEditor : UserControl
	{
		private readonly TemplateViewModel templateViewModel;
		private readonly WorkerHelper workerHelper;

		public TemplateEditor(TemplateViewModel templateViewModel, Form parentForm, WorkerHelper workerHelper)
		{
			this.templateViewModel = templateViewModel;
			this.workerHelper = workerHelper;

			InitializeComponent();

			templateViewModel.CodeEditor = new EditorControl();
			templateViewModel.CodeEditor.Dock = DockStyle.Fill;

			templateViewModel.TextOutputControl = new OutputControl(this);
			templateViewModel.TextOutputControl.Dock = DockStyle.Fill;
		}

		private void TemplateEditor_Load(object sender, EventArgs e)
		{
			ShowEditor();
		}

		public void ShowEditor()
		{
			panelCodeEditor.Controls.Clear();
			panelCodeEditor.Controls.Add(templateViewModel.CodeEditor);
			templateViewModel.CodeEditor.Dock = DockStyle.Fill;
		}

		public string GetEditorText()
		{
			return templateViewModel.CodeEditor.GetText();
		}

		public void SetEditorText(string text)
		{
			templateViewModel.CodeEditor.SetText(text);
		}

		public void ShowOutput(string text)
		{
			templateViewModel.TextOutputControl.SetText(text);
			panelCodeEditor.Controls.Clear();
			panelCodeEditor.Controls.Add(templateViewModel.TextOutputControl);
			templateViewModel.CodeEditor.Dock = DockStyle.Fill;
		}
	}
}
