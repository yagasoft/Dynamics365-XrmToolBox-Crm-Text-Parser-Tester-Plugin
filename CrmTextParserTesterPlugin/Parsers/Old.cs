#region Imports

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Yagasoft.Libraries.Common;

#endregion

namespace Yagasoft.CrmTextParserTesterPlugin.Parsers
{
	public static class CrmParserOld
	{
		private enum OpType
		{
			Pre,
			Binary,
			Ternary,
			Post
		}

		private const string Operators = @"^(?:[!+\-*/?:<>~]|\|\||&&|\?\?|>=|<=|!=|==)$";

		private static readonly Dictionary<string, OpType> operatorType =
			new()
			{
				{ "!", OpType.Pre },
				{ "~", OpType.Pre },
				{ "*", OpType.Binary },
				{ "/", OpType.Binary },
				{ "+", OpType.Binary },
				{ "-", OpType.Binary },
				{ ">", OpType.Binary },
				{ "<", OpType.Binary },
				{ ">=", OpType.Binary },
				{ "<=", OpType.Binary },
				{ "!=", OpType.Binary },
				{ "==", OpType.Binary },
				{ "||", OpType.Binary },
				{ "&&", OpType.Binary },
				{ "??", OpType.Binary },
				{ "?", OpType.Ternary },
				{ ":", OpType.Ternary }
			};

		private static readonly Dictionary<string, int> operatorPrecedence =
			new()
			{
				{ "!", 200 },
				{ "~", 200 },
				{ "*", 100 },
				{ "/", 100 },
				{ "+", 99 },
				{ "-", 99 },
				{ ">", 75 },
				{ "<", 75 },
				{ ">=", 75 },
				{ "<=", 75 },
				{ "!=", 74 },
				{ "==", 74 },
				{ "&&", 65 },
				{ "||", 64 },
				{ "??", 57 },
				{ "?", 50 },
				{ ":", 50 }
			};

		private static TimeSpan fallbackCacheDuration = TimeSpan.FromMinutes(1);

		#region Entry point

		public static string Parse(string input, params Type[] constructTypes)
		{
			var state = new GlobalState(null, constructTypes);
			return Parse(input, state);
		}

		public static string Parse(string input, IOrganizationService service,
			Guid? orgId = null, params Type[] constructTypes)
		{
			return Parse(input, service, null, orgId, constructTypes);
		}

		public static string Parse(string input, Entity context, IOrganizationService service,
			Guid? orgId = null, params Type[] constructTypes)
		{
			return Parse(input, context, service, null, orgId, constructTypes);
		}

		public static string Parse(string input, EntityReference contextRef, IOrganizationService service,
			Guid? orgId = null, params Type[] constructTypes)
		{
			return Parse(input, contextRef, service, null, orgId, constructTypes);
		}

		public static string Parse(string input, IOrganizationService service, object contextObject,
			Guid? orgId = null, params Type[] constructTypes)
		{
			var state = new GlobalState(service, constructTypes, contextObject, orgId);
			return Parse(input, state);
		}

		public static string Parse(string input, Entity context, IOrganizationService service, object contextObject,
			Guid? orgId = null, params Type[] constructTypes)
		{
			service.Require(nameof(service));
			var state = new GlobalState(context, service, constructTypes, contextObject, orgId);
			return Parse(input, state);
		}

		public static string Parse(string input, EntityReference contextRef, IOrganizationService service, object contextObject,
			Guid? orgId = null, params Type[] constructTypes)
		{
			service.Require(nameof(service));
			var state = new GlobalState(contextRef, service, constructTypes, contextObject, orgId);
			return Parse(input, state);
		}

		public static string HighlightCode(string input)
		{
			input.Require(nameof(input));

			var token = new TokenGlobal(input, true);
			token.ProcessInput();

			var code = token.Code;
			code = WebUtility.HtmlEncode(code)
				.Replace("\t", "  ")
				.Replace(" ", "&nbsp;")
				.Replace("\r", "")
				.Replace("\n", "<br/>");
			
			return 
				Regex
					.Replace(code, @"```PRECOLOUR(.+?)~~~",
						m => $"<```ELEMENT~~~ style=\"color:#{m.Groups[1].ExtractCaptures().FirstOrDefault() ?? 0.ToString()}\" class=\"code\">")
					.Replace("```POSTCOLOUR~~~", "</```ELEMENT~~~>");
		}

		#endregion

		private static string Parse(string input, GlobalState state)
		{
			input.Require(nameof(input));
			state.Require(nameof(state));

			var token = new TokenGlobal(input);
			token.ProcessInput();
			return ProcessToken(token, state).FirstOrDefault();
		}

		private static IReadOnlyList<string> ProcessToken(Token token, GlobalState state)
		{
			var result = new List<string>();

			switch (token)
			{
				// starting point
				case TokenGlobal global:
					result.Add(global.Queue.SelectMany(e => ProcessToken(e, state)).StringAggregate(""));
					return result;

				case TokenText:
					result.Add(token.Value);
					return result;

				case TokenParameters tokenParameters:
					result.AddRange(tokenParameters.Queue.Select(e => ProcessToken(e, state)).Select(s => s.StringAggregate("")));
					return result;

				case TokenScope scope:
					result.Add(ProcessScope(scope, state));
					return result;

				case TokenKeyword { Parameters: not null } keyword:
					keyword.ProcessedParams = ProcessToken(keyword.Parameters, state);
					break;
			}

			if (token is TokenConstruct tokenConstruct)
			{
				foreach (var preprocessor in tokenConstruct.Preprocessors)
				{
					ProcessToken(preprocessor, state);
				}

				foreach (var postProcessor in tokenConstruct.PostProcessors)
				{
					ProcessToken(postProcessor, state);
				}

				var construct =
					ConstructFactory
						.GetConstruct(tokenConstruct, state,
							tokenConstruct.Preprocessors.Select(p => ProcessorFactory.GetProcessor<PreprocessorAttribute>(p, state))
								.OfType<Preprocessor>().ToArray(),
							tokenConstruct.PostProcessors.Select(p => ProcessorFactory.GetProcessor<PostProcessorAttribute>(p, state))
								.OfType<PostProcessor>().ToArray());

				if (tokenConstruct.Body != null && construct is not IScoped)
				{
					tokenConstruct.ProcessedBody = ProcessToken(tokenConstruct.Body, state).StringAggregate("");
				}

				result.Add(construct
					.Execute(tokenConstruct.ProcessedBody)
					.StringAggregate(""));

				return result;
			}

			result.Add(token.Value);

			return result;
		}

		#region Tokeniser

		private static string ProcessScope(TokenScope scope, GlobalState state)
		{
			var stack = new Stack<Token>(scope.Stack);

			var opStack = new Stack<Token>();
			var operandStack = new Stack<Token>();

			Token previousToken = null;

			while (true)
			{
				if (!stack.Any())
				{
					var currentStack = operandStack;

					while (opStack.Any() || operandStack.Any())
					{
						if (currentStack.Any())
						{
							stack.Push(currentStack.Pop());
						}

						currentStack = currentStack == operandStack ? opStack : operandStack;
					}
				}

				if (!stack.Any())
				{
					return string.Empty;
				}

				if (stack.All(t => t is TokenText || t is TokenOperand) && !opStack.Any() && !operandStack.Any())
				{
					return stack.Select(t => t.Value).StringAggregate("");
				}

				var token = stack.Pop();

				if (token is TokenOperator && token.Value == "-" && previousToken is null or TokenOperator)
				{
					token.Value = "~";
				}

				previousToken = token;

				switch (token)
				{
					case TokenOperator:
						ProcessOperator(stack, token, opStack, operandStack, scope.ErrorLocationContext);
						break;

					case TokenOperand operandToken:
						while (opStack.Any() && operatorType[opStack.Peek().Value] == OpType.Pre)
						{
							operandToken.Value = ProcessOperation(opStack.Pop().Value, token, token.Value);
						}

						operandStack.Push(token);
						break;

					default:
						operandStack.Push(new TokenOperand(ProcessToken(token, state).StringAggregate("")));
						break;
				}
			}
		}

		private static void ProcessOperator(Stack<Token> stack, Token token, Stack<Token> opStack,
			Stack<Token> operandStack, string errorLocationContext)
		{
			var op = (TokenOperator)token;

			if (!operatorType.TryGetValue(op.Value, out var opType))
			{
				throw new InvalidOperationException($"Unknown operator '{op.Value}' at {errorLocationContext}");
			}

			switch (opType)
			{
				case OpType.Pre:
					opStack.Push(token);
					break;

				case OpType.Binary:
				{
					if (operandStack.IsEmpty() || stack.IsEmpty())
					{
						throw new InvalidOperationException($"Operator '{op.Value}' missing an operand: {errorLocationContext}");
					}

					Token nextOp = null;

					if (stack.Count >= 2)
					{
						var temp1 = stack.Pop();
						nextOp = stack.Peek();
						stack.Push(temp1);
					}

					if (stack.Peek() is TokenOperand &&
						(!opStack.Any()
							|| (opStack.Any() && opStack.Peek() is TokenOperator
								&& operatorPrecedence[opStack.Peek().Value.Trim()] < operatorPrecedence[token.Value.Trim()]))
						&& (nextOp is not TokenOperator || operatorPrecedence[nextOp.Value.Trim()] <= operatorPrecedence[token.Value.Trim()]))
					{
						var operand1 = operandStack.Pop();
						var operand2 = stack.Pop();
						operand1.Value = ProcessOperation(token.Value, token, operand1.Value, operand2.Value);
						operandStack.Push(operand1);
					}
					else
					{
						opStack.Push(token);
					}

					break;
				}

				case OpType.Ternary:
				{
					opStack.Push(token);

					if (opStack.Union(stack).OfType<TokenOperator>().Any(o => operatorType[o.Value] != OpType.Ternary))
					{
						break;
					}

					var currentStack = opStack;

					while (opStack.Any() || operandStack.Any())
					{
						if (currentStack.Any())
						{
							stack.Push(currentStack.Pop());
						}

						currentStack = currentStack == operandStack ? opStack : operandStack;
					}

					var ternaryStack = new Stack<Token>(stack);
					stack.Clear();

					while (ternaryStack.Count >= 5)
					{
						var operand2 = ternaryStack.Pop();
						ternaryStack.Pop(); // :
						var operand1 = ternaryStack.Pop();
						ternaryStack.Pop(); // ?
						var condition = ternaryStack.Pop();

						ternaryStack.Push(bool.Parse(ProcessOperation("!", condition, condition.Value)) ? operand2 : operand1);
					}

					if (ternaryStack.Count != 1)
					{
						throw new InvalidOperationException(
							$"Ternary operation failed: {ternaryStack.Reverse().Select(e => e.Value).StringAggregate("")}"
								+ $" at {errorLocationContext}");
					}

					stack.Push(ternaryStack.Pop());

					break;
				}

				case OpType.Post:
				{
					if (operandStack.IsEmpty())
					{
						throw new InvalidOperationException($"PostOperator '{op.Value}' missing an operand: {errorLocationContext}");
					}

					var operand = operandStack.Pop();
					operand.Value = ProcessOperation(token.Value, token, operand.Value);
					operandStack.Push(operand);

					break;
				}

				default:
					throw new ArgumentOutOfRangeException(nameof(opType), opType, $"OpType is unsupported: {token.ErrorLocationContext}");
			}
		}

