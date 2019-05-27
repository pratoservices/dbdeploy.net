namespace Net.Sf.Dbdeploy.Database
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.Data;
    using System.Data.SqlClient;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using global::Dbdeploy.Powershell;
    using Net.Sf.Dbdeploy.Appliers;
    using Net.Sf.Dbdeploy.Configuration;
    using Net.Sf.Dbdeploy.Database.SqlCmd;
    using Net.Sf.Dbdeploy.Exceptions;
    using Net.Sf.Dbdeploy.Scripts;
    using NUnit.Framework;

    [Category("MSSQL"), Category("DbIntegration")]
    public class MsSqlDatabaseSchemaVersionManagerTest : AbstractDatabaseSchemaVersionManagerTest
    {
        private const string DBMS = "mssql";
        private const string FOLDER = "Scripts";
        private static string _connectionString;

        private readonly string[] CHANGELOG_TABLE_DOES_NOT_EXIST_MESSAGES = new[]
        {
            "No table found with name 'ChangeLog'.",
        };

        protected override string[] ChangelogTableDoesNotExistMessages
        {
            get { return CHANGELOG_TABLE_DOES_NOT_EXIST_MESSAGES; }
        }

        protected override string ConnectionString
        {
            get
            {
                if (_connectionString == null)
                {
                    _connectionString = ConfigurationManager.AppSettings["ConnString-" + Environment.MachineName]
                                        ?? ConfigurationManager.AppSettings["ConnString"];
                }
                return _connectionString;
            }
        }

        protected override string Dbms
        {
            get { return DBMS; }
        }

        protected override string Folder
        {
            get { return FOLDER; }
        }

        [Test]
        public void ShouldNotThrowExceptionIfAllPreviousScriptsAreCompleted()
        {
            this.EnsureTableDoesNotExist();
            CreateTable();
            InsertRowIntoTable(3);
            var changeNumbers = new List<ChangeEntry>(databaseSchemaVersion.GetAppliedChanges());

            Assert.AreEqual(1, changeNumbers.Count);
            Assert.AreEqual("Scripts/3", changeNumbers[0].UniqueKey);
        }

        [Test]
        public override void TestCanRetrieveSchemaVersionFromDatabase()
        {
            base.TestCanRetrieveSchemaVersionFromDatabase();
        }

        [Test]
        public void TestDoesNotRunSecondScriptIfFirstScriptFails()
        {
            this.EnsureTableDoesNotExist("TableWeWillUse");
            this.EnsureTableDoesNotExist(TableName);

            var factory = new DbmsFactory(this.Dbms, this.ConnectionString);
            var dbmsSyntax = factory.CreateDbmsSyntax();

            var output = new StringBuilder();

            var applier = new TemplateBasedApplier(
                new StringWriter(output),
                dbmsSyntax,
                TableName,
                ";",
                DbDeployDefaults.DelimiterType,
                DbDeployDefaults.TemplateDirectory);

            applier.Apply(new ChangeScript[]
            {
                new StubChangeScript(1, "1.test.sql", "INSERT INTO TableWeWillUse VALUES (1);"),
                new StubChangeScript(2, "2.test.sql", "CREATE TABLE dbo.TableWeWillUse (Id int NULL);"),
            }, createChangeLogTable: true);

            using (var sqlExecuter = new SqlCmdExecutor(DbDeployDefaults.WorkingDirectory, this.ConnectionString))
            {
                var cmdOutput = new StringBuilder();
                sqlExecuter.ExecuteString(output.ToString(), cmdOutput);
            }
            this.AssertTableDoesNotExist("TableWeWillUse");
        }

        [Test]
        public override void TestReturnsNoAppliedChangesWhenDatabaseTableDoesNotExist()
        {
            base.TestReturnsNoAppliedChangesWhenDatabaseTableDoesNotExist();
        }

        [Test]
        public override void TestShouldCreateChangeLogTableWhenToldToDoSo()
        {
            base.TestShouldCreateChangeLogTableWhenToldToDoSo();
        }

        /// <summary>
        /// Tests that <see cref="DatabaseSchemaVersionManager" /> can create a change log table under a specified schema.
        /// </summary>
        [Test]
        public void TestShouldHandleCreatingChangeLogTableWithSchema()
        {
            this.EnsureTableDoesNotExist("log.Installs");

            var factory = new DbmsFactory(this.Dbms, this.ConnectionString);
            var executer = new QueryExecuter(factory);
            var databaseSchemaManager = new DatabaseSchemaVersionManager(executer, factory.CreateDbmsSyntax(), "log.Installs");

            var applier = new DirectToDbApplier(executer, databaseSchemaManager, new QueryStatementSplitter(),
                factory.CreateDbmsSyntax(), "log.Installs", new NullWriter());

            applier.Apply(new ChangeScript[] { }, createChangeLogTable: true);

            this.AssertTableExists("log.Installs");
        }

        [Test]
        public override void TestShouldReturnEmptySetWhenTableHasNoRows()
        {
            base.TestShouldReturnEmptySetWhenTableHasNoRows();
        }

        /// <summary>
        /// Ensures the table does not exist.
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        protected override void EnsureTableDoesNotExist(string tableName)
        {
            var syntax = new MsSqlDbmsSyntax();
            var tableInfo = syntax.GetTableInfo(tableName);
            this.ExecuteSql(string.Format(
                CultureInfo.InvariantCulture,
@"IF (EXISTS (SELECT *
    FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = '{0}'
    AND  TABLE_NAME = '{1}'))
BEGIN
    DROP Table {0}.{1}
END",
                tableInfo.Schema, tableInfo.TableName));
        }

        protected override IDbConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        protected override void InsertRowIntoTable(int i)
        {
            this.ExecuteSql("INSERT INTO " + TableName
                       + " (Folder, ScriptNumber, StartDate, CompleteDate, AppliedBy, ScriptName, ScriptStatus, ScriptOutput) VALUES ( "
                       + "'" + FOLDER + "', " + i
                       + ", getdate(), getdate(), user_name(), 'Unit test', 1, '')");
        }
    }
}