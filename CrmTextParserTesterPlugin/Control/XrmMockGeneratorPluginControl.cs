#region Imports

using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Data.Linq;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using XrmToolBox.Extensibility;
using XrmToolBox.Extensibility.Args;
using XrmToolBox.Extensibility.Interfaces;
using Yagasoft.CrmTextParserTesterPlugin.Helpers;
using Yagasoft.CrmTextParserTesterPlugin.Model;
using Yagasoft.CrmTextParserTesterPlugin.Model.Settings;
using Yagasoft.CrmTextParserTesterPlugin.Model.ViewModels;
using Yagasoft.Libraries.Common;
using Label = System.Windows.Forms.Label;
using MessageBox = System.Windows.Forms.MessageBox;
using Point = System.Drawing.Point;

#endregion

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	public partial class PluginControl : PluginControlBase, IStatusBarMessenger, IGitHubPlugin, IPayPalPlugin, IHelpPlugin
	{
	    public string UserName => "yagasoft";

	    public string RepositoryName => "Dynamics365-XrmToolBox-CrmTextParser-Tester-Plugin";

	    public string EmailAccount => "mail@yagasoft.com";

	    public string DonationDescription => "Thank you!";

	    public string HelpUrl => "https://blog.yagasoft.com/2021/08/dynamics-365-dynamic-text-parser-supercharged-mage-series";

		private ToolStrip toolBar;
		private ToolStripButton buttonCloseTool;
		private ToolStripButton buttonGenerate;
		private ToolStripSeparator toolStripSeparator2;
		private ToolStripButton buttonClearCache;
		private ToolStripSeparator toolStripSeparator5;

		private Button buttonCancel;

		private TableLayoutPanel tableLayoutMainPanel;
		private TableLayoutPanel tableLayoutTopBar;
		private Panel panelHost;

		private ToolStripSeparator toolStripSeparator4;
		private ToolStripButton buttonTemplateEditor;
		private Panel panelToast;
		private Label labelToast;
		private ToolStripButton buttonDefaultT4;
		private ToolStripLabel labelYagasoft;

		private PluginSettings pluginSettings;

		private TemplateEditor templateEditor;
		private readonly TemplateViewModel templateViewModel;

		private readonly WorkerHelper workerHelper;
		private readonly UiHelper uiHelper;
		private ToolStripSeparator toolStripSeparator1;
		private ToolStripLabel labelQuickGuide;
		private BackgroundWorker currentWorker;

		#region Base tool implementation

		public PluginControl()
		{
			InitializeComponent();
			LoadPluginSettings();
			ShowReleaseNotes();

			workerHelper = new WorkerHelper(
				(s, work, callback) => InvokeSafe(() => RunAsync(s, work, callback)),
				(s, work, callback) => InvokeSafe(() => RunAsync(s, work, callback)));
			templateViewModel = new TemplateViewModel();
			uiHelper = new UiHelper(panelToast, labelToast, InvokeSafe);
		}

		private void InvokeSafe(Action action)
		{
			if (IsHandleCreated)
			{
				Invoke(action);
			}
			else
			{
				action();
			}
		}

		private void LoadPluginSettings()
		{
			try
			{
				SettingsManager.Instance.TryLoad(typeof(TemplateCodeGeneratorPlugin), out pluginSettings);
			}
			catch
			{
				// ignored
			}

			pluginSettings ??= new PluginSettings();
		}

		private void ShowReleaseNotes()
		{
			if (pluginSettings.ReleaseNotesShownVersion != Constants.AppVersion)
			{
				MessageBox.Show(Constants.ReleaseNotes, "Release Notes",
					MessageBoxButtons.OK, MessageBoxIcon.Information);

				pluginSettings.ReleaseNotesShownVersion = Constants.AppVersion;
				SettingsManager.Instance.Save(typeof(TemplateCodeGeneratorPlugin), pluginSettings);
			}
		}

		public override void ClosingPlugin(PluginCloseInfo info)
		{
			if (!PromptSave("Text"))
			{
				info.Cancel = true;
			}

			base.ClosingPlugin(info);
		}

		private void BtnCloseClick(object sender, EventArgs e)
		{
			CloseTool(); // PluginBaseControl method that notifies the XrmToolBox that the user wants to close the plugin
		}

		private static bool PromptSave(string name)
		{
			var result = MessageBox.Show($"{name} not saved. Are you sure you want to exit before saving?", $"{name} Not Saved",
				MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

			return result == DialogResult.Yes;
		}

		#endregion Base tool implementation

		#region UI Generated

		private void InitializeComponent()
		{
			System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PluginControl));
			this.toolBar = new System.Windows.Forms.ToolStrip();
			this.buttonCloseTool = new System.Windows.Forms.ToolStripButton();
			this.toolStripSeparator4 = new System.Windows.Forms.ToolStripSeparator();
			this.buttonTemplateEditor = new System.Windows.Forms.ToolStripButton();
			this.buttonDefaultT4 = new System.Windows.Forms.ToolStripButton();
			this.buttonGenerate = new System.Windows.Forms.ToolStripButton();
			this.toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
			this.buttonClearCache = new System.Windows.Forms.ToolStripButton();
			this.toolStripSeparator5 = new System.Windows.Forms.ToolStripSeparator();
			this.labelYagasoft = new System.Windows.Forms.ToolStripLabel();
			this.toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
			this.labelQuickGuide = new System.Windows.Forms.ToolStripLabel();
			this.buttonCancel = new System.Windows.Forms.Button();
			this.tableLayoutMainPanel = new System.Windows.Forms.TableLayoutPanel();
			this.panelHost = new System.Windows.Forms.Panel();
			this.tableLayoutTopBar = new System.Windows.Forms.TableLayoutPanel();
			this.panelToast = new System.Windows.Forms.Panel();
			this.labelToast = new System.Windows.Forms.Label();
			this.toolBar.SuspendLayout();
			this.tableLayoutMainPanel.SuspendLayout();
			this.panelToast.SuspendLayout();
			this.SuspendLayout();
			// 
			// toolBar
			// 
			this.toolBar.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.buttonCloseTool,
            this.toolStripSeparator4,
            this.buttonTemplateEditor,
            this.buttonDefaultT4,
            this.buttonGenerate,
            this.toolStripSeparator2,
            this.buttonClearCache,
            this.toolStripSeparator5,
            this.labelQuickGuide,
            this.toolStripSeparator1,
            this.labelYagasoft});
			this.toolBar.Location = new System.Drawing.Point(0, 0);
			this.toolBar.Name = "toolBar";
			this.toolBar.Size = new System.Drawing.Size(1000, 25);
			this.toolBar.TabIndex = 0;
			this.toolBar.Text = "toolBar";
			// 
			// buttonCloseTool
			// 
			this.buttonCloseTool.Image = ((System.Drawing.Image)(resources.GetObject("buttonCloseTool.Image")));
			this.buttonCloseTool.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.buttonCloseTool.Name = "buttonCloseTool";
			this.buttonCloseTool.Size = new System.Drawing.Size(56, 22);
			this.buttonCloseTool.Text = "Close";
			this.buttonCloseTool.Click += new System.EventHandler(this.BtnCloseClick);
			// 
			// toolStripSeparator4
			// 
			this.toolStripSeparator4.Name = "toolStripSeparator4";
			this.toolStripSeparator4.Size = new System.Drawing.Size(6, 25);
			// 
			// buttonTemplateEditor
			// 
			this.buttonTemplateEditor.Image = ((System.Drawing.Image)(resources.GetObject("buttonTemplateEditor.Image")));
			this.buttonTemplateEditor.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.buttonTemplateEditor.Name = "buttonTemplateEditor";
			this.buttonTemplateEditor.Size = new System.Drawing.Size(58, 22);
			this.buttonTemplateEditor.Text = "Editor";
			this.buttonTemplateEditor.Click += new System.EventHandler(this.buttonTemplateEditor_Click);
			// 
			// buttonDefaultT4
			// 
			this.buttonDefaultT4.Image = ((System.Drawing.Image)(resources.GetObject("buttonDefaultT4.Image")));
			this.buttonDefaultT4.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.buttonDefaultT4.Name = "buttonDefaultT4";
			this.buttonDefaultT4.Size = new System.Drawing.Size(55, 22);
			this.buttonDefaultT4.Text = "Reset";
			this.buttonDefaultT4.Click += new System.EventHandler(this.buttonDefaultT4_Click);
			// 
			// buttonGenerate
			// 
			this.buttonGenerate.Enabled = false;
			this.buttonGenerate.Image = ((System.Drawing.Image)(resources.GetObject("buttonGenerate.Image")));
			this.buttonGenerate.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.buttonGenerate.Name = "buttonGenerate";
			this.buttonGenerate.Size = new System.Drawing.Size(55, 22);
			this.buttonGenerate.Text = "Parse";
			this.buttonGenerate.Click += new System.EventHandler(this.buttonGenerate_Click);
			// 
			// toolStripSeparator2
			// 
			this.toolStripSeparator2.Name = "toolStripSeparator2";
			this.toolStripSeparator2.Size = new System.Drawing.Size(6, 25);
			// 
			// buttonClearCache
			// 
			this.buttonClearCache.Image = ((System.Drawing.Image)(resources.GetObject("buttonClearCache.Image")));
			this.buttonClearCache.ImageTransparentColor = System.Drawing.Color.Magenta;
			this.buttonClearCache.Name = "buttonClearCache";
			this.buttonClearCache.Size = new System.Drawing.Size(90, 22);
			this.buttonClearCache.Text = "Clear Cache";
			this.buttonClearCache.Click += new System.EventHandler(this.buttonClearCache_Click);
			// 
			// toolStripSeparator5
			// 
			this.toolStripSeparator5.Name = "toolStripSeparator5";
			this.toolStripSeparator5.Size = new System.Drawing.Size(6, 25);
			// 
			// labelYagasoft
			// 
			this.labelYagasoft.Font = new System.Drawing.Font("Verdana", 8F, System.Drawing.FontStyle.Bold);
			this.labelYagasoft.ForeColor = System.Drawing.Color.DarkViolet;
			this.labelYagasoft.IsLink = true;
			this.labelYagasoft.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
			this.labelYagasoft.LinkColor = System.Drawing.Color.DarkViolet;
			this.labelYagasoft.Name = "labelYagasoft";
			this.labelYagasoft.Size = new System.Drawing.Size(95, 22);
			this.labelYagasoft.Text = "Yagasoft.com";
			this.labelYagasoft.VisitedLinkColor = System.Drawing.Color.DarkBlue;
			this.labelYagasoft.Click += new System.EventHandler(this.labelYagasoft_Click);
			// 
			// toolStripSeparator1
			// 
			this.toolStripSeparator1.Name = "toolStripSeparator1";
			this.toolStripSeparator1.Size = new System.Drawing.Size(6, 25);
			// 
			// labelQuickGuide
			// 
			this.labelQuickGuide.Font = new System.Drawing.Font("Verdana", 8F, System.Drawing.FontStyle.Bold);
			this.labelQuickGuide.ForeColor = System.Drawing.Color.DarkViolet;
			this.labelQuickGuide.IsLink = true;
			this.labelQuickGuide.LinkBehavior = System.Windows.Forms.LinkBehavior.HoverUnderline;
			this.labelQuickGuide.LinkColor = System.Drawing.Color.DarkViolet;
			this.labelQuickGuide.Name = "labelQuickGuide";
			this.labelQuickGuide.Size = new System.Drawing.Size(84, 22);
			this.labelQuickGuide.Text = "Quick Guide";
			this.labelQuickGuide.VisitedLinkColor = System.Drawing.Color.DarkBlue;
			this.labelQuickGuide.Click += new System.EventHandler(this.labelQuickGuide_Click);
			// 
			// buttonCancel
			// 
			this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonCancel.Location = new System.Drawing.Point(948, 3);
			this.buttonCancel.Name = "buttonCancel";
			this.buttonCancel.Size = new System.Drawing.Size(49, 20);
			this.buttonCancel.TabIndex = 21;
			this.buttonCancel.Text = "Cancel";
			this.buttonCancel.UseVisualStyleBackColor = true;
			this.buttonCancel.Click += new System.EventHandler(this.buttonCancel_Click);
			// 
			// tableLayoutMainPanel
			// 
			this.tableLayoutMainPanel.ColumnCount = 1;
			this.tableLayoutMainPanel.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutMainPanel.Controls.Add(this.panelHost, 0, 0);
			this.tableLayoutMainPanel.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutMainPanel.Location = new System.Drawing.Point(0, 25);
			this.tableLayoutMainPanel.Name = "tableLayoutMainPanel";
			this.tableLayoutMainPanel.RowCount = 1;
			this.tableLayoutMainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 32F));
			this.tableLayoutMainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutMainPanel.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.tableLayoutMainPanel.Size = new System.Drawing.Size(1000, 416);
			this.tableLayoutMainPanel.TabIndex = 22;
			// 
			// panelHost
			// 
			this.panelHost.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panelHost.Location = new System.Drawing.Point(3, 3);
			this.panelHost.Name = "panelHost";
			this.panelHost.Size = new System.Drawing.Size(994, 410);
			this.panelHost.TabIndex = 1;
			// 
			// tableLayoutTopBar
			// 
			this.tableLayoutTopBar.ColumnCount = 3;
			this.tableLayoutTopBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 250F));
			this.tableLayoutTopBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutTopBar.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 220F));
			this.tableLayoutTopBar.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutTopBar.Location = new System.Drawing.Point(3, 3);
			this.tableLayoutTopBar.Name = "tableLayoutTopBar";
			this.tableLayoutTopBar.RowCount = 1;
			this.tableLayoutTopBar.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutTopBar.Size = new System.Drawing.Size(994, 26);
			this.tableLayoutTopBar.TabIndex = 0;
			// 
			// panelToast
			// 
			this.panelToast.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.panelToast.BackColor = System.Drawing.Color.Black;
			this.panelToast.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
			this.panelToast.Controls.Add(this.labelToast);
			this.panelToast.ForeColor = System.Drawing.Color.Black;
			this.panelToast.Location = new System.Drawing.Point(741, 341);
			this.panelToast.Name = "panelToast";
			this.panelToast.Size = new System.Drawing.Size(250, 65);
			this.panelToast.TabIndex = 0;
			this.panelToast.Visible = false;
			// 
			// labelToast
			// 
			this.labelToast.AutoEllipsis = true;
			this.labelToast.BackColor = System.Drawing.Color.DarkViolet;
			this.labelToast.Dock = System.Windows.Forms.DockStyle.Fill;
			this.labelToast.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
			this.labelToast.ForeColor = System.Drawing.Color.White;
			this.labelToast.Location = new System.Drawing.Point(0, 0);
			this.labelToast.Name = "labelToast";
			this.labelToast.Size = new System.Drawing.Size(248, 63);
			this.labelToast.TabIndex = 0;
			this.labelToast.Text = "<toast>";
			this.labelToast.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			// 
			// PluginControl
			// 
			this.Controls.Add(this.panelToast);
			this.Controls.Add(this.tableLayoutMainPanel);
			this.Controls.Add(this.buttonCancel);
			this.Controls.Add(this.toolBar);
			this.Name = "PluginControl";
			this.Size = new System.Drawing.Size(1000, 441);
			this.Load += new System.EventHandler(this.PluginControl_Load);
			this.toolBar.ResumeLayout(false);
			this.toolBar.PerformLayout();
			this.tableLayoutMainPanel.ResumeLayout(false);
			this.panelToast.ResumeLayout(false);
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		public event EventHandler<StatusBarMessageEventArgs> SendMessageToStatusBar;

		#region Event handlers

		private void PluginControl_Load(object sender, EventArgs e)
		{
			buttonCancel.Hide();

			templateEditor = new TemplateEditor(templateViewModel, ParentForm, workerHelper);
			ShowTemplateEditor();
		}

		private void buttonTemplateEditor_Click(object sender, EventArgs e)
		{
			ShowTemplateEditor();
		}

		private void buttonGenerate_Click(object sender, EventArgs eArgs)
		{
			ExecuteMethod(GenerateCode);
		}

		private void buttonClearCache_Click(object sender, EventArgs e)
		{
			uiHelper.ShowToast("Cache cleared.");
		}

		private void labelQuickGuide_Click(object sender, EventArgs e)
		{
			Process.Start(new ProcessStartInfo(HelpUrl));
		}

		private void labelYagasoft_Click(object sender, EventArgs e)
		{
			Process.Start(new ProcessStartInfo("https://yagasoft.com"));
		}

		private void linkQuickGuide_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
		{
			Process.Start(new ProcessStartInfo(HelpUrl));
		}

		private void buttonCancel_Click(object sender, EventArgs e)
		{
			currentWorker.ReportProgress(99, $"Cancelling ...");
			uiHelper.ShowToast("Generation cancelled.");
			currentWorker.CancelAsync();
		}

		private void buttonDefaultT4_Click(object sender, EventArgs e)
		{
			var result = MessageBox.Show($"Resetting the template will overwrite the one in the editor. Are you sure you want to proceed?",
				$"Template Reset", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

			if (result == DialogResult.Yes)
			{
				templateEditor.SetEditorText(string.Empty);
				uiHelper.ShowToast("Template text has been reset to the default.");
			}
		}

		#endregion

		private void RunAsync(string message, Action<Action<int, string>> work, Action callback = null)
		{
			RunAsync<object>(message,
				progressReporter =>
				{
					work(progressReporter);
					return null;
				},
				result => callback?.Invoke());
		}

		private void RunAsync<TOut>(string message, Func<Action<int, string>, TOut> work, Action<TOut> callback = null)
		{
			DisableTool();

			WorkAsync(
				new WorkAsyncInfo
				{
					Message = message,
					Work =
						(w, e) =>
						{
							try
							{
								work(w.ReportProgress);
							}
							finally
							{
								EnableTool();
							}
						},
					ProgressChanged =
						e =>
						{
							// If progress has to be notified to user, use the following method:
							SetWorkingMessage(e.UserState.ToString());

							// If progress has to be notified to user, through the
							// status bar, use the following method
							SendMessageToStatusBar?.Invoke(this,
								new StatusBarMessageEventArgs(e.ProgressPercentage, e.UserState.ToString()));
						},
					PostWorkCallBack =
						e =>
						{
							SendMessageToStatusBar?.Invoke(this, new StatusBarMessageEventArgs(""));
							callback?.Invoke((TOut)e.Result);
						},
					AsyncArgument = null,
					IsCancelable = false,
					MessageWidth = 340,
					MessageHeight = 150
				});
		}

		private void GenerateCode()
		{
			uiHelper.ShowToast("Generating text output ...");

			var template = templateEditor.GetEditorText();

			DisableTool();

			buttonCancel.Show();

			WorkAsync(
				new WorkAsyncInfo
				{
					Message = "Generating text output ...",
					Work =
						(w, e) =>
						{
							currentWorker = w;
							w.WorkerSupportsCancellation = true;

							try
							{
								w.ReportProgress(0, $"Generating text output ...");

								var output = string.Empty;

								var isError = false;

								try
								{
									var thread = new Thread(
										() =>
										{
											try
											{
												output = CrmParser
													.Parse(template,
														new Entity("ys_test", Guid.Parse("{4E8D9F14-37F9-EB11-9AC9-000D3A4C87C6}")),
														Service,
														Guid.NewGuid());
											}
											catch (ThreadAbortException)
											{ }
											catch (Exception ex)
											{
												isError = true;

												if (ex.InnerException is ThreadAbortException)
												{
													return;
												}

												MessageBox.Show(ex.ToString(), "Generation Error", MessageBoxButtons.OK,
													MessageBoxIcon.Error);
											}
										});
									thread.Start();

									while (thread.IsAlive)
									{
										if (w.CancellationPending)
										{
											thread.Abort();
										}
									}
								}
								catch (ThreadAbortException)
								{
									return;
								}
								catch (Exception ex)
								{
									if (ex.InnerException is not ThreadAbortException)
									{
										MessageBox.Show(ex.ToString(), "Generation Error", MessageBoxButtons.OK,
											MessageBoxIcon.Error);
									}

									return;
								}

								if (isError)
								{
									return;
								}

								w.ReportProgress(99, $"Writing output ...");

								uiHelper.ShowToast("Output generated.");

								e.Result = output;
							}
							catch (Exception exception)
							{
								Console.WriteLine(exception);
								throw;
							}
							finally
							{
								InvokeSafe(() => buttonCancel.Hide());
								EnableTool();
							}
						},
					ProgressChanged =
						e =>
						{
							// If progress has to be notified to user, use the following method:
							SetWorkingMessage(e.UserState.ToString());

							if (e.ProgressPercentage < 0)
							{
								return;
							}

							// If progress has to be notified to user, through the
							// status bar, use the following method
							SendMessageToStatusBar?.Invoke(this,
								new StatusBarMessageEventArgs(e.ProgressPercentage, e.UserState.ToString()));
						},
					PostWorkCallBack =
						e =>
						{
							SendMessageToStatusBar?.Invoke(this, new StatusBarMessageEventArgs(""));

							if (e.Result != null)
							{
								templateEditor.ShowOutput((string)e.Result);
							}
						},
					AsyncArgument = null,
					IsCancelable = false,
					MessageWidth = 340,
					MessageHeight = 150
				});
		}

		private void EnableTool()
		{
			InvokeSafe(
				() =>
				{
					tableLayoutMainPanel.Enabled = true;
					toolBar.Enabled = true;
				});
		}

		private void DisableTool()
		{
			InvokeSafe(
				() =>
				{
					toolBar.Enabled = false;
					tableLayoutMainPanel.Enabled = false;
				});
		}

		private void ShowTemplateEditor()
		{
			buttonTemplateEditor.Enabled = false;
			buttonGenerate.Enabled = true;

			panelHost.Controls.Clear();
			panelHost.Controls.Add(templateEditor);
			templateEditor.Dock = DockStyle.Fill;
		}
	}
}
