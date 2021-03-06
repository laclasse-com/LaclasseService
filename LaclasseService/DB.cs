﻿// DB.cs
// 
//  Simple query layer on top of MySqlClient 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
// Copyright (c) 2017 Daniel LACROIX
// Copyright (c) 2017 Metropole de Lyon
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
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Erasme.Json;
using Erasme.Http;
using Laclasse.Authentication;
using System.Reflection;
using System.Data;

namespace Laclasse
{
    public enum SortDirection
    {
        Ascending,
        Descending
    }

    public enum Right
    {
        Create,
        Read,
        Update,
        Delete
    }

    public enum CompareOperator
    {
        Equal,
        NotEqual,
        Less,
        LessOrEqual,
        Greater,
        GreaterOrEqual
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ModelAttribute : Attribute
    {
        public string Table;
        public string PrimaryKey;
        public string DB;
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class ModelFieldAttribute : Attribute
    {
        public bool Required;
        public bool Search = true;
        public bool DB = true;
        public Type ForeignModel;
        public string RegexMatch;
    }

    [AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
    public class ModelExpandFieldAttribute : Attribute
    {
        public bool Search = true;
        public bool Visible = true;
        public string Name;
        public Type ForeignModel;
        public string ForeignField;
    }

    public struct SqlFilter
    {
        public string Where;
        public string Inner;
    }

    public class Model
    {
        internal enum State
        {
            New,
            Diff
        }

        public virtual bool IsEmpty
        {
            get
            {
                return Fields.Count == 0;
            }
        }

        internal State state = State.New;
        public Dictionary<string, object> Fields = new Dictionary<string, object>();

        public static T CreateFromJson<T>(JsonValue value, params string[] filterFields) where T : Model, new()
        {
            T result = null;
            if (value is JsonObject)
            {
                result = new T();
                var obj = (JsonObject)value;

                result.FromJson(obj, filterFields);
            }
            return result;
        }

        public static bool operator ==(Model a, Model b)
        {
            if (ReferenceEquals(a, b))
                return true;
            if (((object)a == null) || ((object)b == null))
                return false;
            foreach (var key in a.Fields.Keys)
                if ((!b.Fields.ContainsKey(key)) || (!a.Fields[key].Equals(b.Fields[key])))
                    return false;
            foreach (var key in b.Fields.Keys)
                if (!a.Fields.ContainsKey(key))
                    return false;
            return true;
        }

        public static bool operator !=(Model a, Model b)
        {
            return !(a == b);
        }

        public override bool Equals(object obj)
        {
            var o = obj as Model;
            if ((object)o == null)
                return false;
            return this == o;
        }

        public bool EqualsIntersection(Model obj)
        {
            if (obj == null)
                return false;
            foreach (var key in obj.Fields.Keys)
            {
                if (!Fields.ContainsKey(key))
                    continue;

                var prop = GetType().GetProperty(key);
                if (prop == null)
                    continue;
                var attr = prop.GetCustomAttribute(typeof(ModelFieldAttribute));
                if (attr == null)
                    continue;
                if (Fields[key] == null)
                {
                    if (obj.Fields[key] != null)
                        return false;
                }
                else if (!Fields[key].Equals(obj.Fields[key]))
                    return false;
            }
            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public T Diff<T>(T b) where T : Model, new()
        {
            var diff = new T();
            diff.state = State.Diff;
            foreach (var key in b.Fields.Keys)
            {
                if (!Fields.ContainsKey(key))
                    continue;

                var prop = typeof(T).GetProperty(key);
                if (prop == null)
                    continue;
                var attr = (ModelFieldAttribute)prop.GetCustomAttribute(typeof(ModelFieldAttribute));
                if (attr == null)
                    continue;

                if (Fields[key] == null)
                {
                    if (b.Fields[key] != null)
                        diff.Fields[key] = b.Fields[key];
                }
                else if (!Fields[key].Equals(b.Fields[key]))
                    diff.Fields[key] = b.Fields[key];
            }
            return diff;
        }

        public T DiffWithId<T>(T b) where T : Model, new()
        {
            var attrs = GetType().GetCustomAttributes(typeof(ModelAttribute), true);
            string primaryKey = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";
            var diff = Diff(b);
            diff.Fields[primaryKey] = Fields[primaryKey];
            return diff;
        }

        public T SelectFields<T>(params string[] args) where T : Model, new()
        {
            var result = new T();
            foreach (var key in args)
            {
                if (Fields.ContainsKey(key))
                    result.Fields[key] = Fields[key];
            }
            return result;
        }

        public bool IsSet(string name)
        {
            return Fields.ContainsKey(name);
        }

        public void SetField<T>(string name, T value)
        {
            Fields[name] = value;
        }

        public void UnSetField(string name)
        {
            Fields.Remove(name);
        }

        public T GetField<T>(string name, T defaultValue)
        {
            return Fields.ContainsKey(name) ? (T)Fields[name] : defaultValue;
        }

        public virtual void FromJson(JsonObject json, string[] filterFields = null, HttpContext context = null)
        {
            foreach (var property in GetType().GetProperties())
            {
                if ((filterFields != null) && (filterFields.Length > 0) && !filterFields.Contains(property.Name))
                    continue;

                if (!json.ContainsKey(property.Name))
                    continue;

                var fieldAttribute = (ModelFieldAttribute)property.GetCustomAttribute(typeof(ModelFieldAttribute));
                if (fieldAttribute != null)
                {
                    var val = json[property.Name];
                    if (val is JsonPrimitive)
                    {
                        if ((fieldAttribute.RegexMatch != null) && !Regex.IsMatch(val.Value.ToString(), fieldAttribute.RegexMatch))
                            throw new WebException(400, $"Invalid field '{property.Name}' value '{val.Value}'");

                        var nullableType = Nullable.GetUnderlyingType(property.PropertyType);
                        if (nullableType == null)
                        {
                            if (property.PropertyType.IsEnum && val.JsonType == JsonType.String)
                                Fields[property.Name] = Enum.Parse(property.PropertyType, (string)val.Value);
                            else
                                Fields[property.Name] = Convert.ChangeType(val.Value, property.PropertyType);
                        }
                        else
                        {
                            if (nullableType.IsEnum && val.JsonType == JsonType.String)
                                Fields[property.Name] = Enum.Parse(nullableType, (string)val.Value);
                            else
                                Fields[property.Name] = Convert.ChangeType(val.Value, nullableType);
                        }
                    }
                    else if (val == null)
                    {
                        Fields[property.Name] = null;
                    }
                }
                var expandFieldAttribute = (ModelExpandFieldAttribute)property.GetCustomAttribute(typeof(ModelExpandFieldAttribute));
                if (expandFieldAttribute != null)
                {
                    var jsonArray = json[property.Name] as JsonArray;
                    if (jsonArray != null)
                    {
                        var modelListType = (typeof(ModelList<>)).MakeGenericType(expandFieldAttribute.ForeignModel);
                        var list = (IModelList)Activator.CreateInstance(modelListType);
                        Fields[property.Name] = list;
                        foreach (var jsonItem in jsonArray)
                        {
                            if (jsonItem.JsonType == JsonType.Object)
                            {
                                var item = (Model)Activator.CreateInstance(expandFieldAttribute.ForeignModel);
                                item.FromJson((JsonObject)jsonItem, null, context);
                                list.Add(item);
                            }
                        }
                    }
                    else
                    {
                        var jsonObject = json[property.Name] as JsonObject;
                        // Handle ModelListDiff
                        if (jsonObject != null && jsonObject.ContainsKey("diff"))
                        {
                            var diff = jsonObject["diff"] as JsonObject;

                            if (diff != null)
                            {
                                var modelListType = (typeof(ModelList<>)).MakeGenericType(expandFieldAttribute.ForeignModel);
                                var list = (IModelList)Activator.CreateInstance(modelListType);
                                Fields[property.Name] = list;

                                var modelListDiffType = (typeof(ModelListDiff<>)).MakeGenericType(expandFieldAttribute.ForeignModel);
                                var listDiff = (IModelListDiff)Activator.CreateInstance(modelListDiffType);

                                modelListType.GetField("diff").SetValue(list, listDiff);

                                if (diff.ContainsKey("add") && diff["add"] is JsonArray)
                                {
                                    var listAdd = (IModelList)Activator.CreateInstance(modelListType);
                                    (listDiff as Model).Fields["add"] = listAdd;
                                    var jsonAddArray = diff["add"] as JsonArray;
                                    foreach (var jsonItem in jsonAddArray)
                                    {
                                        if (jsonItem.JsonType == JsonType.Object)
                                        {
                                            var item = (Model)Activator.CreateInstance(expandFieldAttribute.ForeignModel);
                                            item.FromJson((JsonObject)jsonItem, null, context);
                                            listAdd.Add(item);
                                        }
                                    }
                                }

                                if (diff.ContainsKey("change") && diff["change"] is JsonArray)
                                {
                                    var listChange = (IModelList)Activator.CreateInstance(modelListType);
                                    (listDiff as Model).Fields["change"] = listChange;
                                    var jsonChangeArray = diff["change"] as JsonArray;
                                    foreach (var jsonItem in jsonChangeArray)
                                    {
                                        if (jsonItem.JsonType == JsonType.Object)
                                        {
                                            var item = (Model)Activator.CreateInstance(expandFieldAttribute.ForeignModel);
                                            item.FromJson((JsonObject)jsonItem, null, context);
                                            listChange.Add(item);
                                        }
                                    }

                                }

                                if (diff.ContainsKey("remove") && diff["remove"] is JsonArray)
                                {
                                    var listRemove = (IModelList)Activator.CreateInstance(modelListType);
                                    (listDiff as Model).Fields["remove"] = listRemove;
                                    var jsonRemoveArray = diff["remove"] as JsonArray;
                                    foreach (var jsonItem in jsonRemoveArray)
                                    {
                                        if (jsonItem.JsonType == JsonType.Object)
                                        {
                                            var item = (Model)Activator.CreateInstance(expandFieldAttribute.ForeignModel);
                                            item.FromJson((JsonObject)jsonItem, null, context);
                                            listRemove.Add(item);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        public virtual JsonObject ToJson()
        {
            var result = new JsonObject();
            foreach (var key in Fields.Keys)
            {
                var value = Fields[key];
                if (value is string)
                    result[key] = (string)value;
                else if (value is int)
                    result[key] = (int)value;
                else if (value is int?)
                    result[key] = (int?)value;
                else if (value is uint)
                    result[key] = (uint)value;
                else if (value is uint?)
                    result[key] = (uint?)value;
                else if (value is long)
                    result[key] = (long)value;
                else if (value is long?)
                    result[key] = (long?)value;
                else if (value is ulong)
                    result[key] = (ulong)value;
                else if (value is ulong?)
                    result[key] = (ulong?)value;
                else if (value is float)
                    result[key] = (float)value;
                else if (value is float?)
                    result[key] = (float?)value;
                else if (value is double)
                    result[key] = (double)value;
                else if (value is double?)
                    result[key] = (double?)value;
                else if (value is bool)
                    result[key] = (bool)value;
                else if (value is bool?)
                    result[key] = (bool?)value;
                else if (value is DateTime)
                    result[key] = (DateTime)value;
                else if (value is DateTime?)
                    result[key] = (DateTime?)value;
                else if (value is TimeSpan)
                    result[key] = ((TimeSpan)value).TotalSeconds;
                else if (value is Enum)
                    result[key] = ((Enum)value).ToString();
                else if (value is Model)
                    result[key] = ((Model)value).ToJson();
                else if (value is IModelList)
                    result[key] = ((IModelList)value).ToJson();
                if (value as object == null)
                    result[key] = null;
            }
            return result;
        }

        public static implicit operator JsonObject(Model model)
        {
            return model.ToJson();
        }

        public static implicit operator HttpContent(Model model)
        {
            return new ModelContent(model);
        }

        public async virtual Task<bool> LoadAsync(DB db, bool expand = false, SqlFilter sqlFilter = new SqlFilter())
        {
            var attrs = GetType().GetCustomAttributes(typeof(ModelAttribute), true);
            string primaryKey = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";
            return await db.LoadRowAsync(GetType(), Fields[primaryKey], expand, this, sqlFilter);
        }

        internal static PropertyInfo FindForeignProperty(Type sourceModel, Type foreignModel, string foreignField)
        {
            PropertyInfo[] properties;
            if (foreignField != null)
            {
                var prop = foreignModel.GetProperty(foreignField);
                if (prop != null)
                    properties = new PropertyInfo[] { prop };
                else
                    properties = new PropertyInfo[] { };
            }
            else
                properties = foreignModel.GetProperties();
            PropertyInfo foreignfieldProperty = null;
            foreach (var property in properties)
            {
                var fieldAttr = property.GetCustomAttributes(typeof(ModelFieldAttribute), true);
                if (fieldAttr.Length > 0)
                {
                    var attr = (ModelFieldAttribute)fieldAttr[0];
                    if (attr.ForeignModel == sourceModel)
                    {
                        foreignfieldProperty = property;
                        break;
                    }
                }
            }
            return foreignfieldProperty;
        }

        public static IEnumerable<string> GetSearchAllowedFieldsFromModel(Type model, bool expand = true)
        {
            var searchAllowedFields = new List<string>();
            var properties = model.GetProperties();
            foreach (var prop in properties)
            {
                var propAttrs = prop.GetCustomAttributes(typeof(ModelFieldAttribute), true);
                foreach (var a in propAttrs)
                {
                    var propAttr = (ModelFieldAttribute)a;
                    if (propAttr.Search)
                    {
                        searchAllowedFields.Add(prop.Name);
                        break;
                    }
                }
                if (expand)
                {
                    var expandPropAttrs = prop.GetCustomAttributes(typeof(ModelExpandFieldAttribute), true);
                    foreach (var a in expandPropAttrs)
                    {
                        var propAttr = (ModelExpandFieldAttribute)a;
                        if (propAttr.Search)
                        {
                            // allow search on empty join
                            searchAllowedFields.Add(propAttr.Name);
                            var fields = GetSearchAllowedFieldsFromModel(propAttr.ForeignModel, false);
                            // allow seach on foreign fields
                            foreach (var field in fields)
                                searchAllowedFields.Add(propAttr.Name + "." + field);
                            break;
                        }
                    }
                }
            }
            return searchAllowedFields;
        }

        public virtual async Task ApplyAsync(DB db)
        {
            await UpdateAsync(db);
        }

        public async Task<bool> SaveAsync(DB db, bool expand = false)
        {
            var res = await InsertAsync(db);
            if (res)
            {
                var attrs = GetType().GetCustomAttributes(typeof(ModelAttribute), true);
                string primaryKey = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";
                if (!IsSet(primaryKey))
                    Fields[primaryKey] = Convert.ChangeType(await db.LastInsertIdAsync(), GetType().GetProperty(primaryKey).PropertyType);
                // handle expanded fields
                if (expand)
                {
                    foreach (var property in GetType().GetProperties())
                    {
                        if (!Fields.ContainsKey(property.Name))
                            continue;
                        var attr = (ModelExpandFieldAttribute)property.GetCustomAttribute(typeof(ModelExpandFieldAttribute));
                        if (attr != null)
                        {
                            var list = (IModelList)Fields[property.Name];
                            var foreignProp = FindForeignProperty(GetType(), attr.ForeignModel, attr.ForeignField);
                            foreach (var item in list)
                            {
                                var model = (Model)item;
                                model.Fields[foreignProp.Name] = Fields[primaryKey];
                                await ((Model)item).SaveAsync(db, expand);
                            }
                        }
                    }
                }
                await LoadAsync(db, expand);
            }
            return res;
        }

        public virtual async Task<bool> InsertAsync(DB db)
        {
            var attrs = GetType().GetCustomAttributes(typeof(ModelAttribute), true);
            string tableName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).Table : GetType().Name;

            var filterFields = new List<string>();
            // check if all required fields are present
            foreach (var property in GetType().GetProperties())
            {
                var fieldAttribute = (ModelFieldAttribute)property.GetCustomAttribute(typeof(ModelFieldAttribute));
                if (fieldAttribute != null && fieldAttribute.DB)
                {
                    if ((fieldAttribute.Required) && !Fields.ContainsKey(property.Name))
                        throw new WebException(400, $"Missing required field {property.Name}");
                    filterFields.Add(property.Name);
                }
            }
            return (await db.InsertRowAsync(tableName, Fields, filterFields)) == 1;
        }

        public virtual async Task<bool> UpdateAsync(DB db)
        {
            var filterFields = new List<string>();
            // check if all required fields are present
            foreach (var property in GetType().GetProperties())
            {
                var fieldAttribute = (ModelFieldAttribute)property.GetCustomAttribute(typeof(ModelFieldAttribute));
                if (fieldAttribute != null && fieldAttribute.DB)
                    filterFields.Add(property.Name);
            }
            var attrs = GetType().GetCustomAttributes(typeof(ModelAttribute), true);
            string tableName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).Table : GetType().Name;
            string primaryKey = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";
            var done = (await db.UpdateRowAsync(tableName, primaryKey, Fields[primaryKey], Fields, filterFields)) == 1;
            // check if all required fields are present
            foreach (var property in GetType().GetProperties())
            {
                var expandFieldAttribute = (ModelExpandFieldAttribute)property.GetCustomAttribute(typeof(ModelExpandFieldAttribute));
                if (expandFieldAttribute != null)
                {
                    if (Fields.ContainsKey(property.Name))
                    {
                        var list = Fields[property.Name] as IModelList;
                        if (list != null)
                        {
                            var foreignProp = FindForeignProperty(GetType(), expandFieldAttribute.ForeignModel, expandFieldAttribute.ForeignField);
                            if ((list.diff != null) && (list.diff.add != null))
                            {
                                foreach (Model item in list.diff.add)
                                    item.Fields[foreignProp.Name] = Fields[primaryKey];
                            }
                            await list.ApplyAsync(db);
                        }
                    }
                }
            }
            return done;
        }

        public virtual async Task<bool> DeleteAsync(DB db)
        {
            var attrs = GetType().GetCustomAttributes(typeof(ModelAttribute), true);
            string tableName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).Table : GetType().Name;
            string primaryKey = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";
            var res = (await db.DeleteAsync($"DELETE FROM `{tableName}` WHERE `{primaryKey}`=?", Fields[primaryKey])) == 1;
            return res;
        }

        public bool Delete(DB db)
        {
            var task = DeleteAsync(db);
            task.Wait();
            return task.Result;
        }

        public static async Task<SearchResult<T>> SearchAsync<T>(DB db, HttpContext c, SqlFilter sqlFilter = new SqlFilter()) where T : Model, new()
        {
            Dictionary<string, List<string>> parsedQuery;
            string[] orderBy;
            SortDirection[] orderDir;
            bool expand;
            int offset;
            int count;
            ParseSearch<T>(c, out parsedQuery, out orderBy, out orderDir, out expand, out offset, out count);
            return await SearchAsync<T>(db, parsedQuery, orderBy, orderDir, expand, offset, count, sqlFilter);
        }

        public static void ParseSearch<T>(
            HttpContext c,
            out Dictionary<string, List<string>> parsedQuery, out string[] orderBy,
            out SortDirection[] orderDir, out bool expand, out int offset, out int count) where T : Model
        {
            expand = true;
            offset = 0;
            count = -1;
            orderBy = null;
            orderDir = null;
            var query = "";
            if (c.Request.QueryString.ContainsKey("query"))
                query = c.Request.QueryString["query"];
            if (c.Request.QueryString.ContainsKey("limit"))
            {
                count = int.Parse(c.Request.QueryString["limit"]);
                if (c.Request.QueryString.ContainsKey("page"))
                    offset = Math.Max(0, (int.Parse(c.Request.QueryString["page"]) - 1) * count);
            }
            if (c.Request.QueryString.ContainsKey("sort_col"))
                orderBy = new string[] { c.Request.QueryString["sort_col"] };
            if (c.Request.QueryStringArray.ContainsKey("sort_col"))
                orderBy = c.Request.QueryStringArray["sort_col"].ToArray();
            if (c.Request.QueryString.ContainsKey("sort_dir") && (c.Request.QueryString["sort_dir"] == "desc"))
                orderDir = new SortDirection[] { SortDirection.Descending };
            if (c.Request.QueryStringArray.ContainsKey("sort_dir"))
            {
                orderDir = new SortDirection[c.Request.QueryStringArray["sort_dir"].Count];
                for (var i = 0; i < c.Request.QueryStringArray["sort_dir"].Count; i++)
                {
                    if (c.Request.QueryStringArray["sort_dir"][i] == "desc")
                        orderDir[i] = SortDirection.Descending;
                    else
                        orderDir[i] = SortDirection.Ascending;
                }
            }
            if (c.Request.QueryString.ContainsKey("expand"))
                expand = Convert.ToBoolean(c.Request.QueryString["expand"]);

            var keywords = new string[] { "expand", "sort_dir", "sort_col", "page", "limit", "query" };
            parsedQuery = query.QueryParser();
            foreach (var key in c.Request.QueryString.Keys)
            {
                if (keywords.Contains(key))
                    continue;
                if (!parsedQuery.ContainsKey(key))
                    parsedQuery[key] = new List<string> { c.Request.QueryString[key] };
            }
            foreach (var key in c.Request.QueryStringArray.Keys)
            {
                if (keywords.Contains(key))
                    continue;
                if (!parsedQuery.ContainsKey(key))
                    parsedQuery[key] = c.Request.QueryStringArray[key];
            }
        }

        static string CompareOperatorToSql(CompareOperator op)
        {
            string opStr = "=";
            if (op == CompareOperator.NotEqual)
                opStr = "!=";
            else if (op == CompareOperator.Less)
                opStr = "<";
            else if (op == CompareOperator.LessOrEqual)
                opStr = "<=";
            else if (op == CompareOperator.Greater)
                opStr = ">";
            else if (op == CompareOperator.GreaterOrEqual)
                opStr = ">=";
            return opStr;
        }

        public static string SearchToSql<T>(
            Dictionary<string, List<string>> queryFields, string[] orderBy = null,
            SortDirection[] sortDir = null, int offset = 0, int count = -1, SqlFilter sqlFilter = new SqlFilter()) where T : Model, new()
        {
            var attrs = typeof(T).GetCustomAttributes(typeof(ModelAttribute), true);
            string modelTableName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).Table : typeof(T).Name;
            string primaryKey = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";

            if (orderBy == null)
                orderBy = new string[] { primaryKey };
            if (sortDir == null)
                sortDir = new SortDirection[] { SortDirection.Ascending };

            var result = new SearchResult<T>();
            string filter = "";
            var tables = new Dictionary<string, Dictionary<string, List<string>>>();
            foreach (string keyOp in queryFields.Keys)
            {
                string key = keyOp;
                var op = CompareOperator.Equal;
                if (keyOp.EndsWith("!", StringComparison.InvariantCulture))
                {
                    op = CompareOperator.NotEqual;
                    key = keyOp.Substring(0, keyOp.Length - 1);
                }
                else if (keyOp.EndsWith("<", StringComparison.InvariantCulture))
                {
                    op = CompareOperator.Less;
                    key = keyOp.Substring(0, keyOp.Length - 1);
                }
                else if (keyOp.EndsWith("<=", StringComparison.InvariantCulture))
                {
                    op = CompareOperator.LessOrEqual;
                    key = keyOp.Substring(0, keyOp.Length - 2);
                }
                else if (keyOp.EndsWith(">", StringComparison.InvariantCulture))
                {
                    op = CompareOperator.Greater;
                    key = keyOp.Substring(0, keyOp.Length - 1);
                }
                else if (keyOp.EndsWith(">=", StringComparison.InvariantCulture))
                {
                    op = CompareOperator.GreaterOrEqual;
                    key = keyOp.Substring(0, keyOp.Length - 2);
                }

                if (key.IndexOf('.') > 0)
                {
                    var pos = key.IndexOf('.');
                    var tableName = key.Substring(0, pos);
                    var fieldName = key.Substring(pos + 1);
                    Dictionary<string, List<string>> table;
                    if (!tables.ContainsKey(tableName))
                    {
                        table = new Dictionary<string, List<string>>();
                        tables[tableName] = table;
                    }
                    else
                        table = tables[tableName];
                    table[fieldName] = queryFields[key];
                }
                else
                {
                    var keyProperty = typeof(T).GetProperty(key);
                    if (keyProperty == null)
                        continue;

                    var expandFieldAttr = (ModelExpandFieldAttribute)keyProperty.GetCustomAttribute(typeof(ModelExpandFieldAttribute));
                    var fieldAttr = (ModelFieldAttribute)keyProperty.GetCustomAttribute(typeof(ModelFieldAttribute));
                    if (expandFieldAttr == null && fieldAttr == null)
                        continue;
                    if (fieldAttr != null && (!fieldAttr.DB || !fieldAttr.Search))
                        continue;
                    if (expandFieldAttr != null && !expandFieldAttr.Search)
                        continue;

                    var words = queryFields[keyOp];
                    if (words.Count == 1)
                    {
                        if (filter != "")
                            filter += " AND ";
                        if (words[0] == "NULL")
                        {
                            // handle empty foreign fields join
                            if (expandFieldAttr != null)
                            {
                                var foreignModelAttr = (ModelAttribute)expandFieldAttr.ForeignModel.GetCustomAttribute(typeof(ModelAttribute));
                                string foreignTableName = foreignModelAttr.Table;

                                PropertyInfo foreignProperty = null;
                                PropertyInfo[] properties;
                                if (expandFieldAttr.ForeignField != null)
                                {
                                    var prop = expandFieldAttr.ForeignModel.GetProperty(expandFieldAttr.ForeignField);
                                    if (prop != null)
                                        properties = new PropertyInfo[] { prop };
                                    else
                                        properties = new PropertyInfo[] { };
                                }
                                else
                                    properties = expandFieldAttr.ForeignModel.GetProperties();
                                foreach (var prop in properties)
                                {
                                    var propAttrs = prop.GetCustomAttributes(typeof(ModelFieldAttribute), true);
                                    foreach (var a in propAttrs)
                                    {
                                        var propAttr = (ModelFieldAttribute)a;
                                        if (propAttr.ForeignModel == typeof(T))
                                        {
                                            foreignProperty = prop;
                                            break;
                                        }
                                    }
                                    if (foreignProperty != null)
                                        break;
                                }
                                filter += $"`{primaryKey}` NOT IN (SELECT DISTINCT(`{foreignProperty.Name}`) FROM `{foreignTableName}`)";
                            }
                            else
                            {
                                if (op == CompareOperator.NotEqual)
                                    filter += "`" + key + "` IS NOT NULL";
                                else
                                    filter += "`" + key + "` IS NULL";
                            }
                        }
                        else
                        {
                            if (keyProperty.PropertyType == typeof(Boolean))
                            {
                                var value = Boolean.Parse(words[0]);
                                filter += $"`{key}`{CompareOperatorToSql(op)} {(value ? "TRUE" : "FALSE")}";
                            }
                            else
                                filter += "`" + key + "`" + CompareOperatorToSql(op) + "'" + DB.EscapeString(words[0]) + "'";
                        }
                    }
                    else if (words.Count > 1)
                    {
                        List<string> inWords = words;
                        bool hasNull = false;
                        if (words.Any((arg) => arg == "NULL"))
                        {
                            hasNull = true;
                            inWords = words.FindAll((obj) => obj != "NULL");
                        }
                        if (filter != "")
                            filter += " AND ";
                        filter += "(";
                        if (inWords.Count > 0)
                        {
                            if (op == CompareOperator.NotEqual)
                                filter += DB.NotInFilter(key, inWords);
                            else
                                filter += DB.InFilter(key, inWords);
                        }
                        if (hasNull)
                        {
                            if (inWords.Count > 0)
                                filter += " OR ";
                            filter += $"`{key}` IS NULL";
                        }
                        filter += ")";
                    }
                }
            }

            if (queryFields.ContainsKey("global"))
            {
                var properties = typeof(T).GetProperties();
                var searchFields = new List<string>();
                foreach (var prop in properties)
                {
                    var attr = (ModelFieldAttribute)prop.GetCustomAttribute(typeof(ModelFieldAttribute));
                    if ((attr != null) && attr.Search && attr.DB)
                        searchFields.Add(prop.Name);
                }

                var words = queryFields["global"];
                foreach (string word in words)
                {
                    if (filter != "")
                        filter += " AND ";
                    filter += "(";
                    var first = true;
                    foreach (var field in searchFields)
                    {
                        if (field.IndexOf('.') > 0)
                            continue;
                        if (first)
                            first = false;
                        else
                            filter += " OR ";
                        filter += "`" + field + "` LIKE '%" + DB.EscapeString(word) + "%'";
                    }
                    filter += ")";
                }
            }
            foreach (string tableName in tables.Keys)
            {
                var property = typeof(T).GetProperty(tableName);
                if (property == null)
                    continue;
                var fieldAttr = property.GetCustomAttributes(typeof(ModelExpandFieldAttribute), true);
                if (fieldAttr.Length == 0)
                    continue;
                var attr = (ModelExpandFieldAttribute)fieldAttr[0];
                if (!attr.Search)
                    continue;
                var foreignAttrs = attr.ForeignModel.GetCustomAttributes(typeof(ModelAttribute), true);
                string foreignTableName = (foreignAttrs.Length > 0) ? ((ModelAttribute)foreignAttrs[0]).Table : attr.ForeignModel.Name;
                PropertyInfo foreignProperty = null;
                ModelFieldAttribute foreignFieldAttr = null;

                PropertyInfo[] properties;
                if (attr.ForeignField != null)
                {
                    var prop = attr.ForeignModel.GetProperty(attr.ForeignField);
                    if (prop != null)
                        properties = new PropertyInfo[] { prop };
                    else
                        properties = new PropertyInfo[] { };
                }
                else
                    properties = attr.ForeignModel.GetProperties();
                foreach (var prop in properties)
                {
                    var propAttrs = prop.GetCustomAttributes(typeof(ModelFieldAttribute), true);
                    foreach (var a in propAttrs)
                    {
                        var propAttr = (ModelFieldAttribute)a;
                        if (propAttr.ForeignModel == property.DeclaringType)
                        {
                            foreignFieldAttr = propAttr;
                            foreignProperty = prop;
                            break;
                        }
                    }
                    if (foreignProperty != null)
                        break;
                }

                if (foreignProperty == null)
                    continue;
                if (!foreignFieldAttr.Search)
                    continue;
                if (filter != "")
                    filter += " AND ";
                filter += $"`{primaryKey}` IN (SELECT `{foreignProperty.Name}` FROM `{foreignTableName}` WHERE ";

                var first = true;
                var itemsTable = tables[tableName];
                foreach (var itemKey in itemsTable.Keys)
                {
                    var words = itemsTable[itemKey];

                    if (words.Count == 1)
                    {
                        if (first)
                            first = false;
                        else
                            filter += " AND ";
                        filter += "`" + itemKey + "`='" + DB.EscapeString(words[0]) + "'";
                    }
                    else if (words.Count > 1)
                    {
                        if (first)
                            first = false;
                        else
                            filter += " AND ";
                        filter += DB.InFilter(itemKey, words);
                    }
                }
                filter += ")";
            }

            if (filter == "")
                filter = "TRUE";
            string limit = "";
            if (count > 0)
                limit = $"LIMIT {count} OFFSET {offset}";

            var sqlInnerFilter = "";
            if (sqlFilter.Inner != null)
                sqlInnerFilter = sqlFilter.Inner;
            var sqlWhereFilter = "";
            if (sqlFilter.Where != null)
                sqlWhereFilter = " AND " + sqlFilter.Where;
            var sql = $"SELECT SQL_CALC_FOUND_ROWS * FROM `{modelTableName}` {sqlInnerFilter} WHERE {filter} {sqlWhereFilter} " +
                $"ORDER BY ";
            for (var i = 0; i < orderBy.Length; i++)
            {
                var key = orderBy[i];
                if (i > 0)
                    sql += ", ";
                var direction = "ASC";
                if (i < sortDir.Length)
                    direction = (sortDir[i] == SortDirection.Ascending) ? "ASC" : "DESC";
                sql += $"`{DB.EscapeString(key)}` {direction}";
            }

            // if the order is not the primary key add a second order criteria to ensure a predictible order
            // else paging might be useless
            if (orderBy.Length != 1 || orderBy[0] != primaryKey)
                sql += $", `{primaryKey}` ASC";
            sql += $" {limit}";
            return sql;
        }

        public static async Task<SearchResult<T>> SearchAsync<T>(
            DB db, Dictionary<string, List<string>> queryFields, string[] orderBy = null,
            SortDirection[] sortDir = null, bool expand = true, int offset = 0, int count = -1, SqlFilter sqlFilter = new SqlFilter()) where T : Model, new()
        {
            var result = new SearchResult<T>();
            var sql = SearchToSql<T>(queryFields, orderBy, sortDir, offset, count, sqlFilter);

            result.Limit = count;
            result.Offset = offset;
            if (expand)
            {
                // get the found rows just after the main query because the value is changed
                // by the others queries done for expanding the data
                result.Data = await db.SelectExpandAsync<T>(sql, new object[] { }, async () => result.Total = (int)await db.FoundRowsAsync());
            }
            else
            {
                result.Data = await db.SelectAsync<T>(sql);
                result.Total = (int)await db.FoundRowsAsync();
            }
            return result;
        }


        public delegate bool MatchFunc<T>(T src, T dst);
        public delegate T DiffFunc<T>(T src, T dst);

        public static async Task<ModelListDiff<T>> SyncAsync<T>(DB db, IEnumerable<T> srcItems, IEnumerable<T> dstItems, MatchFunc<T> matchFunc = null) where T : Model, new()
        {
            var result = Diff(srcItems, dstItems, matchFunc);
            await result.ApplyAsync(db);
            return result;
        }

        public static ModelListDiff<T> Diff<T>(IEnumerable<T> srcItems, IEnumerable<T> dstItems, MatchFunc<T> matchFunc = null, DiffFunc<T> diffFunc = null) where T : Model, new()
        {
            var result = new ModelListDiff<T>();
            result.add = new ModelList<T>();
            result.change = new ModelList<T>();
            result.remove = new ModelList<T>();

            //bool testChange = true;
            if (matchFunc == null)
            {
                //testChange = false;
                matchFunc = (src, dst) => dst.EqualsIntersection(src);
            }

            if (diffFunc == null)
                diffFunc = (src, dst) => src.DiffWithId(dst);

            // delete before in case of DB constraint
            foreach (var srcItem in srcItems)
            {
                if (!dstItems.Any(dstItem => matchFunc(srcItem, dstItem)))
                    result.remove.Add(srcItem);
            }

            foreach (var dstItem in dstItems)
            {
                var foundItem = srcItems.FirstOrDefault(srcItem => matchFunc(srcItem, dstItem));
                if (foundItem == null)
                    result.add.Add(dstItem);
                else
                {
                    var itemDiff = diffFunc(foundItem, dstItem);
                    if (itemDiff.Fields.Count > 1)
                        result.change.Add(itemDiff);
                    //if (testChange && !dstItem.EqualsIntersection(foundItem))
                    //	result.change.Add(diffFunc);
                }
            }

            return result;
        }

        public async Task LoadExpandFieldAsync(DB db, string fieldName)
        {
            if (!IsSet(fieldName))
            {
                var attrs = GetType().GetCustomAttributes(typeof(ModelAttribute), true);
                string primaryKey = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";
                Fields[fieldName] = await LoadExpandFieldAsync(GetType(), db, fieldName, Fields[primaryKey]);
            }
        }

        internal static Task<object> LoadExpandFieldAsync<T>(DB db, string fieldName, object id) where T : Model
        {
            return LoadExpandFieldAsync(typeof(T), db, fieldName, id);
        }

        internal static async Task<object> LoadExpandFieldAsync(Type model, DB db, string fieldName, object id)
        {
            var property = model.GetProperty(fieldName);
            var attr = (ModelExpandFieldAttribute)property.GetCustomAttribute(typeof(ModelExpandFieldAttribute));
            var task = typeof(DB).GetMethod(nameof(DB.SelectForeignRowsAsync)).MakeGenericMethod(attr.ForeignModel).Invoke(db, new object[] { model, attr.ForeignField, id }) as Task;
            await task;
            var resultProperty = typeof(Task<>).MakeGenericType(property.PropertyType).GetProperty("Result");
            return resultProperty.GetValue(task);
        }

        public virtual async Task EnsureRightAsync(HttpContext context, Right right, Model diff)
        {
            await Task.FromResult(false);
        }

        public virtual SqlFilter FilterAuthUser(AuthenticatedUser user)
        {
            return new SqlFilter() { Where = null, Inner = null };
        }
    }

    public interface IModelList : IList
    {
        IModelListDiff diff { get; }

        JsonValue ToJson();

        Task ApplyAsync(DB db);
    }

    public class ModelList<T> : List<T>, IModelList where T : Model
    {
        public ModelListDiff<T> diff;

        public ModelList()
        {
        }

        public ModelList(IEnumerable<T> values)
        {
            AddRange(values);
        }

        IModelListDiff IModelList.diff
        {
            get
            {
                return diff;
            }
        }

        public JsonValue ToJson()
        {
            if (diff != null)
                return diff.ToJson();

            var json = new JsonArray();
            foreach (var item in this)
                json.Add(item.ToJson());
            return json;
        }

        public static implicit operator JsonValue(ModelList<T> list)
        {
            return list.ToJson();
        }

        public static implicit operator HttpContent(ModelList<T> list)
        {
            return new JsonContent(list.ToJson());
        }

        public ModelList<T> Filter(HttpContext c)
        {
            return Filter(c.ToFilter());
        }

        public ModelList<T> Filter(Dictionary<string, List<string>> filter)
        {
            var res = new ModelList<T>();
            foreach (var item in this)
                if (item.IsFilterMatch(filter))
                    res.Add(item);
            return res;
        }

        public SearchResult<T> Search(HttpContext c)
        {
            Dictionary<string, List<string>> parsedQuery;
            string[] orderBy;
            SortDirection[] orderDir;
            bool expand;
            int offset;
            int count;
            Model.ParseSearch<T>(c, out parsedQuery, out orderBy, out orderDir,
                out expand, out offset, out count);
            var result = new SearchResult<T>();
            var data = Filter(parsedQuery);
            data.Sort(orderBy, orderDir);
            var dataLength = data.Count();
            result.Limit = count;
            result.Offset = offset;
            result.Total = dataLength;
            if (count != -1)
                data = new ModelList<T>(data.GetRange(Math.Min(offset, dataLength), Math.Max(0, Math.Min(dataLength-offset, count))));
            result.Data = data;
            return result;
        }

        public async Task ApplyAsync(DB db)
        {
            if (diff != null)
                await diff.ApplyAsync(db);
        }

        public void Sort(string[] orderBy, SortDirection[] orderDir)
        {
            if (orderBy == null)
                return;
            Func<object, object, int> cmp = (object a, object b) =>
            {
                if (a == null)
                    return 0;
                if (a is IComparable)
                    return ((IComparable)a).CompareTo(b);
                return 0;
            };
            Sort((T x, T y) =>
            {
                var res = 0;
                for (var i = 0; i < orderBy.Length && res == 0; i++)
                {
                    if (x.IsSet(orderBy[i]) && y.IsSet(orderBy[i]))
                    {
                        res = cmp(x.Fields[orderBy[i]], y.Fields[orderBy[i]]);
                        if (orderDir != null && i < orderDir.Length)
                            res = orderDir[i] == SortDirection.Ascending ? res : -res;
                    }
                }
                return res;
            });             
        }
    }

    public interface IModelListDiff
    {
        IModelList add { get; }
        IModelList change { get; }
        IModelList remove { get; }
    }

    public class ModelListDiff<T> : Model, IModelListDiff where T : Model
    {
        [ModelField]
        public ModelList<T> add { get { return GetField<ModelList<T>>("add", null); } set { SetField("add", value); } }
        [ModelField]
        public ModelList<T> change { get { return GetField<ModelList<T>>("change", null); } set { SetField("change", value); } }
        [ModelField]
        public ModelList<T> remove { get { return GetField<ModelList<T>>("remove", null); } set { SetField("remove", value); } }

        IModelList IModelListDiff.add
        {
            get
            {
                return add;
            }
        }

        IModelList IModelListDiff.change
        {
            get
            {
                return change;
            }
        }

        IModelList IModelListDiff.remove
        {
            get
            {
                return remove;
            }
        }

        public override bool IsEmpty
        {
            get
            {
                return !add.Any() && !change.Any() && !remove.Any();
            }
        }

        public override async Task ApplyAsync(DB db)
        {
            if (remove != null)
            {
                foreach (var item in remove)
                    await item.DeleteAsync(db);
            }
            if (change != null)
            {
                foreach (var item in change)
                    await item.ApplyAsync(db);
            }
            if (add != null)
            {
                foreach (var item in add)
                    await item.SaveAsync(db, true);
            }
        }
    }

    public class ModelContent : StreamContent
    {
        public ModelContent(Model model) : base(new MemoryStream())
        {
            Headers.ContentType = "application/json; charset=\"UTF-8\"";

            var bytes = Encoding.UTF8.GetBytes(model.ToJson().ToString());
            Stream.Write(bytes, 0, bytes.Length);
            Stream.Seek(0, SeekOrigin.Begin);
        }

        public static implicit operator ModelContent(Model value)
        {
            return new ModelContent(value);
        }
    }

    public class DB : IDisposable
    {
        internal readonly MySqlConnection connection;
        internal MySqlTransaction transaction;
        Queue<Action> commitActions = null;

        DB(string connectionUrl)
        {
            connection = new MySqlConnection(connectionUrl);
        }

        public void AddCommitAction(Action action)
        {
            if (commitActions == null)
                commitActions = new Queue<Action>();
            commitActions.Enqueue(action);
        }

        public static string EscapeString(string value)
        {
            return MySqlHelper.EscapeString(value);
        }

        public static async Task<DB> CreateAsync(string connectionUrl, bool startTransaction = false)
        {
            var db = new DB(connectionUrl);
            await db.connection.OpenAsync();
            if (startTransaction)
                db.transaction = await db.connection.BeginTransactionAsync();
            return db;
        }

        public static DB Create(string connectionUrl, bool startTransaction = false)
        {
            var db = new DB(connectionUrl);
            db.connection.Open();
            if (startTransaction)
                db.transaction = db.connection.BeginTransaction();
            return db;
        }

        public IList<Dictionary<string, object>> Select(string query, params object[] args)
        {
            var res = SelectAsync(query, args);
            res.Wait();
            return res.Result;
        }

        public async Task<IList<Dictionary<string, object>>> SelectAsync(string query, params object[] args)
        {
            var result = new List<Dictionary<string, object>>();
            var cmd = new MySqlCommand(query, connection);
            if (transaction != null)
                cmd.Transaction = transaction;
            args.ForEach(arg => cmd.Parameters.Add(new MySqlParameter(string.Empty, arg)));
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var item = new Dictionary<string, object>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        item[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                    }
                    result.Add(item);
                }
            }
            return result;
        }

        public class PropertyDetail
        {
            public string Name;
            public PropertyInfo Property;
            public ModelFieldAttribute Attribute;
            public Type PropertyType;
            public bool IsNullable;
            public bool IsEnum;
            public bool IsDirectMapping;
        }
        public static Dictionary<Type, Dictionary<string, PropertyDetail>> DBToTypeInfo = new Dictionary<Type, Dictionary<string, PropertyDetail>>();

        public static void DBToFields<T>(PropertyDetail[] propertiesDetails, object[] values, T result) where T : Model
        {
            for (int i = 0; i < propertiesDetails.Length; i++)
            {
                PropertyDetail detail = propertiesDetails[i];
                if (detail == null)
                    continue;
                var name = detail.Name;
                object value = values[i];

                if (value == DBNull.Value)
                    result.Fields[name] = null;
                else if (value.GetType() == detail.PropertyType)
                    result.Fields[name] = value;
                else if (detail.IsEnum)
                    result.Fields[name] = Enum.Parse(detail.PropertyType, (string)value);
                else
                    result.Fields[name] = Convert.ChangeType(value, detail.PropertyType);
            }
        }

        public IEnumerableAsync<T> SelectEnumerable<T>(string query, params object[] args) where T : Model, new()
        {
            return new RequestEnumerable<T>(this, query, args);
        }

        public async Task<ModelList<T>> SelectAsync<T>(string query, params object[] args) where T : Model, new()
        {
            var result = new ModelList<T>();

            var cmd = new MySqlCommand(query, connection);
            if (transaction != null)
                cmd.Transaction = transaction;
            args.ForEach(arg => cmd.Parameters.Add(new MySqlParameter(string.Empty, arg)));

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                string[] names = new string[reader.FieldCount];
                object[] values = new object[reader.FieldCount];

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    names[i] = reader.GetName(i);
                }

                PropertyDetail[] propertiesDetails = new PropertyDetail[reader.FieldCount];
                var details = DBToTypeInfo[typeof(T)];
                for (int i = 0; i < names.Length; i++)
                {
                    var name = names[i];
                    if (!details.ContainsKey(name))
                        continue;
                    propertiesDetails[i] = details[name];
                }

                while (await reader.ReadAsync())
                {
                    var item = new T();
                    reader.GetValues(values);
                    DBToFields(propertiesDetails, values, item);
                    result.Add(item);
                }
            }
            return result;
        }

        public delegate Task SimpleActionAsync();

        public async Task<ModelList<T>> SelectExpandAsync<T>(string query, object[] args, SimpleActionAsync beforeExpand = null) where T : Model, new()
        {
            var attrs = typeof(T).GetCustomAttributes(typeof(ModelAttribute), true);
            string primaryKey = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";

            var result = new ModelList<T>();
            var cmd = new MySqlCommand(query, connection);
            if (transaction != null)
                cmd.Transaction = transaction;
            var ids = new List<object>();
            args.ForEach(arg => cmd.Parameters.Add(new MySqlParameter(string.Empty, arg)));
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                string[] names = new string[reader.FieldCount];
                object[] values = new object[reader.FieldCount];

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    names[i] = reader.GetName(i);
                }

                PropertyDetail[] propertiesDetails = new PropertyDetail[reader.FieldCount];
                var details = DBToTypeInfo[typeof(T)];
                for (int i = 0; i < names.Length; i++)
                {
                    var name = names[i];
                    if (!details.ContainsKey(name))
                        continue;
                    propertiesDetails[i] = details[name];
                }

                while (await reader.ReadAsync())
                {
                    var item = new T();
                    reader.GetValues(values);
                    DBToFields(propertiesDetails, values, item);
                    if (item.Fields.ContainsKey(primaryKey))
                        ids.Add(item.Fields[primaryKey]);
                    result.Add(item);
                }
            }
            if (beforeExpand != null)
                await beforeExpand();
            if (ids.Any())
            {
                var properties = typeof(T).GetProperties();
                foreach (var property in properties)
                {
                    var fieldAttr = property.GetCustomAttributes(typeof(ModelExpandFieldAttribute), true);
                    if (fieldAttr.Length > 0)
                    {
                        var attr = (ModelExpandFieldAttribute)fieldAttr[0];
                        if (attr.Visible)
                        {
                            if (property.PropertyType.IsSubclassOf(typeof(Model)))
                            {
                                var foreignFieldProperty = Model.FindForeignProperty(attr.ForeignModel, typeof(T), attr.ForeignField);
                                if (foreignFieldProperty != null)
                                {
                                    List<object> fieldIds = new List<object>();
                                    result.ForEach((item) =>
                                    {
                                        if (item.Fields.ContainsKey(foreignFieldProperty.Name) && item.Fields[foreignFieldProperty.Name] != null)
                                            fieldIds.Add(item.Fields[foreignFieldProperty.Name]);
                                    });
                                    var task = GetType().GetMethod(nameof(SelectLocalsRowsAsync)).MakeGenericMethod(attr.ForeignModel).Invoke(this, new object[] { fieldIds }) as Task;
                                    await task;

                                    var resultProperty = typeof(Task<>).MakeGenericType(typeof(Dictionary<,>).MakeGenericType(typeof(object), attr.ForeignModel)).GetProperty("Result");
                                    var items = resultProperty.GetValue(task) as IDictionary;

                                    result.ForEach((item) =>
                                    {
                                        if (item.Fields.ContainsKey(foreignFieldProperty.Name))
                                        {
                                            var key = item.Fields[foreignFieldProperty.Name];
                                            item.Fields[property.Name] = key != null && items.Contains(key) ? items[key] : null;
                                        }
                                    });
                                }
                            }
                            else
                            {
                                // because the ForeignModel type is not statically known, use reflexion
                                // get a base Task result for the same reason
                                var task = GetType().GetMethod(nameof(SelectForeignsRowsAsync)).MakeGenericMethod(attr.ForeignModel).Invoke(this, new object[] { property.DeclaringType, attr.ForeignField, ids }) as Task;
                                await task;

                                var modelListType = (typeof(ModelList<>)).MakeGenericType(attr.ForeignModel);
                                var dictModelType = (typeof(Dictionary<,>)).MakeGenericType(typeof(object), modelListType);
                                var resultProperty = typeof(Task<>).MakeGenericType(dictModelType).GetProperty("Result");
                                var foreignsRows = resultProperty.GetValue(task) as IDictionary;

                                foreach (var item in result)
                                {
                                    var itemKey = item.Fields[primaryKey];
                                    if (foreignsRows.Contains(itemKey))
                                        item.Fields[property.Name] = foreignsRows[itemKey];
                                    else
                                        item.Fields[property.Name] = Activator.CreateInstance(modelListType);
                                }
                            }
                        }
                    }
                }
            }
            return result;
        }

        public async Task<T> SelectRowAsync<T>(object idValue, bool expand = false, SqlFilter sqlFilter = new SqlFilter()) where T : Model, new()
        {
            T result = new T();
            return await LoadRowAsync(typeof(T), idValue, expand, result, sqlFilter) ? result : null;
        }

        internal async Task<bool> LoadRowAsync(Type modelType, object idValue, bool expand, Model result, SqlFilter sqlFilter = new SqlFilter())
        {
            bool found = false;
            var attrs = modelType.GetCustomAttributes(typeof(ModelAttribute), true);
            string tableName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).Table : modelType.Name;
            string primaryKey = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";

            var sqlInnerFilter = "";
            if (sqlFilter.Inner != null)
                sqlInnerFilter = sqlFilter.Inner;
            var sqlWhereFilter = "";
            if (sqlFilter.Where != null)
                sqlWhereFilter = " AND " + sqlFilter.Where;

            var cmd = new MySqlCommand($"SELECT * FROM `{tableName}` {sqlInnerFilter} WHERE `{primaryKey}`=? {sqlWhereFilter}", connection);
            if (transaction != null)
                cmd.Transaction = transaction;
            cmd.Parameters.Add(new MySqlParameter(string.Empty, idValue));
            object id = null;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                string[] names = new string[reader.FieldCount];
                object[] values = new object[reader.FieldCount];

                for (var i = 0; i < reader.FieldCount; i++)
                {
                    names[i] = reader.GetName(i);
                }

                PropertyDetail[] propertiesDetails = new PropertyDetail[reader.FieldCount];
                var details = DBToTypeInfo[modelType];
                for (int i = 0; i < names.Length; i++)
                {
                    var name = names[i];
                    if (!details.ContainsKey(name))
                        continue;
                    propertiesDetails[i] = details[name];
                }

                while (await reader.ReadAsync() && !found)
                {
                    reader.GetValues(values);
                    found = true;
                    DBToFields(propertiesDetails, values, result);
                    if (expand && result.Fields.ContainsKey(primaryKey))
                        id = result.Fields[primaryKey];
                }
            }
            if (expand && (id != null))
            {
                var properties = modelType.GetProperties();
                foreach (var property in properties)
                {
                    var fieldAttr = property.GetCustomAttributes(typeof(ModelExpandFieldAttribute), false);
                    if (fieldAttr.Length > 0)
                    {
                        var attr = (ModelExpandFieldAttribute)fieldAttr[0];
                        if (attr.Visible)
                        {
                            if (property.PropertyType.IsSubclassOf(typeof(Model)))
                            {
                                var foreignFieldProperty = Model.FindForeignProperty(attr.ForeignModel, modelType, attr.ForeignField);
                                if (foreignFieldProperty != null && result.Fields.ContainsKey(foreignFieldProperty.Name))
                                {
                                    var task = GetType().GetMethod(nameof(SelectRowAsync)).MakeGenericMethod(attr.ForeignModel).Invoke(this, new object[] { result.Fields[foreignFieldProperty.Name], false, new SqlFilter() }) as Task;
                                    await task;
                                    var resultProperty = typeof(Task<>).MakeGenericType(attr.ForeignModel).GetProperty("Result");
                                    result.Fields[attr.Name] = resultProperty.GetValue(task);
                                }
                            }
                            else
                            {
                                var task = GetType().GetMethod(nameof(SelectForeignRowsAsync)).MakeGenericMethod(attr.ForeignModel).Invoke(this, new object[] { modelType, attr.ForeignField, id }) as Task;
                                await task;
                                var resultProperty = typeof(Task<>).MakeGenericType((typeof(ModelList<>)).MakeGenericType(attr.ForeignModel)).GetProperty("Result");
                                result.Fields[attr.Name] = resultProperty.GetValue(task);
                            }
                        }
                    }
                }
            }
            return found;
        }

        public async Task<ModelList<T>> SelectForeignRowsAsync<T>(Type sourceModel, string foreignField, object sourceId) where T : Model, new()
        {
            ModelList<T> result;

            Type foreignModel = typeof(T);
            var attrs = foreignModel.GetCustomAttributes(typeof(ModelAttribute), true);
            string destTableName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).Table : foreignModel.Name;

            var foreignFieldProperty = Model.FindForeignProperty(sourceModel, foreignModel, foreignField);

            // if found, generate the needed SQL
            if (foreignFieldProperty != null)
            {
                var sql = $"SELECT * FROM `{destTableName}` WHERE `{foreignFieldProperty.Name}`=?";
                //result = await SelectExpandAsync<T>(sql, new object[] { sourceId });
                result = await SelectAsync<T>(sql, sourceId);
            }
            else
                result = new ModelList<T>();
            return result;
        }

