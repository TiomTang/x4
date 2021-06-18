using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;
using Wmhelp.XPath2;
namespace X4.Infrastructure
{
    public class GameDataDocManager
    {
        /// <summary>
        /// 合并后的完整游戏数据xml文档
        /// </summary>
        public XDocument GameXmlDocument { get; protected set; } = new XDocument(new XElement("gameroot"));
        /// <summary>
        /// 待合并的xml文件缓存
        /// </summary>
        protected List<KeyValuePair<string, XDocument>> InputXmlDoc { get; set; } = new List<KeyValuePair<string, XDocument>>();
        /// <summary>
        /// 获取输入xml中原始xml部分
        /// </summary>
        protected List<KeyValuePair<string, XDocument>> OriginalXmlDoc
        {
            get
            {
                return InputXmlDoc.Where(it => it.Value.Root.Name.LocalName != "diff").ToList();
            }
        }
        /// <summary>
        /// 获得输入xml中的diff补丁部分
        /// </summary>
        protected List<KeyValuePair<string, XDocument>> DiffXmlDoc
        {
            get
            {
                return InputXmlDoc.Where(it => it.Value.Root.Name.LocalName == "diff").ToList();
            }
        }

        /// <summary>
        /// 获取合并时出现错误的xml节点
        /// </summary>
        public List<KeyValuePair<string, (XElement ele, Exception ex)>> ExecuteMergeErrorList { get; set; } = new List<KeyValuePair<string, (XElement ele, Exception ex)>>();



        /// <summary>
        /// 合并事件的参数部分
        /// </summary>
        public class MergeEventArgs
        {
            public string XmlName { get; set; }
            public int Index { get; set; }
            public int Count { get; set; }

            public int Progress { get; set; }
        }
        /// <summary>
        /// 合并事件响应
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        public delegate void MergeEventHandler(object sender, MergeEventArgs e);
        /// <summary>
        /// 合并过程中触发事件，递送进度信息
        /// </summary>
        public event MergeEventHandler MergeEvent;

        /// <summary>
        /// 插入待合并的xml文档，插入完成后必须执行ExecuteMerge()
        /// </summary>
        /// <param name="xmlDoc"></param>
        public void AppendXml(params KeyValuePair<string, XDocument>[] xmlDoc)
        {
            InputXmlDoc.AddRange(xmlDoc);
        }

        public void ExecuteMerge(bool insertOnly = false)
        {

            var originaXmlDoc = this.OriginalXmlDoc;
            var diffXmlDoc = this.DiffXmlDoc;
            int totalCount = originaXmlDoc.Count + diffXmlDoc.Count;
            int current = 0;
            originaXmlDoc.ForEach(it =>
            {
                MergeEvent?.Invoke(this, new MergeEventArgs() { XmlName = it.Key, Index = ++current, Count = totalCount, Progress = 0 });
                this.GameXmlDocument.Root.Add(it.Value.Root);
                MergeEvent?.Invoke(this, new MergeEventArgs() { XmlName = it.Key, Index = current, Count = totalCount, Progress = 100 });
            });

            //List<KeyValuePair<string, XElement>> diffAddList = new List<KeyValuePair<string, XElement>>();
            //List<KeyValuePair<string, XElement>> diffReplaceList = new List<KeyValuePair<string, XElement>>();
            //List<KeyValuePair<string, XElement>> diffRemoveList = new List<KeyValuePair<string, XElement>>();

            //diffXmlDoc.ForEach(it =>
            //{
            //    var xDoc = it.Value;
            //    foreach (XElement ele in xDoc.Root.Elements())
            //    {
            //        switch (ele.Name.LocalName)
            //        {
            //            case "add":
            //                diffAddList.Add(KeyValuePair.Create(it.Key, ele));
            //                break;
            //            case "replace":
            //                diffReplaceList.Add(KeyValuePair.Create(it.Key, ele));
            //                break;
            //            case "remove":
            //                diffRemoveList.Add(KeyValuePair.Create(it.Key, ele));
            //                break;
            //        }
            //    }
            //});



            diffXmlDoc.ForEach(it =>
            {
                MergeEvent?.Invoke(this, new MergeEventArgs() { XmlName = it.Key, Index = ++current, Count = totalCount, Progress = 0 });
                int index = 1;
                int count = it.Value.Root.Elements().Count();
                it.Value.Root.Elements().ToList().ForEach(ele =>
                {
                    MergeEvent?.Invoke(this, new MergeEventArgs() { XmlName = it.Key, Index = current, Count = totalCount, Progress = (int)Math.Floor(((double)index++ / (double)count * 100)) });
                    string xPathStr = ele.Attribute("sel").Value;
                    XObject selectResult = ((IEnumerable)GameXmlDocument.Root.XPath2Select(xPathStr)).Cast<XObject>().FirstOrDefault();

                    if (selectResult is null)
                    {
                        ExecuteMergeErrorList.Add(KeyValuePair.Create(it.Key, (ele, new Exception("未找到节点"))));
                    }
                    else
                    {
                        try
                        {
                            switch (ele.Name.LocalName)
                            {
                                case "add":
                                    this.Insert(selectResult, ele);
                                    break;
                                case "replace":
                                    if (!insertOnly) this.Replace(selectResult, ele);
                                    break;
                                case "remove":
                                    if (!insertOnly) this.Remove(selectResult, ele);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            ExecuteMergeErrorList.Add(KeyValuePair.Create(it.Key, (ele, ex)));
                        }
                    }

                });
            });
        }

        public void Clear()
        {
            this.InputXmlDoc = new List<KeyValuePair<string, XDocument>>();
            this.GameXmlDocument = new XDocument();
        }

        private void Insert(XObject selectElement, XElement element)
        {
            if (element.Attribute("type") != null)
            {
                string newAttributesName = element.Attribute("type").Value.Replace("@", "");
                string newAttributesValue = element.Value;

                if (((XElement)selectElement).Attribute(newAttributesName) == null)
                {
                    ((XElement)selectElement).Add(new XAttribute(newAttributesName, newAttributesValue));
                }
                else
                {
                    ((XElement)selectElement).Attribute(newAttributesName).Value = newAttributesValue;
                }

            }
            else if (element.Attribute("pos") != null)
            {
                switch (element.Attribute("pos").Value)
                {
                    case "before"://添加到同级元素前面
                        ((XElement)selectElement).AddBeforeSelf(element.Elements());
                        break;
                    case "prepend"://添加到子元素首位
                        ((XElement)selectElement).AddFirst(element.Elements());
                        break;
                }
            }
            else
            {
                ((XElement)selectElement).Add(element.Elements());
            }
        }

        private void Replace(XObject selectElement, XElement element)
        {
            switch (selectElement.NodeType)
            {
                case XmlNodeType.Attribute:
                    ((XAttribute)selectElement).Value = element.Value;
                    break;
                case XmlNodeType.Element:
                    ((XElement)selectElement).ReplaceWith(element.Elements());
                    break;
            }
        }

        private void Remove(XObject selectElement, XElement element)
        {
            switch (selectElement.NodeType)
            {
                case XmlNodeType.Attribute:
                    ((XAttribute)selectElement).Remove();
                    break;
                case XmlNodeType.Element:
                    ((XElement)selectElement).Remove();
                    break;
            }
        }
    }
}