		private static string ProcessOperation(string op, Token token, params string[] operands)
		{
			var opType = operatorType[op];

			switch (opType)
			{
				case OpType.Ternary:
					{
						//var t3 = tokenStack.Pop();
						//var t2 = tokenStack.Pop();
						//var t1 = tokenStack.Pop();
						break;
					}

				case OpType.Binary:
					{
						if (Regex.IsMatch(op, @"^(?:[+\-*/])$"))
						{
							var isT1Num = double.TryParse(operands[0], out var doubleT1);
							var isT2Num = double.TryParse(operands[1], out var doubleT2);

							if (isT1Num && isT2Num)
							{
								return (op switch
								{
									"+" => (doubleT1 + doubleT2),
									"-" => (doubleT1 - doubleT2),
									"*" => (doubleT1 * doubleT2),
									"/" => (doubleT1 / doubleT2)
								}).ToString();
							}

							var isT1Date = DateTime.TryParse(operands[0], out var dateT1);
							var isT2Date = DateTime.TryParse(operands[1], out var dateT2);

							if (Regex.IsMatch(op, @"^(?:[+\-])$") && (isT1Date || isT2Date))
							{
								return ApplyDateOp(isT1Date ? dateT1 : dateT2, op,
									isT1Date ? operands[1] : operands[0], token).ToString("s");
							}

							throw new FormatException($"Invalid operation on operands: {operands[0]}{op}{operands[1]} at {token.ErrorLocationContext}");
						}

						switch (op)
						{
							case "||":
								{
									var t1Bool = bool.TryParse(operands[0], out var t1);
									var t2Bool = bool.TryParse(operands[0], out var t2);

									if (t1Bool && t2Bool)
									{
										return (t1 || t2).ToString();
									}

									throw new FormatException($"Invalid operand types: {operands[0]}{op}{operands[1]} at {token.ErrorLocationContext}");
								}

							case "&&":
								{
									var t1Bool = bool.TryParse(operands[0], out var t1);
									var t2Bool = bool.TryParse(operands[0], out var t2);

									if (t1Bool && t2Bool)
									{
										return (t1 && t2).ToString();
									}

									throw new FormatException($"Invalid operand types: {operands[0]}{op}{operands[1]} at {token.ErrorLocationContext}");
								}

							case "??":
								{
									var t1 = ParseValue(operands[0]);
									var t2 = ParseValue(operands[1]);
									return (t1 ?? t2)?.ToString() ?? string.Empty;
								}

							case "<":
								{
									var t1 = ParseValue(operands[0]);
									var t2 = ParseValue(operands[1]);
									return (t1?.CompareTo(t2) < 0).ToString();
								}

							case ">":
								{
									var t1 = ParseValue(operands[0]);
									var t2 = ParseValue(operands[1]);
									return (t1?.CompareTo(t2) > 0).ToString();
								}

							case ">=":
								{
									var t1 = ParseValue(operands[0]);
									var t2 = ParseValue(operands[1]);
									return (t1?.CompareTo(t2) >= 0).ToString();
								}

							case "<=":
								{
									var t1 = ParseValue(operands[0]);
									var t2 = ParseValue(operands[1]);
									return (t1?.CompareTo(t2) <= 0).ToString();
								}

							case "==":
								{
									var t1 = ParseValue(operands[0]);
									var t2 = ParseValue(operands[1]);
									return ((t1 == null && t2 == null) || t1?.Equals(t2) == true || t2?.Equals(t1) == true).ToString();
								}

							case "!=":
								{
									var t1 = ParseValue(operands[0]);
									var t2 = ParseValue(operands[1]);
									return (!((t1 == null && t2 == null) || t1?.Equals(t2) == true || t2?.Equals(t1) == true)).ToString();
								}
						}

						break;
					}

				case OpType.Pre:
					{
						switch (op)
						{
							case "!":
								{
									var t1Bool = bool.TryParse(operands[0], out var t1);

									if (t1Bool)
									{
										return (!t1).ToString();
									}

									throw new FormatException($"Invalid operand types: {operands[0]} at {token.ErrorLocationContext}");
								}

							case "~":
								{
									var t1Double = double.TryParse(operands[0], out var t1);

									if (t1Double)
									{
										return (-t1).ToString();
									}

									throw new FormatException($"Invalid operand types: {operands[0]} at {token.ErrorLocationContext}");
								}
						}

						break;
					}

				case OpType.Post:
					{
						break;
					}

				default:
					throw new ArgumentOutOfRangeException(nameof(opType), opType, $"OpType is unsupported: {token.ErrorLocationContext}");
			}

			return string.Empty;
		}

		private static IComparable ParseValue(string value)
		{
			if (value.IsEmpty() || value == "null")
			{
				return null;
			}

			if (int.TryParse(value, out int intValue))
			{
				return intValue;
			}

			if (double.TryParse(value, out var doubleValue))
			{
				return doubleValue;
			}

			if (DateTime.TryParse(value, out var dateValue))
			{
				return dateValue;
			}

			if (bool.TryParse(value, out var boolValue))
			{
				return boolValue;
			}

			return value;
		}

		private static DateTime ApplyDateOp(DateTime date, string op, string value, Token token)
		{
			op.RequireFormat(@"^[+\-]$", nameof(op), $"Date operator is invalid: {date}{op}");

			if (value.IsEmpty())
			{
				throw new FormatException($"Date operand is empty: {date}{op}x at {token.ErrorLocationContext}");
			}

			var isSpan = TimeSpan.TryParse(value, out var span);

			if (isSpan)
			{
				return op switch
				{
					"+" => date + span,
					"-" => date - span
				};
			}

			foreach (var s in Regex.Match(value, @"^(\d+[yMdhmsf])+$").Groups[1].ExtractCaptures())
			{
				var amount = (int)double.Parse(Regex.Match(s, @"^(\d+)(.+)$").Groups[1].ExtractCaptures().FirstOrDefault() ?? "0")
					* (op == "+" ? 1 : -1);
				var unit = Regex.Match(s, @"^(\d+)(.+)$").Groups[2].ExtractCaptures().FirstOrDefault();

				date = unit switch
				{
					"y" => date.AddYears(amount),
					"M" => date.AddMonths(amount),
					"d" => date.AddDays(amount),
					"h" => date.AddHours(amount),
					"m" => date.AddMinutes(amount),
					"s" => date.AddSeconds(amount),
					"f" => date.AddMilliseconds(amount),
				};
			}

			return date;
		}

		#endregion
		
		private static IEnumerable<Entity> BuildTraversalContext(Entity context, IReadOnlyList<string> traversal,
			GlobalState state, IReadOnlyList<string> distinctFields,
			IReadOnlyList<string> orderFields, bool isCacheResult, bool isCacheGlobal)
		{
			IEnumerable<Entity> contextBuffer = new[] { context };

			foreach (var n in traversal)
			{
				var isRelation = n.StartsWith("#");
				var node = n.TrimStart('.', '#');

				contextBuffer = contextBuffer.SelectMany(
					c =>
					{
						if (isRelation)
						{
							var attributes = distinctFields?.Union(orderFields?.Select(o => o.Trim('#')) ?? Array.Empty<string>()).Distinct().ToArray();
							attributes = attributes?.Any() == true ? attributes : Array.Empty<string>();
							var key = $"CrmParser.BuildContextBuffer|{c.LogicalName}|{c.Id}|{node}|{attributes.StringAggregate()}";

							var related = c
								.RelatedEntities
								.Where(r => r.Key.SchemaName == node && r.Value != null)
								.SelectMany(r => r.Value?.Entities).ToArray();

							related =
								related.Any()
									? related
									: state.GetCachedAdd(key, () =>
									CrmHelpers.GetRelatedRecords(state.Service, c.ToEntityReference(), node, null, state.OrgId, attributes)
										.ToArray(),
									isCacheGlobal);

							if (related.Any())
							{
								c.RelatedEntities[new Relationship(node)] = new EntityCollection(related);
							}

							var q = related.AsEnumerable();

							if (distinctFields.IsFilled())
							{
								q = q.DistinctBy(
									e => distinctFields
										.Select(s => CrmHelpers.GetAttributeName(s, e))
										.StringAggregate());
							}

							if (orderFields.IsFilled())
							{
								var ordered = q.OrderBy(e => e.Id);

								for (var i = 0; i < orderFields.Count; i++)
								{
									var orderRaw = orderFields[i];
									var order = orderRaw.Trim('#');
									var isDesc = orderRaw.StartsWith("#");

									if (i == 0)
									{
										if (isDesc)
										{
											ordered = ordered.OrderByDescending(e =>
												CrmHelpers.GetAttributeName(order, e));
										}
										else
										{
											ordered = ordered.OrderBy(e =>
												CrmHelpers.GetAttributeName(order, e));
										}
									}
									else
									{
										if (isDesc)
										{
											ordered = ordered.ThenByDescending(e =>
												CrmHelpers.GetAttributeName(order, e));
										}
										else
										{
											ordered = ordered.ThenBy(e =>
												CrmHelpers.GetAttributeName(order, e));
										}
									}
								}

								q = ordered;
							}

							return q;
						}
						else
						{
							var fieldValue = c.GetAttributeValue<object>(node);

							// get the entity record
							c = fieldValue == null && c.LogicalName.IsFilled() && c.Id != Guid.Empty
								? c.IntegrateAttributes(Retrieve(state, c.LogicalName, c.Id, 
									isCacheResult, isCacheGlobal, node))
								: c;

							fieldValue = c.GetAttributeValue<object>(node);

							// if the field value is not a lookup, then we can't recurse
							if (!(fieldValue is EntityReference reference))
							{
								throw new Exception($"Field \"{node}\" is not a lookup.");
							}

							return
								new[]
								{
									new Entity(reference.LogicalName)
									{
										Id = reference.Id
									}
								};
						}
					});
			}

			return contextBuffer;
		}

		public static Entity Retrieve(GlobalState state,
			string entityName, Guid id, bool isCache, bool isCacheGlobal, params string [] attributes)
		{
			Entity Action() => state.Service.Retrieve(entityName, id,
				attributes?.Any() == true ? new ColumnSet(attributes) : new ColumnSet(false));

			return isCache
				? state.GetCachedAdd($"CrmParser.Retrieve|{entityName}|{id}|{attributes.StringAggregate()}", Action, isCacheGlobal)
				: Action();
		}

		public static Entity[] RetrieveMultiple(IOrganizationService service, GlobalState state, string fetchXml,
			bool isCache, bool isCacheGlobal)
		{
			Entity[] Action() => CrmHelpers.RetrieveRecords(service, fetchXml).ToArray();

			return isCache
				? state.GetCachedAdd($"CrmParser.RetrieveMultiple|{fetchXml}", Action, isCacheGlobal)
				: Action();
		}

		public static Entity CallAction(IOrganizationService service, string actionName, EntityReference target, string parameters)
		{
			var request = new OrganizationRequest(actionName);

			if (parameters.IsFilled())
			{
				foreach (var pair in SerialiserHelpers.DeserialiseSimpleJson(parameters))
				{
					request[pair.Key] = pair.Value;
				}
			}

			if (target != null)
			{
				request["Target"] = target;
			}

			var result = new Entity();

			foreach (var pair in service.Execute(request).Results)
			{
				var key = pair.Key;
				var value = pair.Value;

				if (value is EntityCollection collection)
				{
					if (collection.Entities.Any())
					{
						result.RelatedEntities[new Relationship(key)] = new EntityCollection(collection.Entities);
					}
				}
				else
				{
					result[pair.Key] = pair.Value;
				}
			}

			return result;
		}

		#region Text parser classes

		public abstract class Token
		{
			public virtual string Value { get; set; }

			public virtual string Code => CodeBuffer.ToString();

			public string ErrorLocationContext { get; private set; }

			private readonly Stack<Token> stack = new();

			protected readonly Queue<char> Input;
			protected string CurrentChar;
			protected string CurrentBuffer;
			protected readonly StringBuilder Buffer = new();
			protected readonly StringBuilder CodeBuffer = new();
			
			protected readonly bool IsHighlightMode;

			private bool isEscapeMode;

			protected readonly List<string> RawCharList;

