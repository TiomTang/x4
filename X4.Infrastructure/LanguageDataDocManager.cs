using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.IO;
using System.Xml.XPath;
using System.Collections;
using Wmhelp.XPath2;
namespace X4.Infrastructure
{
    public class LanguageDataDocManager : GameDataDocManager
    {
        public new void ExecuteMerge(bool insertOnly = true)
        {
            List<KeyValuePair<string, XDocument>> newList = new List<KeyValuePair<string, XDocument>>();
            foreach (var group in this.InputXmlDoc.GroupBy(it => Path.GetDirectoryName(it.Key.Split(";")[0]) ?? it.Key.Split(";")[0]))
            {
                var file = group.FirstOrDefault(it => Regex.IsMatch(it.Key.Split(";")[1], @"\d{4}-[Ll]086\.xml$"));
                if (file.Equals(default(KeyValuePair<string, XDocument>))) file = group.FirstOrDefault(it => Regex.IsMatch(it.Key.Split(";")[1], @"\d{4}-[Ll]044\.xml$"));
                if (file.Equals(default(KeyValuePair<string, XDocument>))) file = group.FirstOrDefault(it => Regex.IsMatch(it.Key.Split(";")[1], @"\d{4}.xml$"));

                if (!file.Equals(default(KeyValuePair<string, XDocument>)))
                {
                    newList.Add(file);
                }
            }
            this.InputXmlDoc = newList;
            base.ExecuteMerge(insertOnly);
        }

        public string GetLanguageText(string code)
        {
            if (!Regex.IsMatch(code, @"^{\d+,\d+}$"))
            {
                throw new Exception("传参词条标记不正确，无法获取词条内容");
            }
            MatchCollection indexArr = Regex.Matches(code, @"\d+");
            string xPath = $"/language/page[@id='{indexArr[0].Value}']/t[@id='{indexArr[1].Value}']";
            XElement selectResult = ((IEnumerable)GameXmlDocument.Root.XPath2Select(xPath)).Cast<XElement>().FirstOrDefault();
            return selectResult.Value;
        }
    }
}
