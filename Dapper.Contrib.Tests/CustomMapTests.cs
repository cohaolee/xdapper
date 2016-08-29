using System.Data;
using System.Data.SqlServerCe;
using System.IO;
using System.Linq;
using System.Reflection;
using Dapper.Contrib.Extensions;
using System.Collections.Generic;
using System;
using Dapper;
using System.Data.SqlClient;


namespace Dapper.Contrib.Tests
{
    //public interface IUser
    //{
    //    [Key]
    //    int Id { get; set; }
    //    string Name { get; set; }
    //    int Age { get; set; }
    //}

    //public class User : IUser
    //{
    //    public int Id { get; set; }
    //    public string Name { get; set; }
    //    public int Age { get; set; }
    //}

    //[Table("Automobiles")]
    //public class Car
    //{
    //    public int Id { get; set; }
    //    public string Name { get; set; }
    //}

    //[Table("Results")]
    //public class Result
    //{
    //    public int Id { get; set; }
    //    public string Name { get; set; }
    //    public int Order { get; set; }
    //}

    [Table("sys_user")]
    public class SysUser
    {
        [Column("user_id")]
        public int Id { get; set; }
        public string UserName { get; set; }
        public int Age { get; set; }

    }

    public class CustomMapTests
    {


        public const string ConnectionString = "Data Source=.; Initial Catalog=tempdb; User ID=sa; Password=admin888;",
   OleDbConnectionString = "Provider=SQLOLEDB;Data Source=.;Initial Catalog=tempdb;Integrated Security=SSPI";

        public static SqlConnection GetOpenConnection()
        {
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            return connection;
        }


        public void Init()
        {



            var createSql = @"
if exists (select 1
            from  sysobjects
           where  id = object_id('sys_user')
            and   type = 'U')
   drop table sys_user;
                create table sys_user (user_id int identity, user_name varchar(20), age int)
                insert sys_user values('Sam', 5)
                insert sys_user values('I am', 6)
";
            GetOpenConnection().Execute(createSql);
        }

        public void Clear()
        {
            GetOpenConnection().Execute("drop table sys_user");
        }


        public void TestSimpleGet()
        {
            Init();
            try
            {
                using (var connection = GetOpenConnection())
                {
                    var id = connection.Insert(new SysUser { UserName = "Adama", Age = 10 });
                    var user = connection.Get<SysUser>(id);
                    user.Id.IsEqualTo((int)id);
                    user.UserName.IsEqualTo("Adama");
                    connection.Delete(user);
                }
            }
            finally
            {
                Clear();
            }
        }

        public void InsertGetUpdate()
        {
            using (var connection = GetOpenConnection())
            {
                connection.Get<User>(3).IsNull();

                var id = connection.Insert(new User { Name = "Adam", Age = 10 });

                //get a user with "isdirty" tracking
                var user = connection.Get<IUser>(id);
                user.Name.IsEqualTo("Adam");
                connection.Update(user).IsEqualTo(false);    //returns false if not updated, based on tracking
                user.Name = "Bob";
                connection.Update(user).IsEqualTo(true);    //returns true if updated, based on tracking
                user = connection.Get<IUser>(id);
                user.Name.IsEqualTo("Bob");

                //get a user with no tracking
                var notrackedUser = connection.Get<User>(id);
                notrackedUser.Name.IsEqualTo("Bob");
                connection.Update(notrackedUser).IsEqualTo(true);   //returns true, even though user was not changed
                notrackedUser.Name = "Cecil";
                connection.Update(notrackedUser).IsEqualTo(true);
                connection.Get<User>(id).Name.IsEqualTo("Cecil");

                connection.Query<User>("select * from Users").Count().IsEqualTo(1);
                connection.Delete(user).IsEqualTo(true);
                connection.Query<User>("select * from Users").Count().IsEqualTo(0);

                connection.Update(notrackedUser).IsEqualTo(false);   //returns false, user not found
            }
        }

        public void InsertCheckKey()
        {
            using (var connection = GetOpenConnection())
            {
                connection.Get<IUser>(3).IsNull();
                User user = new User { Name = "Adamb", Age = 10 };
                int id = (int)connection.Insert(user);
                user.Id.IsEqualTo(id);
            }
        }

        public void BuilderSelectClause()
        {
            using (var connection = GetOpenConnection())
            {
                var rand = new Random(8675309);
                var data = new List<User>();
                for (int i = 0; i < 100; i++)
                {
                    var nU = new User { Age = rand.Next(70), Id = i, Name = Guid.NewGuid().ToString() };
                    data.Add(nU);
                    nU.Id = (int)connection.Insert<User>(nU);
                }

                var builder = new SqlBuilder();
                var justId = builder.AddTemplate("SELECT /**select**/ FROM Users");
                var all = builder.AddTemplate("SELECT Name, /**select**/, Age FROM Users");

                builder.Select("Id");

                var ids = connection.Query<int>(justId.RawSql, justId.Parameters);
                var users = connection.Query<User>(all.RawSql, all.Parameters);

                foreach (var u in data)
                {
                    if (!ids.Any(i => u.Id == i)) throw new Exception("Missing ids in select");
                    if (!users.Any(a => a.Id == u.Id && a.Name == u.Name && a.Age == u.Age)) throw new Exception("Missing users in select");
                }
            }
        }

        public void BuilderTemplateWOComposition()
        {
            var builder = new SqlBuilder();
            var template = builder.AddTemplate("SELECT COUNT(*) FROM Users WHERE Age = @age", new { age = 5 });

            if (template.RawSql == null) throw new Exception("RawSql null");
            if (template.Parameters == null) throw new Exception("Parameters null");

            using (var connection = GetOpenConnection())
            {
                connection.Insert(new User { Age = 5, Name = "Testy McTestington" });

                if (connection.Query<int>(template.RawSql, template.Parameters).Single() != 1)
                    throw new Exception("Query failed");
            }
        }

        public void InsertFieldWithReservedName()
        {
            using (var conneciton = GetOpenConnection())
            {
                var id = conneciton.Insert(new Result() { Name = "Adam", Order = 1 });

                var result = conneciton.Get<Result>(id);
                result.Order.IsEqualTo(1);
            }

        }

        public void DeleteAll()
        {
            using (var connection = GetOpenConnection())
            {
                var id1 = connection.Insert(new User() { Name = "Alice", Age = 32 });
                var id2 = connection.Insert(new User() { Name = "Bob", Age = 33 });
                connection.DeleteAll<User>();
                connection.Get<User>(id1).IsNull();
                connection.Get<User>(id2).IsNull();
            }
        }

    }
}
