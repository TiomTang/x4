using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace GameObject
{
    public class GameDataFile
    {
        protected GameDataFile()
        {

        }

        public string GameRootPath { get; set; }

        

        public Dictionary<string, List<DataFile>> DataFiles { get; protected set; } = new Dictionary<string, List<DataFile>>();
        public class LoadEventArgs
        {
            public string Msg { get; set; }
            public int Total { get; set; }
            public int Current { get; set; }
        }
        public delegate void LoadEventHandler(object sender, LoadEventArgs e);
        public static event LoadEventHandler LoadEvent;

        public static GameDataFile LoadXml(string path)
        {
            var xmlDoc = new GameDataFile() { GameRootPath = path };

            //加载根目录
            var catFiles = Directory.GetFiles(path, "*.cat", SearchOption.TopDirectoryOnly).Where(it => !it.Contains("_sig"));
            int catCount = catFiles.Count();
            xmlDoc.DataFiles.Add("root", new List<DataFile>());
            LoadEvent.Invoke(xmlDoc, new LoadEventArgs { Current = 0, Total = catCount, Msg = $"读取Cat文件开始，已获取{catCount}个文件" });
            foreach (string catFilePath in catFiles)
            {
                xmlDoc.DataFiles["root"].AddRange(CatDataFile.Load(catFilePath));
                LoadEvent.Invoke(xmlDoc, new LoadEventArgs { Msg = $"已加载根目录包[{catFilePath.Replace(path + "\\", "")}]", Current = 0, Total = 0 });
            }


            //加载mod目录
            string extensionsPath = Path.Combine(path, "extensions");
            var extensionsDirectoryArr = Directory.GetDirectories(extensionsPath).Where(it => File.Exists(Path.Combine(it, "content.xml")));

            int extensionsCount = extensionsDirectoryArr.Count();
            foreach (var extensionsDirectory in extensionsDirectoryArr)
            {
                List<DataFile> files = new List<DataFile>();

                catFiles = Directory.GetFiles(extensionsDirectory, "*.cat", SearchOption.TopDirectoryOnly).Where(it => !it.Contains("_sig"));
                if (catFiles.Count() > 0)
                {
                    foreach (string catFilePath in catFiles)
                    {
                        files.AddRange(CatDataFile.Load(catFilePath));

                    }
                }
                else
                {
                    var xmlFiles = Directory.GetFiles(extensionsDirectory, "*.xml", SearchOption.TopDirectoryOnly);
                    if (xmlFiles.Count() > 0)
                    {
                        foreach (var xmlFile in Directory.GetFiles(extensionsDirectory, "*.xml", SearchOption.AllDirectories).Where(it => !it.Contains("content.xml")))
                        {
                            files.Add(OriginalDataFile.Load(xmlFile));
                        }
                    }
                }
                xmlDoc.DataFiles.Add(extensionsDirectory.Replace(extensionsPath + "\\", ""), files);
                LoadEvent.Invoke(xmlDoc, new LoadEventArgs { Msg = $"已加载Mod目录[{extensionsDirectory.Replace(extensionsPath + "\\", "")}]", Current = 0, Total = 0 });

            }
            LoadEvent.Invoke(xmlDoc, new LoadEventArgs { Msg = $"全部加载完成，共{xmlDoc.DataFiles.Values.Count}个文档", Current = 0, Total = 0 });
            return xmlDoc;
        }

        /// <summary>
        /// 获取指定路径下的xml文档，路径可以是前部匹配，例如：@"assets\\fx",带路径分隔符的必须加@
        /// </summary>
        /// <param name="directoryPath"></param>
        /// <returns></returns>
        public Dictionary<string, List<DataFile>> WhereByDirectoryPath(string directoryPath)
        {
            
            Dictionary<string,List<DataFile>> temp = new Dictionary<string, List<DataFile>>();
            foreach (var item in DataFiles)
            {
                temp.Add(item.Key, item.Value.Where(it => Regex.IsMatch(it.DirectoryPath, @$"(^{directoryPath}\\)|(^{directoryPath}$)")).ToList());
            }
            return temp;
        }
        public abstract class DataFile
        {
            /// <summary>
            /// 数据文件相对目录
            /// </summary>
            public string DirectoryPath { get => Path.GetDirectoryName(FullPath); }
            /// <summary>
            /// 数据文件名
            /// </summary>
            public string FileName { get => Path.GetFileName(FullPath); }
            /// <summary>
            /// 数据文件相对路径
            /// </summary>
            public string FullPath { get; set; }
            /// <summary>
            /// 文件大小
            /// </summary>
            public long Size { get; set; }

            public abstract string GetXml();
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
                List<DataFile> files = new List<DataFile>();
                long currentIndex = 0;
                docs.ForEach(it =>
                {
                    if (it.filePath.Contains(".xml"))
                    {
                        files.Add(new CatDataFile()
                        {
                            DatFilePath = catPath.Replace(".cat", ".dat"),
                            FullPath = it.filePath.Replace("/", "\\"),
                            Size = it.size,
                            StartIndex = currentIndex
                        });

                    }
                    currentIndex += it.size;
                });
                return files;
            }
            public string DatFilePath { get; set; }
            public long StartIndex { get; set; }
            public override string GetXml()
            {
                using var fs = new FileStream(DatFilePath, FileMode.Open);
                fs.Seek(StartIndex, SeekOrigin.Begin);
                byte[] readString = new byte[Size];
                fs.Read(readString, 0, (int)Size);
                return Encoding.UTF8.GetString(readString);
            }
        }
        public class OriginalDataFile : DataFile
        {
            public string XmlFilePath { get; set; }
            public static DataFile Load(string xmlPath)
            {
                var fileInfo = new FileInfo(xmlPath);
                return new OriginalDataFile()
                {
                    XmlFilePath = xmlPath,
                    FullPath = Regex.Replace(xmlPath, @".*extensions\\.*?\\", ""),
                    Size = fileInfo.Length
                };
            }
            public override string GetXml()
            {
                return File.ReadAllText(this.XmlFilePath);
            }
        }
    }
}
