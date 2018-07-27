// ModelService.cs
// 
//  Handle emails API. 
//
// Author(s):
//  Daniel Lacroix <dlacroix@erasme.org>
// 
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
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Reflection;
using Erasme.Http;
using Erasme.Json;
using Laclasse.Authentication;

namespace Laclasse.Directory
{
	public class ModelService<T> : HttpRouting where T : Model, new()
	{
		readonly ModelDetails details;

		readonly string dbUrl;
		readonly Dictionary<string,ModelExpandDetails> expandFields;

		class ModelDetails
		{
			public string TableName;
			public string PrimaryKeyName;
			public Type PrimaryKeyType;
		}

		class ModelExpandDetails
		{
			public ModelExpandFieldAttribute Attribute;
			public Type ForeignModel;
			public string ForeignField;
			public ModelDetails ForeignDetails;
			public IEnumerable<string> ForeignSearchAllowedFields;
		}

		public ModelService(string dbUrl)
		{
			this.dbUrl = dbUrl;

			details = GetModelDetails(typeof(T));

			expandFields = new Dictionary<string, ModelExpandDetails>();
			var properties = typeof(T).GetProperties();
			foreach (var prop in properties)
			{
				var expandFieldAttribute = (ModelExpandFieldAttribute)prop.GetCustomAttribute(typeof(ModelExpandFieldAttribute));
				if (expandFieldAttribute != null)
				{
					// find the foreign property name
					PropertyInfo foreignProperty = null;
					PropertyInfo[] foreignProperties;
					if (expandFieldAttribute.ForeignField != null)
					{
						var foreignProp = expandFieldAttribute.ForeignModel.GetProperty(expandFieldAttribute.ForeignField);
						if (foreignProp != null)
							foreignProperties = new PropertyInfo[] { foreignProp };
						else
							foreignProperties = new PropertyInfo[] { };
					}
					else
						foreignProperties = expandFieldAttribute.ForeignModel.GetProperties();
					foreach (var foreignProp in foreignProperties)
					{
						var propAttrs = foreignProp.GetCustomAttributes(typeof(ModelFieldAttribute), false);
						foreach (var a in propAttrs)
						{
							var propAttr = (ModelFieldAttribute)a;
							if (propAttr.ForeignModel == typeof(T))
							{
								foreignProperty = foreignProp;
								break;
							}
						}
						if (foreignProperty != null)
							break;
					}

					var foreignDetails = new ModelExpandDetails
					{
						Attribute = expandFieldAttribute,
						ForeignModel = expandFieldAttribute.ForeignModel,
						ForeignField = foreignProperty.Name,
						ForeignDetails = GetModelDetails(expandFieldAttribute.ForeignModel),
						ForeignSearchAllowedFields = Model.GetSearchAllowedFieldsFromModel(expandFieldAttribute.ForeignModel, false)
					};
					expandFields[expandFieldAttribute.Name] = foreignDetails;
				}
			}
		}