			protected Token(string value = "", bool isHighlightMode = false)
			{
				Value = value;
				IsHighlightMode = isHighlightMode;
			}

			protected Token(Queue<char> input, List<string> rawCharList, bool isHighlightMode = false)
			{
				Input = input;
				RawCharList = rawCharList;
				IsHighlightMode = isHighlightMode;
			}

			protected Token(Queue<char> input, List<string> rawCharList, string value = "", bool isHighlightMode = false) : this(input, rawCharList, isHighlightMode)
			{
				Value = value;
			}

			public void ProcessInput()
			{
				while (Input.Any())
				{
					CurrentBuffer = Buffer.ToString();
					CurrentChar = Dequeue();

					if (isEscapeMode)
					{
						if (CurrentChar == "`")
						{
							isEscapeMode = false;
							continue;
						}

						ProcessEscapedChar(Buffer, CurrentChar);

						continue;
					}

					switch (CurrentChar)
					{
						case @"\":
							Buffer.Append(Dequeue());
							continue;

						case "`":
							isEscapeMode = true;
							continue;
					}

					if (!ProcessCharacter())
					{
						break;
					}
				}

				PostProcess();

				SetErrorLocationContext();
			}

			protected abstract bool ProcessCharacter();

			protected abstract void PostProcess();

			protected virtual void ProcessEscapedChar(StringBuilder buffer, string character)
			{
				buffer.Append(character);
			}

			protected string Dequeue()
			{
				var character = Input.Any() ? Input.Dequeue().ToString() : "";
				RawCharList.Add(character);
				return character;
			}

			protected void ClearBuffer()
			{
				Buffer.Clear();
				CurrentBuffer = null;
			}

			protected void ThrowFormatException(string message)
			{
				SetErrorLocationContext();

				throw new FormatException($"{message}: {ErrorLocationContext}",
					new FormatException($"{this} {CurrentBuffer}{CurrentChar}"));
			}
			
			protected void HighlightCode()
			{
				if (!IsHighlightMode)
				{
					return;
				}
				
				var colour = GetColour();

				if (colour.IsEmpty())
				{
					return;
				}

				var code = CodeBuffer.ToString();
				CodeBuffer.Clear();
				CodeBuffer.Append($"```PRECOLOUR{colour}~~~");
				CodeBuffer.Append(code);
				CodeBuffer.Append("```POSTCOLOUR~~~");
			}
			
			protected virtual string GetColour()
			{
				return GetType().GetCustomAttribute<ColourAttribute>()?.Colour;
			}
			
			private void SetErrorLocationContext()
			{
				var skip = Math.Max(RawCharList.Count - 100, 0);
				var take = Math.Max(RawCharList.Count - skip, 1);

				ErrorLocationContext = $"{(skip > 0 ? "[...]" : "")}{RawCharList.Skip(skip).Take(take).StringAggregate("")}";
			}

			public override string ToString()
			{
				return $"{GetType().Name}{(Value.IsFilled() ? $":{Value}" : "")}";
			}
		}

		public class TokenGlobal : Token
		{
			public readonly Stack<Token> Stack = new();
			public Queue<Token> Queue => new(Stack.Reverse());

			public TokenGlobal(string input, bool isHighlightMode = false)
				: base(new Queue<char>(input), new List<string>(), isHighlightMode)
			{ }

			protected override bool ProcessCharacter()
			{
				if (CurrentChar == "{")
				{
					if (CurrentBuffer.IsAny())
					{
						var text = new TokenText(CurrentBuffer, IsHighlightMode);
						Stack.Push(text);
						CodeBuffer.Append(text.Code);
						ClearBuffer();
					}

					var construct = new TokenConstruct(Input, RawCharList, IsHighlightMode);
					construct.ProcessInput();
					Stack.Push(construct);
					CodeBuffer.Append("{");
					CodeBuffer.Append(construct.Code);
					CodeBuffer.Append("}");

					return true;
				}

				Buffer.Append(CurrentChar);
				return true;
			}

			protected override void PostProcess()
			{
				if (Buffer.Length > 0)
				{
					CurrentBuffer = Buffer.ToString();
					var text = new TokenText(CurrentBuffer, IsHighlightMode);
					Stack.Push(text);
					CodeBuffer.Append(text.Code);
					ClearBuffer();
				}

				HighlightCode();
			}
		}

		public abstract class TokenCode : Token
		{
			protected TokenCode(string value = "", bool isHighlightMode = false)
				: base(value, isHighlightMode)
			{ }

			protected TokenCode(Queue<char> input, List<string> rawCharList, string value = "", bool isHighlightMode = false)
				: base(input, rawCharList, value, isHighlightMode)
			{ }
		}

		public class TokenScope : TokenCode
		{
			public readonly Stack<Token> Stack = new();
			public Queue<Token> Queue => new(Stack.Reverse());

			public TokenScope(Queue<char> input, List<string> rawCharList, bool isHighlightMode = false)
				: base(input, rawCharList, isHighlightMode: isHighlightMode)
			{ }

			protected override bool ProcessCharacter()
			{
				if (CurrentChar == "{")
				{
					if (CurrentBuffer.IsAny())
					{
						var token = new TokenOperand(CurrentBuffer, IsHighlightMode);
						Stack.Push(token);
						CodeBuffer.Append(token.Code);
						ClearBuffer();
					}

					var construct = new TokenConstruct(Input, RawCharList, IsHighlightMode);
					construct.ProcessInput();
					Stack.Push(construct);
					CodeBuffer.Append("{");
					CodeBuffer.Append(construct.Code);
					CodeBuffer.Append("}");

					return true;
				}
				
				if (Regex.IsMatch(CurrentChar, @"[()]"))
				{
					if (CurrentBuffer.IsAny())
					{
						var token = new TokenOperand(CurrentBuffer, IsHighlightMode);
						Stack.Push(token);
						CodeBuffer.Append(token.Code);
						ClearBuffer();
					}
				
					if (CurrentChar == "(")
					{
						var scope = new TokenScope(Input, RawCharList, IsHighlightMode);
						scope.ProcessInput();
						Stack.Push(scope);
						CodeBuffer.Append("(");
						CodeBuffer.Append(scope.Code);
						CodeBuffer.Append(")");
					}
				
					if (CurrentChar == ")")
					{
						return false;
					}

					return true;
				}

				if (Regex.IsMatch(CurrentChar, @"\s"))
				{
					return true;
				}

				var isOp = Regex.IsMatch(CurrentChar, Operators);

				if (Input.Any())
				{
					var doubleOp = CurrentChar + Input.Peek();
					var isDoubleOp = Regex.IsMatch(doubleOp, Operators);

					if (isDoubleOp)
					{
						Dequeue();
						isOp = true;
						CurrentChar = doubleOp;
					}
				}
				
				if (isOp)
				{
					if (CurrentBuffer.IsAny())
					{
						var token = new TokenOperand(CurrentBuffer, IsHighlightMode);
						Stack.Push(token);
						CodeBuffer.Append(token.Code);
						ClearBuffer();
					}

					var opToken = new TokenOperator(CurrentChar, IsHighlightMode);
					Stack.Push(opToken);
					CodeBuffer.Append(opToken.Code);
					return true;
				}

				Buffer.Append(CurrentChar);

				return true;
			}

			protected override void PostProcess()
			{
				if (Buffer.Length <= 0)
				{
					return;
				}

				CurrentBuffer = Buffer.ToString();
				var token = GetType() == typeof(Token) ? (Token)new TokenText(CurrentBuffer, IsHighlightMode) : new TokenOperand(CurrentBuffer, IsHighlightMode);
				Stack.Push(token);
				CodeBuffer.Append(token.Code);
				ClearBuffer();
			}

			public override string ToString()
			{
				return $"{base.ToString()}{(Stack.Any() ? $">({Stack.Select(t => t.ToString()).Reverse().StringAggregate(" ")})" : "")}";
			}
		}

		public class TokenParameters : TokenScope
		{
			private bool isLatestComma;

			public TokenParameters(Queue<char> input, List<string> rawCharList, bool isHighlightMode = false)
				: base(input, rawCharList, isHighlightMode)
			{ }

			protected override bool ProcessCharacter()
			{
				switch (CurrentChar)
				{
					case ",":
						var token = new TokenOperand(CurrentBuffer, IsHighlightMode);
						Stack.Push(token);
						CodeBuffer.Append(token.Code);
						CodeBuffer.Append(",");
						ClearBuffer();
						isLatestComma = true;
						return true;

					case ")" when isLatestComma:
						var lastOperandToken = new TokenOperand(CurrentBuffer, IsHighlightMode);
						Stack.Push(lastOperandToken);
						CodeBuffer.Append(lastOperandToken.Code);
						ClearBuffer();
						return false;

					default:
						isLatestComma = false;
						return base.ProcessCharacter();
				}
			}
		}

		public class TokenBody : TokenScope
		{
			public TokenBody(Queue<char> input, List<string> rawCharList, bool isHighlightMode = false)
				: base(input, rawCharList, isHighlightMode)
			{ }

			protected override bool ProcessCharacter()
			{
				if (!base.ProcessCharacter())
				{
					return false;
				}

				if (CurrentChar == "|")
				{
					if (CurrentBuffer.IsAny())
					{
						var token = new TokenOperand(CurrentBuffer, IsHighlightMode);
						Stack.Push(token);
						CodeBuffer.Append(token.Code);
					}

					ClearBuffer();
					return false;
				}

				return true;
			}
		}

		public abstract class TokenKeyword : TokenCode
		{
			protected abstract char StartChar { get; }
			protected abstract char EndChar { get; }

			public TokenParameters Parameters;
			public IReadOnlyList<string> ProcessedParams = Array.Empty<string>();

			public bool IsDefined => Value.IsFilled();

			protected TokenKeyword(Queue<char> input, List<string> rawCharList, bool isHighlightMode = false)
				: base(input, rawCharList, isHighlightMode: isHighlightMode)
			{ }

			protected override bool ProcessCharacter()
			{
				if (Regex.IsMatch(CurrentChar, @"\s"))
				{
					return true;
				}

				if (IsDefined)
				{
					if (CurrentChar == "(")
					{
						if (Parameters != null)
						{
							ThrowFormatException("Cannot define parameters at this position");
						}

						Parameters = new TokenParameters(Input, RawCharList, IsHighlightMode);
						Parameters.ProcessInput();
						CodeBuffer.Append("(");
						CodeBuffer.Append(Parameters.Code);
						CodeBuffer.Append(")");

						return true;
					}

					if (CurrentChar == EndChar.ToString())
					{
						if (CurrentBuffer.IsFilled() && CurrentBuffer != Value)
						{
							ThrowFormatException($"Keyword closure mismatch ({CurrentBuffer} should be {Value})");
						}

						var code = Code;
						
						if (this is TokenConstruct && code.Any() && code.Last() != code.First())
						{
							CodeBuffer.Append(code.First());
						}

						return false;
					}

					ProcessKeyword();
				}
				else
				{
					if (Regex.IsMatch(CurrentChar, @"[`{}@%|()\\]"))
					{
						Value = CurrentBuffer;
						CodeBuffer.Append(CurrentBuffer);
						ClearBuffer();

						if (Value.IsEmpty())
						{
							ThrowFormatException($"Keyword identifier is missing");
						}
						
						return ProcessCharacter();
					}

					Buffer.Append(CurrentChar);
				}

				return true;
			}

			protected abstract void ProcessKeyword();

			protected void ValidateEmptyBuffer()
			{
				if (CurrentBuffer.IsAny())
				{
					ThrowFormatException("Previous characters are in an invalid position");
				}
			}

			protected override void PostProcess()
			{ }

			public override string ToString()
			{
				return $"{base.ToString()} {Parameters}";
			}
		}

