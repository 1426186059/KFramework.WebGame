using Library.MirDB;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using Library.SystemModels;

namespace MirDB
{
    public sealed class Session
    {
        public const string Extension = @".db";
        public const string TempExtension = @".TMP";
        public const string CompressExtension = @".gz";
        public const string SystemDatabaseInfoName = "System";
        private static readonly Regex SystemVersionRegex = new Regex(@"^(?<year>\d{4})\.(?<month>\d{2})\.(?<day>\d{2})\.(?<count>\d+)$", RegexOptions.Compiled);

        public string Root { get; }
        public SessionMode Mode { get; }

        /// <summary>
        /// 无文件系统环境（如浏览器 WASM）用来按需取数据库字节的钩子：传入类似 "X:\...\Data\System.db" 的路径，
        /// 由调用方映射到 HTTP URL（如 MyRes/Data/System.db）；返回 null 表示文件不存在。
        /// 未设置时回退到 System.IO.File（桌面 / 服务器）。
        /// </summary>
        public static Func<string, byte[]> DatabaseBytesLoader { get; set; }

        public bool BackUp { get; set; } = true;
        public int BackUpDelay { get; set; }
        private string BackupRoot { get; }

        public string SystemPath => Root + "System" + Extension;
        public string SystemBackupPath => BackupRoot + @"System\";
        public byte[] SystemHeader;
        public bool SystemDatabaseExists { get; private set; }
        public string SystemDatabaseVersion { get; private set; }

        public string UsersPath => Root + "Users" + Extension;
        public string UsersBackupPath => BackupRoot + @"Users\";

        public Assembly[] Assemblies { get; private set; }

        public byte[] UsersHeader;
        private bool SystemVersionPending;
        private bool UsersChangesPending;

        //internal ConcurrentQueue<DBObject> KeyedObjects = new ConcurrentQueue<DBObject>();
        internal Dictionary<Type, DBRelationship> Relationships = new Dictionary<Type, DBRelationship>();

        private Dictionary<Type, ADBCollection> Collections;

        public Session(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                throw new ArgumentNullException(nameof(connectionString), "Database:DBConnStr is empty");

            var args = connectionString.Split(';').Select(x => x.Split(new char[] { '=' }, 2)).ToDictionary(x => x[0].ToUpperInvariant(), x => x[1]);

            if (!args.ContainsKey("MODE") || !args.ContainsKey("ROOT") || !args.ContainsKey("BACKUP"))
                throw new ArgumentException($"Connection string is not valid");

            if (!Enum.TryParse(args["MODE"], out SessionMode mode))
                throw new ArgumentException($"{args["MODE"]} is not valid option for Mode");

            if (args.ContainsKey("BACKUPDELAY"))
            {
                if (!int.TryParse(args["BACKUPDELAY"], out int backupDelay))
                    throw new ArgumentException($"{args["BACKUPDELAY"]} is not valid number");
                BackUpDelay = backupDelay;
            }

            Root = args["ROOT"];
            BackupRoot = args["BACKUP"];
            Mode = mode;
        }

