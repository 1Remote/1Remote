using System;
using System.Collections.Generic;
using System.Data.SQLite;
using PRM.Core.Model;

namespace PRM.Core.DB
{
    public abstract class SimpleOrmBase
    {
        protected SimpleOrmBase(string tableName)
        {
            TableName = tableName;
        }
        protected string TableName;
        public abstract string SQLCreateTable();
        /// <summary>
        /// return id if success else return 0
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public abstract bool Insert(SQLiteConnection connection);
        public abstract bool Update(SQLiteConnection connection, uint id = 0);
        public abstract bool Delete(SQLiteConnection connection, uint id = 0);
    }

    public class PRM_DAO
    {
        private static PRM_DAO _uniqueInstance;
        private static readonly object UniqueInstanceLocker = new object();
        public static PRM_DAO GetInstance()
        {
            lock (UniqueInstanceLocker)
            {
                if (_uniqueInstance == null)
                {
                    _uniqueInstance = new PRM_DAO();
                }
            }
            return _uniqueInstance;
        }

        private SQLiteConnection _connection = null;
        //private string password = @"123456";
        private string password = @"";


        private PRM_DAO()
        {
        }

        private static string DbPath => SystemConfig.GetInstance().General.DbPath;
        

        public static bool TestDb(string path, string psw)
        {
            SQLiteConnection connection = null;
            try
            {
                connection = new SQLiteConnection($"Data Source={path};Password={psw};Version=3;New=True;Compress=True");
                connection.Open();

                // Create Table
                using (var tr = connection.BeginTransaction())
                {
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = (new ServerOrm()).SQLCreateTable();
                        command.ExecuteNonQuery();
                    }

                    tr.Commit();
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
            finally
            {
                connection?.Dispose();
            }
        }


        private void Open()
        {
            try
            {
                _connection?.Dispose();
                _connection = new SQLiteConnection($"Data Source={DbPath};Password={password};Version=3;New=True;Compress=True");
                // Create Db
                _connection?.Open();

                // Create Table
                using (var tr = _connection.BeginTransaction())
                {
                    using (var command = _connection.CreateCommand())
                    {
                        command.CommandText = (new ServerOrm()).SQLCreateTable();
                        command.ExecuteNonQuery();
                    }
                    tr.Commit();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        private void Close()
        {
            if (_connection?.State == System.Data.ConnectionState.Open)
            {
                _connection?.Close();
            }
        }

        public bool Insert<T>(T data) where T : SimpleOrmBase
        {
            try
            {
                Open();
                return data?.Insert(_connection) ?? false;
            }
            finally
            {
                Close();
            }
        }

        
        public bool Update<T>(T data) where T : SimpleOrmBase
        {
            try
            {
                Open();
                return data?.Update(_connection) ?? false;
            }
            finally
            {
                Close();
            }
        }


        public List<ServerOrm> ListAllServer()
        {
            try
            {
                Open();
                return ServerOrm.ListAll(_connection);
            }
            finally
            {
                Close();
            }
        }

        public void DeleteServer(uint id)
        {
            try
            {
                Open();
                new ServerOrm().Delete(_connection, id);
            }
            finally
            {
                Close();
            }
        }
    }
}
