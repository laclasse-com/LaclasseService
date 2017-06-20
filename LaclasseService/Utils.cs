// Utils.cs
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017 Daniel LACROIX
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.IO;
using System.Xml;
using System.Text;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using Erasme.Json;
using Erasme.Http;

namespace Laclasse
{
	public interface IJsonable
	{
		JsonValue ToJson();
	}

	public struct SearchResult<T>: IJsonable where T : Model
	{
		public ModelList<T> Data;
		public int Limit;
		public int Total;
		public int Offset;

		public JsonValue ToJson()
		{
			if (Limit > 0)
			{
				return new JsonObject
				{
					["total"] = Total,
					["page"] = (Offset / Limit) + 1,
					["data"] = Data
				};
			}
			else
				return Data.ToJson();
		}

		public static implicit operator HttpContent(SearchResult<T> searchResult)
		{
			return new JsonContent(searchResult.ToJson());
		}
	}

	public class XmlContent : StreamContent
	{
		public XmlContent(XmlDocument doc) : base(new MemoryStream())
		{
			Headers.ContentType = "text/xml; charset=\"UTF-8\"";

			var settings = new XmlWriterSettings();
			settings.Indent = true;
			settings.OmitXmlDeclaration = true;
			// use UTF-8 but without the BOM (3 bytes at the beginning which give the byte order)
			settings.Encoding = new UTF8Encoding(false);
			using (var xmlTextWriter = XmlWriter.Create(Stream, settings))
			{
				doc.Save(xmlTextWriter);
			}
			Stream.WriteByte(10);
			Stream.Seek(0, SeekOrigin.Begin);
		}

		public static implicit operator XmlContent(XmlDocument value)
		{
			return new XmlContent(value);
		}
	}

	public static class HttpContextExtensions
	{
		public static Setup GetSetup(this HttpContext context)
		{
			return (Setup)context.Data["setup"];
		}

		public static string SelfURL(this HttpContext context)
		{
			var publicUrl = (string)context.Data["publicUrl"];
			if (publicUrl[publicUrl.Length - 1] == '/')
				publicUrl = publicUrl.Substring(0, publicUrl.Length - 1);
			if (context.Request.Headers.ContainsKey("x-forwarded-proto") &&
			    context.Request.Headers.ContainsKey("x-forwarded-host"))
				publicUrl = context.Request.Headers["x-forwarded-proto"] + "://" +  context.Request.Headers["x-forwarded-host"];
			return publicUrl + context.Request.AbsolutePath;
		}
	}

	public static class StringExt
	{
		public static string RandomString(int size = 10, string randchars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789")
		{
			// generate the random id
			var rand = new Random();
			var sb = new StringBuilder(size);
			for (int i = 0; i < size; i++)
				sb.Append(randchars[rand.Next(randchars.Length)]);
			return sb.ToString();
		}
	}

	public static class DirExt
	{
		public static DirectoryInfo CreateRecursive(string path)
		{
			DirectoryInfo info;
			if (!System.IO.Directory.Exists(path))
			{
				var parent = System.IO.Directory.GetParent(path);
				if (!parent.Exists)
					CreateRecursive(parent.FullName);
				info = System.IO.Directory.CreateDirectory(path);
			}
			else
				info = new DirectoryInfo(path);
			return info;
		}
	}

	public static class IEnumerableExtensions
	{
		public static void ForEach<T>(this IEnumerable<T> xs, Action<T> f)
		{
			foreach (var x in xs) f(x);
		}

		public static void ForEach(this IEnumerable xs, Action<object> f)
		{
			foreach (var x in xs) f(x);
		}
	}

	public static class DictionaryExtensions
	{
		public static void RequireFields(this Dictionary<string, object> dict, params string[] fields)
		{
			foreach (var field in fields)
			{
				if (!dict.ContainsKey(field))
					throw new WebException(400, $"Bad protocol. '{field}' is needed");
			}
		}

		public static void RequireFields(this Dictionary<string, string> dict, params string[] fields)
		{
			foreach (var field in fields)
			{
				if (!dict.ContainsKey(field))
					throw new WebException(400, $"Bad protocol. '{field}' is needed");
			}
		}
	}