        public Session(SessionMode mode, string root = @".\Database\", string backup = @".\Backup\")
        {
            Root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, root));
            BackupRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, backup));

            Mode = mode;
        }

        /// <summary>
        /// 初始化（对齐 Unity 移植版）：返回 IEnumerator，由协程调度器当作嵌套协程逐表推进。
        /// 内部 yield return InitializeSystem() / InitializeUsers()（均为 IEnumerator），
        /// 每个 DB 表 yield return null（等价于 Unity 的「等一帧」），把 81 张表分摊到多帧，
        /// 避免单线程 WASM 一次性解析全部 DB 表冻结主线程导致心跳超时。
        /// 所有表加载完成后，统一执行 ConsumeKeys / OnLoaded（与 Unity 移植版一致）。
        /// </summary>
        public System.Collections.IEnumerator Initialize(params Assembly[] assemblies)
        {
            Assemblies = assemblies;

            if (DatabaseBytesLoader == null)
            {
                if (!Directory.Exists(Root))
                    Directory.CreateDirectory(Root);
            }

            Collections = new Dictionary<Type, ADBCollection>();

            List<Type> types = assemblies
                .Select(x => x.GetTypes())
                .SelectMany(x => x)
                .ToList();

            Type collectionType = typeof(DBCollection<>);

            foreach (Type type in types)
            {
                if (!type.IsSubclassOf(typeof(DBObject))) continue;

                Collections[type] = (ADBCollection)Activator.CreateInstance(collectionType.MakeGenericType(type), this);
            }

            PrintTool.Write("DB", $"Initialize: 发现 {Collections.Count} 张表, 来自 {assemblies.Length} 个程序集");

            Stopwatch dbSw = Stopwatch.StartNew();

            yield return InitializeSystem();
            if ((Mode & SessionMode.Users) == SessionMode.Users)
                yield return InitializeUsers();

            Stopwatch phaseSw = Stopwatch.StartNew();
            foreach (KeyValuePair<Type, DBRelationship> v in Relationships)
                v.Value.ConsumeKeys(this);
            PrintTool.Write("DB", $"阶段[ConsumeKeys] 耗时 {phaseSw.ElapsedMilliseconds}ms");

            Relationships = null;

            phaseSw.Restart();
            foreach (KeyValuePair<Type, ADBCollection> pair in Collections)
                pair.Value.OnLoaded();
            PrintTool.Write("DB", $"阶段[OnLoaded] 耗时 {phaseSw.ElapsedMilliseconds}ms");

            bool migrationPending = Collections.Values.Any(x => !x.ReadOnly && x.HasMigrations() &&
                (x.IsSystemData ? (Mode & SessionMode.System) == SessionMode.System : (Mode & SessionMode.Users) == SessionMode.Users));

            if (migrationPending)
                Save(true);

            dbSw.Stop();
            PrintTool.Write("DB", $"Initialize 完成: 共 {Collections.Count} 张表, 耗时 {dbSw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 系统表加载（对齐 Unity 移植版）：返回 IEnumerator，每个表 yield return null 分摊到一帧。
        /// 浏览器环境经 DatabaseBytesLoader（LoadFileBytes）取字节；桌面端走 System.IO.File。
        /// </summary>
        private System.Collections.IEnumerator InitializeSystem()
        {
            List<DBMapping> mappings = new List<DBMapping>();
            if ((Mode & SessionMode.System) == SessionMode.System)
            {
                foreach (KeyValuePair<Type, ADBCollection> pair in Collections)
                {
                    if (!pair.Value.IsSystemData) continue;

                    mappings.Add(pair.Value.Mapping);
                }

                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(mappings.Count);
                    foreach (DBMapping mapping in mappings)
                        mapping.Save(writer);

                    SystemHeader = stream.ToArray();
                }

                mappings.Clear();
            }

            byte[] systemBytes = LoadFileBytes(SystemPath, out bool systemExists);
            SystemDatabaseExists = systemExists;

            if (!SystemDatabaseExists) yield break;

            Stopwatch sw = Stopwatch.StartNew();
            using (BinaryReader reader = Library.Encryption.GetReader(new MemoryStream(systemBytes)))
            {
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                    mappings.Add(new DBMapping(Assemblies, reader));

                int loaded = 0;
                foreach (DBMapping mapping in mappings)
                {
                    byte[] data = reader.ReadBytes(reader.ReadInt32());

                    ADBCollection value;
                    if (mapping.Type == null || !Collections.TryGetValue(mapping.Type, out value)) continue;

                    value.Load(data, mapping);
                    PrintTool.Write("DB", $"加载完成 (系统 {++loaded}): {(mapping.Type?.Name ?? "未知类型")}");
                    yield return null; // 让出：等价于 Unity 的「等一帧」，下一帧再加载下一张表
                }
            }
            PrintTool.Write("DB", $"阶段[系统表加载] 耗时 {sw.ElapsedMilliseconds}ms");

            SystemDatabaseVersion = GetSystemDatabaseInfo()?.Version;

            if (string.IsNullOrWhiteSpace(SystemDatabaseVersion) && (Mode & SessionMode.System) == SessionMode.System)
            {
                SetSystemVersion(GetNextSystemVersion(SystemDatabaseVersion, DateTime.Now));
                SystemVersionPending = true;
            }
        }

        /// <summary>
        /// 用户表加载（对齐 Unity 移植版）：返回 IEnumerator，每个表 yield return null 分摊到一帧。
        /// </summary>
        private System.Collections.IEnumerator InitializeUsers()
        {
            List<DBMapping> mappings = new List<DBMapping>();

            foreach (KeyValuePair<Type, ADBCollection> pair in Collections)
            {
                if (pair.Value.IsSystemData) continue;

                mappings.Add(pair.Value.Mapping);
            }

            using (MemoryStream stream = new MemoryStream())
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(mappings.Count);
                foreach (DBMapping mapping in mappings)
                    mapping.Save(writer);

                UsersHeader = stream.ToArray();
            }
            mappings.Clear();

            byte[] usersBytes = LoadFileBytes(UsersPath, out bool usersExists);
            if (!usersExists) yield break;

            Stopwatch sw = Stopwatch.StartNew();
            using (BinaryReader reader = Library.Encryption.GetReader(new MemoryStream(usersBytes)))
            {
                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                    mappings.Add(new DBMapping(Assemblies, reader));

                int loaded = 0;
                foreach (DBMapping mapping in mappings)
                {
                    byte[] data = reader.ReadBytes(reader.ReadInt32());

                    ADBCollection value;
                    if (mapping.Type == null || !Collections.TryGetValue(mapping.Type, out value)) continue;

                    value.Load(data, mapping);
                    PrintTool.Write("DB", $"加载完成 (用户 {++loaded}): {(mapping.Type?.Name ?? "未知类型")}");
                    yield return null; // 让出：等价于 Unity 的「等一帧」，下一帧再加载下一张表
                }
            }
            PrintTool.Write("DB", $"阶段[用户表加载] 耗时 {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 读取数据库文件全部字节。浏览器（DatabaseBytesLoader 已设置）经 HTTP 取；否则走 System.IO.File。
        /// 返回值与 exists 语义一致：不存在时返回 null 且 exists=false。
        /// </summary>
        private byte[] LoadFileBytes(string path, out bool exists)
        {
            if (DatabaseBytesLoader != null)
            {
                byte[] data = DatabaseBytesLoader(path);
                exists = data != null && data.Length > 0;
                return data;
            }

            exists = File.Exists(path);
            if (!exists) return null;

            using FileStream fs = File.OpenRead(path);
            using MemoryStream ms = new MemoryStream();
            fs.CopyTo(ms);
            return ms.ToArray();
        }

        public void Save(bool commit)
        {
            if (DatabaseBytesLoader != null) return; // 浏览器只读：配置库不回写

            bool systemChanged = HasSystemChanges();
            bool usersChanged = HasUserChanges();
            bool versionAlreadyPending = SystemVersionPending;
            SystemVersionPending |= systemChanged;
            UsersChangesPending |= usersChanged;

            if ((Mode & SessionMode.System) == SessionMode.System && ((systemChanged && !versionAlreadyPending) || string.IsNullOrWhiteSpace(SystemDatabaseVersion)))
            {
                BumpSystemVersion();
                SystemVersionPending = true;
            }

            Parallel.ForEach(Collections, x =>
            {
                if (x.Value.IsSystemData ? SystemVersionPending : UsersChangesPending)
                    x.Value.SaveObjects();
            });

            if (commit)
                Commit(SystemVersionPending, UsersChangesPending);
        }
        public void Commit()
        {
            if (DatabaseBytesLoader != null) return; // 浏览器只读

            if ((Mode & SessionMode.System) == SessionMode.System && string.IsNullOrWhiteSpace(SystemDatabaseVersion))
            {
                BumpSystemVersion();
                Parallel.ForEach(Collections, x => x.Value.SaveObjects());
                SystemVersionPending = true;
            }

            Commit(HasSystemChanges() || SystemVersionPending, HasUserChanges() || UsersChangesPending);
        }
        private void Commit(bool systemChanged, bool usersChanged)
        {
            SaveSystem(systemChanged);
            SaveUsers(usersChanged);
            SystemVersionPending = false;
            UsersChangesPending = false;
        }

        private void SaveSystem(bool systemChanged)
        {
            if ((Mode & SessionMode.System) != SessionMode.System || !systemChanged) return;

            if (!Directory.Exists(Root))
                Directory.CreateDirectory(Root);

            using (BinaryWriter writer = Library.Encryption.GetWriter(File.Create(SystemPath + TempExtension)))
            {
                writer.Write(SystemHeader);

                foreach (KeyValuePair<Type, ADBCollection> pair in Collections)
                {
                    if (!pair.Value.IsSystemData) continue;
                    byte[] data = pair.Value.GetSaveData();

                    writer.Write(data.Length);
                    writer.Write(data);
                }
            }

            if (BackUp && !Directory.Exists(SystemBackupPath))
                Directory.CreateDirectory(SystemBackupPath);

            if (File.Exists(SystemPath))
            {
                if (BackUp)
                {
                    using (FileStream sourceStream = File.OpenRead(SystemPath))
                    using (FileStream destStream = File.Create(SystemBackupPath + "System " + ToBackUpFileName(DateTime.UtcNow) + Extension + CompressExtension))
                    using (GZipStream compress = new GZipStream(destStream, CompressionMode.Compress))
                        sourceStream.CopyTo(compress);
                }

                File.Delete(SystemPath);
            }

            File.Move(SystemPath + TempExtension, SystemPath);
        }
        private void SaveUsers(bool usersChanged)
        {
            if ((Mode & SessionMode.Users) != SessionMode.Users || !usersChanged) return;

            if (!Directory.Exists(Root))
                Directory.CreateDirectory(Root);

            using (BinaryWriter writer = Library.Encryption.GetWriter(File.Create(UsersPath + TempExtension)))
            {
                writer.Write(UsersHeader);

                foreach (KeyValuePair<Type, ADBCollection> pair in Collections)
                {
                    if (pair.Value.IsSystemData) continue;

                    byte[] data = pair.Value.GetSaveData();

                    writer.Write(data.Length);
                    writer.Write(data);
                }
            }
            if (BackUp && !Directory.Exists(UsersBackupPath))
                Directory.CreateDirectory(UsersBackupPath);

            if (File.Exists(UsersPath))
            {
                if (BackUp)
                {
                    using (FileStream sourceStream = File.OpenRead(UsersPath))
                    using (FileStream destStream = File.Create(UsersBackupPath + "Users " + ToBackUpFileName(DateTime.UtcNow) + Extension + CompressExtension))
                    using (GZipStream compress = new GZipStream(destStream, CompressionMode.Compress))
                        sourceStream.CopyTo(compress);
                }

                File.Delete(UsersPath);
            }

            File.Move(UsersPath + TempExtension, UsersPath);
        }

        public DBCollection<T> GetCollection<T>() where T : DBObject, new()
        {
            return (DBCollection<T>)Collections[typeof(T)];
        }
        public ADBCollection GetCollection(Type type)
        {
            return Collections[type];
        }
        internal DBObject GetObject(Type type, int index)
        {
            return Collections[type].GetObjectByIndex(index);
        }
        public DBObject GetObject(Type type, string fieldName, object value)
        {
            return Collections[type].GetObjectbyFieldName(fieldName, value);
        }

        public T InsertObjectAfter<T>(int insertAfterIndex) where T : DBObject, new()
        {
            DBCollection<T> collection = GetCollection<T>();

            if (insertAfterIndex < 0 || insertAfterIndex > collection.Index)
                throw new ArgumentOutOfRangeException(nameof(insertAfterIndex), $"Value must be between 0 and {collection.Index}");

            List<DBObject> shiftedObjects = new List<DBObject>();

            for (int i = collection.Binding.Count - 1; i >= 0; i--)
            {
                T ob = collection.Binding[i];

                if (ob.Index <= insertAfterIndex) continue;

                shiftedObjects.Add(ob);

                int oldIndex = ob.Index;
                ob.Index = oldIndex + 1;
                ob.OnChanged(oldIndex, ob.Index, nameof(DBObject.Index));
            }

            collection.Index++;

            int targetIndex = insertAfterIndex + 1;
            int insertPosition = 0;

            while (insertPosition < collection.Binding.Count && collection.Binding[insertPosition].Index < targetIndex)
                insertPosition++;

            T newObject = new T
            {
                Collection = collection,
                Index = targetIndex
            };

            newObject.OnCreated();

            collection.Binding.Insert(insertPosition, newObject);

            if (shiftedObjects.Count > 0)
                MarkReferencesModified(shiftedObjects);

            return newObject;
        }

        private void MarkReferencesModified(List<DBObject> updatedObjects)
        {
            HashSet<DBObject> changedObjects = [.. updatedObjects];

            foreach (ADBCollection collection in Collections.Values)
            {
                foreach (DBObject ob in collection.GetObjects())
                {
                    foreach (DBValue value in collection.Mapping.Properties)
                    {
                        if (value.Property == null) continue;
                        if (!value.Property.PropertyType.IsSubclassOf(typeof(DBObject))) continue;

                        if (value.Property.GetValue(ob) is DBObject link && changedObjects.Contains(link))
                            ob.OnChanged(link, link, value.Property.Name);
                    }
                }
            }
        }

        internal T CreateObject<T>() where T : DBObject, new()
        {
            return (T)Collections[typeof(T)].CreateObject();
        }

        private static string ToFileName(DateTime time)
        {
            return $"{time.Year:0000}-{time.Month:00}-{time.Day:00} {time.Hour:00}-{time.Minute:00}";
        }

        private bool HasSystemChanges()
        {
            if ((Mode & SessionMode.System) != SessionMode.System) return false;

            foreach (KeyValuePair<Type, ADBCollection> pair in Collections)
            {
                if (!pair.Value.IsSystemData) continue;
                if (pair.Value.HasChanges()) return true;
            }

            return false;
        }

        private bool HasUserChanges()
        {
            if ((Mode & SessionMode.Users) != SessionMode.Users) return false;

            foreach (KeyValuePair<Type, ADBCollection> pair in Collections)
            {
                if (pair.Value.IsSystemData) continue;
                if (pair.Value.HasChanges()) return true;
            }

            return false;
        }

        public string RefreshSystemVersion()
        {
            SystemDatabaseExists = File.Exists(SystemPath);
            SystemDatabaseVersion = ReadSystemVersionFromFile();

            return SystemDatabaseVersion;
        }

        public void BumpSystemVersion()
        {
            SetSystemVersion(GetNextSystemVersion(SystemDatabaseVersion, DateTime.Now));
        }

        private SystemDatabaseInfo GetSystemDatabaseInfo()
        {
            if (Collections == null) return null;
            if (!Collections.TryGetValue(typeof(SystemDatabaseInfo), out ADBCollection collection)) return null;

            return collection.GetObjects().OfType<SystemDatabaseInfo>().FirstOrDefault(x => x.Name == SystemDatabaseInfoName);
        }

        private void SetSystemVersion(string version)
        {
            if (!Collections.TryGetValue(typeof(SystemDatabaseInfo), out ADBCollection collection)) return;

            SystemDatabaseInfo info = collection.GetObjects().OfType<SystemDatabaseInfo>().FirstOrDefault(x => x.Name == SystemDatabaseInfoName);

            if (info == null)
            {
                info = (SystemDatabaseInfo)collection.CreateObject();
                info.Name = SystemDatabaseInfoName;
            }

            info.Version = version;
            SystemDatabaseVersion = version;
        }

        private string ReadSystemVersionFromFile()
        {
            if (!SystemDatabaseExists || Assemblies == null) return null;

            try
            {
                Session session = new Session(SessionMode.None, Root, BackupRoot) { BackUp = false };
                // Initialize 返回 IEnumerator（供协程嵌套），同步消费需手动 MoveNext 驱动到结束。
                System.Collections.IEnumerator initEnum = session.Initialize(Assemblies);
                while (initEnum.MoveNext()) { }

                return session.GetSystemDatabaseInfo()?.Version;
            }
            catch
            {
                return null;
            }
        }

        private static string GetNextSystemVersion(string currentVersion, DateTime time)
        {
            int count = 1;
            Match match = string.IsNullOrWhiteSpace(currentVersion) ? Match.Empty : SystemVersionRegex.Match(currentVersion);

            if (match.Success &&
                int.Parse(match.Groups["year"].Value) == time.Year &&
                int.Parse(match.Groups["month"].Value) == time.Month &&
                int.Parse(match.Groups["day"].Value) == time.Day)
            {
                count = int.Parse(match.Groups["count"].Value) + 1;
            }

            return $"{time.Year:0000}.{time.Month:00}.{time.Day:00}.{count}";
        }

        public string ToBackUpFileName(DateTime time)
        {
            if (BackUpDelay == 0)
                return ToFileName(time);

            time = new DateTime(time.Ticks - (time.Ticks % (BackUpDelay * TimeSpan.TicksPerMinute)));

            return $"{time.Year:0000}-{time.Month:00}-{time.Day:00} {time.Hour:00}-{time.Minute:00}";
        }

        internal void Delete(DBObject ob)
        {
            if (ob.IsDeleted) return;

            Collections[ob.ThisType].Delete(ob);

            ob.OnDeleted();

            PropertyInfo[] properties = ob.ThisType.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.SetProperty);

            //Remove Internal Reference
            foreach (PropertyInfo property in properties)
            {
                AssociationAttribute link = property.GetCustomAttribute<AssociationAttribute>();

                if (property.PropertyType.IsSubclassOf(typeof(DBObject)))
                {
                    if (link != null && link.Aggregate)
                    {
                        DBObject tempOb = (DBObject)property.GetValue(ob);

                        tempOb?.Delete();
                        continue;
                    }

                    property.SetValue(ob, null);
                    continue;
                }

                if (!property.PropertyType.IsGenericType || property.PropertyType.GetGenericTypeDefinition() != typeof(DBBindingList<>)) continue;

                IBindingList list = (IBindingList)property.GetValue(ob);

                if (link != null && link.Aggregate)
                {
                    for (int i = list.Count - 1; i >= 0; i--)
                        ((DBObject)list[i]).Delete();
                    continue;
                }

                list.Clear();
            }

        }
        internal void FastDelete(DBObject ob)
        {
            if (ob.IsDeleted) return;

            ob.IsTemporary = true;

            ob.OnDeleted();
        }
    }
}