        public async Task<Dictionary<object, ModelList<T>>> SelectForeignsRowsAsync<T>(Type sourceModel, string foreignField, List<object> sourceIds) where T : Model, new()
        {
            var result = new Dictionary<object, ModelList<T>>();

            Type foreignModel = typeof(T);
            var attrs = foreignModel.GetCustomAttributes(typeof(ModelAttribute), true);
            string destTableName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).Table : foreignModel.Name;

            var foreignFieldProperty = Model.FindForeignProperty(sourceModel, foreignModel, foreignField);

            // if found, generate the needed SQL
            if (foreignFieldProperty != null)
            {
                var tmpIds = new List<object>();
                var idsCount = 0;

                Func<Task> loadForeign = async delegate
                {
                    var sql = $"SELECT * FROM `{destTableName}` WHERE {InFilter(foreignFieldProperty.Name, tmpIds)} ORDER BY `{foreignFieldProperty.Name}`";
                    object currentSourceId = null;
                    ModelList<T> currentSourceItems = null;
                    foreach (var item in await SelectAsync<T>(sql))
                    {
                        if ((currentSourceId == null) || !currentSourceId.Equals(item.Fields[foreignFieldProperty.Name]))
                        {
                            currentSourceId = item.Fields[foreignFieldProperty.Name];
                            currentSourceItems = new ModelList<T>();
                            result[currentSourceId] = currentSourceItems;
                        }
                        currentSourceItems.Add(item);
                    }
                };

                // batch request by a group of 200 because it is too slowest for bigger id group
                foreach (var id in sourceIds)
                {
                    tmpIds.Add(id);
                    idsCount++;
                    if (idsCount > 200)
                    {
                        await loadForeign();
                        tmpIds.Clear();
                        idsCount = 0;
                    }
                }
                if (idsCount > 0)
                    await loadForeign();
            }
            return result;
        }

