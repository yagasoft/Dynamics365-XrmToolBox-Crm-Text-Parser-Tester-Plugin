using System.Collections.Generic;

namespace Yagasoft.CrmTextParserTesterPlugin.Model
{
	public static class Constants
	{
		public const string AppName = "Dynamics 365 Text Parser Tester";
		public const string AppId = "xrmtoolbox-text-parser-test-plugin";
		public const string AppVersion = "4.1.2.1";

		//public const string SettingsVersion = "1.1.1.1";

		//public const string MetaCacheMemKey = "ys_CrmParser_Meta_639156";
		//public const string ConnCacheMemKey = "ys_CrmParser_Conn_185599";

		public static readonly string ReleaseNotes =
			$@"{AppName}
v{AppVersion}
~~~~~~~~~~
  - Please report issues and improvement suggestions on the generator's GitHub repository. Use the 'Help' menu above to access the page.
  
  * 4.1.2.1
  Added: reworked and simpler CRM Parser.
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
		
		public static readonly Dictionary<string, Dictionary<string, string>> Constructs =
		    new()
			{
				{ "Scopes",
					new()
					{
						{ "Code", "{}" },
						{ "Collection", "[]" },
						{ "Scope", "()" },
						{ "Separator", "," },
					}
				},
				{ "Memory",
				  new()
				  {
					  { "Store current value", "~" },
					  { "Load value", "@" },
				  }
				},
				{ "Objects",
				  new()
				  {
					  { "Parser context", "@this" },
					  { "Current value", "@value" },
					  { "Connection user", "@user" },
					  { "Label / name", ".name" },
					  { "Choice value", ".value" },
					  { "Logical name", ".logical" },
					  { "Record ID", ".id" },
					  { "Record URL", ".url" },
					  { "Property", "." },
					  { "Relation (Schema name)", "#" },
				  }
				},
				{ "Literals",
				  new()
				  {
					  { "Regex", "`/.*/f2`" },
					  { "Time span", "`1[yMdhmsf]`" },
					  { "Text", "``" },
				  }
				},
				{ "Functions",
				  new()
				  {
					  { "Retrieve", "$retrieve(`<logical-name>`, `<id>`, [`<optional-attr1>`, `<optional-attr2>`])" },
					  { "Retrieve by attr", "$retrbyattr(`<logical-name>`, `{\"<attr-name1>\":\"<attr-value1\", \"<attr-name2>\":\"<attr-value2\"}`, [`<optional-attr1>`, `<optional-attr2>`])" },
					  { "Fetch", "$fetch(`<fetch-xml>`)" },
					  { "Action", "$action(`<action-name>`, `{\"<param-name1>\":\"<param-value1\", \"<param-name2>\":\"<param-value2\"}`, `<optional-target-logical-name>`, `<optional-target-id>`)" },
					  { "Localise", "$loc(<lcid>)" },
					  { "Map", "$map(<code>)" },
					  { "For", "$for(<not-supported-yet-!>)" },
					  { "Get", "$get(<start-index-or-n>, <optional-end-index-or-n>)" },
					  { "Random", "$rand(<length>, <either>[`<char1>`, `char2>`]<or>`<u-l-n>`, <is-letter-start-?>, <number-letter-ratio>)" },
					  { "Debug", "$debug" },
					  { "Memory-only", "$mem(<code>)" },
					  { "Now", "$now" },
					  { "UTC now", "$utcnow" },
					  { "To UTC", "$utc" },
					  { "To local", "$local" },
					  { "Time zone offset", "$offset(`<time-zone>`)" },
					  { "Parse exactly", "$exact(`<format>`)" },
					  { "Format date", "$date(`<format>`)" },
					  { "Text length", "$length(`<optional-regex>`)" },
					  { "Sub-string index", "$index(<either>`<sub-string>`<or>`<-regex>`)" },
					  { "Get sub-string", "$sub(<start-index>, <optional-length>)" },
					  { "Trim text", "$trim(`<what-to-trim>`, <optional-is-trim-start-only-?>, <optional-is-trim-end-only-?>, `<optional-regex>`)" },
					  { "Pad text", "$pad(`<pad-string>`, <optional-total-length>, <optional-is-right-side-only-?>, `<optional-regex>`)" },
					  { "Truncate text", "$trunc(<max-length>, `<optional-fill-string>`, `<optional-regex>`)" },
					  { "Upper case", "$upper(`<optional-regex>`)" },
					  { "Lower case", "$lower(`<optional-regex>`)" },
					  { "Sentence case", "$sentence(`<optional-regex>`)" },
					  { "Title case", "$title(`<optional-regex>`)" },
					  { "Extract text", "$extract(`<regex>`)" },
					  { "Split text", "$split(<either>`<separator>`<or>`<regex>`)" },
					  { "Replace text", "$replace(`<replacement-text>`, `<optional-regex>`)" },
					  { "Encode HTML", "$enchtml(`<optional-regex>`)" },
					  { "Decode HTML", "$dechtml(`<optional-regex>`)" },
					  { "Format number", "$num(`<format>`, `<optional-regex>`)" },
					  { "Count", "$count" },
					  { "Distinct", "$distinct(<code>)" },
					  { "Order", "$order(<not-supported-yet-!>)" },
					  { "Clear", "$clear" },
					  { "Where", "$where(<code>)" },
					  { "Filter", "$filter(<code>)" },
					  { "Join", "$join(`<separator>`)" },
					  { "Min", "$min" },
					  { "Max", "$max" },
					  { "Average", "$avg" },
					  { "Sum", "$sum" },
					  { "Flatten", "$flat(<levels>)" }
				  }
				},
				{ "Operators (by precedence)",
				  new()
				  {
					  { "Not", "!" },
					  { "Negative", "-" },
					  { "Multiply", "*" },
					  { "Divide", "/" },
					  { "Add", "+" },
					  { "Subtract", "-" },
					  { "Greater than", ">" },
					  { "Less than", "<" },
					  { "Greater or equal", ">=" },
					  { "Less or equal", "<=" },
					  { "Not equal", "!=" },
					  { "Equals", "==" },
					  { "And", "&&" },
					  { "Or", "||" },
					  { "Null or", "??" },
					  { "If ternary", "?:" },
				  }
				}
			};
	}
}