		public class TokenConstruct : TokenKeyword
		{
			public readonly List<TokenPreprocessor> Preprocessors = new();
			public TokenBody Body;
			public string ProcessedBody;
			public readonly List<TokenPostProcessor> PostProcessors = new();

			protected override char StartChar => '{';
			protected override char EndChar => '}';

			public TokenConstruct(Queue<char> input, List<string> rawCharList, bool isHighlightMode = false)
				: base(input, rawCharList, isHighlightMode)
			{ }

			protected override void ProcessKeyword()
			{
				if (!IsDefined)
				{
					return;
				}

				switch (CurrentChar)
				{
					case "%":
					{
						if (Body != null)
						{
							ThrowFormatException($"Can only define a post-processor or end keyword at this position");
						}

						ValidateEmptyBuffer();
						var processor = new TokenPreprocessor(Input, RawCharList, IsHighlightMode);
						processor.ProcessInput();
						Preprocessors.Add(processor);
						CodeBuffer.Append("%");
						CodeBuffer.Append(processor.Code);
						CodeBuffer.Append("%");
						return;
					}

					case "@":
					{
						ValidateEmptyBuffer();
						var processor = new TokenPostProcessor(Input, RawCharList, IsHighlightMode);
						processor.ProcessInput();
						PostProcessors.Add(processor);
						CodeBuffer.Append("@");
						CodeBuffer.Append(processor.Code);
						CodeBuffer.Append("@");
						return;
					}

					case "|":
					{
						if (Body != null)
						{
							ThrowFormatException($"Can only define a post-processor or end keyword at this position");
						}

						ValidateEmptyBuffer();
						Body = new TokenBody(Input, RawCharList, IsHighlightMode);
						Body.ProcessInput();
						CodeBuffer.Append("|");
						CodeBuffer.Append(Body.Code);
						CodeBuffer.Append("|");
						return;
					}

					default:
						if (Regex.IsMatch(CurrentChar, @"[^`{}@%|()\\]"))
						{
							Buffer.Append(CurrentChar);
							CodeBuffer.Append(CurrentChar);
							return;
						}

						ThrowFormatException($"Can only define [{(Body == null ? "Preprocessor, Body," : "")}Post-processor] or end keyword at this position");
						return;
				}
			}

			protected override void ProcessEscapedChar(StringBuilder buffer, string character)
			{
				if (IsDefined)
				{
					base.ProcessEscapedChar(buffer, character);
				}
			}

			protected override void PostProcess()
			{
				base.PostProcess();
				HighlightCode();
			}
			
			protected override string GetColour()
			{
				return TypeHelpers.GetType<ConstructAttribute>(c => c?.Key == Value)?.GetCustomAttribute<ColourAttribute>()?.Colour;
			}
			
			public override string ToString()
			{
				return $"{base.ToString()} {Preprocessors?.StringAggregate("")} {Body} {PostProcessors?.StringAggregate("")}";
			}
		}

		public class TokenPreprocessor : TokenKeyword
		{
			protected override char StartChar => '%';
			protected override char EndChar => '%';

			public TokenPreprocessor(Queue<char> input, List<string> rawCharList, bool isHighlightMode = false)
				: base(input, rawCharList, isHighlightMode)
			{ }

			protected override void ProcessKeyword()
			{ }
		}

		public class TokenPostProcessor : TokenKeyword
		{
			protected override char StartChar => '@';
			protected override char EndChar => '@';

			public TokenPostProcessor(Queue<char> input, List<string> rawCharList, bool isHighlightMode = false)
				: base(input, rawCharList, isHighlightMode)
			{ }

			protected override void ProcessKeyword()
			{ }
		}

		public class TokenString : Token
		{
			public TokenString(string value = "", bool isHighlightMode = false) : base(value, isHighlightMode)
			{
				CodeBuffer.Append(Value);
			}

			protected override bool ProcessCharacter()
			{
				return true;
			}

			protected override void PostProcess()
			{ }
		}

		public class TokenText : TokenString
		{
			public TokenText(string value = "", bool isHighlightMode = false) : base(value, isHighlightMode)
			{ }
		}

		public class TokenOperator : TokenString
		{
			public TokenOperator(string value = "", bool isHighlightMode = false) : base(value, isHighlightMode)
			{ }
		}

		public class TokenOperand : TokenString
		{
			public override string Value
			{
				get => base.Value == "null" || base.Value == null ? string.Empty : base.Value;
				set => base.Value = value;
			}

			public TokenOperand(string value = "", bool isHighlightMode = false) : base(value, isHighlightMode)
			{ }
		}

		#endregion

		#region Factories

		private static class ConstructFactory
		{
			public static Construct GetConstruct(TokenConstruct keyword, GlobalState state,
				IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
			{
				keyword.Require(nameof(keyword));
				state.Require(nameof(state));

				var key = keyword.Value;

				var error = $"Unable to find a construct class with key '{key}' for '{keyword.ErrorLocationContext}'.";

				var type = state.ConstructTypes.FirstNotNullOrDefault(key);

				if (type == null)
				{
					throw new KeyNotFoundException(error);
				}

				var construct = Activator.CreateInstance(type, state, keyword, preProcessors, postProcessors) as Construct;

				if (construct == null)
				{
					throw new KeyNotFoundException(error);
				}

				return construct;
			}
		}

		private static class ProcessorFactory
		{
			public static StageProcessor GetProcessor<TProcessor>(TokenKeyword keyword, GlobalState state)
				where TProcessor : ProcessorAttribute
			{
				keyword.Require(nameof(keyword));
				state.Require(nameof(state));

				var key = keyword.Value;
				var className = key + typeof(TProcessor).FullName;

				var error = $"Unable to find a processor class '{className}' with key '{key}' for '{keyword.ErrorLocationContext}'.";

				var type = state.ProcessorTypes.FirstNotNullOrDefault(className);

				if (type == null)
				{
					throw new KeyNotFoundException(error);
				}

				var processor = Activator.CreateInstance(type, state, keyword) as StageProcessor;

				if (processor == null)
				{
					throw new KeyNotFoundException(error);
				}

				return processor;
			}
		}
		
		#endregion

		public class GlobalState
		{
			public readonly IOrganizationService Service;
			public readonly Guid? OrgId;

			public Entity Context;
			public object ContextObject;

			public readonly Type[] ConstructTypesLookup;

			public int Lcid = 1033;

			public readonly bool IsContextProvided = false;

			public IDictionary<string, Type> ConstructTypes
				=> CacheHelpers.GetFromMemCacheAdd("Yagasoft.CrmParser.GetTypes<ConstructAttribute>",
					() => Yagasoft.Libraries.Common.TypeHelpers.GetTypes<ConstructAttribute>(ConstructTypesLookup)
						.ToDictionary(t => t.GetCustomAttribute<ConstructAttribute>().Key, t => t),
					fallbackCacheDuration: fallbackCacheDuration);

			public IDictionary<string, Type> ProcessorTypes
				=> CacheHelpers.GetFromMemCacheAdd("Yagasoft.CrmParser.GetTypes<ProcessorAttribute>",
					() => Yagasoft.Libraries.Common.TypeHelpers.GetTypes<ProcessorAttribute>(ConstructTypesLookup)
						.ToDictionary(t =>
									  {
										  var attribute = t.GetCustomAttribute<ProcessorAttribute>();
										  return attribute.Key + attribute.GetType().FullName;
									  }, t => t),
					fallbackCacheDuration: fallbackCacheDuration);

			public InlineConfig InlineConfig;

			public readonly IDictionary<string, string> Templates = new Dictionary<string, string>();

			private readonly IDictionary<string, object> cache = new Dictionary<string, object>();

			private readonly IDictionary<string, object> memory = new Dictionary<string, object>();

			private readonly IDictionary<string, string> tokens = new Dictionary<string, string>();

			private int nextIndex = 1;

			public GlobalState(IOrganizationService service, Type[] constructTypes = null, object contextObject = null, Guid? orgId = null)
			{
				Service = service;
				ContextObject = contextObject;
				ConstructTypesLookup = (constructTypes ?? Type.EmptyTypes).Union(new [] {typeof(CrmParser)}).ToArray();
				OrgId = orgId;
			}

			public GlobalState(EntityReference contextRef, IOrganizationService service, Type[] constructTypes = null, object contextObject = null, Guid? orgId = null)
				: this(new Entity(contextRef.LogicalName, contextRef.Id), service, constructTypes, contextObject, orgId)
			{
				IsContextProvided = false;
			}

			public GlobalState(Entity context, IOrganizationService service, Type[] constructTypes = null, object contextObject = null, Guid? orgId = null)
				: this(service, constructTypes, contextObject, orgId)
			{
				Context = context;
				IsContextProvided = true;
			}

			public string GenerateToken(string str)
			{
				var index = nextIndex.ToString();
				nextIndex++;
				tokens[index] = str;
				return index;
			}

			public string GetToken(string token)
			{
				return tokens.TryGetValue(token, out var str) ? str : null;
			}

			public T AddCached<T>(string key, T obj, bool isGlobal = false)
			{
				if (isGlobal)
				{
					CacheHelpers.AddToMemCache(key, obj, Service, fallbackCacheDuration: fallbackCacheDuration, orgId: OrgId);
				}
				else
				{
					cache[key] = obj;
				}

				return obj;
			}

			public T GetCachedAdd<T>(string key, Func<T> objFunc, bool isGlobal = false)
			{
				var cached = GetCached<T>(key);

				if (cached != null)
				{
					return cached;
				}

				var obj = objFunc();

				if (isGlobal)
				{
					CacheHelpers.AddToMemCache(key, obj, Service, fallbackCacheDuration: fallbackCacheDuration, orgId: OrgId);
				}
				else
				{
					cache[key] = obj;
				}

				return obj;
			}

			public T GetCached<T>(string key)
			{
				return cache.TryGetValue(key, out var obj) && obj is T cast
					? cast
					: (CacheHelpers.GetFromMemCache<object>(key, orgId: OrgId) is T castGlobal ? castGlobal : default);
			}

			public string GetCached(string key)
			{
				return GetCached<string>(key);
			}

			public T Store<T>(string key, T obj)
			{
				memory[key] = obj;
				return obj;
			}

			public T Read<T>(string key)
			{
				return memory.TryGetValue(key, out var obj) && obj is T cast ? cast : default;
			}

			public string Read(string key)
			{
				return Read<string>(key);
			}
		}

		#region Definitions

		public enum ValueForm
		{
			Raw,
			Name,
			LogicalName,
			Id
		}

		public abstract class Processor
		{
			public readonly TokenKeyword Keyword;

			protected readonly GlobalState State;

			protected Processor(GlobalState state, TokenKeyword keyword)
			{
				State = state;
				Keyword = keyword;
			}

			protected void ThrowMisformattedParam(string paramName = null)
			{
				throw new FormatException($"Parameter {(paramName.IsFilled() ? $"'{paramName}' " : "")}for '{Keyword.Value}'"
					+ $" {GetType().BaseType?.Name} is misformatted for '{Keyword.ErrorLocationContext}'");
			}

			protected ProcessorParameters ExtractParameters(int minCount = 1, bool isRegexRequired = false)
			{
				var parameters = Keyword.ProcessedParams;
				var paramCount = parameters.Count;

				var regex = GetRegex(parameters.FirstOrDefault());

				if (regex == null)
				{
					if (isRegexRequired)
					{
						ThrowMisformattedParam();
					}
				}
				else
				{
					if (!isRegexRequired)
					{
						paramCount--;
					}
				}

				if (paramCount < minCount)
				{
					ThrowMisformattedParam();
				}

				return new ProcessorParameters { Params = parameters.Skip(regex == null ? 0 : 1).ToArray(), Regex = regex };
			}