		public async override Task ProcessRequestAsync(HttpContext context)
		{
			await base.ProcessRequestAsync(context);
			var c = context;         
			if (c.Request.QueryString.ContainsKey("seenBy")) {
				var authUser = await context.GetAuthenticatedUserAsync();
				if (authUser == null || !authUser.IsUser || authUser.user.id != c.Request.QueryString["seenBy"])
				{
					await context.EnsureIsSuperAdminAsync();
                    await c.SetAuthenticatedUserAsync(c.Request.QueryString["seenBy"]);					
				}            
			}
                
			if (c.Response.StatusCode == -1)
			{
				var parts = c.Request.Path.Split(new char[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                switch (c.Request.Method) {
                case "GET":
                    // search API
                    if (parts.Length == 0) {
                        await RunBeforeAsync (null, context);
                        var authUser = await context.GetAuthenticatedUserAsync ();
                        var filterAuth = (new T ()).FilterAuthUser (authUser);
                        using (DB db = await DB.CreateAsync (dbUrl)) {
                            var result = await Model.SearchAsync<T> (db, c, filterAuth);
                            foreach (var item in result.Data)
                                await item.EnsureRightAsync (c, Right.Read);
                            c.Response.Content = result.ToJson (context);
                        }
                        c.Response.StatusCode = 200;
                    }
                    // get item
                    else if (parts.Length == 1) {
                        await RunBeforeAsync (null, context);
                        object id = null;
                        try {
                            id = Convert.ChangeType (parts [0], details.PrimaryKeyType);
                        } catch (InvalidCastException) { } catch (FormatException) { }
                        if (id != null) {
                            bool expand = true;
                            if (context.Request.QueryString.ContainsKey ("expand"))
                                expand = Convert.ToBoolean (context.Request.QueryString ["expand"]);
                            T item = null;
                            using (DB db = await DB.CreateAsync (dbUrl))
                                item = await db.SelectRowAsync<T> (id, expand);
                            if (item != null) {
                                await item.EnsureRightAsync (c, Right.Read);
                                c.Response.StatusCode = 200;
                                c.Response.Content = item;
                            }
                        }
                    } else if ((parts.Length > 1) && expandFields.ContainsKey (parts [1])) {
                        object id = null;
                        try {
                            id = Convert.ChangeType (parts [0], details.PrimaryKeyType);
                        } catch (InvalidCastException) { } catch (FormatException) { }
                        if (id != null) {
                            if (parts.Length == 3) {
                                object foreignId = null;
                                try {
                                    foreignId = Convert.ChangeType (parts [2], expandFields [parts [1]].ForeignDetails.PrimaryKeyType);
                                } catch (InvalidCastException) { } catch (FormatException) { }

                                if (foreignId != null) {
                                    await RunBeforeAsync (null, context);
                                    Model item = null;
                                    using (DB db = await DB.CreateAsync (dbUrl, true)) {
                                        var task = typeof (DB).GetMethod (nameof (DB.SelectRowAsync)).MakeGenericMethod (expandFields [parts [1]].Attribute.ForeignModel).Invoke (db, new object [] { foreignId, false }) as Task;
                                        await task;
                                        var resultProperty = typeof (Task<>).MakeGenericType (expandFields [parts [1]].Attribute.ForeignModel).GetProperty ("Result");
                                        item = resultProperty.GetValue (task) as Model;

                                        // check if it correspond to the current parent ID
                                        if ((item != null) && (!id.Equals (item.Fields [expandFields [parts [1]].ForeignField])))
                                            item = null;

                                        db.Commit ();
                                    }
                                    if (item == null)
                                        c.Response.StatusCode = 404;
                                    else {
                                        c.Response.StatusCode = 200;
                                        c.Response.Content = item;
                                    }
                                }
                            } else if (parts.Length == 2) {
                                await RunBeforeAsync (null, context);
                                T item = null;
                                using (DB db = await DB.CreateAsync (dbUrl)) {
                                    item = await db.SelectRowAsync<T> (id, true);
                                    if (item != null)
                                        await item.LoadExpandFieldAsync (db, parts [1]);
                                }

                                if ((item != null) && (item.Fields.ContainsKey (parts [1]))) {
                                    await item.EnsureRightAsync (c, Right.Read);
                                    c.Response.StatusCode = 200;
                                    // filter the result
                                    var modelListType = item.GetType ().GetProperty (parts [1]).PropertyType;
                                    var resultFiltered = modelListType.GetMethod (nameof (ModelList<T>.Filter), new Type [] { typeof (HttpContext) }).Invoke (item.Fields [parts [1]], new object [] { c });
                                    c.Response.Content = (resultFiltered as IModelList).ToJson ();
                                }
                            }
                        }
                    }
                    break;
                case "POST":
                    if (parts.Length == 0) {
                        await RunBeforeAsync (null, context);
                        var json = await c.Request.ReadAsJsonAsync ();
                        // multiple create
                        if (json is JsonArray) {
                            var result = new JsonArray ();
                            using (DB db = await DB.CreateAsync (dbUrl, true)) {
                                foreach (var jsonItem in (JsonArray)json) {
                                    var item = new T ();
                                    item.FromJson ((JsonObject)jsonItem, null, c);
                                    await item.EnsureRightAsync (c, Right.Create);
                                    await item.SaveAsync (db, true);
                                    await OnCreatedAsync (db, item);
                                    result.Add (item);
                                }
                                db.Commit ();
                            }
                            c.Response.StatusCode = 200;
                            c.Response.Content = result;
                        } else if (json is JsonObject) {
                            await RunBeforeAsync (null, context);
                            var item = new T ();
                            item.FromJson ((JsonObject)json, null, c);
                            await item.EnsureRightAsync (c, Right.Create);

                            using (DB db = await DB.CreateAsync (dbUrl, true)) {
                                await item.SaveAsync (db, true);
                                await OnCreatedAsync (db, item);
                                db.Commit ();
                            }
                            c.Response.StatusCode = 200;
                            c.Response.Content = item;
                        }
                    } else if ((parts.Length == 2) && expandFields.ContainsKey (parts [1])) {
                        object id = null;
                        try {
                            id = Convert.ChangeType (parts [0], details.PrimaryKeyType);
                        } catch (InvalidCastException) { } catch (FormatException) { }
                        if (id != null) {
                            await RunBeforeAsync (null, context);
                            var json = await c.Request.ReadAsJsonAsync ();
                            using (DB db = await DB.CreateAsync (dbUrl, true)) {
                                if (json is JsonArray) {
                                    foreach (var jsonItem in (JsonArray)json) {
                                        if (!(jsonItem is JsonObject))
                                            continue;

                                        var foreignItem = (Model)Activator.CreateInstance (expandFields [parts [1]].ForeignModel);
                                        foreignItem.FromJson ((JsonObject)jsonItem, null, c);
                                        foreignItem.Fields [expandFields [parts [1]].ForeignField] = id;
                                        await foreignItem.SaveAsync (db, false);
                                    }
                                } else if (json is JsonObject) {
                                    var foreignItem = (Model)Activator.CreateInstance (expandFields [parts [1]].ForeignModel);
                                    foreignItem.FromJson ((JsonObject)json, null, c);
                                    foreignItem.Fields [expandFields [parts [1]].ForeignField] = id;
                                    await foreignItem.SaveAsync (db, false);
                                }

                                // return the whole item
                                T item = null;
                                item = await db.SelectRowAsync<T> (id, true);
                                await item.EnsureRightAsync (c, Right.Update);
                                await OnChangedAsync (db, item);
                                if (item != null) {
                                    c.Response.StatusCode = 200;
                                    c.Response.Content = item;
                                }
                                db.Commit ();
                            }
                        }
                    }
                    break;
                case "PUT":
                    // multiple modify
                    if (parts.Length == 0) {
                        await RunBeforeAsync (null, context);
                        var json = await c.Request.ReadAsJsonAsync ();
                        if (json is JsonArray) {
                            var result = new JsonArray ();
                            using (DB db = await DB.CreateAsync (dbUrl, true)) {
                                foreach (var jsonItem in (JsonArray)json) {
                                    var item = new T ();
                                    item.FromJson ((JsonObject)jsonItem, null, c);
									var oldItem = new T();
									oldItem.Fields[details.PrimaryKeyName] = item.Fields[details.PrimaryKeyName];
									await oldItem.LoadAsync(db, true);
									// need a loaded user to check the rights
                                    await oldItem.EnsureRightAsync(c, Right.Update);
                                    await item.UpdateAsync (db);
                                    await item.LoadAsync (db, true);
                                    await OnChangedAsync (db, item);
                                    result.Add (item);
                                }
                                db.Commit ();
                            }
                            c.Response.StatusCode = 200;
                            c.Response.Content = result;
                        }
                    }
                    // item modify
                    else if (parts.Length == 1) {
                        await RunBeforeAsync (null, context);
                        object id = null;
                        try {
                            id = Convert.ChangeType (parts [0], details.PrimaryKeyType);
                        } catch (InvalidCastException) { } catch (FormatException) { }

                        if (id != null) {
                            var json = await c.Request.ReadAsJsonAsync ();
                            if (json is JsonObject) {
                                var itemDiff = new T ();
                                itemDiff.FromJson ((JsonObject)json, null, c);
                                itemDiff.Fields [details.PrimaryKeyName] = id;

                                using (DB db = await DB.CreateAsync (dbUrl, true)) {
									var oldItem = new T();
                                    oldItem.Fields[details.PrimaryKeyName] = id;
                                    await oldItem.LoadAsync(db, true);
									// need a loaded user to check the rights
                                    await oldItem.EnsureRightAsync(c, Right.Update);

                                    await itemDiff.UpdateAsync (db);
                                    await itemDiff.LoadAsync (db, true);
                                    await OnChangedAsync (db, itemDiff);
                                    db.Commit ();
                                }
                                c.Response.StatusCode = 200;
                                c.Response.Content = itemDiff;
                            }
                        }
                    } else if ((parts.Length > 1) && expandFields.ContainsKey (parts [1])) {
                        object id = null;
                        try {
                            id = Convert.ChangeType (parts [0], details.PrimaryKeyType);
                        } catch (InvalidCastException) { } catch (FormatException) { }
                        if (id != null) {
                            if (parts.Length == 3) {
                                object foreignId = null;
                                try {
                                    foreignId = Convert.ChangeType (parts [2], expandFields [parts [1]].ForeignDetails.PrimaryKeyType);
                                } catch (InvalidCastException) { } catch (FormatException) { }

                                if (foreignId != null) {
                                    await RunBeforeAsync (null, context);
                                    var json = await c.Request.ReadAsJsonAsync ();

                                    await RunBeforeAsync (null, context);
                                    Model foreignItem = null;
                                    using (DB db = await DB.CreateAsync (dbUrl, true)) {
										var oldItem = new T();
                                        oldItem.Fields[details.PrimaryKeyName] = id;
                                        await oldItem.LoadAsync(db, true);
                                        // need a loaded user to check the rights
                                        await oldItem.EnsureRightAsync(c, Right.Update);

                                        var task = typeof (DB).GetMethod (nameof (DB.SelectRowAsync)).MakeGenericMethod (expandFields [parts [1]].Attribute.ForeignModel).Invoke (db, new object [] { foreignId, false }) as Task;
                                        await task;
                                        var resultProperty = typeof (Task<>).MakeGenericType (expandFields [parts [1]].Attribute.ForeignModel).GetProperty ("Result");
                                        foreignItem = resultProperty.GetValue (task) as Model;

                                        // check if it correspond to the current parent ID
                                        if ((foreignItem != null) && (!id.Equals (foreignItem.Fields [expandFields [parts [1]].ForeignField])))
                                            foreignItem = null;

                                        if (foreignItem != null) {
                                            var foreignItemDiff = (Model)Activator.CreateInstance (expandFields [parts [1]].ForeignModel);
                                            foreignItemDiff.FromJson ((JsonObject)json, null, c);
                                            foreignItemDiff.Fields [expandFields [parts [1]].ForeignDetails.PrimaryKeyName] = foreignId;
                                            await foreignItemDiff.UpdateAsync (db);
                                        }

                                        // return the whole item
                                        T item = null;
                                        item = await db.SelectRowAsync<T> (id, true);
                                        await OnChangedAsync (db, item);
                                        if (item != null) {
                                            c.Response.StatusCode = 200;
                                            c.Response.Content = item;
                                        }
                                        db.Commit ();
                                    }
                                }
                            }
                            if (parts.Length == 2) {
                                await RunBeforeAsync (null, context);

                                JsonValue json = await context.Request.ReadAsJsonAsync ();

                                using (DB db = await DB.CreateAsync (dbUrl, true)) {
                                    // multiple PUT
                                    if (json is JsonArray) {
                                        // TODO
                                        throw new NotImplementedException ();
                                    }
									var oldItem = new T();
                                    oldItem.Fields[details.PrimaryKeyName] = id;
                                    await oldItem.LoadAsync(db, true);
                                    // need a loaded user to check the rights
                                    await oldItem.EnsureRightAsync(c, Right.Update);

                                    // return the whole item
                                    T item = null;
                                    item = await db.SelectRowAsync<T> (id, true);
                                    await OnChangedAsync (db, item);
                                    if (item != null) {
                                        c.Response.StatusCode = 200;
                                        c.Response.Content = item;
                                    }
                                    db.Commit ();
                                }
                            }
                        }
                    }
                    break;
                case "DELETE":
                    // multiple delete
                    if (parts.Length == 0) {
                        await RunBeforeAsync (null, context);
                        var json = await c.Request.ReadAsJsonAsync ();
                        var jsonArray = json as JsonArray;
                        if (jsonArray != null) {
                            var ids = ((JsonArray)json).Select ((arg) => Convert.ChangeType (arg.Value, details.PrimaryKeyType));
                            using (DB db = await DB.CreateAsync (dbUrl, true)) {
                                foreach (var id in ids) {
                                    var item = await db.SelectRowAsync<T> (id, true);
                                    if (item != null) {
                                        await item.EnsureRightAsync (c, Right.Delete);
                                        await item.DeleteAsync (db);
                                        await OnDeletedAsync (db, item);
                                    }
                                }
                                c.Response.StatusCode = 200;
                                db.Commit ();
                            }
                        }
                    }
                    // item delete
                    else if (parts.Length == 1) {
                        await RunBeforeAsync (null, context);
                        object id = null;
                        try {
                            id = Convert.ChangeType (parts [0], details.PrimaryKeyType);
                        } catch (InvalidCastException) { } catch (FormatException) { }

                        if (id != null) {
                            T item = null;
                            using (DB db = await DB.CreateAsync (dbUrl, true)) {
                                item = await db.SelectRowAsync<T> (id, true);
                                if (item != null) {
                                    await item.EnsureRightAsync (c, Right.Delete);
                                    await item.DeleteAsync (db);
                                    await OnDeletedAsync (db, item);
                                }
                                db.Commit ();
                            }
                            if (item != null)
                                c.Response.StatusCode = 200;
                        }
                    } else if ((parts.Length > 1) && expandFields.ContainsKey (parts [1])) {
                        object id = null;
                        try {
                            id = Convert.ChangeType (parts [0], details.PrimaryKeyType);
                        } catch (InvalidCastException) { } catch (FormatException) { }
                        if (id != null) {
                            if (parts.Length == 3) {
                                object foreignId = null;
                                try {
                                    foreignId = Convert.ChangeType (parts [2], expandFields [parts [1]].ForeignDetails.PrimaryKeyType);
                                } catch (InvalidCastException) { } catch (FormatException) { }

                                if (foreignId != null) {
                                    await RunBeforeAsync (null, context);
                                    Model foreignItem = null;
                                    using (DB db = await DB.CreateAsync (dbUrl, true)) {
                                        var task = typeof (DB).GetMethod (nameof (DB.SelectRowAsync)).MakeGenericMethod (expandFields [parts [1]].Attribute.ForeignModel).Invoke (db, new object [] { foreignId, false }) as Task;
                                        await task;
                                        var resultProperty = typeof (Task<>).MakeGenericType (expandFields [parts [1]].Attribute.ForeignModel).GetProperty ("Result");
                                        foreignItem = resultProperty.GetValue (task) as Model;

                                        // check if it correspond to the current parent ID
                                        if ((foreignItem != null) && (!id.Equals (foreignItem.Fields [expandFields [parts [1]].ForeignField])))
                                            foreignItem = null;

                                        if (foreignItem != null)
                                            await foreignItem.DeleteAsync (db);

                                        // return the whole item
                                        T item = null;
                                        item = await db.SelectRowAsync<T> (id, true);
                                        await item.EnsureRightAsync (c, Right.Update);
                                        await OnChangedAsync (db, item);
                                        if (item != null) {
                                            c.Response.StatusCode = 200;
                                            c.Response.Content = item;
                                        }
                                        db.Commit ();
                                    }
                                }
                            }
                            if (parts.Length == 2) {
                                await RunBeforeAsync (null, context);

                                JsonValue json = await context.Request.ReadAsJsonAsync ();

                                using (DB db = await DB.CreateAsync (dbUrl, true)) {
                                    // multiple DELETE
                                    if (json is JsonArray) {
                                        // WARNING: dont go thrown the model constraints
                                        var ids = ((JsonArray)json).Select ((arg) => (Convert.ChangeType (arg.Value, expandFields [parts [1]].ForeignDetails.PrimaryKeyType)));
                                        await db.DeleteAsync ($"DELETE FROM `{expandFields [parts [1]].ForeignDetails.TableName}` WHERE `{expandFields [parts [1]].ForeignField}`=? AND {DB.InFilter (expandFields [parts [1]].ForeignDetails.PrimaryKeyName, ids)}", id);
                                    }

                                    // return the whole item
                                    T item = null;
                                    item = await db.SelectRowAsync<T> (id, true);
                                    await item.EnsureRightAsync (c, Right.Update);
                                    await OnChangedAsync (db, item);
                                    if (item != null) {
                                        c.Response.StatusCode = 200;
                                        c.Response.Content = item;
                                    }
                                    db.Commit ();
                                }
                            }
                        }
                    }
                    break;
                }
			}
		}

		protected virtual Task OnCreatedAsync(DB db, T item)
		{
			return Task.FromResult(false);
		}

		protected virtual Task OnChangedAsync(DB db, T item)
		{
			return Task.FromResult(false);
		}

		protected virtual Task OnDeletedAsync(DB db, T item)
		{
			return Task.FromResult(false);
		}

		static ModelDetails GetModelDetails(Type model)
		{
			var details = new ModelDetails();
			var attrs = model.GetCustomAttributes(typeof(ModelAttribute), false);
			details.TableName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).Table : model.Name;
			details.PrimaryKeyName = (attrs.Length > 0) ? ((ModelAttribute)attrs[0]).PrimaryKey : "id";

			var property = model.GetProperty(details.PrimaryKeyName);
			details.PrimaryKeyType = property.PropertyType;
			return details;
		}
	}
}
