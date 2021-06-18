using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using static GameObject.GameDataFile;

namespace GameObject
{
    public class GameDataDocument
    {
        public XDocument GameXmlDocument { get; protected set; } = new XDocument();
        private GameDataDocument()
        {

        }
        
        public static GameDataDocument Create(string rootName)
        {
            return new GameDataDocument() { GameXmlDocument = new XDocument(new XElement(rootName)) };
        }
        public static GameDataDocument CreateFromXml(string xml)
        {
            return new GameDataDocument() { GameXmlDocument = new XDocument(XElement.Parse(xml)) };
        }
        private void Insert(XElement element)
        {
            string xPath = element.Attribute("sel").Value;
            IEnumerable result = (IEnumerable)GameXmlDocument.XPathEvaluate(xPath);
            XObject selectResult = result.Cast<XObject>().FirstOrDefault();

            if (element.Attribute("type") != null)
            {
                string newAttributesName = element.Attribute("type").Value.Replace("@", "");
                string newAttributesValue = element.Value;

                ((XContainer)selectResult).Add(new XAttribute(newAttributesName, newAttributesValue));
            }
            else if (element.Attribute("pos") != null)
            {
                switch (element.Attribute("pos").Value)
                {
                    case "before"://添加到同级元素前面
                        ((XContainer)selectResult).AddBeforeSelf(element.FirstNode);
                        break;
                    case "prepend"://添加到子元素首位
                        ((XContainer)selectResult).AddFirst(element.FirstNode);
                        break;
                }

            }
            else
            {
                ((XContainer)selectResult).Add(element.FirstNode);
            }
        }

        private void Replace(XElement element)
        {
            string xPath = element.Attribute("sel").Value;
            XObject selectResult = ((IEnumerable)GameXmlDocument.XPathEvaluate(xPath)).Cast<XObject>().FirstOrDefault();
            if (selectResult != null)
            {
                switch (selectResult.NodeType)
                {
                    case XmlNodeType.Attribute:
                        ((XAttribute)selectResult).Value = element.Value;
                        break;
                    case XmlNodeType.Element:
                        ((XElement)selectResult).ReplaceWith(element.FirstNode);
                        break;
                    default:
                        throw new Exception($"查询路径[{xPath}出现预期外的结果]");
                }
            }
        }

        private void Remove(string xPath)
        {
            XObject selectResult = ((IEnumerable)GameXmlDocument.XPathEvaluate(xPath)).Cast<XObject>().FirstOrDefault();
            if (selectResult != null)
            {
                switch (selectResult.NodeType)
                {
                    case XmlNodeType.Attribute:
                        selectResult.Parent.Remove();
                        break;
                    case XmlNodeType.Element:
                        ((XElement)selectResult).Remove();
                        break;
                    default:
                        throw new Exception($"查询路径[{xPath}出现预期外的结果]");
                }
            }
        }

        public void MergerXml(string xml,bool insertOnly=false)
        {
            XDocument fromXml = XDocument.Parse(xml);

            if (fromXml.Root.Name == "diff")
            {
                foreach (XElement element in fromXml.Root.Elements())
                {
                    if (Regex.IsMatch(element.Attribute("sel").Value, @"^\*"))
                    {
                        element.Attribute("sel").Value = Regex.Replace(element.Attribute("sel").Value, @"^\*", @"/");
                    }

                    switch (element.Name.LocalName)
                    {
                        case "add":
                            this.Insert(element);
                            break;
                        case "replace":
                            this.Replace(element);
                            break;
                        case "remove":
                            this.Remove(element.Attribute("sel").Value);
                            break;
                    }
                }
            }
            else
            {
                if (GameXmlDocument.Root.Name != fromXml.Root.Name)
                {
                    throw new Exception($"合并xml文档错误，根目录不一致。目标根目录为{GameXmlDocument.Root.Name.LocalName},要合并的根目录为{fromXml.Root.Name.LocalName}");
                }
                GameXmlDocument.Root.Add(fromXml.Root.Elements().ToArray());
            }
        }

        protected string GetValue(string xPath)
        {
            XObject selectResult = ((IEnumerable)GameXmlDocument.XPathEvaluate(xPath)).Cast<XObject>().FirstOrDefault();
            if (selectResult != null)
            {
                switch (selectResult.NodeType)
                {
                    case XmlNodeType.Attribute:
                        return ((XAttribute)selectResult).Value;
                    case XmlNodeType.Element:
                        return ((XElement)selectResult).Value;
                    default:
                        return null;
                }
            }
            else
            {
                return null;
            }
        }

        public class LanguageGameData : GameDataDocument
        {
            private LanguageGameData()
            {

            }
            public static LanguageGameData Load(Dictionary<string, List<DataFile>> dataFile)
            {
                LanguageGameData doc =new LanguageGameData() { GameXmlDocument = new XDocument(new XElement("language"))};
                foreach (var item in dataFile)
                {
                    var group = item.Value.GroupBy(it => it.FileName.Substring(0, 4));
                    foreach (var groupItem in group)
                    {
                        var file = groupItem.LastOrDefault(it => it.FileName.ToLower().Contains("l086")) ?? groupItem.LastOrDefault(it => it.FileName.ToLower().Contains("l044")) ?? groupItem.LastOrDefault(it => Regex.IsMatch(it.FileName, @"^\d{4}\.xml$"));
                        if (file != null)
                        {
                            doc.MergerXml(file.GetXml());
                        }
                    }
                }
                return doc;
            }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="code">例如：{2001,100}</param>
            /// <returns></returns>
            public string GetLanguageText(string code)
            {
                if (!Regex.IsMatch(code,@"^{\d+,\d+}$"))
                {
                    throw new Exception("传参词条标记不正确，无法获取词条内容");
                }
                MatchCollection indexArr = Regex.Matches(code, @"\d+");
                string xPath = $"/language/page[@id='{indexArr[0].Value}']/t[@id='{indexArr[1].Value}']";
                return this.GetValue(xPath);
            }
        }
    }
}