			private static RegexParams GetRegex(string buffer)
			{
				if (buffer.IsEmpty())
				{
					return null;
				}

				var match = Regex.Match(buffer, @"^(?:[^\\]?/(.*?[^\\]?)/)+$");

				if (!match.Success)
				{
					return null;
				}

				var captures = match.Groups[1].ExtractCaptures().ToArray();
				var regex = captures.FirstOrDefault();
				var groups = captures.Skip(1).ToArray();

				return new RegexParams { Regex = regex, Groups = groups.Any() ? groups : null };
			}

			protected IReadOnlyList<string> ExtractMatches(string input, ProcessorParameters procParams,
				Func<Capture, string> captureOperation = null, string defaultValue = "")
			{
				captureOperation ??= (s => s.Value);

				var regex = procParams.Regex;

				if (regex == null)
				{
					return new [] { input };
				}

				var parameters = procParams.Params;
				var isLast = parameters.Any(s => s == "last");
				var isSingle = parameters.Any(s => s == "single");

				var matches = Regex.Matches(input, regex.Regex).Cast<Match>().ToArray();

				var isCapture = matches.Any(m => m.Groups.Count > 1);

				string[] captures;

				if (isLast)
				{
					captures = matches
						.LastOrDefault()?
						.Groups.Cast<Group>()
						.LastOrDefault()?
						.Captures.Cast<Capture>()
						.Select(captureOperation).ToArray();
				}
				else
				{
					var groups = matches
						.FirstOrDefault()?
						.Groups.Cast<Group>().ToArray();

					var captureGroup = groups?.Skip(1).FirstOrDefault();

					var group = isCapture && captureGroup != null
						? captureGroup
						: groups?.FirstOrDefault();

					captures = group?.Captures.Cast<Capture>().Select(captureOperation).ToArray();
				}

				return (captures?.Any() == true ? (isSingle ? captures.Take(1) : captures) : new[] { defaultValue }).ToArray();
			}

			protected static IReadOnlyList<string> Replace(IReadOnlyList<string> buffer, ProcessorParameters procParams)
			{
				var regex = procParams.Regex;
				var pattern = regex?.Regex ?? procParams.Params.First();

				if (pattern == null)
				{
					return buffer;
				}

				var replacementPattern = regex?.Regex == null ? procParams.Params.Skip(1).First() : procParams.Params.First();

				IDictionary<string, string> replacementMap = null;

				try
				{
					replacementMap = SerialiserHelpers.DeserialiseSimpleJson(replacementPattern);
				}
				catch (FormatException)
				{ }

				return buffer
					.Select(s => replacementMap?.Any() == true
						? s.ReplaceGroups(pattern, replacementMap)
						: (regex?.Groups == null
							? Regex.Replace(s, pattern, replacementPattern)
							: s.ReplaceGroups(pattern, regex.Groups.ToDictionary(p => p, _ => replacementPattern)))).ToArray();
			}
		}

		public class ProcessorParameters
		{
			public IReadOnlyList<string> Params { get; set; }
			public RegexParams Regex { get; set; }
		}

		public class RegexParams
		{
			protected internal string Regex { get; set; }
			protected internal string[] Groups { get; set; }
		}

		public abstract class Construct : Processor
		{
			public readonly IReadOnlyList<Preprocessor> Preprocessors;
			public IReadOnlyList<PostProcessor> PostProcessors;

			protected internal Entity Context => State.Context;

			protected internal readonly Stack<PostProcessor> ResetProcessors = new();
			protected internal readonly Queue<IModifier> Modifiers = new();

			protected internal bool IsCacheResult = true;
			protected internal bool IsCacheGlobal;

			protected internal int? BackupLcid;

			protected Construct(GlobalState state, TokenKeyword keyword,
				IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
			: base(state, keyword)
			{
				Preprocessors = preProcessors;
				PostProcessors = postProcessors;
			}

			public virtual IReadOnlyList<string> Execute(string block)
			{
				var durationBackup = fallbackCacheDuration;

				try
				{
					var cacheConfig = State.InlineConfig?.CacheConfig;

					if (cacheConfig != null)
					{
						IsCacheResult = cacheConfig.IsEnabled;
						IsCacheGlobal = cacheConfig.IsGlobal;
						fallbackCacheDuration = cacheConfig.Duration;
					}

					if (this is IPreExecutable preExecutable)
					{
						preExecutable.PreExecute(ref block);
					}

					return ExecutePostProcessors(ExecuteConstruct(ExecutePreprocessors(block))
						.FilterNull().ToArray()).FilterNull().ToArray();
				}
				catch (Exception ex)
				{
					throw new Exception($"CrmParser failed ({Keyword.ErrorLocationContext}): {ex.BuildShortExceptionMessage()}", ex);
				}
				finally
				{
					State.Lcid = BackupLcid ?? State.Lcid;
					fallbackCacheDuration = durationBackup;
				}
			}

			protected string ExecutePreprocessors(string buffer)
			{
				Modifiers.Clear();

				foreach (var preprocessor in Preprocessors)
				{
					if (preprocessor is IModifiable modifiable)
					{
						ApplyModifiers(modifiable);
					}

					if (preprocessor is IModifier modifier)
					{
						Modifiers.Enqueue(modifier);
					}

					buffer = preprocessor.Execute(buffer, this);
				}

				if (this is IModifiable modifiableConstruct)
				{
					ApplyModifiers(modifiableConstruct);
				}

				return buffer;
			}

			private void ApplyModifiers(IModifiable modifiable)
			{
				var modifierCount = Modifiers.Count;

				for (var i = 0; i < modifierCount; i++)
				{
					var modifierDequeue = Modifiers.Dequeue();

					if (!modifierDequeue.Apply(modifiable))
					{
						Modifiers.Enqueue(modifierDequeue);
					}
				}
			}

			protected abstract IReadOnlyList<string> ExecuteConstruct(string buffer);

			protected IReadOnlyList<string> ExecutePostProcessors(IReadOnlyList<string> buffer)
			{
				Modifiers.Clear();

				var result =
					PostProcessors.IsFilled()
						? PostProcessors
							.Aggregate(buffer,
								(current, processor) =>
								{
									if (processor is IModifiable modifiable)
									{
										ApplyModifiers(modifiable);
									}

									if (processor is IModifier modifier)
									{
										Modifiers.Enqueue(modifier);
									}

									return processor.Execute(current).ToArray();
								})
							.FilterNull().ToArray()
						: buffer;

				// clean up
				while (ResetProcessors.Count > 0)
				{
					result = ResetProcessors.Pop().Execute(result);
				}

				return result;
			}
		}

		public abstract class DefaultConstruct : Construct
		{
			protected DefaultConstruct(GlobalState state, TokenKeyword keyword,
				IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override IReadOnlyList<string> ExecuteConstruct(string buffer)
			{
				return ExecuteLogic(buffer);
			}

			protected abstract IReadOnlyList<string> ExecuteLogic(string buffer);
		}

		public abstract class DefaultContextConstruct : DefaultConstruct
		{
			protected DefaultContextConstruct(GlobalState state, TokenKeyword keyword,
				IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override IReadOnlyList<string> ExecuteLogic(string buffer)
			{
				Context.Require(nameof(Context),
					$"An entity context was not provided for the '{GetType().Name}' construct for '{Keyword.ErrorLocationContext}'.");
				return new[] { ExecuteContextLogic(Context, buffer) };
			}

			protected abstract string ExecuteContextLogic(Entity context, string buffer);
		}

		public abstract class DefaultNoContextConstruct : DefaultConstruct
		{
			protected DefaultNoContextConstruct(GlobalState state, TokenKeyword keyword,
				IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override IReadOnlyList<string> ExecuteLogic(string buffer)
			{
				return new[] { ExecuteNoContextLogic(buffer) };
			}

			protected abstract string ExecuteNoContextLogic(string buffer);
		}

		public abstract class StageProcessor : Processor
		{
			protected StageProcessor(GlobalState state, TokenKeyword token) : base(state, token)
			{ }
		}

		public abstract class Preprocessor : StageProcessor
		{
			protected Preprocessor(GlobalState state, TokenKeyword token)
				: base(state, token)
			{ }

			public abstract string Execute(string block, Construct construct);
		}

		public abstract class PostProcessor : StageProcessor
		{
			protected PostProcessor(GlobalState state, TokenKeyword token)
				: base(state, token)
			{ }

			public abstract IReadOnlyList<string> Execute(IReadOnlyList<string> buffer);

			protected IReadOnlyList<string> ApplyToCaptures(IReadOnlyList<string> buffer, Func<string, string> action, RegexParams regex)
			{
				return buffer
					.Select(s => regex?.Regex == null ? action(s) : s.ReplaceGroups(regex.Regex, action)).ToArray();
			}
		}

		public class RuntimeGenerated : Attribute
		{ }

		public interface IScoped
		{ }

		public interface IPreExecutable
		{
			void PreExecute(ref string block);
		}

		public interface IModifier
		{
			bool Apply(IModifiable target);
		}

		public interface IModifiable
		{ }

		public interface IQuery : IModifiable
		{
			IReadOnlyList<string> Distinct { get; set; }
			IReadOnlyList<string> Order { get; set; }
		}

		public class RowValue
		{
			public virtual string StringValue
			{
				get
				{
					var key = $"CrmParser.RowValue.StringValue|{Context.LogicalName}|{Context.Id}";
					return StringValueInner.IsFilled()
						? StringValueInner
						: (State.Service == null
							? $"{Context.LogicalName}:{Context.Id.ToString().ToUpper()}"
							: State.GetCachedAdd(key,
								() => CrmHelpers.GetRecordName(State.Service, Context, true, null, State.OrgId),
								IsCacheGlobal));
				}
				set => StringValueInner = value;
			}

			public readonly Entity Context;

			public readonly GlobalState State;

			protected readonly bool IsCacheGlobal;
			protected string StringValueInner;

			public RowValue(Entity context, GlobalState state, bool isCacheGlobal)
			{
				Context = context;
				State = state;
				IsCacheGlobal = isCacheGlobal;
			}

			public override string ToString()
			{
				return StringValue;
			}
		}

		public class FieldValue : RowValue
		{
			public override string StringValue
			{
				get => StringValueInner.IsFilled() ? StringValueInner : CrmHelpers.GetAttributeName(FieldName, Context);
				set => StringValueInner = value;
			}

			public readonly string FieldName;
			public object Value => Context.GetAttributeValue<object>(FieldName);

			public FieldValue(Entity context, string fieldName, GlobalState state, bool isCacheGlobal)
				: base(context, state, isCacheGlobal)
			{
				FieldName = fieldName;
			}
		}

		#region Attributes

		[AttributeUsage(AttributeTargets.Class)]
		public class ProcessorAttribute : Attribute
		{
			public readonly string Key;
			public readonly string LongForm;

			public ProcessorAttribute(string key, string longForm = null)
			{
				Key = key;
				LongForm = longForm;
			}
		}

		[AttributeUsage(AttributeTargets.Class)]
		public class ConstructAttribute : ProcessorAttribute
		{
			public ConstructAttribute(string key, string longForm = null) : base(key, longForm)
			{ }
		}

		[AttributeUsage(AttributeTargets.Class)]
		public class PreprocessorAttribute : ProcessorAttribute
		{
			public PreprocessorAttribute(string key, string longForm = null) : base(key, longForm)
			{ }
		}

		[AttributeUsage(AttributeTargets.Class)]
		public class PostProcessorAttribute : ProcessorAttribute
		{
			public PostProcessorAttribute(string key, string longForm = null) : base(key, longForm)
			{ }
		}

		[AttributeUsage(AttributeTargets.Class)]
		public class ColourAttribute : Attribute
		{
			public readonly string Colour;

			public ColourAttribute(string colour = "d4d4d4")
			{
				Colour = colour;
			}
		}

		#endregion

		#endregion

		#region Constructs

