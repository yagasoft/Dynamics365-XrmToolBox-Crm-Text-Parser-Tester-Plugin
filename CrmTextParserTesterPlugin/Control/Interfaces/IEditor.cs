using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yagasoft.CrmTextParserTesterPlugin.Control.Interfaces
{
    public interface IEditor
    {
	    string GetText();
	    Task SetText(string text);
    }
}
