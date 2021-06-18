using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Xml.Linq;
using System.Xml.XPath;
using X4.Infrastructure;

namespace x4
{
    class Program
    {
        static void Main(string[] args)
        {

            GameFileManager.LoadEvent += XmlDocLoadEventHandler;
            string path = @"C:\Program Files (x86)\Steam\steamapps\common\X4 Foundations"; // @"E:\SteamGame\steamapps\common\X4 Foundations";
            var xDoc = GameFileManager.Load(path);



            var list = xDoc.WhereByPath(@"t/");
            LanguageDataDocManager language = new LanguageDataDocManager();
            language.MergeEvent += XmlMergeEventHandler;
            language.AppendXml(list.ToArray());
            language.ExecuteMerge();
            Console.WriteLine(language.GetLanguageText("{1001,1}"));

            //var list2 = xDoc.WhereByPath(@"libraries");
            //GameDataDocManager gameData = new GameDataDocManager();
            //gameData.MergeEvent += XmlMergeEventHandler;
            //gameData.AppendXml(list2.ToArray());
            //gameData.ExecuteMerge();
            //Console.WriteLine(gameData.GameXmlDocument.ToString());

            Console.ReadKey();
        }

        public static void XmlDocLoadEventHandler(object sender, X4.Infrastructure.GameFileManager.LoadEventArgs e)
        {
            Console.WriteLine($"{e.Current}/{e.Total}  {e.CurrentDirectory}  {e.CurrentFileName}");
        }
        static int mergeIndex = 0;
        public static void XmlMergeEventHandler(object sender, GameDataDocManager.MergeEventArgs e)
        {
            if (mergeIndex == e.Index)
            {
                int row = Console.CursorTop;
                int column = Console.WindowWidth;
                string pro = $"[{"==========".Substring(0, (int)(e.Progress / 10)).PadRight(10, char.Parse(" "))}{e.Progress.ToString().PadLeft(3, char.Parse(" "))}%]";
                Console.SetCursorPosition(column - 16, row);
                Console.Write(pro);

            }
            else
            {
                Console.WriteLine();
                mergeIndex = e.Index;
                int row = Console.CursorTop;
                int col = Console.WindowWidth;
                string msg = $"{e.Index}/{e.Count}  {e.XmlName}";
                if (msg.Length>=col-20)
                {
                    msg.Substring(0, col - 20);
                }
                Console.Write(msg);
                Console.SetCursorPosition(col - 16, row);
                Console.Write($"[          {e.Progress.ToString().PadLeft(3, char.Parse(" "))}%]");
            }
        }

        public static void TestXml()
        {
            //string rootXml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?><root><bar><foo a=\"1\"/><foo a=\"2\"/></bar></root>";

            //string[] testData = new[]
            //{
            //    "<add sel=\"root/bar\"><foo a=\"3\"/></add>",
            //    "<replace sel=\"root/bar/foo[@a='1']\"><foo a=\"3\"/></replace>",
            //    "<remove sel=\"root/bar/foo[@a='1']\"/>",
            //    "<add sel=\"root/bar\" type=\"@cat\">3</add>",
            //    "<add sel=\"root/bar\" pos=\"before\"><foo a=\"3\"/></add>",
            //    "<add sel=\"root/bar\" pos=\"prepend\"><foo a=\"3\"/></add>",
            //    "<add sel=\"root/bar\"><foo a=\"3\"/></add>",
            //    "<add sel=\"*/bar\"><foo a=\"3\"/></add>",
            //    "<add sel=\"*/foo[@a='1']\"><cat a=\"3\"/></add>",
            //    "<replace sel=\"*/foo[@a='1']\"><cat a=\"3\"/></replace>",
            //    "<replace sel=\"root/bar/foo[@a='1']/@a\">3</replace>",
            //    "<remove sel=\"root/bar/foo[@a='1']/@a\"/>"
            //};
            //foreach (string cmd in testData)
            //{
            //    GameDataDocument doc = GameDataDocument.CreateFromXml(rootXml);
            //    doc.MergerXml("<?xml version=\"1.0\" encoding=\"UTF-8\"?><diff xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">" + cmd+ "</diff>");
            //    Console.WriteLine("指令：" + cmd);
            //    Console.WriteLine(doc.GameXmlDocument.ToString());
            //    Console.WriteLine();
            //    Console.WriteLine();
            //}
        }
    }
}