		[Construct("t", "template")]
		[Colour("6B3880")]
		public class TemplateConstruct : Construct
		{
			public TemplateConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors,
				IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override IReadOnlyList<string> ExecuteConstruct(string buffer)
			{
				var parameters = ExtractParameters();
				State.Templates[parameters.Params.First()] = buffer;
				return Array.Empty<string>();
			}
		}

		[Construct("p", "placeholder")]
		[Colour("AA71C1")]
		public class PlaceholderConstruct : Construct, IPreExecutable
		{
			public PlaceholderConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			public void PreExecute(ref string block)
			{
				var replacements = ExtractParameters(0).Params;

				if ((replacements.Count % 2) != 0)
				{
					ThrowMisformattedParam();
				}

				var isFound = State.Templates.TryGetValue(block, out var template);

				if (!isFound)
				{
					throw new KeyNotFoundException($"Template '{block}' for 'placeholder (p)' construct is undefined for '{Keyword.ErrorLocationContext}'");
				}

				for (var i = 0; i < replacements.Count; i += 2)
				{
					block = template.Replace(replacements[i], replacements[i + 1]);
				}
			}

			protected override IReadOnlyList<string> ExecuteConstruct(string buffer)
			{
				var body = new TokenGlobal(buffer);
				body.ProcessInput();
				buffer = ProcessToken(body, State).StringAggregate("");

				return new[] { buffer };
			}
		}

		[Construct(".", "reference")]
		[Colour("6C8BDA")]
		public class ReferenceConstruct : Construct, IQuery, IScoped
		{
			public IReadOnlyList<string> Distinct { get; set; }
			public IReadOnlyList<string> Order { get; set; }

			public ReferenceConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override IReadOnlyList<string> ExecuteConstruct(string buffer)
			{
				Context.Require(nameof(Context),
					$"An entity context was not provided for the 'context switch (.)' construct for '{Keyword.ErrorLocationContext}'.");

				var parameters = ExtractParameters().Params;

				var backupContext = Context;
				backupContext.Require(nameof(backupContext), "An entity context was not provided for the 'context switch (.)' construct.");

				var match = Regex.Match(parameters.First(),
					@"^([a-zA-Z0-9_]+)(?:((?:\.?|#)[a-zA-Z0-9_]+))*$");

				if (!match.Success)
				{
					return new[] { buffer };
				}

				var output = new List<string>();
				var scopeName = match.Groups[1].Value;

				var isGlobal = parameters.Contains("global");

				// context given name
				var contextName = (parameters.Count == 2 && !isGlobal) || (parameters.Count > 2) ? parameters[1] : null;

				// loop element name
				var localVarName = (parameters.Count == 3 && !isGlobal) || (parameters.Count > 3) ? parameters[2] : null;

				var storedScope = State.Read<object>(scopeName);
				var scope = new List<object>();

				if (storedScope is Func<object> storedFunc)
				{
					storedScope = storedFunc();
				}

				if (storedScope is IEnumerable<object> storedCollection)
				{
					scope.AddRange(storedCollection);
				}
				else
				{
					scope.Add(storedScope);
				}

				var isContextStored = scope.FirstOrDefault() is Entity;

				var localContexts = isContextStored ? scope.Cast<Entity>() : new[] { Context };

				foreach (var context in localContexts)
				{
					var traversalContexts = BuildTraversalContext(context, match.Groups[2].ExtractCaptures().ToArray(),
						State, Distinct, Order, IsCacheResult, IsCacheGlobal);

					foreach (var traversalContext in traversalContexts)
					{
						if (contextName.IsFilled())
						{
							State.Store(contextName, traversalContext);
						}

						State.Context = traversalContext;

						if (localVarName.IsFilled())
						{
							if (isContextStored)
							{
								State.Store(localVarName, traversalContext);
								output.Add(ProcessBody() ?? buffer);
							}
							else
							{
								foreach (var element in scope)
								{
									State.Store(localVarName, element);
									output.Add(ProcessBody() ?? buffer);
								}
							}
						}
						else
						{
							output.Add(ProcessBody() ?? buffer);
						}

						if (!isGlobal)
						{
							State.Context = backupContext;
						}
					}
				}

				return output;
			}

			private string ProcessBody()
			{
				return Keyword is TokenConstruct{Body: not null } construct ? ProcessToken(construct.Body, State).StringAggregate("") : null;
			}
		}

		[Construct("c", "column")]
		[Colour("E9590C")]
		public class ColumnConstruct : DefaultContextConstruct, IModifiable
		{
			public ColumnConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteContextLogic(Entity context, string buffer)
			{
				var match = Regex.Match(buffer, @"^([a-zA-Z0-9_]+?)(?:\.([a-zA-Z0-9_]+?))*$");

				if (!match.Success)
				{
					return null;
				}

				var initialFieldName = match.Groups[1].Value;
				var traversal = new[] { initialFieldName }.Union(match.Groups[2].ExtractCaptures()).ToArray();

				FieldValue returnValue = null;

				foreach (var fieldName in traversal)
				{
					var fieldValue = context.GetAttributeValue<object>(fieldName);

					// get the entity record
					context = fieldValue == null && context.LogicalName.IsFilled() && context.Id != Guid.Empty
						&& !State.IsContextProvided
						? context.IntegrateAttributes(Retrieve(State, context.LogicalName, context.Id,
							IsCacheResult, IsCacheGlobal, fieldName))
						: context;

					fieldValue = context.GetAttributeValue<object>(fieldName);

					returnValue = new FieldValue(context, fieldName, State, IsCacheGlobal);

					// if the field value is not a lookup, then we can't recurse
					if (fieldValue is not EntityReference reference)
					{
						break;
					}

					context =
						new Entity(reference.LogicalName)
						{
							Id = reference.Id
						};
				}

				var key = $"CrmParser.ColumnConstruct.ExecuteContextLogic|{context.LogicalName}|{context.Id}|{traversal.StringAggregate()}";

				if (returnValue == null)
				{
					return null;
				}

				var parameters = ExtractParameters(0).Params;

				if (parameters.IsEmpty())
				{
					return returnValue.StringValue;
				}

				switch (parameters.First())
				{
					case "raw":
						return returnValue.ToString();
					case "name":
						return
							returnValue.Value is OptionSetValue optionSet && State.Lcid != 1033
								? MetadataHelpers.GetOptionSetLabel(State.Service, returnValue.Context.LogicalName, returnValue.FieldName,
									optionSet.Value, State.Lcid, State.OrgId)
								: CrmHelpers.GetAttributeName(returnValue.FieldName, returnValue.Context);
					case "log":
						return returnValue.Value is EntityReference er1 ? er1.LogicalName : "";
					case "id":
						return
							(returnValue.Value is EntityReference er2
								? er2.Id
								: returnValue.Value is Guid id
									? id
									: (Guid?)null)?
								.ToString().ToUpper();
					case "url":
						return
							returnValue.Value is EntityReference er3
								? State.GetCachedAdd(key, () => CrmHelpers.GetRecordUrl(State.Service, er3, State.OrgId), IsCacheGlobal)
								: "";
					default:
						return returnValue.StringValue;
				}
			}
		}
		
		[Construct("i", "rowinfo")]
		[Colour("FABE9E")]
		public class RowInfoConstruct : DefaultContextConstruct
		{
			public RowInfoConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteContextLogic(Entity context, string buffer)
			{
				buffer.RequireFilled("block", $"Block for 'row info' construct is missing for '{Keyword.ErrorLocationContext}'.");
				var key = $"CrmParser.RowInfoConstruct.ExecuteContextLogic|{buffer}|{context.LogicalName}|{context.Id}";

				return
					buffer switch {
						"raw" => $"{context.LogicalName}:{context.Id.ToString().ToUpper()}",
						"name" =>
							State.GetCachedAdd($"{key}|name", () => CrmHelpers.GetRecordName(State.Service, context, true, null, State.OrgId),
								IsCacheGlobal),
						"log" => context.LogicalName,
						"id" => context.Id.ToString().ToUpper(),
						"url" =>
							State.GetCachedAdd($"{key}|url", () => CrmHelpers.GetRecordUrl(State.Service, context.ToEntityReference(), State.OrgId),
								IsCacheGlobal),
						_ =>
							throw
								new NotSupportedException($"Value for 'row info' construct" + $" is not supported ('{buffer}') for '{Keyword.ErrorLocationContext}'.")
						};
			}
		}

		[Construct("u", "userinfo")]
		[Colour("F58549")]
		public class UserInfoConstruct : DefaultNoContextConstruct
		{
			public UserInfoConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteNoContextLogic(string buffer)
			{
				var isId = Guid.TryParse(ExtractParameters(0).Params.FirstOrDefault(), out var userIdParam);

				buffer.RequireFilled("block", $"Block for {Keyword.Value} construct is missing for '{Keyword.ErrorLocationContext}'.");
				var key = $"CrmParser.UserInfoConstruct.ExecuteContextLogic|{buffer}";

				var userId = isId ? userIdParam : ((WhoAmIResponse)State.Service.Execute(new WhoAmIRequest())).UserId;
				const string logicalName = "systemuser";
				var user = new Entity("systemuser", userId);

				return
					buffer switch {
						"raw" => $"{logicalName}:{userId.ToString().ToUpper()}",
						"name" =>
							State.GetCachedAdd($"{key}|name", () => CrmHelpers.GetRecordName(State.Service, user, true, null, State.OrgId),
								IsCacheGlobal),
						"log" => logicalName,
						"id" => userId.ToString().ToUpper(),
						"lcid" =>
							State.GetCachedAdd($"{key}|lcid", () => (int?)CrmHelpers.GetPreferredLangCode(State.Service, user.ToEntityReference()),
								IsCacheGlobal).ToString(),
						"url" =>
							State.GetCachedAdd($"{key}|url", () => CrmHelpers.GetRecordUrl(State.Service, user.ToEntityReference(), State.OrgId),
								IsCacheGlobal),
						_ =>
							throw
								new NotSupportedException($"Value for 'row info' construct" + $" is not supported ('{buffer}') for '{Keyword.ErrorLocationContext}'.")
						};
			}
		}

		[Construct("<", "preload")]
		[Colour("AE4309")]
		public class PreloadConstruct : DefaultContextConstruct
		{
			public PreloadConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteContextLogic(Entity context, string buffer)
			{
				buffer.RequireFilled("block", $"Block for '{Keyword.Value}' construct is missing for '{Keyword.ErrorLocationContext}'.");

				var list = buffer.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);

				if (context.Attributes.Keys.Intersect(list).Count() == list.Length)
				{
					return null;
				}
				
				context.IntegrateAttributes(Retrieve(State, context.LogicalName, context.Id,
					IsCacheResult, IsCacheGlobal, list));

				return null;
			}
		}

		[Construct("_", "discard")]
		[Colour("912F40")]
		public class DiscardConstruct : DefaultNoContextConstruct
		{
			public DiscardConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			public override IReadOnlyList<string> Execute(string block)
			{
				base.Execute(block);
				return Array.Empty<string>();
			}

			protected override string ExecuteNoContextLogic(string buffer)
			{
				return buffer;
			}
		}

		[Construct("e", "expression")]
		[Colour("8A7968")]
		public class ExpressionConstruct : DefaultNoContextConstruct
		{
			public ExpressionConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteNoContextLogic(string buffer)
			{
				return buffer;
			}
		}

