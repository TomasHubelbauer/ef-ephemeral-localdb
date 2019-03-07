using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace ef_ephemeral_localdb
{
    class Program
    {
        enum Scope
        {
            ExecutingAssemblyLocation,
            ExeuctingAssemblyName,
            ProcessId,
            NewGuid,
        }

        static async Task Main(string[] args)
        {
            // Prepare the database name based on the selected scope
            var name = GetNameFromScope(Scope.ExeuctingAssemblyName);
            
            // Delete the backing database file to prevent reuse which would cause EF Core to fail later on
            DeleteBackingFile(name);

            // Issue a command to create the SQL Server LocalDB instance (reentrant, always succeeds)
            ExecuteSqlLocalDb("create " + name);

            // Use the database
            using (var appDbContext = new AppDbContext(name))
            {
                // Note that this starts the SQL LocalDB instance even though it is created in stopped state
                await appDbContext.Database.EnsureDeletedAsync();
                await appDbContext.Database.EnsureCreatedAsync();
                await appDbContext.Tests.AddAsync(new Test { Name = "test" });
                await appDbContext.SaveChangesAsync();
            }

            // Ensure data persisted across DB context instances
            using (var appDbContext = new AppDbContext(name))
            {
                var test = await appDbContext.Tests.SingleAsync();
                Console.WriteLine(test.Name);
            }

            // Issue commands to stop and delete the SQL Server LocalDB instance (reentrant, always succeeds)
            ExecuteSqlLocalDb("stop " + name);
            ExecuteSqlLocalDb("delete " + name);
            DeleteBackingFile(name);
        }

        static string GetNameFromScope(Scope scope)
        {
            // Decide what the DB name should be based on the configured scope
            // https://docs.microsoft.com/en-us/sql/relational-databases/databases/database-identifiers#rules-for-regular-identifiers
            var name = string.Empty;
            var assembly = Assembly.GetExecutingAssembly();
            switch (scope)
            {
                case Scope.ExecutingAssemblyLocation: {
                    name = assembly.Location;
                    break;
                }
                case Scope.ExeuctingAssemblyName: {
                    name = new AssemblyName(assembly.FullName).Name;
                    break;
                }
                case Scope.ProcessId: {
                    name = Process.GetCurrentProcess().Id.ToString();
                    break;
                }
                case Scope.NewGuid: {
                    name = Guid.NewGuid().ToString("N");
                    break;
                }
                default: throw new Exception("Unexpected scope value");
            }

            // Convert the desired name to a T-SQL identifier by replacing non-alphanumerics characters with an underscore
            return Regex.Replace(name, "[^a-zA-Z0-9]", "_");
        }

        static void ExecuteSqlLocalDb(string arguments)
        {
            // https://docs.microsoft.com/en-us/sql/tools/sqllocaldb-utility#arguments
            var process = new Process();
            process.StartInfo.FileName = "sqllocaldb";
            process.StartInfo.Arguments = arguments;
            process.Start();
            process.WaitForExit();

            // Check the operation succeeded
            if (process.ExitCode != 0)
            {
                throw new Exception("Unexpected exit code");
            }
        }
        
        static void DeleteBackingFile(string name)
        {
            var userDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var mdfFilePath = Path.Combine(userDirectoryPath, name + ".mdf");
            File.Delete(mdfFilePath);
            var ldfFilePath = Path.Combine(userDirectoryPath, name + "_log.ldf");
            File.Delete(ldfFilePath);
        }

        public class AppDbContext: DbContext
        {
            private readonly string databaseName;

            public DbSet<Test> Tests { get; set; }

            public AppDbContext(string databaseName)
            {
                this.databaseName = databaseName;
            }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
            {
                optionsBuilder.UseSqlServer($@"Server=(localdb)\{databaseName};Database={databaseName};");
            }
        }

        public class Test
        {
            public Guid Id { get; set; }
            public String Name { get; set; }
        }
    }
}
