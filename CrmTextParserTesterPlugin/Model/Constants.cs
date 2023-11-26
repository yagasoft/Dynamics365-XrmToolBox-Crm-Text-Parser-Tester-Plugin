namespace Yagasoft.CrmTextParserTesterPlugin.Model
{
	public static class Constants
	{
		public const string AppName = "Dynamics 365 Text Parser Tester";
		public const string AppId = "xrmtoolbox-text-parser-test-plugin";
		public const string AppVersion = "3.1.2.1";

		//public const string SettingsVersion = "1.1.1.1";

		//public const string MetaCacheMemKey = "ys_CrmParser_Meta_639156";
		//public const string ConnCacheMemKey = "ys_CrmParser_Conn_185599";

		public static readonly string ReleaseNotes =
			$@"{AppName}
v{AppVersion}
~~~~~~~~~~
  - Please report issues and improvement suggestions on the generator's GitHub repository. Use the 'Help' menu above to access the page.
  
  * 3.1.2.1
  Improved: syntax highlighter.
  * 3.1.1.2
  Added: syntax highlighter.
  * 2.1.1.5
  Updated: latest parser version.
  Changed: use built in WebView of XrmToolBox.
  * 1.1.1.2
  Initial Release
~~~~~~~~~~";
	}
}