		[Construct("s", "inlineconfig")]
		[Colour("B2EF9B")]
		public class ConfigConstruct : Construct
		{
			public ConfigConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override IReadOnlyList<string> ExecuteConstruct(string buffer)
			{
				var parameters = ExtractParameters().Params;
				var input = SerialiserHelpers.DeserialiseSimpleJson(parameters.FirstOrDefault());
				var isGlobal = parameters.Contains("global");

				var config = new InlineConfig();

				foreach (var option in input)
				{
					var value = option.Value;

					switch (option.Key)
					{
						case "html":
							if (bool.TryParse(value, out var isParseHtml))
							{
								config.IsParseHtml = isParseHtml;
							}

							break;

						case "cache":
							var cacheConfigRaw = SerialiserHelpers.DeserialiseSimpleJson(value);
							var cacheConfig = config.CacheConfig = new CacheConfig();

							foreach (var cacheOption in cacheConfigRaw)
							{
								var cacheOptionValue = cacheOption.Value;

								switch (cacheOption.Key)
								{
									case "enabled":
										if (bool.TryParse(cacheOptionValue, out var isCacheEnabled))
										{
											cacheConfig.IsEnabled = isCacheEnabled;
										}

										break;

									case "global":
										if (bool.TryParse(cacheOptionValue, out var isCacheGlobal))
										{
											cacheConfig.IsGlobal = isCacheGlobal;
										}

										break;

									case "dur":
										if (int.TryParse(cacheOptionValue, out var duration))
										{
											cacheConfig.Duration = TimeSpan.FromSeconds(duration);
										}

										break;
								}
							}

							break;
					}
				}

				State.InlineConfig = config;

				var output = new[] { ProcessBody() };

				if (!isGlobal)
				{
					State.InlineConfig = null;
				}

				return output;
			}

			private string ProcessBody()
			{
				return Keyword is TokenConstruct construct ? ProcessToken(construct.Body, State).StringAggregate("") : string.Empty;
			}
		}

		public class InlineConfig
		{
			public bool IsParseHtml { get; set; }
			public CacheConfig CacheConfig { get; set; }
		}

		public class CacheConfig
		{
			public bool IsEnabled { get; set; } = true;
			public bool IsGlobal { get; set; }
			public TimeSpan Duration { get; set; } = fallbackCacheDuration;
		}
		
		[Construct("r", "replace")]
		[Colour("585123")]
		public class ReplaceConstruct : DefaultNoContextConstruct
		{
			public ReplaceConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteNoContextLogic(string buffer)
			{
				var procParams = ExtractParameters(1, true);
				return Replace(new [] { buffer }, procParams).FirstOrDefault();
			}
		}

		[Construct("v", "dictionary")]
		[Colour("77E250")]
		public class DictionaryConstruct : DefaultNoContextConstruct
		{
			public DictionaryConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteNoContextLogic(string buffer)
			{
				var fetch =
					$@"<fetch no-lock='true'>
  <entity name='ys_keyvalue' >
    <attribute name='ys_value' />
    <attribute name='ys_value{(State.Lcid == 1033 ? "" : $"_{State.Lcid}")}' />
    <filter>
      <condition attribute='ys_name' operator='eq' value='{buffer}' />
    </filter>
  </entity>
</fetch>";

				return RetrieveMultiple(State.Service, State, fetch, IsCacheResult, IsCacheGlobal)
					.FirstOrDefault()?.GetAttributeValue<string>("ys_value");
			}
		}

		[Construct("g", "commonconfig")]
		[Colour("4BC11F")]
		public class CommonConfigConstruct : DefaultNoContextConstruct
		{
			public CommonConfigConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteNoContextLogic(string buffer)
			{
				buffer.RequireFilled("block", $"Block for '{Keyword.Value}' construct is missing for '{Keyword.ErrorLocationContext}'.");

				var config = CrmHelpers.GetGenericConfig(State.Service, State.OrgId);
				var value = CrmHelpers.GetGenericConfig(State.Service, State.OrgId).GetAttributeValue<object>(buffer);

				return
					value is OptionSetValue optionSet && State.Lcid != 1033
						? MetadataHelpers.GetOptionSetLabel(State.Service, config.LogicalName, buffer,
							optionSet.Value, State.Lcid, State.OrgId)
						: CrmHelpers.GetAttributeName(buffer, config);
			}
		}
		
		[Construct("f", "fetch")]
		[Colour("3B65CE")]
		public class FetchConstruct : DefaultNoContextConstruct
		{
			public FetchConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteNoContextLogic(string buffer)
			{
				var name = ExtractParameters().Params.First();

				if (name.IsEmpty())
				{
					ThrowMisformattedParam(nameof(name));
				}

				var body = new TokenGlobal(buffer);
				body.ProcessInput();

				State.Store(name, new Func<Entity[]>(
					() => RetrieveMultiple(State.Service, State, ProcessToken(body, State).StringAggregate(""), IsCacheResult, IsCacheGlobal)));

				return null;
			}
		}
		
		[Construct("a", "action")]
		[Colour("294BA3")]
		public class ActionConstruct : DefaultContextConstruct
		{
			public ActionConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteContextLogic(Entity context, string buffer)
			{
				var parameters = ExtractParameters().Params;
				var storeName = parameters.First();
				var input = parameters.Count > 1 ? parameters.Skip(1).FirstOrDefault(p => p != "global") : null;
				var isGlobal = parameters.Contains("global");

				State.Store(storeName, new Func<Entity>(() =>
					CallAction(State.Service, buffer, isGlobal ? null : context.ToEntityReference(), input)));

				return null;
			}
		}
		
		[Construct("*", "rand")]
		[Colour("FFC2C2")]
		public class RandConstruct : DefaultNoContextConstruct
		{
			public RandConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteNoContextLogic(string buffer)
			{
				var processorParameters = ExtractParameters(2);
				var parameters = processorParameters.Params;

				if (!int.TryParse(parameters[0], out var length))
				{
					throw new ArgumentNullException("Length", "Random generator string length param is missing.");
				}

				if (parameters.Count < 2)
				{
					throw new ArgumentNullException("Pool", "Random generator character pool param is missing.");
				}

				var pool = parameters[1];
				
				const string flagIndexer = "culn";
				var flags = pool.Select(flag => (RandomGenerator.SymbolFlag)flagIndexer.IndexOf(flag))
					.ToArray();

				if (flags.IsEmpty())
				{
					throw new ArgumentNullException("Pool", "Random generator character pool param is missing.");
				}

				var isLetterStart = false;

				if (parameters.Count >= 3 && !bool.TryParse(parameters[2], out isLetterStart))
				{
					throw new ArgumentNullException("Letter Start", "Random generator 'letter start' param flag value is invalid.");
				}

				var numberLetterRatio = 50;

				if (parameters.Count >= 4 && !int.TryParse(parameters[3], out numberLetterRatio))
				{
					throw new ArgumentNullException("Number to Letter Ratio", "Random generator 'number-letter ratio' param value is invalid (percentage; e.g. '43').");
				}


				return flags.Contains(RandomGenerator.SymbolFlag.Custom)
					? RandomGenerator.GetRandomString(length, isLetterStart, numberLetterRatio, buffer.Split(','))
					: RandomGenerator.GetRandomString(length, isLetterStart, numberLetterRatio, flags.ToArray());
			}
		}
		
		[Construct("d", "date")]
		[Colour("FFFD98")]
		public class DateConstruct : DefaultNoContextConstruct
		{
			public DateConstruct(GlobalState state, TokenKeyword keyword, IReadOnlyList<Preprocessor> preProcessors, IReadOnlyList<PostProcessor> postProcessors)
				: base(state, keyword, preProcessors, postProcessors)
			{ }

			protected override string ExecuteNoContextLogic(string buffer)
			{
				return DateTime.UtcNow.ToString("s");
			}
		}
		
		#endregion

		#region Preprocessors

		[Preprocessor("filter")]
		public class FilterPreprocessor : Preprocessor, IModifier
		{
			public FilterPreprocessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override string Execute(string block, Construct construct)
			{
				return block;
			}

			public bool Apply(IModifiable target)
			{
				throw new NotImplementedException();
			}
		}

		[Preprocessor("distinct")]
		public class DistinctPreprocessor : Preprocessor, IModifier
		{
			public DistinctPreprocessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override string Execute(string block, Construct construct)
			{
				return block;
			}

			public bool Apply(IModifiable target)
			{
				var parameters = ExtractParameters(0).Params;

				if (target is IQuery query && parameters.IsFilled())
				{
					query.Distinct = parameters;
					return true;
				}

				return false;
			}
		}

		[Preprocessor("order")]
		public class OrderPreprocessor : Preprocessor, IModifier
		{
			public OrderPreprocessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override string Execute(string block, Construct construct)
			{
				return block;
			}

			public bool Apply(IModifiable target)
			{
				var parameters = ExtractParameters(0).Params;

				if (target is IQuery query && parameters.IsFilled())
				{
					query.Order = parameters;
					return true;
				}

				return false;
			}
		}

		[Preprocessor("cache")]
		public class CachePreprocessor : Preprocessor
		{
			public CachePreprocessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override string Execute(string block, Construct construct)
			{
				var parameters = ExtractParameters(0).Params;

				if (bool.TryParse(parameters.FirstOrDefault(), out var isCache))
				{
					construct.IsCacheResult = isCache;
				}

				if (parameters.Contains("global"))
				{
					construct.IsCacheGlobal = true;
				}

				return block;
			}
		}

		[Preprocessor("store")]
		public class StorePreprocessor : Preprocessor
		{
			public StorePreprocessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override string Execute(string block, Construct construct)
			{
				return State.Store(ExtractParameters().Params.First(), block);
			}
		}

		[Preprocessor("read")]
		public class ReadPreprocessor : Preprocessor
		{
			public ReadPreprocessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override string Execute(string block, Construct construct)
			{
				return
					State.Read<object>(ExtractParameters().Params.First()) switch {
						string str => str,
						IEnumerable<object> e => e.StringAggregate(""),
						_ => ""
						};
			}
		}

		[Preprocessor("local")]
		public class LocalisePreprocessor : Preprocessor, IModifier
		{
			public LocalisePreprocessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override string Execute(string block, Construct construct)
			{
				return block;
			}

			public bool Apply(IModifiable target)
			{
				var parameters = ExtractParameters(0).Params;

				if (target is Construct construct && parameters.IsFilled())
				{
					var isLcid = int.TryParse(parameters.First(), out var lcid);
					var isGlobal = parameters.Contains("global");

					if (isLcid)
					{
						if (!isGlobal)
						{
							construct.BackupLcid = State.Lcid;
						}

						State.Lcid = lcid;
					}

					return true;
				}

				return false;
			}
		}
		
		[Preprocessor("replace")]
		public class StringReplacePreprocessor : Preprocessor
		{
			public StringReplacePreprocessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override string Execute(string block, Construct construct)
			{
				var procParams = ExtractParameters(1, true);
				return Replace(new[] { block }, procParams).StringAggregate("");
			}
		}

		#endregion

		#region Post processors

		[PostProcessor("store")]
		public class StorePostProcessor : PostProcessor
		{
			public StorePostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return State.Store(ExtractParameters().Params.First(), buffer.FilterNull()).ToArray();
			}
		}

		[PostProcessor("read")]
		public class ReadPostProcessor : PostProcessor
		{
			public ReadPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return
					State.Read<object>(ExtractParameters().Params.First()) switch {
						string str => new[] { str },
						IEnumerable<string> e => e.ToArray(),
						_ => Array.Empty<string>()
						};
			}
		}

		[PostProcessor("discard")]
		public class DiscardPostProcessor : PostProcessor
		{
			public DiscardPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return buffer.Select(_ => (string)null).ToArray();
			}
		}

		[PostProcessor("sub")]
		public class StringSubPostProcessor : PostProcessor
		{
			public StringSubPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var processorParameters = ExtractParameters();
				var parameters = processorParameters.Params;

				var isStart = int.TryParse(parameters.First(), out var start);

				if (!isStart)
				{
					ThrowMisformattedParam(nameof(start));
				}

				var isLength = int.TryParse(parameters.Skip(1).First(), out var length);

