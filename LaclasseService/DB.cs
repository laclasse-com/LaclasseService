// DB.cs
// 
//  Simple query layer on top of MySqlClient 
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
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;

namespace Laclasse
{
	public enum SortDirection
	{
		Ascending,
		Descending
	}

	public class DB : IDisposable
	{
		readonly MySqlConnection connection;
		MySqlTransaction transaction;

		DB(string connectionUrl)
		{
			connection = new MySqlConnection(connectionUrl);
		}

		public string EscapeString(string value)
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

		public async Task<IList<Dictionary<string,object>>> SelectAsync(string query, params object[] args)
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

		async Task<int> NonQueryAsync(string query, object[] args)
		{
			var cmd = new MySqlCommand(query, connection);
			if (transaction != null)
				cmd.Transaction = transaction;
			args.ForEach(arg => cmd.Parameters.Add(new MySqlParameter(string.Empty, arg)));
			return await cmd.ExecuteNonQueryAsync();
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

		public async Task<int> InsertRowAsync(string table, IDictionary values)
		{
			var fieldsList = "";
			var valuesList = "";
			foreach (object key in values.Keys)
			{
				var strKey = key as string;
				if (strKey != null)
				{
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
					cmd.Parameters.Add(new MySqlParameter(strKey, values[strKey]));
				}
			}
			return await cmd.ExecuteNonQueryAsync();
		}

		public async Task<int> UpdateRowAsync(string table, string idKey, object idValue, IDictionary values)
		{
			var setList = "";
			foreach (object key in values.Keys)
			{
				var strKey = key as string;
				if (strKey != null)
				{
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
					cmd.Parameters.Add(new MySqlParameter(strKey, values[strKey]));
				}
			}
			cmd.Parameters.Add(new MySqlParameter(idKey, idValue));
			return await cmd.ExecuteNonQueryAsync();
		}

		public string InFilter(string field, IEnumerable values)
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
				filter = "TRUE";
			else
				filter = "`" + field + "` IN (" + filter + ")";
			return filter;
		}

		public void Commit()
		{
			if (transaction != null)
			{
				transaction.Commit();
				transaction.Dispose();
				transaction = null;
			}
		}

		public void Rollback()
		{
			if (transaction != null)
			{
				transaction.Rollback();
				transaction.Dispose();
				transaction = null;
			}
		}

		public void Dispose()
		{
			if (transaction != null)
			{
				transaction.Commit();
				transaction.Dispose();
			}
			connection.Close();
		}
	}
}