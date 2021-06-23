using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace X4.Infrastructure
{
    public class GameFileManager
    {

        public Dictionary<string, List<DataFile>> DataFiles { get; protected set; } = new Dictionary<string, List<DataFile>>();

        public List<DataFile> LoadFailedFiles { get; set; } = new List<DataFile>();
        public class LoadEventArgs
        {
            public string CurrentDirectory { get; set; }
            public string CurrentFileName { get; set; }
            public int Total { get; set; }
            public int Current { get; set; }
        }
        public delegate void LoadEventHandler(object sender, LoadEventArgs e);
        public static event LoadEventHandler LoadEvent;

        public string GameRootPath { get; protected set; }
        public static GameFileManager Load(string gameRootPath)
        {
            if (gameRootPath is null || !Directory.Exists(gameRootPath) || !gameRootPath.Contains("X4 Foundations"))
            {
                throw new IOException("加载目录不存在或没有正确选择游戏目录");
            }

            var xmlDoc = new GameFileManager() { GameRootPath = gameRootPath };

            //加载根目录
            var catFiles = Directory.GetFiles(gameRootPath, "*.cat", SearchOption.TopDirectoryOnly).Where(it => !it.Contains("_sig"));
            int catCount = catFiles.Count();
            int currentCatIndex = 1;
            xmlDoc.DataFiles.Add("root", new List<DataFile>());

            foreach (string catFilePath in catFiles)
            {
                xmlDoc.DataFiles["root"].AddRange(CatDataFile.Load(catFilePath));
                LoadEvent?.Invoke(xmlDoc, new LoadEventArgs { CurrentDirectory = "root", CurrentFileName = Path.GetFileName(catFilePath), Current = currentCatIndex++, Total = catCount });
            }


            //加载mod目录
            string extensionsPath = Path.Combine(gameRootPath, "extensions");
            var extensionsDirectoryArr = Directory.GetDirectories(extensionsPath).Where(it => File.Exists(Path.Combine(it, "content.xml")));

            int extensionsCount = extensionsDirectoryArr.Count();
            int currentExtensionsIndex = 1;

            List<ExtensionsMod> extensionsMod = new List<ExtensionsMod>();

            foreach (var extensionsDirectory in extensionsDirectoryArr)
            {
                List<DataFile> files = new List<DataFile>();
                catFiles = Directory.GetFiles(extensionsDirectory, "ext_??.cat", SearchOption.TopDirectoryOnly);//.Where(it => !it.Contains("_sig"));
                //如果发现cat包，优先使用该包
                if (catFiles.Count() > 0)
                {
                    foreach (string catFilePath in catFiles)
                    {
                        files.AddRange(CatDataFile.Load(catFilePath));
                    }
                }
                else
                {
                    var xmlFiles = Directory.GetFiles(extensionsDirectory, "*.xml", SearchOption.AllDirectories).Where(it => !it.Contains("content.xml"));
                    if (xmlFiles.Count() > 0)
                    {
                        foreach (var xmlFile in xmlFiles)
                        {
                            files.Add(OriginalDataFile.Load(xmlFile));
                        }
                    }
                }

                XDocument contentXml = XDocument.Load(Path.Combine(extensionsDirectory, "content.xml"));
                extensionsMod.Add(new ExtensionsMod { ContentXml = contentXml, DataFiles = files, ExtensionsDirectory = extensionsDirectory.Replace(extensionsPath + "\\", "") });


                LoadEvent?.Invoke(xmlDoc, new LoadEventArgs { CurrentDirectory = extensionsDirectory.Remove(0, gameRootPath.Length + 1), CurrentFileName = extensionsDirectory.Replace(extensionsPath + "\\", ""), Current = currentExtensionsIndex++, Total = extensionsCount });

            }
            //extensionsMod.Sort();

            List<ExtensionsMod> GetExtensionsModSortByDependency(List<ExtensionsMod> startList)
            {
                //按依赖关系将mod排序，被依赖的排前面

                List<ExtensionsMod> ResultDependency = new List<ExtensionsMod>();
                List<ExtensionsMod> hasDependency = new List<ExtensionsMod>();
                int hasCount = 0;
                while (startList.Count > 0)
                {

                    foreach (var item in startList)
                    {
                        if (item.DependencyId.Length == 0)
                        {
                            ResultDependency.Add(item);
                            continue;
                        }
                        if (startList.FindAll(it => item.DependencyId.Contains(it.Id)).Count == 0)
                        {
                            ResultDependency.Add(item);
                        }
                        else
                        {
                            hasDependency.Add(item);
                        }
                    }
                    if (hasCount > 0 && hasDependency.Count == hasCount)
                    {
                        ResultDependency.AddRange(hasDependency);
                        startList.Clear();
                        hasDependency.Clear();
                        break;
                    }

                    hasCount = hasDependency.Count;
                    startList.Clear();
                    startList.AddRange(hasDependency);
                    hasDependency.Clear();
                }
                return ResultDependency;
            }

            var egoMod = GetExtensionsModSortByDependency(extensionsMod.FindAll(it => it.ExtensionsDirectory.Contains("ego_dlc")));
            foreach (var item in egoMod)
            {
                xmlDoc.DataFiles[item.ExtensionsDirectory] = item.DataFiles;
            }

            var playMod = GetExtensionsModSortByDependency(extensionsMod.FindAll(it => !it.ExtensionsDirectory.Contains("ego_dlc")));
            foreach (var item in playMod)
            {
                xmlDoc.DataFiles[item.ExtensionsDirectory] = item.DataFiles;
            }
            return xmlDoc;
        }

        public List<KeyValuePair<string, XDocument>> GetXDocuments(Dictionary<string, List<DataFile>> dicDataFiles = null)
        {
            dicDataFiles = dicDataFiles ?? this.DataFiles;

            List<KeyValuePair<string, XDocument>> xDocs = new List<KeyValuePair<string, XDocument>>(dicDataFiles.Values.Count);
            foreach (var dicItem in dicDataFiles)
            {
                string loadDirName = dicItem.Key;
                List<DataFile> dataFiles = dicItem.Value;

                dataFiles.ForEach(it =>
                {
                    try
                    {
                        xDocs.Add(KeyValuePair.Create($"{it.FilePath.Remove(0, this.GameRootPath.Length + 1)};{it.DataPath}", it.GetXDocument()));
                    }
                    catch (Exception)
                    {
                        this.LoadFailedFiles.Add(it);
                    }
                });
                //dicToXDocuments.Add(loadDirName, xDocs);
            }
            return xDocs;
        }

        public List<KeyValuePair<string, XDocument>> WhereByPath(string pathPattern)
        {
            //if (path is null)
            //{
            //    throw new ArgumentNullException("path");
            //}
            Dictionary<string, List<DataFile>> temp = new Dictionary<string, List<DataFile>>();

            foreach (var item in DataFiles)
            {
                List<DataFile> list = item.Value.Where(it => Regex.IsMatch(it.DataPath, pathPattern)).ToList();


                if (list != null && list.Count > 0)
                {
                    temp.Add(item.Key, list);
                }

            }

            return this.GetXDocuments(temp);
        }


        public abstract class DataFile
        {
            /// <summary>
            /// 数据文件相对路径
            /// </summary>
            public string DataPath { get; set; }
            /// <summary>
            /// 文件大小
            /// </summary>
            public long Size { get; set; }

            public string FilePath { get; set; }
            public abstract XDocument GetXDocument();
        }
        public class CatDataFile : DataFile
        {
            public static List<DataFile> Load(string catPath)
            {
                List<(string filePath, long size, DateTime updateTime, string sign)> docs = File.ReadAllLines(catPath).ToList().ConvertAll(it =>
                {
                    string[] result = new string[4];

                    string[] splitFileList = it.Split();
                    if (splitFileList.Length < 4)
                    {
                        throw new Exception($"加载{catPath}出错,拆包数量错误.");
                    }
                    result[3] = splitFileList[splitFileList.Length - 1];
                    result[2] = splitFileList[splitFileList.Length - 2];
                    result[1] = splitFileList[splitFileList.Length - 3];
                    if (splitFileList.Length - 4 > 0)
                    {
                        result[0] = string.Join(" ", splitFileList.Take(splitFileList.Count() - 3));
                    }
                    else
                    {
                        result[0] = splitFileList[0];
                    }

                    if (!string.Join(" ", result).Contains(it))
                    {
                        throw new Exception($"加载{catPath}出错,拆分[{it}]错误.");
                    }
                    return (result[0],
                    long.Parse(result[1]),
                    TimeZoneInfo.ConvertTimeFromUtc(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(long.Parse(result[2])), TimeZoneInfo.Local),
                    result[3]);
                });
                Dictionary<string, DataFile> files = new Dictionary<string, DataFile>();
                long currentIndex = 0;
                docs.ForEach(it =>
                {
                    if (it.filePath.Contains(".xml"))
                    {
                        files[it.filePath] = new CatDataFile()
                        {
                            FilePath = catPath.Replace(".cat", ".dat"),
                            DataPath = it.filePath,
                            Size = it.size,
                            StartIndex = currentIndex
                        };
                    }
                    currentIndex += it.size;
                });
                return files.Values.ToList();
            }
            public long StartIndex { get; set; }

            public override XDocument GetXDocument()
            {
                using var fs = new FileStream(FilePath, FileMode.Open);
                fs.Seek(StartIndex, SeekOrigin.Begin);
                byte[] readString = new byte[Size];
                fs.Read(readString, 0, (int)Size);
                return XDocument.Load(new MemoryStream(readString));
            }
        }
        public class OriginalDataFile : DataFile
        {
            public static DataFile Load(string xmlPath)
            {
                var fileInfo = new FileInfo(xmlPath);
                return new OriginalDataFile()
                {
                    FilePath = xmlPath,
                    DataPath = Regex.Replace(xmlPath, @".*extensions\\.*?\\", "").Replace("\\", "/"),
                    Size = fileInfo.Length
                };
            }

            public override XDocument GetXDocument()
            {
                return XDocument.Load(this.FilePath);
            }

        }

        private class ExtensionsMod : IComparable<ExtensionsMod>
        {
            public XDocument ContentXml { get; set; }
            public List<DataFile> DataFiles { get; set; }
            public string ExtensionsDirectory { get; set; }
            public string Id { get => ContentXml.Root.Attribute("id").Value; }
            public string Name { get => ContentXml.Root.Attribute("name").Value; }
            public string[] DependencyId
            {
                get
                {
                    var dependencyEleList = ContentXml.Root.Elements("dependency").Where(it => it.Attribute("id")?.Value != null);
                    if (dependencyEleList.Count() == 0)
                    {
                        return new string[0];
                    }
                    else
                    {
                        return dependencyEleList.Select(it => it.Attribute("id").Value).ToArray();
                    }
                }
            }


            public int CompareTo(ExtensionsMod other)
            {
                if (this.Id.Contains("ego_dlc") && other.Id.Contains("ego_dlc"))
                {
                    return 0;
                }
                if (this.Id.Contains("ego_dlc") && !other.Id.Contains("ego_dlc"))
                {
                    return -1;
                }
                if (!this.Id.Contains("ego_dlc") && other.Id.Contains("ego_dlc"))
                {
                    return 1;
                }
                return this.DependencyId.Length - other.DependencyId.Length;
            }
        }
    }
}