        public async Task<Dictionary<object, T>> SelectLocalsRowsAsync<T>(List<object> ids) where T : Model, new()
        {
            var result = new Dictionary<object, T>();

            Type foreignModel = typeof(T);
            var attrs = foreignModel.GetCustomAttributes(typeof(ModelAttribute), true);
            string destTableName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).Table : foreignModel.Name;

            string primaryKey = ((ModelAttribute)attrs[0]).PrimaryKey;
            primaryKey = (primaryKey == null) ? "id" : primaryKey;

            // if found, generate the needed SQL
            var tmpIds = new List<object>();
            var idsCount = 0;

            Func<Task> loadForeign = async delegate
            {
                var sql = $"SELECT * FROM `{destTableName}` WHERE {InFilter(primaryKey, tmpIds)} ORDER BY `{primaryKey}`";
                foreach (var item in await SelectAsync<T>(sql))
                    result[item.Fields[primaryKey]] = item;
            };

            // batch request by a group of 200 because it is too slowest for bigger id group
            foreach (var id in ids)
            {
                tmpIds.Add(id);
                idsCount++;
                if (idsCount > 200)
                {
                    await loadForeign();
                    tmpIds.Clear();
                    idsCount = 0;
                }
            }
            if (idsCount > 0)
                await loadForeign();

