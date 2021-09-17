#region Imports

using System.Windows.Forms;

#endregion

namespace Yagasoft.CrmTextParserTesterPlugin.Control
{
	public partial class BaseEditorControl : UserControl
	{
		protected BaseEditorControl()
		{
			InitializeComponent();
		}

		public virtual string GetText()
		{
			return null;
		}

		public virtual void SetText(string text)
		{ }
	}
}
