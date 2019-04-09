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
using System.Linq;
using System.Globalization;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Erasme.Json;
using Erasme.Http;

namespace Laclasse
{
    public interface IJsonable
    {
        JsonValue ToJson(HttpContext context);
    }

    public class SearchResult<T> : IJsonable where T : Model
    {
        public ModelList<T> Data;
        public int Limit;
        public int Total;
        public int Offset;

        public JsonValue ToJson(HttpContext context = null)
        {
            if (Limit > 0)
            {
                return new JsonObject
                {
                    ["total"] = Total,
                    ["limit"] = Limit,
                    ["page"] = (Offset / Limit) + 1,
                    ["data"] = Data
                };
            }
            else
                return Data;
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

    public static class HttpContextFilterExtension
    {
        public static Dictionary<string, List<string>> ToFilter(this HttpContext c)
        {
            var query = "";
            if (c.Request.QueryString.ContainsKey("query"))
                query = c.Request.QueryString["query"];

            var parsedQuery = query.QueryParser();
            foreach (var key in c.Request.QueryString.Keys)
                if (!parsedQuery.ContainsKey(key) && key != "query")
                    parsedQuery[key] = new List<string> { c.Request.QueryString[key] };
            foreach (var key in c.Request.QueryStringArray.Keys)
                if (!parsedQuery.ContainsKey(key))
                    parsedQuery[key] = c.Request.QueryStringArray[key];

            return parsedQuery;
        }
    }

    public static class ModelFilterExtension
    {
        static bool IsFieldMatch(Model item, string fieldOp, List<string> values)
        {
            string field = fieldOp;
            var op = CompareOperator.Equal;
            if (fieldOp.EndsWith("!", StringComparison.InvariantCulture))
            {
                op = CompareOperator.NotEqual;
                field = fieldOp.Substring(0, fieldOp.Length - 1);
            }
            else if (fieldOp.EndsWith("<", StringComparison.InvariantCulture))
            {
                op = CompareOperator.Less;
                field = fieldOp.Substring(0, fieldOp.Length - 1);
            }
            else if (fieldOp.EndsWith("<=", StringComparison.InvariantCulture))
            {
                op = CompareOperator.LessOrEqual;
                field = fieldOp.Substring(0, fieldOp.Length - 2);
            }
            else if (fieldOp.EndsWith(">", StringComparison.InvariantCulture))
            {
                op = CompareOperator.Greater;
                field = fieldOp.Substring(0, fieldOp.Length - 1);
            }
            else if (fieldOp.EndsWith(">=", StringComparison.InvariantCulture))
            {
                op = CompareOperator.GreaterOrEqual;
                field = fieldOp.Substring(0, fieldOp.Length - 2);
            }

            var pos = field.IndexOf('.');
            if (pos == -1)
            {
                if (!item.Fields.ContainsKey(field))
                    return false;

                var property = item.GetType().GetProperty(field);
                if (property == null)
                    return false;
                object value;
                var nullableType = Nullable.GetUnderlyingType(property.PropertyType);

                foreach (var oneValue in values)
                {
                    if (nullableType == null)
                        value = Convert.ChangeType(oneValue, property.PropertyType);
                    else
                        value = Convert.ChangeType(oneValue, nullableType);

                    if ((op == CompareOperator.Equal) && (value.Equals(item.Fields[field])))
                        return true;
                    else if ((op == CompareOperator.NotEqual) && (!value.Equals(item.Fields[field])))
                        return true;
                    else if (value is IComparable)
                    {
                        var comp = ((IComparable)value).CompareTo(item.Fields[field]);
                        if ((op == CompareOperator.Greater) && (comp < 0))
                            return true;
                        if ((op == CompareOperator.GreaterOrEqual) && (comp <= 0))
                            return true;
                        if ((op == CompareOperator.Less) && (comp > 0))
                            return true;
                        if ((op == CompareOperator.LessOrEqual) && (comp >= 0))
                            return true;
                    }
                }
                return false;
            }
            else
            {
                var expandField = field.Substring(0, pos);
                var remainField = field.Substring(pos + 1);

                if (!item.Fields.ContainsKey(expandField))
                    return false;

                if (item.Fields[expandField] is IModelList)
                {
                    foreach (Model obj in (IModelList)item.Fields[expandField])
                    {
                        if (IsFieldMatch(obj, remainField, values))
                            return true;
                    }
                    return false;
                }
                if (item.Fields[expandField] is Model)
                    return IsFieldMatch((Model)item.Fields[expandField], remainField, values);
                return false;
            }
        }

        static bool IsGlobalMatch(Model item, List<string> values)
        {
            foreach (var value in values)
            {
                var val = value.RemoveDiacritics();
                var found = false;
                foreach (var field in item.Fields.Keys)
                {
                    var fieldValue = item.Fields[field];
                    if (fieldValue != null)
                        if (CultureInfo.CurrentCulture.CompareInfo.IndexOf(fieldValue.ToString().RemoveDiacritics(), val, CompareOptions.IgnoreCase) >= 0)
                        {
                            found = true;
                            break;
                        }
                }
                if (!found)
                    return false;
            }
            return true;
        }

        public static bool IsFilterMatch<T>(this T item, Dictionary<string, List<string>> filter) where T : Model
        {
            foreach (var field in filter.Keys)
            {
                if (field == "global")
                    continue;
                if (!IsFieldMatch(item, field, filter[field]))
                    return false;
            }
            if (filter.ContainsKey("global") && !IsGlobalMatch(item, filter["global"]))
                return false;
            return true;
        }
    }

    public static class HttpContextExtensions
    {
        public static Setup GetSetup(this HttpContext context)
        {
            return (Setup)context.Data["setup"];
        }

        public static string RemoteIP(this HttpContext context)
        {
            var remoteIp = context.Request.RemoteEndPoint.ToString();
            // x-forwarded-for
            if (context.Request.Headers.ContainsKey("x-forwarded-for"))
                remoteIp = context.Request.Headers["x-forwarded-for"];
            return remoteIp;
        }

        public static string SelfURL(this HttpContext context)
        {
            var publicUrl = (string)context.Data["publicUrl"];
            if (publicUrl[publicUrl.Length - 1] == '/')
                publicUrl = publicUrl.Substring(0, publicUrl.Length - 1);
            if (context.Request.Headers.ContainsKey("x-forwarded-proto") &&
                context.Request.Headers.ContainsKey("x-forwarded-host"))
                publicUrl = context.Request.Headers["x-forwarded-proto"] + "://" + context.Request.Headers["x-forwarded-host"];
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
                if ((obj as JsonPrimitive).JsonType == JsonType.String)
                    sb.Append("\"" + (obj as JsonPrimitive).Value + "\"");
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

        public static JsonValue ToJson<T>(this T obj)
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
                    if (json[field] == null)
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

    public interface IEnumeratorAsync<T>
    {
        T Current { get; }
        Task<bool> MoveNextAsync();
    }

    public interface IEnumerableAsync<T>
    {
        Task<IEnumeratorAsync<T>> GetEnumeratorAsync();
    }

    public static class IEnumerableAsyncExtension
    {
        public static async Task ForEachAsync<T>(this IEnumerableAsync<T> list, Action<T> func)
        {
            var enumerator = await list.GetEnumeratorAsync();
            while (await enumerator.MoveNextAsync())
                func(enumerator.Current);
        }

        public static async Task ForEachAsync<T>(this IEnumerableAsync<T> list, Func<T, Task> func)
        {
            var enumerator = await list.GetEnumeratorAsync();
            while (await enumerator.MoveNextAsync())
                await func(enumerator.Current);
        }
    }
}
