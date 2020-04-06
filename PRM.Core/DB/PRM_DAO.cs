using System;
using System.Collections.Generic;
using System.Data.SQLite;

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
        private static string databasePath = @"test.db";
        //private string password = @"123456";
        private string password = @"";


        private PRM_DAO()
        {
            InitAndOpenDb();
        }

        public static string DbPath => databasePath;

        public bool InitAndOpenDb()
        {
            try
            {
                // Create Db
                _connection = new SQLiteConnection($"Data Source={databasePath};Password={password};Version=3;New=True;Compress=True");
                Open();

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
            finally
            {
                Close();
            }

            return true;
        }

        private void Open()
        {
            if (_connection?.State == System.Data.ConnectionState.Closed)
            {
                _connection?.Open();
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
