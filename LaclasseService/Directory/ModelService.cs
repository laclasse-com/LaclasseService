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
		readonly Dictionary<string, ModelExpandDetails> expandFields;

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
			public string LocalField;
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
				var expandFieldAttribute = (ModelExpandFieldAttribute)prop.GetCustomAttribute(typeof(ModelExpandFieldAttribute), true);
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
						var propAttrs = foreignProp.GetCustomAttributes(typeof(ModelFieldAttribute), true);
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
                    
					PropertyInfo localProperty = null;
					if (prop.PropertyType.IsSubclassOf(typeof(Model)))
						localProperty = Model.FindForeignProperty(expandFieldAttribute.ForeignModel, typeof(T), expandFieldAttribute.ForeignField);

					var foreignDetails = new ModelExpandDetails
					{
						Attribute = expandFieldAttribute,                  
						ForeignModel = expandFieldAttribute.ForeignModel,
						LocalField = localProperty != null ? localProperty.Name : null,
						ForeignField = foreignProperty != null ? foreignProperty.Name : null,
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
			if (c.Request.QueryString.ContainsKey("seenBy"))
			{
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
				switch (c.Request.Method)
				{
					case "GET":
						// search API
						if (parts.Length == 0)
						{
							await RunBeforeAsync(null, context);
							var authUser = await context.GetAuthenticatedUserAsync();
							var filterAuth = (new T()).FilterAuthUser(authUser);
							using (DB db = await DB.CreateAsync(dbUrl))
							{
								var result = await Model.SearchAsync<T>(db, c, filterAuth);
								foreach (var item in result.Data)
									await item.EnsureRightAsync(c, Right.Read, null);
								c.Response.Content = result.ToJson(context);
							}
							c.Response.StatusCode = 200;
						}
						// get item
						else if (parts.Length == 1)
						{
							await RunBeforeAsync(null, context);
							object id = null;
							try
							{
								id = Convert.ChangeType(parts[0], details.PrimaryKeyType);
							}
							catch (InvalidCastException) { }
							catch (FormatException) { }
							if (id != null)
							{
								bool expand = true;
								if (context.Request.QueryString.ContainsKey("expand"))
									expand = Convert.ToBoolean(context.Request.QueryString["expand"]);
								T item = null;
								using (DB db = await DB.CreateAsync(dbUrl))
									item = await db.SelectRowAsync<T>(id, expand);
								if (item != null)
								{
									await item.EnsureRightAsync(c, Right.Read, null);
									c.Response.StatusCode = 200;
									c.Response.Content = item;
								}
							}
						}
						else if ((parts.Length > 1) && expandFields.ContainsKey(parts[1]))
						{
							object id = null;
							try
							{
								id = Convert.ChangeType(parts[0], details.PrimaryKeyType);
							}
							catch (InvalidCastException) { }
							catch (FormatException) { }
							if (id != null)
							{
								if (parts.Length == 3)
								{
									object foreignId = null;
									try
									{
										foreignId = Convert.ChangeType(parts[2], expandFields[parts[1]].ForeignDetails.PrimaryKeyType);
									}
									catch (InvalidCastException) { }
									catch (FormatException) { }

									if (foreignId != null)
									{
										await RunBeforeAsync(null, context);
										Model item = null;
										using (DB db = await DB.CreateAsync(dbUrl, true))
										{
											var task = typeof(DB).GetMethod(nameof(DB.SelectRowAsync)).MakeGenericMethod(expandFields[parts[1]].Attribute.ForeignModel).Invoke(db, new object[] { foreignId, false }) as Task;
											await task;
											var resultProperty = typeof(Task<>).MakeGenericType(expandFields[parts[1]].Attribute.ForeignModel).GetProperty("Result");
											item = resultProperty.GetValue(task) as Model;

											// check if it correspond to the current parent ID
											if ((item != null) && (!id.Equals(item.Fields[expandFields[parts[1]].ForeignField])))
												item = null;

											await db.CommitAsync();
										}
										if (item == null)
											c.Response.StatusCode = 404;
										else
										{
											c.Response.StatusCode = 200;
											c.Response.Content = item;
										}
									}
								}
								else if (parts.Length == 2)
								{
									await RunBeforeAsync(null, context);
									T item = null;
									using (DB db = await DB.CreateAsync(dbUrl))
									{
										item = await db.SelectRowAsync<T>(id, true);
										if (item != null)
											await item.LoadExpandFieldAsync(db, parts[1]);
									}

									if ((item != null) && (item.Fields.ContainsKey(parts[1])))
									{
										await item.EnsureRightAsync(c, Right.Read, null);
										c.Response.StatusCode = 200;
										// filter the result
										var modelListType = item.GetType().GetProperty(parts[1]).PropertyType;
										var resultFiltered = modelListType.GetMethod(nameof(ModelList<T>.Filter), new Type[] { typeof(HttpContext) }).Invoke(item.Fields[parts[1]], new object[] { c });
										c.Response.Content = (resultFiltered as IModelList).ToJson();
									}
								}
							}
						}
						break;
					case "POST":
						if (parts.Length == 0)
						{
							await RunBeforeAsync(null, context);
							var json = await c.Request.ReadAsJsonAsync();
							// multiple create
							if (json is JsonArray)
							{
								var result = new JsonArray();
								using (DB db = await DB.CreateAsync(dbUrl, true))
								{
									foreach (var jsonItem in (JsonArray)json)
									{
										var item = new T();
										item.FromJson((JsonObject)jsonItem, null, c);
										await item.EnsureRightAsync(c, Right.Create, null);
										await item.SaveAsync(db, true);
										await OnCreatedAsync(db, item);
										result.Add(item);
									}
									await db.CommitAsync();
								}
								c.Response.StatusCode = 200;
								c.Response.Content = result;
							}
							else if (json is JsonObject)
							{
								await RunBeforeAsync(null, context);
								var item = new T();
								item.FromJson((JsonObject)json, null, c);
								await item.EnsureRightAsync(c, Right.Create, null);

								using (DB db = await DB.CreateAsync(dbUrl, true))
								{
									await item.SaveAsync(db, true);
									await OnCreatedAsync(db, item);
									await db.CommitAsync();
								}
								c.Response.StatusCode = 200;
								c.Response.Content = item;
							}
						}
						else if ((parts.Length == 2) && expandFields.ContainsKey(parts[1]))
						{
							object id = null;
							try
							{
								id = Convert.ChangeType(parts[0], details.PrimaryKeyType);
							}
							catch (InvalidCastException) { }
							catch (FormatException) { }
							if (id != null)
							{
								await RunBeforeAsync(null, context);
								var json = await c.Request.ReadAsJsonAsync();
								JsonArray jsonArray;
								if (json is JsonArray)
									jsonArray = json as JsonArray;
								else
								{
									jsonArray = new JsonArray();
									jsonArray.Add(json);
								}

								// generate diff
								var diffJson = new JsonObject
								{
									[parts[1]] = new JsonObject
									{
										["diff"] = new JsonObject
										{
											["add"] = jsonArray,
											["change"] = new JsonArray(),
											["remove"] = new JsonArray()
										}
									}
								};

								var itemDiff = new T();
								itemDiff.FromJson(diffJson, null, c);
								itemDiff.Fields[details.PrimaryKeyName] = id;

								using (DB db = await DB.CreateAsync(dbUrl, true))
								{
									var oldItem = new T();
									oldItem.Fields[details.PrimaryKeyName] = id;
									await oldItem.LoadAsync(db, true);
									// need a loaded user to check the rights
									await oldItem.EnsureRightAsync(c, Right.Update, itemDiff);

									await itemDiff.UpdateAsync(db);
									await itemDiff.LoadAsync(db, true);
									await OnChangedAsync(db, itemDiff);
									await db.CommitAsync();
								}
								c.Response.StatusCode = 200;
								c.Response.Content = itemDiff;
							}
						}
						break;
					case "PUT":
						// multiple modify
						if (parts.Length == 0)
						{
							await RunBeforeAsync(null, context);
							var json = await c.Request.ReadAsJsonAsync();
							if (json is JsonArray)
							{
								var result = new JsonArray();
								using (DB db = await DB.CreateAsync(dbUrl, true))
								{
									foreach (var jsonItem in (JsonArray)json)
									{
										var item = new T();
										item.FromJson((JsonObject)jsonItem, null, c);
										var oldItem = new T();
										oldItem.Fields[details.PrimaryKeyName] = item.Fields[details.PrimaryKeyName];
										await oldItem.LoadAsync(db, true);
										// need a loaded user to check the rights
										await oldItem.EnsureRightAsync(c, Right.Update, item);
										await item.UpdateAsync(db);
										await item.LoadAsync(db, true);
										await OnChangedAsync(db, item);
										result.Add(item);
									}
									await db.CommitAsync();
								}
								c.Response.StatusCode = 200;
								c.Response.Content = result;
							}
						}
						// item modify
						else if (parts.Length == 1)
						{
							await RunBeforeAsync(null, context);
							object id = null;
							try
							{
								id = Convert.ChangeType(parts[0], details.PrimaryKeyType);
							}
							catch (InvalidCastException) { }
							catch (FormatException) { }

							if (id != null)
							{
								var json = await c.Request.ReadAsJsonAsync();
								if (json is JsonObject)
								{
									var itemDiff = new T();
									itemDiff.FromJson((JsonObject)json, null, c);
									itemDiff.Fields[details.PrimaryKeyName] = id;

									using (DB db = await DB.CreateAsync(dbUrl, true))
									{
										var oldItem = new T();
										oldItem.Fields[details.PrimaryKeyName] = id;
										await oldItem.LoadAsync(db, true);
										// need a loaded user to check the rights
										await oldItem.EnsureRightAsync(c, Right.Update, itemDiff);

										await itemDiff.UpdateAsync(db);
										await itemDiff.LoadAsync(db, true);
										await OnChangedAsync(db, itemDiff);
										await db.CommitAsync();
									}
									c.Response.StatusCode = 200;
									c.Response.Content = itemDiff;
								}
							}
						}
						else if ((parts.Length == 2 || parts.Length == 3) && expandFields.ContainsKey(parts[1]))
						{
							object id = null;
							try
							{
								id = Convert.ChangeType(parts[0], details.PrimaryKeyType);
							}
							catch (InvalidCastException) { }
							catch (FormatException) { }
							if (id != null)
							{
								await RunBeforeAsync(null, context);
								var jsonArray = new JsonArray();

								if (parts.Length == 3)
								{
									object foreignId = null;
									try
									{
										foreignId = Convert.ChangeType(parts[2], expandFields[parts[1]].ForeignDetails.PrimaryKeyType);
									}
									catch (InvalidCastException) { }
									catch (FormatException) { }

									if (foreignId != null)
									{
										var json = await c.Request.ReadAsJsonAsync();
										json[expandFields[parts[1]].ForeignDetails.PrimaryKeyName] = parts[2];
										jsonArray.Add(json);
									}
								}
								if (parts.Length == 2)
								{
									JsonValue json = await context.Request.ReadAsJsonAsync();
									if (json is JsonArray)
										jsonArray = json as JsonArray;
								}
								// generate diff
								var diffJson = new JsonObject
								{
									[parts[1]] = new JsonObject
									{
										["diff"] = new JsonObject
										{
											["add"] = new JsonArray(),
											["change"] = jsonArray,
											["remove"] = new JsonArray()
										}
									}
								};

								var itemDiff = new T();
								itemDiff.FromJson(diffJson, null, c);
								itemDiff.Fields[details.PrimaryKeyName] = id;

								using (DB db = await DB.CreateAsync(dbUrl, true))
								{
									var oldItem = new T();
									oldItem.Fields[details.PrimaryKeyName] = id;
									await oldItem.LoadAsync(db, true);
									// need a loaded user to check the rights
									await oldItem.EnsureRightAsync(c, Right.Update, itemDiff);

									await itemDiff.UpdateAsync(db);
									await itemDiff.LoadAsync(db, true);
									await OnChangedAsync(db, itemDiff);
									await db.CommitAsync();
								}
								c.Response.StatusCode = 200;
								c.Response.Content = itemDiff;
							}
						}
						break;
					case "DELETE":
						// multiple delete
						if (parts.Length == 0)
						{
							await RunBeforeAsync(null, context);
							var json = await c.Request.ReadAsJsonAsync();
							var jsonArray = json as JsonArray;
							if (jsonArray != null)
							{
								var ids = ((JsonArray)json).Select((arg) => Convert.ChangeType(arg.Value, details.PrimaryKeyType));
								using (DB db = await DB.CreateAsync(dbUrl, true))
								{
									foreach (var id in ids)
									{
										var item = await db.SelectRowAsync<T>(id, true);
										if (item != null)
										{
											await item.EnsureRightAsync(c, Right.Delete, null);
											await item.DeleteAsync(db);
											await OnDeletedAsync(db, item);
										}
									}
									c.Response.StatusCode = 200;
									await db.CommitAsync();
								}
							}
						}
						// item delete
						else if (parts.Length == 1)
						{
							await RunBeforeAsync(null, context);
							object id = null;
							try
							{
								id = Convert.ChangeType(parts[0], details.PrimaryKeyType);
							}
							catch (InvalidCastException) { }
							catch (FormatException) { }

							if (id != null)
							{
								T item = null;
								using (DB db = await DB.CreateAsync(dbUrl, true))
								{
									item = await db.SelectRowAsync<T>(id, true);
									if (item != null)
									{
										await item.EnsureRightAsync(c, Right.Delete, null);
										await item.DeleteAsync(db);
										await OnDeletedAsync(db, item);
									}
									await db.CommitAsync();
								}
								if (item != null)
									c.Response.StatusCode = 200;
							}
						}
						else if ((parts.Length == 2 || parts.Length == 3) && expandFields.ContainsKey(parts[1]))
						{
							object id = null;
							try
							{
								id = Convert.ChangeType(parts[0], details.PrimaryKeyType);
							}
							catch (InvalidCastException) { }
							catch (FormatException) { }
							if (id != null)
							{
								await RunBeforeAsync(null, context);
								var jsonArray = new JsonArray();

								if (parts.Length == 3)
								{
									object foreignId = null;
									try
									{
										foreignId = Convert.ChangeType(parts[2], expandFields[parts[1]].ForeignDetails.PrimaryKeyType);
									}
									catch (InvalidCastException) { }
									catch (FormatException) { }

									if (foreignId != null)
									{
										jsonArray.Add(new JsonObject()
										{
											[expandFields[parts[1]].ForeignDetails.PrimaryKeyName] = parts[2]
										});
									}
								}
								if (parts.Length == 2)
								{
									JsonValue json = await context.Request.ReadAsJsonAsync();
									if (json is JsonArray)
									{
										foreach (var itemId in json as JsonArray)
											jsonArray.Add(new JsonObject { [expandFields[parts[1]].ForeignDetails.PrimaryKeyName] = itemId });
									}
								}
								// generate diff
								var diffJson = new JsonObject
								{
									[parts[1]] = new JsonObject
									{
										["diff"] = new JsonObject
										{
											["add"] = new JsonArray(),
											["change"] = new JsonArray(),
											["remove"] = jsonArray
										}
									}
								};

								var itemDiff = new T();
								itemDiff.FromJson(diffJson, null, c);
								itemDiff.Fields[details.PrimaryKeyName] = id;

								using (DB db = await DB.CreateAsync(dbUrl, true))
								{
									var oldItem = new T();
									oldItem.Fields[details.PrimaryKeyName] = id;
									await oldItem.LoadAsync(db, true);
									// need a loaded user to check the rights
									await oldItem.EnsureRightAsync(c, Right.Update, itemDiff);

									await itemDiff.UpdateAsync(db);
									await itemDiff.LoadAsync(db, true);
									await OnChangedAsync(db, itemDiff);
									await db.CommitAsync();
								}
								c.Response.StatusCode = 200;
								c.Response.Content = itemDiff;
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