	public static class ObjectExtensions
	{
		static void Dump<T>(this T obj, StringBuilder sb, int indent)
		{
			if ((obj as object) == null)
				sb.Append("null");
			else if (obj is string)
				sb.Append("\"" + obj + "\"");
			else if (obj is ValueType)
				sb.Append(obj.ToString());
			else if (obj is JsonPrimitive)
			{
				if((obj as JsonPrimitive).JsonType == JsonType.String)
					sb.Append("\""+(obj as JsonPrimitive).Value+"\"");
				else
					sb.Append((obj as JsonPrimitive).Value.ToString());
			}
			else if (obj is IDictionary)
			{
				sb.Append(obj.GetType().Name);
				sb.Append(' ');
				sb.Append("{\n");
				var dict = (IDictionary)obj;
				foreach (object key in dict.Keys)
				{
					var keyStr = key as string;
					if (keyStr != null)
					{
						sb.Append(' ', indent + 2);
						sb.Append($"\"{keyStr}\": ");
						dict[key].Dump(sb, indent + 2);
						sb.Append("\n");
					}
				}
				sb.Append(' ', indent);
				sb.Append("}");
			}
			else if (obj is IEnumerable)
			{
				sb.Append(obj.GetType().Name);
				sb.Append(' ');
				sb.Append("[\n");
				var enumerable = (IEnumerable)obj;
				foreach (object child in enumerable)
				{
					sb.Append(' ', indent + 2);
					child.Dump(sb, indent + 2);
					sb.Append("\n");
				}
				sb.Append(' ', indent);
				sb.Append("]");
			}
			else
			{
				sb.Append(obj.GetType().Name);
				sb.Append(' ');
				sb.Append("{\n");
				foreach (var prop in obj.GetType().GetProperties())
				{
					if (prop.GetIndexParameters().Length > 0)
						continue;

					sb.Append(' ', indent + 2);
					sb.Append(prop.Name);
					sb.Append(": ");
					prop.GetValue(obj).Dump(sb, indent + 2);
					sb.Append("\n");
				}
				foreach (var field in obj.GetType().GetFields())
				{
					sb.Append(' ', indent + 2);
					sb.Append(field.Name);
					sb.Append(": ");
					field.GetValue(obj).Dump(sb, indent + 2);
					sb.Append("\n");
				}
				sb.Append(' ', indent);
				sb.Append("}");
			}
		}

		public static string Dump<T>(this T obj)
		{
			var sb = new StringBuilder();
			obj.Dump(sb, 0);
			return sb.ToString();
		}

		public static JsonObject ToJson<T>(this T obj)
		{
			return JsonValue.ObjectToJson(obj);
		}
	}

	public static class JsonValueExtensions
	{
		public static Dictionary<string, object> ExtractFields(this JsonValue json, params string[] fields)
		{
			var res = new Dictionary<string, object>();
			foreach (string field in fields)
			{
				if (json.ContainsKey(field))
				{
					if(json[field] == null)
						res.Add(field, null);
					else
						res.Add(field, json[field].Value);
				}
			}
			return res;
		}

		/// <summary>
		/// Check if the given fields are present. If not, raise a WebException with
		/// HTTP status 400
		/// </summary>
		public static void RequireFields(this JsonValue json, params string[] fields)
		{
			foreach (var field in fields)
			{
				if (!json.ContainsKey(field))
					throw new WebException(400, $"Bad protocol. '{field}' is needed");
			}
		}
	}

	public static class StringExtensions
	{
		/// <summary>
		/// Removes the diacritics (accents and special characters) for the given string.
		/// </summary>
		public static string RemoveDiacritics(this string s)
		{
			string normalizedString = null;
			var stringBuilder = new StringBuilder();
			normalizedString = s.Normalize(NormalizationForm.FormD);
			int i = 0;
			char c = '\0';

			for (i = 0; i <= normalizedString.Length - 1; i++)
			{
				c = normalizedString[i];
				if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
				{
					stringBuilder.Append(c);
				}
			}
			return stringBuilder.ToString();
		}

		public static Dictionary<string, List<string>> QueryParser(this string query)
		{
			var queryFields = new Dictionary<string, List<string>>();
			var parts = new List<string>();
			var sb = new StringBuilder();
			bool inString = false;
			bool inEscape = false;
			foreach (char c in query)
			{
				if (inEscape)
				{
					sb.Append(c);
					inEscape = false;
				}
				else
				{
					if (c == '\\')
						inEscape = true;
					else if (c == '"')
					{
						if (inString)
							inString = false;
						else
							inString = true;
					}
					else
					{
						if (inString)
							sb.Append(c);
						else if (c == ' ')
						{
							parts.Add(sb.ToString());
							sb.Clear();
						}
						else
							sb.Append(c);
					}
				}
			}
			if (sb.Length > 0)
				parts.Add(sb.ToString());

			foreach (var part in parts)
			{
				var fieldName = "global";
				string fieldValue;
				var pos = part.IndexOf(':');
				if (pos >= 0)
				{
					fieldName = part.Substring(0, pos);
					fieldValue = part.Substring(pos + 1);
				}
				else
					fieldValue = part;
				List<string> words;
				if (queryFields.ContainsKey(fieldName))
					words = queryFields[fieldName];
				else
				{
					words = new List<string>();
					queryFields[fieldName] = words;
				}
				if (!words.Contains(fieldValue))
					words.Add(fieldValue);
			}
			return queryFields;
		}
	}
}