            return result;
        }

        async Task<int> NonQueryAsync(string query, object[] args)
        {
            var cmd = new MySqlCommand(query, connection);
            if (transaction != null)
                cmd.Transaction = transaction;
            args.ForEach(arg => cmd.Parameters.Add(new MySqlParameter(string.Empty, arg)));
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> ExecuteNonQueryAsync(string query, params object[] args)
        {
            return await NonQueryAsync(query, args);
        }

        public async Task<object> ExecuteScalarAsync(string query, params object[] args)
        {
            var cmd = new MySqlCommand(query, connection);
            if (transaction != null)
                cmd.Transaction = transaction;
            args.ForEach(arg => cmd.Parameters.Add(new MySqlParameter(string.Empty, arg)));
            return await cmd.ExecuteScalarAsync();
        }

        public int Insert(string query, params object[] args)
        {
            var res = InsertAsync(query, args);
            res.Wait();
            return res.Result;
        }

        public Task<int> InsertAsync(string query, params object[] args)
        {
            return NonQueryAsync(query, args);
        }

        public async Task<ulong> LastInsertIdAsync()
        {
            var res = (await SelectAsync("SELECT LAST_INSERT_ID() AS id")).SingleOrDefault();
            return (res == null) ? 0 : (ulong)res["id"];
        }

        public async Task<long> FoundRowsAsync()
        {
            var res = (await SelectAsync("SELECT FOUND_ROWS() AS count")).SingleOrDefault();
            return (res == null) ? 0 : (long)res["count"];
        }

        public int Delete(string query, params object[] args)
        {
            var res = DeleteAsync(query, args);
            res.Wait();
            return res.Result;
        }

        public Task<int> DeleteAsync(string query, params object[] args)
        {
            return NonQueryAsync(query, args);
        }

        public Task<int> UpdateAsync(string query, params object[] args)
        {
            return NonQueryAsync(query, args);
        }

        public async Task<int> InsertRowAsync(string table, IDictionary values, IEnumerable<string> filterFields = null)
        {
            var fieldsList = "";
            var valuesList = "";
            foreach (object key in values.Keys)
            {
                var strKey = key as string;
                if (strKey != null)
                {
                    if ((filterFields != null) && !filterFields.Contains(strKey))
                        continue;

                    if (fieldsList != "")
                    {
                        fieldsList += ",";
                        valuesList += ",";
                    }
                    fieldsList += $"`{strKey}`";
                    valuesList += "?";
                }
            }
            if (fieldsList == "")
                return 0;

            var cmd = new MySqlCommand($"INSERT INTO `{table}` ({fieldsList}) VALUES ({valuesList})", connection);
            if (transaction != null)
                cmd.Transaction = transaction;
            foreach (object key in values.Keys)
            {
                var strKey = key as string;
                if (strKey != null)
                {
                    if ((filterFields != null) && !filterFields.Contains(strKey))
                        continue;

                    if (values[strKey] != null && values[strKey].GetType().IsEnum)
                        cmd.Parameters.Add(new MySqlParameter(strKey, Enum.GetName(values[strKey].GetType(), values[strKey])));
                    else
                        cmd.Parameters.Add(new MySqlParameter(strKey, values[strKey]));
                }
            }
            return await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> UpdateRowAsync(string table, string idKey, object idValue, IDictionary values, IEnumerable<string> filterFields = null)
        {
            var setList = "";
            foreach (object key in values.Keys)
            {
                var strKey = key as string;
                if (strKey != null)
                {
                    if ((filterFields != null) && !filterFields.Contains(strKey))
                        continue;
                    if (strKey == idKey)
                        continue;
                    if (setList != "")
                        setList += ", ";
                    setList += $"`{strKey}`=?";
                }
            }
            if (setList == "")
                return 0;
            var cmd = new MySqlCommand($"UPDATE `{table}` SET {setList} WHERE `{idKey}`=?", connection);
            if (transaction != null)
                cmd.Transaction = transaction;
            foreach (object key in values.Keys)
            {
                var strKey = key as string;
                if (strKey != null)
                {
                    if ((filterFields != null) && !filterFields.Contains(strKey))
                        continue;
                    if (strKey == idKey)
                        continue;
                    if (values[strKey] != null && values[strKey].GetType().IsEnum)
                        cmd.Parameters.Add(new MySqlParameter(strKey, Enum.GetName(values[strKey].GetType(), values[strKey])));
                    else
                        cmd.Parameters.Add(new MySqlParameter(strKey, values[strKey]));
                }
            }
            cmd.Parameters.Add(new MySqlParameter(idKey, idValue));
            return await cmd.ExecuteNonQueryAsync();
        }

        public static string InFilter(string field, IEnumerable values)
        {
            string filter = "";
            foreach (var value in values)
            {
                if (filter != "")
                    filter += ",";
                if (value is string)
                    filter += "'" + EscapeString((string)value) + "'";
                else
                    filter += value;
            }
            if (filter == "")
                filter = "FALSE";
            else
                filter = "`" + field + "` IN (" + filter + ")";
            return filter;
        }

        public static string NotInFilter(string field, IEnumerable values)
        {
            string filter = "";
            foreach (var value in values)
            {
                if (filter != "")
                    filter += ",";
                if (value is string)
                    filter += "'" + EscapeString((string)value) + "'";
                else
                    filter += value;
            }
            if (filter == "")
                filter = "FALSE";
            else
                filter = "`" + field + "` NOT IN (" + filter + ")";
            return filter;
        }

        public async Task CommitAsync()
        {
            if (transaction != null)
            {
                await transaction.CommitAsync();
                transaction.Dispose();
                transaction = null;
                if (commitActions != null)
                {
                    while (commitActions.Count > 0)
                        commitActions.Dequeue()();
                    commitActions = null;
                }
            }
        }

        public async Task RollbackAsync()
        {
            if (transaction != null)
            {
                await transaction.RollbackAsync();
                transaction.Dispose();
                transaction = null;
            }
        }

        public void Dispose()
        {
            if (transaction != null)
            {
                transaction.Rollback();
                transaction.Dispose();
            }
            else if (commitActions != null)
            {
                while (commitActions.Count > 0)
                    commitActions.Dequeue()();
                commitActions = null;
            }
            connection.Close();
        }

        static string[] DBGetEnumValues(string dbUrl, string table, string col)
        {
            string colDef;
            using (var connection = new MySqlConnection(dbUrl))
            {
                using (var command = new MySqlCommand($"SELECT COLUMN_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{table}' AND COLUMN_NAME='{col}'", connection))
                {
                    connection.Open();
                    colDef = command.ExecuteScalar() as string;
                }
            }
            if (colDef == null)
                return null;
            if (!colDef.StartsWith("enum(", StringComparison.InvariantCultureIgnoreCase) || !colDef.EndsWith(")", StringComparison.InvariantCultureIgnoreCase))
                return null;
            var values = colDef.Substring(5, colDef.Length - 6).Split(',');
            for (var i = 0; i < values.Length; i++)
            {
                if (values[i][0] != '\'' || values[i][values[i].Length - 1] != '\'')
                    return null;
                values[i] = values[i].Substring(1, values[i].Length - 2);
            }
            return values;
        }

        static Dictionary<string, Type> DBTypesMapping = new Dictionary<string, Type>
        {
            ["varchar"] = typeof(string),
            ["char"] = typeof(string),
            ["text"] = typeof(string),
            ["mediumtext"] = typeof(string),
            ["longtext"] = typeof(string),
            ["tinyint"] = typeof(bool),
            ["smallint"] = typeof(short),
            ["int"] = typeof(int),
            ["bigint"] = typeof(long),
            ["decimal"] = typeof(double),
            ["double"] = typeof(double),
            ["datetime"] = typeof(DateTime),
            ["timestamp"] = typeof(DateTime),
            ["date"] = typeof(DateTime),
            ["enum"] = typeof(string),
            ["blob"] = typeof(byte[]),
            ["json"] = typeof(string)
        };

        static List<DataColumn> DBGetTableColumns(string dbUrl, string table)
        {
            List<DataColumn> rows = new List<DataColumn>();
            using (var connection = new MySqlConnection(dbUrl))
            {
                using (var command = new MySqlCommand($"SELECT COLUMN_NAME,IS_NULLABLE, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='{table}' AND TABLE_SCHEMA=DATABASE()", connection))
                {
                    connection.Open();
                    using (var reader = command.ExecuteReader())
                    {
                        object[] values = new object[reader.FieldCount];
                        while (reader.Read())
                        {
                            reader.GetValues(values);
                            if (!DBTypesMapping.ContainsKey((string)values[2]))
                                throw new Exception($"Unknown type mapping for MySQL {(string)values[2]}");
                            rows.Add(new DataColumn
                            {
                                ColumnName = (string)values[0],
                                AllowDBNull = (string)values[1] == "YES",
                                DataType = DBTypesMapping[(string)values[2]]
                            });
                        }
                    }
                }
            }
            return rows;
        }

        public static bool CheckDBModels(Dictionary<string, string> dbsUrl)
        {
            bool valid = true;
            foreach (var type in Assembly.GetExecutingAssembly().GetTypes())
            {
                if (type.IsSubclassOf(typeof(Model)))
                {
                    valid &= CheckDBModel(dbsUrl, type);
                    GenerateDBToTypeInfo(type);
                }
            }
            return valid;
        }

        public static bool CheckDBModel(Dictionary<string, string> dbsUrl, Type model)
        {
            if (!model.IsSubclassOf(typeof(Model)))
                return false;

            bool valid = true;

            var attrs = model.GetCustomAttributes(typeof(ModelAttribute), true);
            if (attrs.Length == 0)
                return true;

            string tableName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).Table : model.Name;
            string primaryKey = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";
            string dbName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).DB : "DEFAULT";
            if (dbName == null)
                dbName = "DEFAULT";

            var dbUrl = dbsUrl[dbName];

            var fieldsProperties = new Dictionary<string, PropertyInfo>();
            var properties = model.GetProperties();
            foreach (var property in properties)
            {
                var fieldAttr = property.GetCustomAttributes(typeof(ModelFieldAttribute), false);

                if (fieldAttr.Length > 0 && ((ModelFieldAttribute)fieldAttr[0]).DB)
                    fieldsProperties[property.Name] = property;
            }

            var colsTypes = new Dictionary<string, System.Data.DataColumn>();

            foreach (var col in DBGetTableColumns(dbUrl, tableName))
                colsTypes[col.ColumnName] = col;

            if (primaryKey != null && !colsTypes.ContainsKey(primaryKey))
            {
                Console.WriteLine($"ERROR in model '{model.Name}', PRIMARY KEY '{primaryKey}' NOT PRESENT in table '{tableName}'");
                valid = false;
            }

            foreach (var key in fieldsProperties.Keys)
            {
                if (!colsTypes.ContainsKey(key))
                {
                    Console.WriteLine($"ERROR in model '{model.Name}', field '{key}' NOT PRESENT in table '{tableName}'");
                    valid = false;
                }
                else
                {
                    var propType = fieldsProperties[key].PropertyType;
                    if (fieldsProperties[key].PropertyType.IsValueType)
                    {
                        if (colsTypes[key].AllowDBNull)
                        {
                            propType = Nullable.GetUnderlyingType(fieldsProperties[key].PropertyType);
                            if (propType == null)
                            {
                                Console.WriteLine(
                                $"ERROR in model '{model.Name}', field '{key}' type NOT COMPATIBLE with col " +
                                $"in table '{tableName}' NOT NULLABLE");
                                valid = false;
                            }
                        }
                    }
                    if (propType != null)
                    {
                        // handle Enum
                        if (propType.IsEnum)
                        {
                            var enumValues = DBGetEnumValues(dbUrl, tableName, key);
                            if (enumValues == null)
                            {
                                Console.WriteLine(
                                $"ERROR in model '{model.Name}', field '{key}' enum NOT COMPATIBLE with col " +
                                $"in table '{tableName}' ({propType} != {colsTypes[key].DataType})");
                                valid = false;
                            }
                            else if ((enumValues.Length != Enum.GetNames(propType).Length) || (Enum.GetNames(propType).Intersect(enumValues).Count() != enumValues.Length))
                            {
                                var sep = "|";
                                Console.WriteLine(
                                $"ERROR in model '{model.Name}', field '{key}' enum VALUES NOT COMPATIBLE with col " +
                                    $"in table '{tableName}' ({String.Join(sep, Enum.GetNames(propType))} != {String.Join(sep, enumValues)})");
                                valid = false;
                            }
                        }
                        else if (!propType.IsAssignableFrom(colsTypes[key].DataType))
                        {
                            Console.WriteLine(
                                $"ERROR in model '{model.Name}', field '{key}' type NOT COMPATIBLE with col " +
                                $"in table '{tableName}' ({propType} != {colsTypes[key].DataType})");
                            valid = false;
                        }
                    }
                }
            }

            foreach (var key in colsTypes.Keys)
            {
                if (!fieldsProperties.ContainsKey(key))
                    Console.WriteLine($"WARN in model '{model.Name}', row '{key}' of table '{tableName}' NOT PRESENT in the model");
            }
            return valid;
        }

        public static void GenerateDBToTypeInfo(Type type)
        {
            var details = new Dictionary<string, PropertyDetail>();
            foreach (var property in type.GetProperties())
            {
                var fieldAttribute = (ModelFieldAttribute)property.GetCustomAttribute(typeof(ModelFieldAttribute));
                if (fieldAttribute == null)
                    continue;
                var nullableType = Nullable.GetUnderlyingType(property.PropertyType);
                var propertyType = nullableType != null ? nullableType : property.PropertyType;
                var isDirectMapping =
                    (propertyType == typeof(string)) ||
                    (propertyType == typeof(int)) ||
                    (propertyType == typeof(long)) ||
                    (propertyType == typeof(DateTime));
                details[property.Name] = new PropertyDetail
                {
                    Name = property.Name,
                    Property = property,
                    Attribute = fieldAttribute,
                    PropertyType = propertyType,
                    IsNullable = nullableType != null,
                    IsEnum = propertyType.IsEnum,
                    IsDirectMapping = isDirectMapping
                };
            }
            DBToTypeInfo[type] = details;
        }
    }

    public class RequestEnumerator<T> : IEnumeratorAsync<T>, IDisposable where T : Model, new()
    {
        readonly System.Data.Common.DbDataReader reader;
        DB.PropertyDetail[] propertiesDetails;
        object[] values;
        T current;

        public RequestEnumerator(System.Data.Common.DbDataReader reader, DB.PropertyDetail[] propertiesDetails, object[] values)
        {
            this.reader = reader;
            this.propertiesDetails = propertiesDetails;
            this.values = values;
        }

        public T Current
        {
            get
            {
                return current;
            }
        }

        public async Task<bool> MoveNextAsync()
        {
            var res = await reader.ReadAsync();
            if (res)
            {
                reader.GetValues(values);
                var item = new T();
                DB.DBToFields(propertiesDetails, values, item);
                current = item;
            }
            else
                current = null;
            return res;
        }

        public void Dispose()
        {
            if (reader is IDisposable)
                reader.Dispose();
        }
    }

    public class RequestEnumerable<T> : IEnumerableAsync<T> where T : Model, new()
    {
        DB db;
        string sql;
        object[] args;

        public RequestEnumerable(DB db, string sql, object[] args)
        {
            this.db = db;
            this.sql = sql;
            this.args = args;
        }

        public async Task<IEnumeratorAsync<T>> GetEnumeratorAsync()
        {
            var cmd = new MySqlCommand(sql, db.connection);
            if (db.transaction != null)
                cmd.Transaction = db.transaction;
            args.ForEach(arg => cmd.Parameters.Add(new MySqlParameter(string.Empty, arg)));

            var reader = await cmd.ExecuteReaderAsync();

            string[] names = new string[reader.FieldCount];
            object[] values = new object[reader.FieldCount];

            for (var i = 0; i < reader.FieldCount; i++)
            {
                names[i] = reader.GetName(i);
            }

            DB.PropertyDetail[] propertiesDetails = new DB.PropertyDetail[reader.FieldCount];
            var details = DB.DBToTypeInfo[typeof(T)];
            for (int i = 0; i < names.Length; i++)
            {
                var name = names[i];
                if (!details.ContainsKey(name))
                    continue;
                propertiesDetails[i] = details[name];
            }

            return new RequestEnumerator<T>(reader, propertiesDetails, values);
        }
    }
}