				return ApplyToCaptures(buffer, s => isLength ? s.Substring(start, length) : s.Substring(start), processorParameters.Regex);
			}
		}

		[PostProcessor("trim")]
		public class StringTrimPostProcessor : PostProcessor
		{
			public StringTrimPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var processorParameters = ExtractParameters();
				var parameters = processorParameters.Params;

				var characters = parameters.First().ToCharArray();
				var isStart = parameters.Skip(1).Contains("start");
				var isEnd = parameters.Skip(1).Contains("end");

				return ApplyToCaptures(buffer,
					s =>
					{
						if (isStart)
						{
							s = s.TrimStart(characters);
						}

						if (isEnd)
						{
							s = s.TrimEnd(characters);
						}

						if (!isStart && !isEnd)
						{
							s = s.Trim(characters);
						}

						return s;
					},
					processorParameters.Regex);
			}
		}

		[PostProcessor("pad")]
		public class StringPadPostProcessor : PostProcessor
		{
			public StringPadPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var processorParameters = ExtractParameters(2);
				var parameters = processorParameters.Params;

				var character = parameters.First().ToCharArray().FirstOrDefault();

				if (character == default(char))
				{
					ThrowMisformattedParam(nameof(character));
				}

				var isLength = int.TryParse(parameters.Skip(1).First(), out var length);

				if (!isLength)
				{
					ThrowMisformattedParam(nameof(length));
				}

				var isRight = parameters.Skip(2).Contains("right");

				return ApplyToCaptures(
					buffer,
					s => isRight
						? s.PadRight(length, character)
						: s.PadLeft(length, character),
					processorParameters.Regex);
			}
		}

		[PostProcessor("length")]
		public class StringLengthPostProcessor : PostProcessor
		{
			public StringLengthPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var processorParameters = ExtractParameters(0);
				var output = new List<string>();

				ApplyToCaptures(
					buffer,
					s =>
					{
						output.Add(s.Length.ToString());
						return null;
					},
					processorParameters.Regex).ToArray();

				return output;
			}
		}

		[PostProcessor("upper")]
		public class StringUpperPostProcessor : PostProcessor
		{
			public StringUpperPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return ApplyToCaptures(
					buffer,
					s => s.ToUpper(),
					ExtractParameters(0).Regex);
			}
		}

		[PostProcessor("lower")]
		public class StringLowerPostProcessor : PostProcessor
		{
			public StringLowerPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return ApplyToCaptures(
					buffer,
					s => s.ToLower(),
					ExtractParameters(0).Regex);
			}
		}

		[PostProcessor("sentence")]
		public class StringSentencePostProcessor : PostProcessor
		{
			public StringSentencePostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return ApplyToCaptures(
					buffer,
					s => s.ToSentenceCase(),
					ExtractParameters(0).Regex);
			}
		}

		[PostProcessor("title")]
		public class StringTitlePostProcessor : PostProcessor
		{
			public StringTitlePostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return ApplyToCaptures(
					buffer,
					s => s.ToTitleCase(),
					ExtractParameters(0).Regex);
			}
		}

		[PostProcessor("truncate")]
		public class StringTruncatePostProcessor : PostProcessor
		{
			public StringTruncatePostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var processorParameters = ExtractParameters();
				var parameters = processorParameters.Params;

				var isLength = int.TryParse(parameters.First(), out var length);

				if (!isLength)
				{
					ThrowMisformattedParam(nameof(length));
				}

				var replacement = parameters.Skip(1).FirstOrDefault();

				return ApplyToCaptures(
					buffer,
					s => s.Truncate(length, replacement),
					processorParameters.Regex);
			}
		}

		[PostProcessor("index")]
		public class StringIndexPostProcessor : PostProcessor
		{
			public StringIndexPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var processorParameters = ExtractParameters(0, true);
				return buffer.Select(s => ExtractMatches(s, processorParameters, c => c.Index.ToString(), "-1").StringAggregate()).ToArray();
			}
		}

		[PostProcessor("extract")]
		public class StringExtractPostProcessor : PostProcessor
		{
			public StringExtractPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var processorParameters = ExtractParameters(0, true);
				return buffer.Select(s => ExtractMatches(s, processorParameters, c => c.Value, null).StringAggregate()).ToArray();
			}
		}

		[PostProcessor("replace")]
		public class StringReplacePostProcessor : PostProcessor
		{
			public StringReplacePostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var procParams = ExtractParameters(1, true);
				return Replace(buffer, procParams);
			}
		}

		[PostProcessor("split")]
		public class StringSplitPostProcessor : PostProcessor
		{
			public StringSplitPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var parameters = ExtractParameters();
				var splitString = parameters.Params.First();

				return buffer.SelectMany(
					s => ExtractMatches(s, parameters)
						.SelectMany(m => m.Split(new[] { splitString }, StringSplitOptions.None))).ToArray();
			}
		}

		[PostProcessor("html")]
		public class StringHtmlPostProcessor : PostProcessor
		{
			public StringHtmlPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var procParams = ExtractParameters(0);
				var isEncode = procParams.Params.Contains("encode") || procParams.Params.All(p => p != "decode");

				string Action(string e) => isEncode ? WebUtility.HtmlEncode(e) : WebUtility.HtmlDecode(e);

				return buffer.Select(s => procParams.Regex == null ? Action(s) : s.ReplaceGroups(procParams.Regex.Regex, Action)).ToArray();
			}
		}
		
		[PostProcessor("date")]
		public class FormatDatePostProcessor : PostProcessor
		{
			public FormatDatePostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var processorParameters = ExtractParameters();
				var parameters = processorParameters.Params;

				var outputFormat = parameters.First();
				var kindString = parameters.Skip(1).FirstOrDefault();
				var kind = kindString == "utc" ? DateTimeStyles.AssumeUniversal : DateTimeStyles.AssumeLocal;
				var inputFormat = parameters.Skip(2).FirstOrDefault();

				return ApplyToCaptures(
					buffer,
					s => inputFormat.IsFilled()
						&& DateTime.TryParseExact(s, inputFormat, CultureInfo.CurrentCulture, kind, out var parsed)
						? parsed.ToString(outputFormat)
						: (DateTime.TryParse(s, CultureInfo.CurrentCulture, kind, out parsed)
							? parsed.ToString(outputFormat)
							: s),
					processorParameters.Regex);
			}
		}

		[PostProcessor("number")]
		public class FormatNumberPostProcessor : PostProcessor
		{
			public FormatNumberPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var processorParameters = ExtractParameters();
				var parameters = processorParameters.Params;

				var format = parameters.First();

				return ApplyToCaptures(
					buffer,
					s => format.IsFilled() && double.TryParse(s, out var parsed)
						? parsed.ToString(format)
						: s,
					processorParameters.Regex);
			}
		}

		[PostProcessor("clear")]
		public class AggrClearPostProcessor : PostProcessor
		{
			public AggrClearPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return buffer.FilterEmpty().ToArray();
			}
		}

		[PostProcessor("first")]
		public class AggrFirstPostProcessor : PostProcessor
		{
			public AggrFirstPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return buffer.Take(1).ToArray();
			}
		}

		[PostProcessor("nth")]
		public class AggrNthPostProcessor : PostProcessor
		{
			public AggrNthPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var isNth = int.TryParse(ExtractParameters().Params.First(), out var nth);

				if (!isNth)
				{
					ThrowMisformattedParam(nameof(nth));
				}

				return buffer.Skip(nth - 1).Take(1).ToArray();
			}
		}

		[PostProcessor("last")]
		public class AggrLastPostProcessor : PostProcessor
		{
			public AggrLastPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var collection = buffer.ToArray();
				return collection.Skip(collection.Length - 1).ToArray();
			}
		}

		[PostProcessor("count")]
		public class AggrCountPostProcessor : PostProcessor
		{
			public AggrCountPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return new[] { buffer.Count().ToString() };
			}
		}

		[PostProcessor("join")]
		public class AggrJoinPostProcessor : PostProcessor
		{
			public AggrJoinPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var join = ExtractParameters(0).Params.FirstOrDefault() ?? "";
				return new[] { string.Join(join, buffer) };
			}
		}

		[PostProcessor("min")]
		public class AggrMinPostProcessor : PostProcessor
		{
			public AggrMinPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return
					new[]
					{
						buffer
							.Select(s => double.TryParse(s, out var parsed) ? parsed : (double?)null)
							.FilterNull()
							.Min().ToString()
					};
			}
		}

		[PostProcessor("max")]
		public class AggrMaxPostProcessor : PostProcessor
		{
			public AggrMaxPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return
					new[]
					{
						buffer
							.Select(s => double.TryParse(s, out var parsed) ? parsed : (double?)null)
							.FilterNull()
							.Max().ToString()
					};
			}
		}

		[PostProcessor("avg")]
		public class AggrAvgPostProcessor : PostProcessor
		{
			public AggrAvgPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return
					new[]
					{
						buffer
							.Select(s => double.TryParse(s, out var parsed) ? parsed : (double?)null)
							.FilterNull()
							.Average().ToString()
					};
			}
		}

		[PostProcessor("sum")]
		public class AggrSumPostProcessor : PostProcessor
		{
			public AggrSumPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return
					new[]
					{
						buffer
							.Select(s => double.TryParse(s, out var parsed) ? parsed : (double?)null)
							.FilterNull()
							.Sum().ToString()
					};
			}
		}

		[PostProcessor("top")]
		public class AggrTopPostProcessor : PostProcessor
		{
			public AggrTopPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var isTop = int.TryParse(ExtractParameters().Params.First(), out var top);

				if (!isTop)
				{
					ThrowMisformattedParam(nameof(top));
				}

				return buffer.FilterNull().Take(top).ToArray();
			}
		}

		[PostProcessor("distinct")]
		public class AggrDistinctPostProcessor : PostProcessor
		{
			public AggrDistinctPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var parameters = ExtractParameters(0);

				return
					parameters?.Regex?.Regex.IsFilled() != true
						? buffer.Distinct().ToArray()
						: buffer.DistinctBy(s => ExtractMatches(s, parameters).StringAggregate()).ToArray();
			}
		}

		[PostProcessor("order")]
		public class AggrOrderPostProcessor : PostProcessor
		{
			public AggrOrderPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var parameters = ExtractParameters(0);
				var isDesc = parameters?.Params.Contains("true");

				return
					parameters?.Regex?.Regex.IsFilled() != true
						? buffer.OrderBy(s => s).ToArray()
						: (isDesc == true
							? buffer.OrderByDescending(s => ExtractMatches(s, parameters).StringAggregate())
							: buffer.OrderBy(s => ExtractMatches(s, parameters).StringAggregate())).ToArray();
			}
		}

		[PostProcessor("where")]
		public class AggrWherePostProcessor : PostProcessor
		{
			public AggrWherePostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				return buffer.Where(s => ExtractMatches(s, ExtractParameters(0, true)).FilterEmpty().Any()).ToArray();
			}
		}

		[PostProcessor("filter")]
		public class AggrFilterPostProcessor : PostProcessor
		{
			public AggrFilterPostProcessor(GlobalState state, TokenKeyword keyword)
				: base(state, keyword)
			{ }

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				var bufferArray = buffer.ToArray();
				return bufferArray
					.Except(bufferArray
						.Where(s => ExtractMatches(s, ExtractParameters(0, true)).FilterEmpty().Any())).ToArray();
			}
		}

		[RuntimeGenerated]
		private class InternalActionPostProcessor : PostProcessor
		{
			private readonly Action action;

			public InternalActionPostProcessor(GlobalState state, Action action)
				: base(state, null)
			{
				this.action = action;
			}

			public override IReadOnlyList<string> Execute(IReadOnlyList<string> buffer)
			{
				action();
				return buffer;
			}
		}

		#endregion
	}
}
