using System.Windows.Forms;
using ScintillaNET;
using Yagasoft.CrmTextParserTesterPlugin.Control;

namespace Yagasoft.CrmTextParserTesterPlugin.Model.ViewModels
{
    public class TemplateViewModel
    {
	    public EditorControl CodeEditor { get; set; }
	    public OutputControl TextOutputControl { get; set; }
    }
}
