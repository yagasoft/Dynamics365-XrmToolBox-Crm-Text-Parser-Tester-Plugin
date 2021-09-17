namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	partial class TemplateEditor
	{
		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Component Designer generated code

		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.tableLayoutPanel2 = new System.Windows.Forms.TableLayoutPanel();
			this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
			this.listBoxConstructs = new System.Windows.Forms.ListBox();
			this.checkBoxHtmlOutput = new System.Windows.Forms.CheckBox();
			this.checkBoxHtmlEditor = new System.Windows.Forms.CheckBox();
			this.panelCodeEditor = new System.Windows.Forms.Panel();
			this.tableLayoutPanel2.SuspendLayout();
			this.tableLayoutPanel1.SuspendLayout();
			this.SuspendLayout();
			// 
			// tableLayoutPanel2
			// 
			this.tableLayoutPanel2.ColumnCount = 2;
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 200F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel2.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 20F));
			this.tableLayoutPanel2.Controls.Add(this.tableLayoutPanel1, 0, 0);
			this.tableLayoutPanel2.Controls.Add(this.panelCodeEditor, 1, 0);
			this.tableLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutPanel2.Location = new System.Drawing.Point(0, 0);
			this.tableLayoutPanel2.Name = "tableLayoutPanel2";
			this.tableLayoutPanel2.RowCount = 1;
			this.tableLayoutPanel2.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 47F));
			this.tableLayoutPanel2.Size = new System.Drawing.Size(879, 587);
			this.tableLayoutPanel2.TabIndex = 0;
			// 
			// tableLayoutPanel1
			// 
			this.tableLayoutPanel1.ColumnCount = 1;
			this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel1.Controls.Add(this.listBoxConstructs, 0, 2);
			this.tableLayoutPanel1.Controls.Add(this.checkBoxHtmlOutput, 0, 1);
			this.tableLayoutPanel1.Controls.Add(this.checkBoxHtmlEditor, 0, 0);
			this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.tableLayoutPanel1.Location = new System.Drawing.Point(3, 3);
			this.tableLayoutPanel1.Name = "tableLayoutPanel1";
			this.tableLayoutPanel1.RowCount = 3;
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
			this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
			this.tableLayoutPanel1.Size = new System.Drawing.Size(194, 581);
			this.tableLayoutPanel1.TabIndex = 0;
			// 
			// listBoxConstructs
			// 
			this.listBoxConstructs.Dock = System.Windows.Forms.DockStyle.Fill;
			this.listBoxConstructs.FormattingEnabled = true;
			this.listBoxConstructs.Location = new System.Drawing.Point(3, 49);
			this.listBoxConstructs.Name = "listBoxConstructs";
			this.listBoxConstructs.Size = new System.Drawing.Size(188, 529);
			this.listBoxConstructs.TabIndex = 2;
			this.listBoxConstructs.Visible = false;
			// 
			// checkBoxHtmlOutput
			// 
			this.checkBoxHtmlOutput.AutoSize = true;
			this.checkBoxHtmlOutput.Location = new System.Drawing.Point(3, 26);
			this.checkBoxHtmlOutput.Name = "checkBoxHtmlOutput";
			this.checkBoxHtmlOutput.Size = new System.Drawing.Size(91, 17);
			this.checkBoxHtmlOutput.TabIndex = 1;
			this.checkBoxHtmlOutput.Text = "HTML Output";
			this.checkBoxHtmlOutput.UseVisualStyleBackColor = true;
			this.checkBoxHtmlOutput.CheckedChanged += new System.EventHandler(this.checkBoxHtmlOutput_CheckedChanged);
			// 
			// checkBoxHtmlEditor
			// 
			this.checkBoxHtmlEditor.AutoSize = true;
			this.checkBoxHtmlEditor.Location = new System.Drawing.Point(3, 3);
			this.checkBoxHtmlEditor.Name = "checkBoxHtmlEditor";
			this.checkBoxHtmlEditor.Size = new System.Drawing.Size(86, 17);
			this.checkBoxHtmlEditor.TabIndex = 0;
			this.checkBoxHtmlEditor.Text = "HTML Editor";
			this.checkBoxHtmlEditor.UseVisualStyleBackColor = true;
			this.checkBoxHtmlEditor.CheckedChanged += new System.EventHandler(this.checkBoxHtmlEditor_CheckedChanged);
			// 
			// panelCodeEditor
			// 
			this.panelCodeEditor.Dock = System.Windows.Forms.DockStyle.Fill;
			this.panelCodeEditor.Location = new System.Drawing.Point(203, 3);
			this.panelCodeEditor.Name = "panelCodeEditor";
			this.panelCodeEditor.Size = new System.Drawing.Size(673, 581);
			this.panelCodeEditor.TabIndex = 20;
			// 
			// TemplateEditor
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.Controls.Add(this.tableLayoutPanel2);
			this.Name = "TemplateEditor";
			this.Size = new System.Drawing.Size(879, 587);
			this.Load += new System.EventHandler(this.TemplateEditor_Load);
			this.tableLayoutPanel2.ResumeLayout(false);
			this.tableLayoutPanel1.ResumeLayout(false);
			this.tableLayoutPanel1.PerformLayout();
			this.ResumeLayout(false);

		}

		#endregion

		private System.Windows.Forms.TableLayoutPanel tableLayoutPanel2;
		private System.Windows.Forms.Panel panelCodeEditor;
		private System.Windows.Forms.CheckBox checkBoxHtmlEditor;
		private System.Windows.Forms.CheckBox checkBoxHtmlOutput;
		private System.Windows.Forms.ListBox listBoxConstructs;
		private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
	}
}
