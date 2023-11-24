using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Yagasoft.CrmTextParserTesterPlugin.Control.Interfaces
{
    public interface IContentChanged<in TContentChangedHandler> where TContentChangedHandler : Delegate
    {
	    Task RegisterContentChange(TContentChangedHandler handler);
    }
}
